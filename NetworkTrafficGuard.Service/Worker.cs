namespace NetworkTrafficGuard.Service;

using Microsoft.Extensions.Options;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Service.Diagnostics;

public sealed class Worker(
    ILogger<Worker> logger,
    IRouteReader routeReader,
    IRouteController routeController,
    INetworkPolicyEngine policyEngine,
    RouteDiagnosticsLogger routeDiagnosticsLogger,
    IOptionsMonitor<NetworkGuardSettings> settingsMonitor,
    IConfiguration configuration,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = settingsMonitor.CurrentValue;

            try
            {
                var routes = await routeReader.GetDefaultRoutesAsync(stoppingToken);
                var result = policyEngine.Evaluate(routes, settings);

                routeDiagnosticsLogger.LogSnapshot(routes, settings, result);

                if (result.ShouldBlockSecondaryRoute)
                {
                    var routeControlResult = await routeController.RemoveSecondaryDefaultRoutesAsync(
                        routes,
                        settings,
                        stoppingToken);

                    logger.LogWarning(
                        "Route control result: {Message} DryRun={IsDryRun}, Matched={MatchedRouteCount}, Changed={ChangedRouteCount}",
                        routeControlResult.Message,
                        routeControlResult.IsDryRun,
                        routeControlResult.MatchedRouteCount,
                        routeControlResult.ChangedRouteCount);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to evaluate Windows default route.");
            }

            if (IsRunOnce())
            {
                applicationLifetime.StopApplication();
                return;
            }

            await Task.Delay(GetCheckInterval(settings), stoppingToken);
        }
    }

    private static TimeSpan GetCheckInterval(NetworkGuardSettings settings)
    {
        var seconds = Math.Clamp(settings.CheckIntervalSeconds, 1, 3600);
        return TimeSpan.FromSeconds(seconds);
    }

    private bool IsRunOnce()
    {
        return string.Equals(configuration["RunOnce"], "true", StringComparison.OrdinalIgnoreCase);
    }
}
