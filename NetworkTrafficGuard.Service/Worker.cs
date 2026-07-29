namespace NetworkTrafficGuard.Service;

using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Core.Traffic;
using NetworkTrafficGuard.Service.Diagnostics;

public sealed class Worker(
    ILogger<Worker> logger,
    IRouteReader routeReader,
    IRouteController routeController,
    INetworkPolicyEngine policyEngine,
    RouteDiagnosticsLogger routeDiagnosticsLogger,
    MonthlyTrafficUsageStore trafficUsageStore,
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
                RecordBestRouteTraffic(routes);

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

    private void RecordBestRouteTraffic(IReadOnlyCollection<DefaultRouteInfo> routes)
    {
        var bestRoute = DefaultRouteSelector.GetBestDefaultRoute(routes);

        if (bestRoute is null)
        {
            return;
        }

        var networkInterface = FindNetworkInterface(bestRoute.InterfaceIndex);

        if (networkInterface is null)
        {
            logger.LogDebug("Traffic usage sample skipped. Interface #{InterfaceIndex} was not found.", bestRoute.InterfaceIndex);
            return;
        }

        var statistics = networkInterface.GetIPv4Statistics();
        var routeKey = $"{bestRoute.InterfaceIndex}|{bestRoute.NextHop}";
        var entry = trafficUsageStore.RecordSample(
            routeKey,
            bestRoute.InterfaceIndex,
            bestRoute.NextHop,
            bestRoute.InterfaceAlias,
            statistics.BytesReceived,
            statistics.BytesSent,
            DateTimeOffset.Now);

        logger.LogInformation(
            "Monthly traffic usage: {DisplayName} {Month}, Received={BytesReceived}, Sent={BytesSent}",
            entry.DisplayName,
            entry.Month,
            MonthlyTrafficUsageStore.FormatBytes(entry.BytesReceived),
            MonthlyTrafficUsageStore.FormatBytes(entry.BytesSent));
    }

    private static NetworkInterface? FindNetworkInterface(int interfaceIndex)
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var properties = networkInterface.GetIPProperties();
            var ipv4Index = TryGetIndex(() => properties.GetIPv4Properties()?.Index);
            var ipv6Index = TryGetIndex(() => properties.GetIPv6Properties()?.Index);

            if (ipv4Index == interfaceIndex || ipv6Index == interfaceIndex)
            {
                return networkInterface;
            }
        }

        return null;
    }

    private static int? TryGetIndex(Func<int?> readIndex)
    {
        try
        {
            return readIndex();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }
}
