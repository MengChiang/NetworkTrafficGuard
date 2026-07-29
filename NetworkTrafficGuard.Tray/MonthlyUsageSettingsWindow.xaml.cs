using System.ComponentModel;
using System.Windows;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Core.Traffic;
using NetworkTrafficGuard.Tray.Localization;
using NetworkTrafficGuard.Tray.Settings;

namespace NetworkTrafficGuard.Tray;

public partial class MonthlyUsageSettingsWindow : Window, INotifyPropertyChanged
{
    private readonly NetworkGuardSettings _settings;
    private readonly MonthlyTrafficUsageStore _trafficUsageStore;
    private string _statusText = string.Empty;

    public MonthlyUsageSettingsWindow(
        NetworkGuardSettings settings,
        MonthlyTrafficUsageStore trafficUsageStore)
    {
        InitializeComponent();
        _settings = settings;
        _trafficUsageStore = trafficUsageStore;
        ShowMonthlyTrafficUsage = _settings.ShowMonthlyTrafficUsage;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiText Texts => UiTextProvider.Get(_settings.CultureName);

    public bool ShowMonthlyTrafficUsage { get; set; }

    public bool WasCleared { get; private set; }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (string.Equals(_statusText, value, StringComparison.Ordinal))
            {
                return;
            }

            _statusText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _trafficUsageStore.ClearMonth();
        WasCleared = true;
        StatusText = Texts.MonthlyTrafficUsageCleared;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowMonthlyTrafficUsage = ShowMonthlyTrafficUsage;
        TraySettingsLoader.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
