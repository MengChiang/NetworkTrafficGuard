using NetworkTrafficGuard.Core.Models;

namespace NetworkTrafficGuard.Core.Routes;

public sealed record RouteControlResult(
    bool IsDryRun,
    int MatchedRouteCount,
    int ChangedRouteCount,
    IReadOnlyList<DefaultRouteInfo> MatchedRoutes,
    string Message);
