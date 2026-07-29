using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Core.Routes;

public interface IRouteController
{
    Task<RouteControlResult> RemoveSimDefaultRoutesAsync(
        IReadOnlyCollection<DefaultRouteInfo> defaultRoutes,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken);
}
