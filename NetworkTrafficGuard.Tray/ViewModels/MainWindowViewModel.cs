using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkTrafficGuard.Core.Adapters;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Tray.Settings;
using NetworkTrafficGuard.Windows;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IRouteReader _routeReader;
    private readonly IRouteController _routeController;
    private readonly IAdapterController _adapterController;
    private readonly INetworkPolicyEngine _policyEngine;
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly DispatcherTimer _trafficTimer = new();
    private int? _bestInterfaceIndex;
    private string? _selectedRouteKey;
    private readonly HashSet<string> _monitoredRouteKeys = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private NetworkGuardSettings _settings;

    [ObservableProperty]
    private ObservableCollection<RouteRowViewModel> _routes = [];

    [ObservableProperty]
    private RouteRowViewModel? _selectedRoute;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _statusDetail = "Click Run Check to inspect the current Windows default routes.";

    [ObservableProperty]
    private string _bestRouteText = "Not checked yet";

    [ObservableProperty]
    private string _routeControlText = "Dry-run route control is idle.";

    [ObservableProperty]
    private string _wifiStatusText = "未確認";

    [ObservableProperty]
    private string _wifiDetailText = "Press Update to check Wi-Fi routing.";

    [ObservableProperty]
    private string _adapterControlStatusText = string.Empty;

    [ObservableProperty]
    private string _mobileDataStatusText = "未確認";

    [ObservableProperty]
    private string _mobileDataDetailText = "Press Update to check mobile data routing.";

    [ObservableProperty]
    private string _activeLineText = "未確認";

    [ObservableProperty]
    private ObservableCollection<TrafficMonitorViewModel> _trafficMonitors = [];

    [ObservableProperty]
    private string _editableWifiDisplayName = string.Empty;

    [ObservableProperty]
    private string _editableSimDisplayName = string.Empty;

    [ObservableProperty]
    private string _editableSimCarrierName = string.Empty;

    [ObservableProperty]
    private string _editableGatewayAddress = "192.168.100.1";

    [ObservableProperty]
    private string _editableGatewayDisplayName = string.Empty;

    [ObservableProperty]
    private string _selectedNetworkTitle = "選取左側網路";

    [ObservableProperty]
    private string _selectedNetworkDetail = "選取一列後，可查看它的流量並編輯顯示名稱。";

    [ObservableProperty]
    private string _editableSelectedNetworkName = string.Empty;

    [ObservableProperty]
    private bool _isWifiToggleChecked = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private DateTimeOffset? _lastCheckedAt;

    public MainWindowViewModel()
        : this(
            TraySettingsLoader.Load(),
            new PowerShellRouteReader(NullLogger<PowerShellRouteReader>.Instance),
            new PowerShellRouteController(NullLogger<PowerShellRouteController>.Instance),
            new PowerShellAdapterController(NullLogger<PowerShellAdapterController>.Instance),
            new NetworkPolicyEngine())
    {
    }

    public MainWindowViewModel(
        NetworkGuardSettings settings,
        IRouteReader routeReader,
        IRouteController routeController,
        IAdapterController adapterController,
        INetworkPolicyEngine policyEngine)
    {
        Settings = settings;
        _routeReader = routeReader;
        _routeController = routeController;
        _adapterController = adapterController;
        _policyEngine = policyEngine;
        RunCheckCommand = new AsyncRelayCommand(RunCheckAsync, () => !IsBusy);
        OpenSettingsCommand = new RelayCommand(OpenSettingsWindow);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        SaveSelectedNetworkNameCommand = new RelayCommand(SaveSelectedNetworkName, () => SelectedRoute is not null);
        SetWifiEnabledCommand = new AsyncRelayCommand<bool?>(SetWifiEnabledAsync, _ => !IsBusy);
        EnableWifiCommand = new AsyncRelayCommand(() => SetWifiEnabledAsync(enabled: true), () => !IsBusy);
        DisableWifiCommand = new AsyncRelayCommand(() => SetWifiEnabledAsync(enabled: false), () => !IsBusy);
        MoveRouteUpCommand = new RelayCommand(MoveSelectedRouteUp, () => SelectedRoute is not null);
        MoveRouteDownCommand = new RelayCommand(MoveSelectedRouteDown, () => SelectedRoute is not null);
        MonitorRouteCommand = new RelayCommand<RouteRowViewModel>(MonitorRoute);
        LoadEditableSettings();
        UpdateAdapterControlStatus();
        StartAutoRefresh();
        StartTrafficTimer();
    }

    public IAsyncRelayCommand RunCheckCommand { get; }

    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand SaveSettingsCommand { get; }

    public IRelayCommand SaveSelectedNetworkNameCommand { get; }

    public IAsyncRelayCommand<bool?> SetWifiEnabledCommand { get; }

    public IAsyncRelayCommand EnableWifiCommand { get; }

    public IAsyncRelayCommand DisableWifiCommand { get; }

    public IRelayCommand MoveRouteUpCommand { get; }

    public IRelayCommand MoveRouteDownCommand { get; }

    public IRelayCommand<RouteRowViewModel> MonitorRouteCommand { get; }

    public string SettingsSummary =>
        $"Wi-Fi {Settings.PrimaryWifiDisplayName} ({Settings.PrimaryWifiInterfaceAlias} #{FormatIndex(Settings.PrimaryWifiInterfaceIndex)}) | " +
        $"回線 {Settings.SimDisplayName} / {Settings.SimCarrierName} ({Settings.SimInterfaceAlias} #{FormatIndex(Settings.SimInterfaceIndex)}) | " +
        $"Mode {Settings.Mode} | Route changes {(Settings.EnableRouteChanges ? "enabled" : "dry-run")} | Adapter changes {(Settings.EnableAdapterChanges ? "enabled" : "dry-run")}";

    public string WifiDisplayName => Settings.PrimaryWifiDisplayName;

    public string RouterLineLabel => "SIMルーター回線";

    public string RouterDisplayName => Settings.SimDisplayName;

    public string MobileDataCarrierName => Settings.SimCarrierName;

    public string OptionsSummary =>
        $"優先: Wi-Fi | SIM接管: {Settings.Mode} | Route: {(Settings.EnableRouteChanges ? "enabled" : "dry-run")} | Adapter: {(Settings.EnableAdapterChanges ? "enabled" : "dry-run")}";

    partial void OnIsBusyChanged(bool value)
    {
        RunCheckCommand.NotifyCanExecuteChanged();
        SetWifiEnabledCommand.NotifyCanExecuteChanged();
        EnableWifiCommand.NotifyCanExecuteChanged();
        DisableWifiCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRouteChanged(RouteRowViewModel? value)
    {
        MoveRouteUpCommand.NotifyCanExecuteChanged();
        MoveRouteDownCommand.NotifyCanExecuteChanged();
        SaveSelectedNetworkNameCommand.NotifyCanExecuteChanged();
        UpdateSelectedRouteDetails(value);
    }

    private async Task RunCheckAsync()
    {
        IsBusy = true;
        StatusText = "Checking";
        StatusDetail = "Reading Windows default routes...";
        RouteControlText = "Dry-run route control is idle.";

        try
        {
            var defaultRoutes = await _routeReader.GetDefaultRoutesAsync(CancellationToken.None);
            var orderedRoutes = DefaultRouteSelector.GetDefaultRoutes(defaultRoutes);
            var bestRoute = orderedRoutes.FirstOrDefault();
            var policyResult = _policyEngine.Evaluate(defaultRoutes, Settings);

            var previousSelectedKey = SelectedRoute is null
                ? null
                : CreateRouteKey(SelectedRoute.InterfaceIndex, SelectedRoute.RawGateway);
            var previousMonitoredKeys = _monitoredRouteKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var bestRouteKey = bestRoute is null
                ? null
                : CreateRouteKey(bestRoute.InterfaceIndex, bestRoute.NextHop);

            if (previousMonitoredKeys.Count == 0 && bestRouteKey is not null)
            {
                previousMonitoredKeys.Add(bestRouteKey);
            }

            Routes = new ObservableCollection<RouteRowViewModel>(
                orderedRoutes.Select(route =>
                {
                    var routeKey = CreateRouteKey(route.InterfaceIndex, route.NextHop);
                    return new RouteRowViewModel(
                        route,
                        route == bestRoute,
                        previousMonitoredKeys.Contains(routeKey),
                        Settings);
                }));

            SelectedRoute = RestoreSelection(previousSelectedKey)
                ?? Routes.FirstOrDefault(route => route.Role == "主回線")
                ?? Routes.FirstOrDefault();

            BestRouteText = bestRoute is null
                ? "No default route found."
                : $"{bestRoute.DestinationPrefix} via {FormatNextHop(bestRoute.NextHop)} on {bestRoute.InterfaceAlias} #{bestRoute.InterfaceIndex} (total metric {bestRoute.TotalMetric})";

            UpdatePhoneSummary(orderedRoutes, bestRoute);
            _bestInterfaceIndex = bestRoute?.InterfaceIndex;
            SyncTrafficMonitors();

            StatusText = policyResult.RiskLevel.ToString();
            StatusDetail = $"{policyResult.Message} Notify={policyResult.ShouldNotify}, BlockSim={policyResult.ShouldBlockSimRoute}";

            if (policyResult.ShouldBlockSimRoute)
            {
                var routeControlResult = await _routeController.RemoveSimDefaultRoutesAsync(
                    defaultRoutes,
                    CreateDryRunSettings(Settings),
                    CancellationToken.None);

                RouteControlText =
                    $"{routeControlResult.Message} Matched={routeControlResult.MatchedRouteCount}, Changed={routeControlResult.ChangedRouteCount}";
            }

            LastCheckedAt = DateTimeOffset.Now;
        }
        catch (Exception exception)
        {
            StatusText = "Error";
            StatusDetail = exception.Message;
            BestRouteText = "Check failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static NetworkGuardSettings CreateDryRunSettings(NetworkGuardSettings source)
    {
        return new NetworkGuardSettings
        {
            PrimaryWifiInterfaceAlias = source.PrimaryWifiInterfaceAlias,
            PrimaryWifiInterfaceIndex = source.PrimaryWifiInterfaceIndex,
            SimInterfaceAlias = source.SimInterfaceAlias,
            SimInterfaceIndex = source.SimInterfaceIndex,
            PrimaryWifiDisplayName = source.PrimaryWifiDisplayName,
            SimDisplayName = source.SimDisplayName,
            SimCarrierName = source.SimCarrierName,
            GatewayDisplayNames = new Dictionary<string, string>(source.GatewayDisplayNames, StringComparer.OrdinalIgnoreCase),
            Mode = source.Mode,
            EnableRouteChanges = false,
            EnableAdapterChanges = false,
            CheckIntervalSeconds = source.CheckIntervalSeconds,
            CultureName = source.CultureName,
            AllowedWifiSsids = [.. source.AllowedWifiSsids]
        };
    }

    private void UpdatePhoneSummary(
        IReadOnlyList<DefaultRouteInfo> orderedRoutes,
        DefaultRouteInfo? bestRoute)
    {
        var wifiRoute = orderedRoutes.FirstOrDefault(IsWifiRoute);
        var mobileDataRoute = orderedRoutes.FirstOrDefault(IsMobileDataRoute);
        var isWifiActive = bestRoute is not null && IsWifiRoute(bestRoute);
        var isMobileDataActive = bestRoute is not null && IsMobileDataRoute(bestRoute);

        WifiStatusText = isWifiActive ? "使用中" : wifiRoute is null ? "未接続" : "可用";
        IsWifiToggleChecked = wifiRoute is not null;
        WifiDetailText = wifiRoute is null
            ? $"{Settings.PrimaryWifiInterfaceAlias} #{FormatIndex(Settings.PrimaryWifiInterfaceIndex)}"
            : $"{Settings.PrimaryWifiInterfaceAlias} #{wifiRoute.InterfaceIndex} ・ {FormatNextHop(wifiRoute.NextHop)}";

        MobileDataStatusText = isMobileDataActive ? "使用中" : mobileDataRoute is null ? "未接続" : "可用";
        MobileDataDetailText = mobileDataRoute is null
            ? $"{Settings.SimCarrierName} ・ {Settings.SimInterfaceAlias} #{FormatIndex(Settings.SimInterfaceIndex)}"
            : $"{Settings.SimCarrierName} ・ {FormatNextHop(mobileDataRoute.NextHop)} ・ {Settings.SimInterfaceAlias} #{mobileDataRoute.InterfaceIndex}";

        ActiveLineText = bestRoute is null
            ? "現在の主回線: なし"
            : isMobileDataActive
                ? $"現在の主回線: {RouterLineLabel} ({Settings.SimDisplayName})"
                : isWifiActive
                    ? $"現在の主回線: Wi-Fi ({Settings.PrimaryWifiDisplayName})"
                    : $"現在の主回線: {bestRoute.InterfaceAlias} #{bestRoute.InterfaceIndex}";
    }

    private bool IsWifiRoute(DefaultRouteInfo route)
    {
        if (Settings.PrimaryWifiInterfaceIndex is { } wifiInterfaceIndex
            && route.InterfaceIndex == wifiInterfaceIndex)
        {
            return true;
        }

        return string.Equals(route.InterfaceAlias, Settings.PrimaryWifiInterfaceAlias, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMobileDataRoute(DefaultRouteInfo route)
    {
        if (Settings.SimInterfaceIndex is { } simInterfaceIndex
            && route.InterfaceIndex == simInterfaceIndex)
        {
            return true;
        }

        return string.Equals(route.InterfaceAlias, Settings.SimInterfaceAlias, StringComparison.OrdinalIgnoreCase);
    }

    private string FormatNextHop(string nextHop)
    {
        return Settings.GatewayDisplayNames.TryGetValue(nextHop, out var displayName)
            ? $"{displayName} ({nextHop})"
            : nextHop;
    }

    private static string FormatIndex(int? interfaceIndex)
    {
        return interfaceIndex?.ToString() ?? "auto";
    }

    private async Task SetWifiEnabledAsync(bool enabled)
    {
        IsBusy = true;
        var requestedStateText = enabled ? "開啟" : "關閉";
        AdapterControlStatusText = $"{requestedStateText} Wi-Fi 中...";

        try
        {
            var result = await _adapterController.SetAdapterEnabledAsync(
                Settings.PrimaryWifiInterfaceAlias,
                enabled,
                Settings,
                CancellationToken.None);

            RouteControlText = result.Message;
            AdapterControlStatusText = result.Message;

            await Task.Delay(TimeSpan.FromSeconds(2));
            await RunCheckAsync();
        }
        catch (Exception exception)
        {
            RouteControlText = exception.Message;
            AdapterControlStatusText = exception.Message;
            await RunCheckAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task SetWifiEnabledAsync(bool? enabled)
    {
        return SetWifiEnabledAsync(enabled ?? false);
    }

    private void MoveSelectedRouteUp()
    {
        MoveSelectedRoute(offset: -1);
    }

    private void MoveSelectedRouteDown()
    {
        MoveSelectedRoute(offset: 1);
    }

    private void MoveSelectedRoute(int offset)
    {
        if (SelectedRoute is null)
        {
            return;
        }

        var currentIndex = Routes.IndexOf(SelectedRoute);
        var newIndex = currentIndex + offset;

        if (currentIndex < 0 || newIndex < 0 || newIndex >= Routes.Count)
        {
            return;
        }

        Routes.Move(currentIndex, newIndex);
        RouteControlText = "Dry-run priority preview updated. Windows metrics were not changed.";
    }

    private void MonitorRoute(RouteRowViewModel? route)
    {
        if (route is null)
        {
            return;
        }

        var routeKey = CreateRouteKey(route.InterfaceIndex, route.RawGateway);

        if (route.IsMonitored)
        {
            _monitoredRouteKeys.Add(routeKey);
        }
        else
        {
            _monitoredRouteKeys.Remove(routeKey);
        }

        SelectedRoute = route;
        SyncTrafficMonitors();
    }

    private void LoadEditableSettings()
    {
        EditableWifiDisplayName = Settings.PrimaryWifiDisplayName;
        EditableSimDisplayName = Settings.SimDisplayName;
        EditableSimCarrierName = Settings.SimCarrierName;
        EditableGatewayAddress = Settings.GatewayDisplayNames.Keys.FirstOrDefault() ?? "192.168.100.1";
        EditableGatewayDisplayName = Settings.GatewayDisplayNames.TryGetValue(EditableGatewayAddress, out var displayName)
            ? displayName
            : Settings.SimDisplayName;
    }

    private void OpenSettingsWindow()
    {
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? Application.Current.MainWindow;

        var settingsWindow = new SettingsWindow(Settings, Routes)
        {
            Owner = owner
        };

        if (settingsWindow.ShowDialog() == true)
        {
            LoadEditableSettings();
            OnPropertyChanged(nameof(SettingsSummary));
            OnPropertyChanged(nameof(WifiDisplayName));
            OnPropertyChanged(nameof(RouterDisplayName));
            OnPropertyChanged(nameof(MobileDataCarrierName));
            OnPropertyChanged(nameof(OptionsSummary));
            UpdateAdapterControlStatus();
            RouteControlText = "設定已儲存。";
            _ = RunCheckAsync();
        }
    }

    private void UpdateAdapterControlStatus()
    {
        AdapterControlStatusText = Settings.EnableAdapterChanges
            ? "Wi-Fi 開關會要求系統管理員權限並實際變更網卡。"
            : "目前是 dry-run：按鈕只預演，不會真的開關 Wi-Fi。";
    }

    private void SaveSettings()
    {
        Settings.PrimaryWifiDisplayName = EditableWifiDisplayName.Trim();
        Settings.SimDisplayName = EditableSimDisplayName.Trim();
        Settings.SimCarrierName = EditableSimCarrierName.Trim();

        if (!string.IsNullOrWhiteSpace(EditableGatewayAddress)
            && !string.IsNullOrWhiteSpace(EditableGatewayDisplayName))
        {
            Settings.GatewayDisplayNames[EditableGatewayAddress.Trim()] = EditableGatewayDisplayName.Trim();
        }

        TraySettingsLoader.Save(Settings);
        OnPropertyChanged(nameof(SettingsSummary));
        OnPropertyChanged(nameof(WifiDisplayName));
        OnPropertyChanged(nameof(RouterDisplayName));
        OnPropertyChanged(nameof(MobileDataCarrierName));
        OnPropertyChanged(nameof(OptionsSummary));
        RouteControlText = "設定已儲存。下一次更新會使用新的顯示名稱。";
    }

    private void SaveSelectedNetworkName()
    {
        if (SelectedRoute is null || string.IsNullOrWhiteSpace(EditableSelectedNetworkName))
        {
            return;
        }

        var displayName = EditableSelectedNetworkName.Trim();

        if (SelectedRoute.InterfaceIndex == Settings.PrimaryWifiInterfaceIndex
            || string.Equals(SelectedRoute.InterfaceAlias, Settings.PrimaryWifiInterfaceAlias, StringComparison.OrdinalIgnoreCase))
        {
            Settings.PrimaryWifiDisplayName = displayName;
            EditableWifiDisplayName = displayName;
        }
        else if (SelectedRoute.InterfaceIndex == Settings.SimInterfaceIndex
            || string.Equals(SelectedRoute.InterfaceAlias, Settings.SimInterfaceAlias, StringComparison.OrdinalIgnoreCase))
        {
            Settings.SimDisplayName = displayName;
            EditableSimDisplayName = displayName;
        }

        if (!string.IsNullOrWhiteSpace(SelectedRoute.RawGateway))
        {
            Settings.GatewayDisplayNames[SelectedRoute.RawGateway] = displayName;
        }

        TraySettingsLoader.Save(Settings);
        RouteControlText = $"{SelectedRoute.RawGateway} 已對應為 {displayName}。";
        OnPropertyChanged(nameof(SettingsSummary));
        OnPropertyChanged(nameof(WifiDisplayName));
        OnPropertyChanged(nameof(RouterDisplayName));
    }

    private void UpdateSelectedRouteDetails(RouteRowViewModel? route)
    {
        if (route is null)
        {
            _selectedRouteKey = null;
            SelectedNetworkTitle = "選取左側網路";
            SelectedNetworkDetail = "選取一列後，可查看它的流量並編輯顯示名稱。";
            EditableSelectedNetworkName = string.Empty;
            return;
        }

        var routeKey = CreateRouteKey(route.InterfaceIndex, route.RawGateway);
        var isSameRoute = routeKey == _selectedRouteKey;
        _selectedRouteKey = routeKey;

        SelectedNetworkTitle = route.NetworkName;
        SelectedNetworkDetail = $"{route.Gateway} ・ {route.Interface} ・ {route.AddressFamily}";

        if (!isSameRoute)
        {
            EditableSelectedNetworkName = route.NetworkName.Split(" / ", StringSplitOptions.None)[0];
        }
    }

    private void StartTrafficTimer()
    {
        _trafficTimer.Interval = TimeSpan.FromSeconds(1);
        _trafficTimer.Tick += (_, _) => SampleTraffic();
        _trafficTimer.Start();
    }

    private void StartAutoRefresh()
    {
        _refreshTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Settings.CheckIntervalSeconds, 1, 3600));
        _refreshTimer.Tick += async (_, _) =>
        {
            if (!IsBusy)
            {
                await RunCheckAsync();
            }
        };
        _refreshTimer.Start();
        _ = RunCheckAsync();
    }

    private void SampleTraffic()
    {
        if (TrafficMonitors.Count == 0)
        {
            return;
        }

        foreach (var monitor in TrafficMonitors)
        {
            SampleTraffic(monitor);
        }
    }

    private static void SampleTraffic(TrafficMonitorViewModel monitor)
    {
        var networkInterface = FindNetworkInterface(monitor.InterfaceIndex);

        if (networkInterface is null)
        {
            monitor.RateText = "找不到網卡";
            return;
        }

        var statistics = networkInterface.GetIPv4Statistics();
        var now = DateTimeOffset.Now;
        var bytesReceived = statistics.BytesReceived;
        var bytesSent = statistics.BytesSent;

        if (monitor.LastBytesReceived is null
            || monitor.LastBytesSent is null
            || monitor.LastSampledAt is null)
        {
            monitor.LastBytesReceived = bytesReceived;
            monitor.LastBytesSent = bytesSent;
            monitor.LastSampledAt = now;
            return;
        }

        var elapsedSeconds = Math.Max(0.001, (now - monitor.LastSampledAt.Value).TotalSeconds);
        var rxBps = Math.Max(0, (bytesReceived - monitor.LastBytesReceived.Value) * 8 / elapsedSeconds);
        var txBps = Math.Max(0, (bytesSent - monitor.LastBytesSent.Value) * 8 / elapsedSeconds);
        var totalBps = rxBps + txBps;

        monitor.LastBytesReceived = bytesReceived;
        monitor.LastBytesSent = bytesSent;
        monitor.LastSampledAt = now;

        monitor.RateText = $"↓ {FormatBitsPerSecond(rxBps)} / ↑ {FormatBitsPerSecond(txBps)}";
        monitor.AddSample(totalBps);
    }

    private void SyncTrafficMonitors()
    {
        if (_monitoredRouteKeys.Count == 0 && _bestInterfaceIndex is { } bestInterfaceIndex)
        {
            var bestRoute = Routes.FirstOrDefault(route => route.InterfaceIndex == bestInterfaceIndex && route.Role == "主回線");

            if (bestRoute is not null)
            {
                _monitoredRouteKeys.Add(CreateRouteKey(bestRoute.InterfaceIndex, bestRoute.RawGateway));
                bestRoute.IsMonitored = true;
            }
        }

        var existingByKey = TrafficMonitors.ToDictionary(monitor => monitor.Key, StringComparer.OrdinalIgnoreCase);
        var nextMonitors = new ObservableCollection<TrafficMonitorViewModel>();

        foreach (var route in Routes.Where(route => route.IsMonitored))
        {
            var routeKey = CreateRouteKey(route.InterfaceIndex, route.RawGateway);

            if (!_monitoredRouteKeys.Contains(routeKey))
            {
                _monitoredRouteKeys.Add(routeKey);
            }

            nextMonitors.Add(existingByKey.TryGetValue(routeKey, out var existing)
                ? existing
                : new TrafficMonitorViewModel(
                    routeKey,
                    route.InterfaceIndex,
                    route.NetworkName,
                    $"{route.Gateway} ・ {route.Interface}"));
        }

        TrafficMonitors = nextMonitors;
    }

    private RouteRowViewModel? RestoreSelection(string? routeKey)
    {
        return routeKey is null
            ? null
            : Routes.FirstOrDefault(route => CreateRouteKey(route.InterfaceIndex, route.RawGateway) == routeKey);
    }

    private static string CreateRouteKey(int interfaceIndex, string gateway)
    {
        return $"{interfaceIndex}|{gateway}";
    }

    private static NetworkInterface? FindNetworkInterface(int interfaceIndex)
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var properties = networkInterface.GetIPProperties();
            var ipv4Index = TryGetIndex(() => properties.GetIPv4Properties()?.Index);
            var ipv6Index = TryGetIndex(() => properties.GetIPv6Properties()?.Index);

            if (ipv4Index == interfaceIndex || ipv6Index == interfaceIndex)
            {
                return networkInterface;
            }
        }

        return null;
    }

    private static int? TryGetIndex(Func<int?> getIndex)
    {
        try
        {
            return getIndex();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static string FormatBitsPerSecond(double bps)
    {
        return bps switch
        {
            >= 1_000_000_000 => $"{bps / 1_000_000_000:0.0} Gbps",
            >= 1_000_000 => $"{bps / 1_000_000:0.0} Mbps",
            >= 1_000 => $"{bps / 1_000:0.0} Kbps",
            _ => $"{bps:0} bps"
        };
    }

}
