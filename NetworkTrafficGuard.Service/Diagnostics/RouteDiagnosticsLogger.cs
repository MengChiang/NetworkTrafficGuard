using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Service.Diagnostics;

public sealed class RouteDiagnosticsLogger(ILogger<RouteDiagnosticsLogger> logger)
{
    public void LogSnapshot(
        IReadOnlyCollection<DefaultRouteInfo> routes,
        NetworkGuardSettings settings,
        NetworkPolicyResult policyResult)
    {
        LogSettings(settings);
        LogDefaultRoutes(routes);
        LogBestRoute(routes);
        LogPolicyResult(policyResult);
    }

    private void LogSettings(NetworkGuardSettings settings)
    {
        logger.LogInformation(
            "Settings: PrimaryWiFi={PrimaryWifiAlias}#{PrimaryWifiIndex}, SecondaryNetwork={SecondaryAlias}#{SecondaryIndex}, Mode={Mode}, EnableRouteChanges={EnableRouteChanges}, Culture={CultureName}, Interval={CheckIntervalSeconds}s",
            settings.PrimaryWifiInterfaceAlias,
            settings.PrimaryWifiInterfaceIndex?.ToString() ?? "auto",
            settings.SecondaryInterfaceAlias,
            settings.SecondaryInterfaceIndex?.ToString() ?? "auto",
            settings.Mode,
            settings.EnableRouteChanges,
            settings.CultureName,
            settings.CheckIntervalSeconds);
    }

    private void LogDefaultRoutes(IReadOnlyCollection<DefaultRouteInfo> routes)
    {
        var defaultRoutes = DefaultRouteSelector.GetDefaultRoutes(routes);

        if (defaultRoutes.Count == 0)
        {
            logger.LogWarning("Default routes: none found.");
            return;
        }

        logger.LogInformation("Default routes: {RouteCount} route(s) found.", defaultRoutes.Count);

        foreach (var route in defaultRoutes)
        {
            logger.LogInformation(
                "Default route: {DestinationPrefix} via {NextHop}, Interface={InterfaceAlias}#{InterfaceIndex}, RouteMetric={RouteMetric}, InterfaceMetric={InterfaceMetric}, TotalMetric={TotalMetric}",
                route.DestinationPrefix,
                route.NextHop,
                route.InterfaceAlias,
                route.InterfaceIndex,
                route.RouteMetric,
                route.InterfaceMetric,
                route.TotalMetric);
        }
    }

    private void LogBestRoute(IReadOnlyCollection<DefaultRouteInfo> routes)
    {
        var bestRoute = DefaultRouteSelector.GetBestDefaultRoute(routes);

        if (bestRoute is null)
        {
            logger.LogWarning("Best route: none.");
            return;
        }

        logger.LogInformation(
            "Best route: {DestinationPrefix} via {NextHop}, Interface={InterfaceAlias}#{InterfaceIndex}, TotalMetric={TotalMetric}",
            bestRoute.DestinationPrefix,
            bestRoute.NextHop,
            bestRoute.InterfaceAlias,
            bestRoute.InterfaceIndex,
            bestRoute.TotalMetric);
    }

    private void LogPolicyResult(NetworkPolicyResult policyResult)
    {
        logger.LogInformation(
            "Policy result: {RiskLevel}. {Message} Notify={ShouldNotify}, BlockSecondary={ShouldBlockSecondaryRoute}",
            policyResult.RiskLevel,
            policyResult.Message,
            policyResult.ShouldNotify,
            policyResult.ShouldBlockSecondaryRoute);
    }
}
