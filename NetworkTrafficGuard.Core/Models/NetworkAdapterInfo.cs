namespace NetworkTrafficGuard.Core.Models;

public sealed record NetworkAdapterInfo(
    int InterfaceIndex,
    string InterfaceAlias,
    string Description,
    bool IsWireless,
    bool IsUp);
