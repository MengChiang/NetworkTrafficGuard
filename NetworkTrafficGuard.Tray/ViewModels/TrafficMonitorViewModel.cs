using CommunityToolkit.Mvvm.ComponentModel;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed partial class TrafficMonitorViewModel(
    string key,
    int interfaceIndex,
    string title,
    string detail) : ObservableObject
{
    private const int MaxSamples = 24;
    private const string EmptySparkline = "\u2581\u2582\u2581\u2582\u2581\u2582\u2581\u2582\u2581\u2582\u2581\u2582";
    private static readonly char[] Blocks =
    [
        '\u2581',
        '\u2582',
        '\u2583',
        '\u2584',
        '\u2585',
        '\u2586',
        '\u2587',
        '\u2588'
    ];

    private readonly Queue<double> _samples = new();

    public string Key { get; } = key;

    public int InterfaceIndex { get; } = interfaceIndex;

    public string Title { get; } = title;

    public string Detail { get; } = detail;

    public long? LastBytesReceived { get; set; }

    public long? LastBytesSent { get; set; }

    public DateTimeOffset? LastSampledAt { get; set; }

    [ObservableProperty]
    private string _rateText = "↓ 0 bps / ↑ 0 bps";

    [ObservableProperty]
    private string _sparkline = EmptySparkline;

    public void AddSample(double bps)
    {
        _samples.Enqueue(bps);

        while (_samples.Count > MaxSamples)
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
            return EmptySparkline;
        }

        var max = Math.Max(1, values.Max());

        return new string(values
            .Select(value =>
            {
                var index = (int)Math.Round(value / max * (Blocks.Length - 1));
                return Blocks[Math.Clamp(index, 0, Blocks.Length - 1)];
            })
            .ToArray());
    }
}
