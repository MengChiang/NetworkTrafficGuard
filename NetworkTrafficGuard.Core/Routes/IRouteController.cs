using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Core.Routes;

public interface IRouteController
{
    Task<RouteControlResult> RemoveSecondaryDefaultRoutesAsync(
        IReadOnlyCollection<DefaultRouteInfo> defaultRoutes,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken);

    Task<RouteControlResult> ApplyDefaultRoutePrioritiesAsync(
        IReadOnlyList<DefaultRouteInfo> defaultRoutesInPriorityOrder,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken);
}
