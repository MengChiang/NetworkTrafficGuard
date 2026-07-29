namespace NetworkTrafficGuard.Core.Traffic;

public sealed class MonthlyTrafficUsageEntry
{
    public required string Month { get; set; }

    public required string RouteKey { get; set; }

    public int InterfaceIndex { get; set; }

    public string Gateway { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long BytesReceived { get; set; }

    public long BytesSent { get; set; }

    public long LastBytesReceived { get; set; }

    public long LastBytesSent { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long TotalBytes => BytesReceived + BytesSent;
}
