namespace NetworkTrafficGuard.Service;

using Microsoft.Extensions.Options;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;

public sealed class Worker(
    ILogger<Worker> logger,
    IRouteReader routeReader,
    INetworkPolicyEngine policyEngine,
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

                logger.LogInformation(
                    "Network status: {RiskLevel}. {Message} Notify={ShouldNotify}, BlockSim={ShouldBlockSimRoute}",
                    result.RiskLevel,
                    result.Message,
                    result.ShouldNotify,
                    result.ShouldBlockSimRoute);
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
