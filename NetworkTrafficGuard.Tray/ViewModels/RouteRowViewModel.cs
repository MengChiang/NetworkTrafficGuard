using NetworkTrafficGuard.Core.Models;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed class RouteRowViewModel(DefaultRouteInfo route, bool isBestRoute)
{
    public string DestinationPrefix { get; } = route.DestinationPrefix;

    public string NextHop { get; } = route.NextHop;

    public string Interface { get; } = $"{route.InterfaceAlias} #{route.InterfaceIndex}";

    public uint RouteMetric { get; } = route.RouteMetric;

    public uint InterfaceMetric { get; } = route.InterfaceMetric;

    public uint TotalMetric { get; } = route.TotalMetric;

    public string Role { get; } = isBestRoute ? "Best" : "";
}
