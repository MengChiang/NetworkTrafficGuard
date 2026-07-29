using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Core.Adapters;

public interface IAdapterController
{
    Task<AdapterStatusResult> GetAdapterStatusAsync(
        string interfaceAlias,
        CancellationToken cancellationToken);

    Task<AdapterControlResult> SetAdapterEnabledAsync(
        string interfaceAlias,
        bool enabled,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken);
}
