namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed class TrafficAlertEventArgs(string title, string message)
    : EventArgs
{
    public string Title { get; } = title;

    public string Message { get; } = message;
}
