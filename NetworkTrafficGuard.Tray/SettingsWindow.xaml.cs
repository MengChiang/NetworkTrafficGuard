using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Tray.Localization;
using NetworkTrafficGuard.Tray.Settings;
using NetworkTrafficGuard.Tray.ViewModels;

namespace NetworkTrafficGuard.Tray;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly NetworkGuardSettings _settings;

    public SettingsWindow(
        NetworkGuardSettings settings,
        IEnumerable<RouteRowViewModel> routes)
    {
        InitializeComponent();
        _settings = settings;
        Rows = new ObservableCollection<NetworkNameMappingRowViewModel>(
            routes
                .GroupBy(route => $"{route.InterfaceIndex}|{route.RawGateway}")
                .Select(group => new NetworkNameMappingRowViewModel(group.First())));
        EnableAdapterChanges = _settings.EnableAdapterChanges;
        EnableRouteChanges = _settings.EnableRouteChanges;
        AlertThresholdKbps = Math.Max(1, _settings.AlertThresholdKbps);
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NetworkNameMappingRowViewModel> Rows { get; }

    public UiText Texts => UiTextProvider.Get(_settings.CultureName);

    public bool EnableAdapterChanges { get; set; }

    public bool EnableRouteChanges { get; set; }

    public int AlertThresholdKbps { get; set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Rows)
        {
            var displayName = row.DisplayName.Trim();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (row.InterfaceIndex == _settings.PrimaryWifiInterfaceIndex
                || string.Equals(row.InterfaceAlias, _settings.PrimaryWifiInterfaceAlias, StringComparison.OrdinalIgnoreCase))
            {
                _settings.PrimaryWifiDisplayName = displayName;
            }
            else if (row.InterfaceIndex == _settings.SimInterfaceIndex
                || string.Equals(row.InterfaceAlias, _settings.SimInterfaceAlias, StringComparison.OrdinalIgnoreCase))
            {
                _settings.SimDisplayName = displayName;
            }

            if (!string.IsNullOrWhiteSpace(row.RawGateway))
            {
                _settings.GatewayDisplayNames[row.RawGateway] = displayName;
            }
        }

        _settings.EnableAdapterChanges = EnableAdapterChanges;
        _settings.EnableRouteChanges = EnableRouteChanges;
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

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
