namespace NetworkTrafficGuard.Core.Models;

public sealed record DefaultRouteInfo(
    string DestinationPrefix,
    string NextHop,
    int InterfaceIndex,
    string InterfaceAlias,
    uint RouteMetric,
    uint InterfaceMetric)
{
    public uint TotalMetric => RouteMetric + InterfaceMetric;
}
