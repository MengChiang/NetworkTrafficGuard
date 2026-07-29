using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Core.Policy;

public interface INetworkPolicyEngine
{
    NetworkPolicyResult Evaluate(
        IReadOnlyCollection<DefaultRouteInfo> defaultRoutes,
        NetworkGuardSettings settings);
}
