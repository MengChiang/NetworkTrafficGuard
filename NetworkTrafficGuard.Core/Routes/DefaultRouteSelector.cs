using NetworkTrafficGuard.Core.Models;

namespace NetworkTrafficGuard.Core.Routes;

public static class DefaultRouteSelector
{
    public static IReadOnlyList<DefaultRouteInfo> GetDefaultRoutes(
        IEnumerable<DefaultRouteInfo> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        return routes
            .Where(route => IsDefaultRoute(route.DestinationPrefix))
            .OrderBy(route => route.TotalMetric)
            .ThenBy(route => route.InterfaceIndex)
            .ToList();
    }

    public static DefaultRouteInfo? GetBestDefaultRoute(
        IEnumerable<DefaultRouteInfo> routes)
    {
        return GetDefaultRoutes(routes).FirstOrDefault();
    }

    public static bool IsDefaultRoute(string destinationPrefix)
    {
        return string.Equals(destinationPrefix, "0.0.0.0/0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(destinationPrefix, "::/0", StringComparison.OrdinalIgnoreCase);
    }
}
