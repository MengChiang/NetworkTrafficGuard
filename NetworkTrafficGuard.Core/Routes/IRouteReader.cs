using NetworkTrafficGuard.Core.Models;

namespace NetworkTrafficGuard.Core.Routes;

public interface IRouteReader
{
    Task<IReadOnlyList<DefaultRouteInfo>> GetDefaultRoutesAsync(CancellationToken cancellationToken);
}
