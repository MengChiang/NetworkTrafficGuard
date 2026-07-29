using CommunityToolkit.Mvvm.ComponentModel;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed partial class TrafficMonitorViewModel(
    string key,
    int interfaceIndex,
    string title,
    string detail) : ObservableObject
{
    private readonly Queue<double> _samples = new();

    public string Key { get; } = key;

    public int InterfaceIndex { get; } = interfaceIndex;

    public string Title { get; } = title;

    public string Detail { get; } = detail;

    public long? LastBytesReceived { get; set; }

    public long? LastBytesSent { get; set; }

    public DateTimeOffset? LastSampledAt { get; set; }

    [ObservableProperty]
    private string _rateText = "等待資料";

    [ObservableProperty]
    private string _sparkline = "▁▁▁▁▁▁▁▁▁▁";

    public void AddSample(double bps)
    {
        _samples.Enqueue(bps);

        while (_samples.Count > 24)
        {
            _samples.Dequeue();
        }

        Sparkline = CreateSparkline(_samples);
    }

    private static string CreateSparkline(IEnumerable<double> samples)
    {
        var values = samples.ToList();

        if (values.Count == 0)
        {
            return "▁▁▁▁▁▁▁▁▁▁";
        }

        var max = Math.Max(1, values.Max());
        var blocks = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        return new string(values
            .Select(value =>
            {
                var index = (int)Math.Round(value / max * (blocks.Length - 1));
                return blocks[Math.Clamp(index, 0, blocks.Length - 1)];
            })
            .ToArray());
    }
}
