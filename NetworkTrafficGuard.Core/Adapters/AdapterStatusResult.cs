namespace NetworkTrafficGuard.Core.Adapters;

public sealed record AdapterStatusResult(
    bool Exists,
    string Name,
    string Status,
    int? InterfaceIndex,
    string Message)
{
    public bool IsEnabled =>
        string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Disconnected", StringComparison.OrdinalIgnoreCase);
}
