namespace NetworkTrafficGuard.Core.Adapters;

public sealed record AdapterControlResult(
    bool IsDryRun,
    bool Changed,
    string Message);
