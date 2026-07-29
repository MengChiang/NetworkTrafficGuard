using FluentAssertions;
using NetworkTrafficGuard.Core.Traffic;

namespace NetworkTrafficGuard.Tests.Traffic;

public sealed class MonthlyTrafficUsageStoreTests
{
    [Fact]
    public void RecordSample_ShouldAccumulateMonthlyDeltas()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new MonthlyTrafficUsageStore(filePath);

        try
        {
            store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 1_000, 2_000, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
            var entry = store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 1_500, 2_750, new DateTimeOffset(2026, 7, 1, 0, 0, 3, TimeSpan.Zero));

            entry.Month.Should().Be("2026-07");
            entry.BytesReceived.Should().Be(500);
            entry.BytesSent.Should().Be(750);
            entry.TotalBytes.Should().Be(1_250);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void RecordSample_WhenCounterResets_ShouldNotSubtractUsage()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new MonthlyTrafficUsageStore(filePath);

        try
        {
            store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 10_000, 20_000, DateTimeOffset.Now);
            var entry = store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 100, 200, DateTimeOffset.Now);

            entry.BytesReceived.Should().Be(0);
            entry.BytesSent.Should().Be(0);
            entry.LastBytesReceived.Should().Be(100);
            entry.LastBytesSent.Should().Be(200);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ClearMonth_ShouldRemoveOnlySelectedMonth()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new MonthlyTrafficUsageStore(filePath);

        try
        {
            store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 1_000, 1_000, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
            store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 1_500, 1_250, new DateTimeOffset(2026, 7, 1, 0, 0, 3, TimeSpan.Zero));
            store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 2_000, 2_000, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
            store.RecordSample("8|192.168.1.1", 8, "192.168.1.1", "Home Wi-Fi", 2_500, 2_250, new DateTimeOffset(2026, 8, 1, 0, 0, 3, TimeSpan.Zero));

            var removed = store.ClearMonth("2026-07");

            removed.Should().Be(1);
            store.GetEntries("2026-07").Should().BeEmpty();
            store.GetEntries("2026-08").Should().ContainSingle();
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
