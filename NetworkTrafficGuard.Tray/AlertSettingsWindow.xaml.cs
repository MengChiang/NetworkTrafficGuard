using System.Windows;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Tray.Localization;
using NetworkTrafficGuard.Tray.Settings;

namespace NetworkTrafficGuard.Tray;

public partial class AlertSettingsWindow : Window
{
    private readonly NetworkGuardSettings _settings;

    public AlertSettingsWindow(NetworkGuardSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        AlertThresholdKbps = Math.Max(1, _settings.AlertThresholdKbps);
        DataContext = this;
    }

    public UiText Texts => UiTextProvider.Get(_settings.CultureName);

    public int AlertThresholdKbps { get; set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlertThresholdKbps = Math.Max(1, AlertThresholdKbps);
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
