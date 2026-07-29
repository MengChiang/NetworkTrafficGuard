using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
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
    private readonly Queue<double> _trafficSamples = new();
    private int? _activeInterfaceIndex;
    private long? _lastBytesReceived;
    private long? _lastBytesSent;
    private DateTimeOffset? _lastTrafficSampleAt;

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
    private string _mobileDataStatusText = "未確認";

    [ObservableProperty]
    private string _mobileDataDetailText = "Press Update to check mobile data routing.";

    [ObservableProperty]
    private string _activeLineText = "未確認";

    [ObservableProperty]
    private string _trafficRateText = "即時流量: 未確認";

    [ObservableProperty]
    private string _trafficSparkline = "▁▁▁▁▁▁▁▁▁▁";

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
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        EnableWifiCommand = new AsyncRelayCommand(() => SetWifiEnabledAsync(enabled: true), () => !IsBusy);
        DisableWifiCommand = new AsyncRelayCommand(() => SetWifiEnabledAsync(enabled: false), () => !IsBusy);
        MoveRouteUpCommand = new RelayCommand(MoveSelectedRouteUp, () => SelectedRoute is not null);
        MoveRouteDownCommand = new RelayCommand(MoveSelectedRouteDown, () => SelectedRoute is not null);
        LoadEditableSettings();
        StartAutoRefresh();
        StartTrafficTimer();
    }

    public IAsyncRelayCommand RunCheckCommand { get; }

    public IRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand EnableWifiCommand { get; }

    public IAsyncRelayCommand DisableWifiCommand { get; }

    public IRelayCommand MoveRouteUpCommand { get; }

    public IRelayCommand MoveRouteDownCommand { get; }

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
        EnableWifiCommand.NotifyCanExecuteChanged();
        DisableWifiCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRouteChanged(RouteRowViewModel? value)
    {
        MoveRouteUpCommand.NotifyCanExecuteChanged();
        MoveRouteDownCommand.NotifyCanExecuteChanged();
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

            Routes = new ObservableCollection<RouteRowViewModel>(
                orderedRoutes.Select(route => new RouteRowViewModel(route, route == bestRoute, Settings)));

            BestRouteText = bestRoute is null
                ? "No default route found."
                : $"{bestRoute.DestinationPrefix} via {FormatNextHop(bestRoute.NextHop)} on {bestRoute.InterfaceAlias} #{bestRoute.InterfaceIndex} (total metric {bestRoute.TotalMetric})";

            UpdatePhoneSummary(orderedRoutes, bestRoute);
            _activeInterfaceIndex = bestRoute?.InterfaceIndex;
            ResetTrafficBaseline();

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

        WifiStatusText = isWifiActive ? "接続中" : wifiRoute is null ? "未接続" : "待機中";
        WifiDetailText = wifiRoute is null
            ? $"{Settings.PrimaryWifiInterfaceAlias} #{FormatIndex(Settings.PrimaryWifiInterfaceIndex)}"
            : $"{Settings.PrimaryWifiInterfaceAlias} #{wifiRoute.InterfaceIndex} ・ {FormatNextHop(wifiRoute.NextHop)}";

        MobileDataStatusText = isMobileDataActive ? "使用中" : mobileDataRoute is null ? "未接続" : "待機中";
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

        try
        {
            var result = await _adapterController.SetAdapterEnabledAsync(
                Settings.PrimaryWifiInterfaceAlias,
                enabled,
                Settings,
                CancellationToken.None);

            RouteControlText = result.Message;
        }
        catch (Exception exception)
        {
            RouteControlText = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
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
        if (_activeInterfaceIndex is null)
        {
            TrafficRateText = "即時流量: 等待路由檢查";
            return;
        }

        var networkInterface = FindNetworkInterface(_activeInterfaceIndex.Value);

        if (networkInterface is null)
        {
            TrafficRateText = "即時流量: 找不到目前主回線網卡";
            return;
        }

        var statistics = networkInterface.GetIPv4Statistics();
        var now = DateTimeOffset.Now;
        var bytesReceived = statistics.BytesReceived;
        var bytesSent = statistics.BytesSent;

        if (_lastBytesReceived is null || _lastBytesSent is null || _lastTrafficSampleAt is null)
        {
            _lastBytesReceived = bytesReceived;
            _lastBytesSent = bytesSent;
            _lastTrafficSampleAt = now;
            return;
        }

        var elapsedSeconds = Math.Max(0.001, (now - _lastTrafficSampleAt.Value).TotalSeconds);
        var rxBps = Math.Max(0, (bytesReceived - _lastBytesReceived.Value) * 8 / elapsedSeconds);
        var txBps = Math.Max(0, (bytesSent - _lastBytesSent.Value) * 8 / elapsedSeconds);
        var totalBps = rxBps + txBps;

        _lastBytesReceived = bytesReceived;
        _lastBytesSent = bytesSent;
        _lastTrafficSampleAt = now;

        TrafficRateText = $"即時流量: ↓ {FormatBitsPerSecond(rxBps)} / ↑ {FormatBitsPerSecond(txBps)}";
        AddTrafficSample(totalBps);
    }

    private void ResetTrafficBaseline()
    {
        _lastBytesReceived = null;
        _lastBytesSent = null;
        _lastTrafficSampleAt = null;
    }

    private void AddTrafficSample(double bps)
    {
        _trafficSamples.Enqueue(bps);

        while (_trafficSamples.Count > 24)
        {
            _trafficSamples.Dequeue();
        }

        TrafficSparkline = CreateSparkline(_trafficSamples);
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
