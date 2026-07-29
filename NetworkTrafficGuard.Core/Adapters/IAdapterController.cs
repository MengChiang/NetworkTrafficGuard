using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Core.Adapters;

public interface IAdapterController
{
    Task<AdapterControlResult> SetAdapterEnabledAsync(
        string interfaceAlias,
        bool enabled,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken);
}
