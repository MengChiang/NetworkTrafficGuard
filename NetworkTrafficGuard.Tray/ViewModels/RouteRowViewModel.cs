using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed class RouteRowViewModel(
    DefaultRouteInfo route,
    bool isBestRoute,
    NetworkGuardSettings settings)
{
    public string DestinationPrefix { get; } = route.DestinationPrefix;

    public string NextHop { get; } = FormatNextHop(route.NextHop, settings);

    public string Interface { get; } = $"{route.InterfaceAlias} #{route.InterfaceIndex}";

    public uint RouteMetric { get; } = route.RouteMetric;

    public uint InterfaceMetric { get; } = route.InterfaceMetric;

    public uint TotalMetric { get; } = route.TotalMetric;

    public string Role { get; } = isBestRoute ? "Best" : "";

    private static string FormatNextHop(string nextHop, NetworkGuardSettings settings)
    {
        return settings.GatewayDisplayNames.TryGetValue(nextHop, out var displayName)
            ? $"{displayName} ({nextHop})"
            : nextHop;
    }
}
