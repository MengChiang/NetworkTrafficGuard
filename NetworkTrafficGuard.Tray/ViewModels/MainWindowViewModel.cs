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
using NetworkTrafficGuard.Tray.Localization;
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
    private bool _isSwitchingWifiAdapter;
    private string _primaryTrafficName = "Network Traffic Guard";
    private TrafficMonitorViewModel? _primaryTrafficMonitor;
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
    private string _routeControlText = UiTextProvider.Get(null).RouteIdle;

    [ObservableProperty]
    private string _wifiStatusText = UiTextProvider.Get(null).Unknown;

    [ObservableProperty]
    private string _wifiDetailText = string.Empty;

    [ObservableProperty]
    private string _adapterControlStatusText = string.Empty;

    [ObservableProperty]
    private string _mobileDataStatusText = UiTextProvider.Get(null).Unknown;

    [ObservableProperty]
    private string _mobileDataDetailText = string.Empty;

    [ObservableProperty]
    private string _activeLineText = UiTextProvider.Get(null).Unknown;

    [ObservableProperty]
    private ObservableCollection<TrafficMonitorViewModel> _trafficMonitors = [];

    [ObservableProperty]
    private string _trayToolTipText = "Network Traffic Guard";

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
        _monitoredRouteKeys.UnionWith(settings.MonitoredRouteKeys);
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
        MoveRouteUpCommand = new AsyncRelayCommand(MoveSelectedRouteUpAsync, () => SelectedRoute is not null && !IsBusy);
        MoveRouteDownCommand = new AsyncRelayCommand(MoveSelectedRouteDownAsync, () => SelectedRoute is not null && !IsBusy);
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

    public IAsyncRelayCommand MoveRouteUpCommand { get; }

    public IAsyncRelayCommand MoveRouteDownCommand { get; }

    public IRelayCommand<RouteRowViewModel> MonitorRouteCommand { get; }

    public string SettingsSummary =>
        $"Wi-Fi {Settings.PrimaryWifiDisplayName} ({Settings.PrimaryWifiInterfaceAlias} #{FormatIndex(Settings.PrimaryWifiInterfaceIndex)}) | " +
        $"回線 {Settings.SimDisplayName} / {Settings.SimCarrierName} ({Settings.SimInterfaceAlias} #{FormatIndex(Settings.SimInterfaceIndex)}) | " +
        $"Mode {Settings.Mode} | Route changes {(Settings.EnableRouteChanges ? "enabled" : "simulation")} | Adapter changes {(Settings.EnableAdapterChanges ? "enabled" : "simulation")}";

    public string WifiDisplayName => Settings.PrimaryWifiDisplayName;

    public UiText Texts => UiTextProvider.Get(Settings.CultureName);

    public string RouterLineLabel => Texts.SimRouterLineLabel;

    public string RouterDisplayName => Settings.SimDisplayName;

    public string MobileDataCarrierName => Settings.SimCarrierName;

    public string OptionsSummary =>
        $"Wi-Fi | Route {(Settings.EnableRouteChanges ? "enabled" : "simulation")} | Adapter {(Settings.EnableAdapterChanges ? "enabled" : "simulation")}";

    public string EnableWifiMenuText => $"{Texts.EnableAction} {Texts.WifiLabel}";

    public string DisableWifiMenuText => $"{Texts.DisableAction} {Texts.WifiLabel}";

    partial void OnIsBusyChanged(bool value)
    {
        RunCheckCommand.NotifyCanExecuteChanged();
        SetWifiEnabledCommand.NotifyCanExecuteChanged();
        EnableWifiCommand.NotifyCanExecuteChanged();
        DisableWifiCommand.NotifyCanExecuteChanged();
        MoveRouteUpCommand.NotifyCanExecuteChanged();
        MoveRouteDownCommand.NotifyCanExecuteChanged();
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

        try
        {
            var defaultRoutes = await _routeReader.GetDefaultRoutesAsync(CancellationToken.None);
            var metricOrderedRoutes = DefaultRouteSelector.GetDefaultRoutes(defaultRoutes);
            var orderedRoutes = ApplySavedRoutePriorities(metricOrderedRoutes);
            var bestRoute = metricOrderedRoutes.FirstOrDefault();
            var policyResult = _policyEngine.Evaluate(defaultRoutes, Settings);
            var wifiAdapterStatus = await GetWifiAdapterStatusAsync();

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
                orderedRoutes.Select((route, index) =>
                {
                    var routeKey = CreateRouteKey(route.InterfaceIndex, route.NextHop);
                    return new RouteRowViewModel(
                        route,
                        index + 1,
                        previousMonitoredKeys.Contains(routeKey),
                        Settings);
                }));

            SelectedRoute = RestoreSelection(previousSelectedKey)
                ?? RestoreSelection(bestRouteKey)
                ?? Routes.FirstOrDefault();

            BestRouteText = bestRoute is null
                ? "No default route found."
                : $"{bestRoute.DestinationPrefix} via {FormatNextHop(bestRoute.NextHop)} on {bestRoute.InterfaceAlias} #{bestRoute.InterfaceIndex} (total metric {bestRoute.TotalMetric})";

            UpdatePhoneSummary(orderedRoutes, bestRoute, wifiAdapterStatus);
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
            RoutePriorities = new Dictionary<string, int>(source.RoutePriorities, StringComparer.OrdinalIgnoreCase),
            MonitoredRouteKeys = [.. source.MonitoredRouteKeys],
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
        DefaultRouteInfo? bestRoute,
        AdapterStatusResult wifiAdapterStatus)
    {
        var wifiRoute = orderedRoutes.FirstOrDefault(IsWifiRoute);
        var mobileDataRoute = orderedRoutes.FirstOrDefault(IsMobileDataRoute);
        var isWifiActive = bestRoute is not null && IsWifiRoute(bestRoute);
        var isMobileDataActive = bestRoute is not null && IsMobileDataRoute(bestRoute);

        if (!_isSwitchingWifiAdapter)
        {
            WifiStatusText = FormatWifiStatusText(wifiAdapterStatus, isWifiActive);
            IsWifiToggleChecked = wifiAdapterStatus.IsEnabled;
        }

        WifiDetailText = FormatWifiDetailText(wifiAdapterStatus, wifiRoute);

        MobileDataStatusText = isMobileDataActive ? Texts.InUse : mobileDataRoute is null ? Texts.NotConnected : Texts.Available;
        MobileDataDetailText = mobileDataRoute is null
            ? string.Format(Texts.InterfaceFormat, FormatIndex(Settings.SimInterfaceIndex))
            : FormatNextHop(mobileDataRoute.NextHop);

        ActiveLineText = bestRoute is null
            ? Texts.NoPrimaryLine
            : isMobileDataActive
                ? string.Format(Texts.PrimaryLineFormat, Settings.SimDisplayName)
                : isWifiActive
                    ? string.Format(Texts.PrimaryLineFormat, Settings.PrimaryWifiDisplayName)
                    : string.Format(Texts.PrimaryLineFormat, $"#{bestRoute.InterfaceIndex}");

        UpdatePrimaryTrafficMonitor(bestRoute, isMobileDataActive, isWifiActive);
    }

    private void UpdatePrimaryTrafficMonitor(
        DefaultRouteInfo? bestRoute,
        bool isMobileDataActive,
        bool isWifiActive)
    {
        if (bestRoute is null)
        {
            _primaryTrafficName = Texts.NoPrimaryLine;
            _primaryTrafficMonitor = null;
            TrayToolTipText = _primaryTrafficName;
            return;
        }

        _primaryTrafficName = isMobileDataActive
            ? Settings.SimDisplayName
            : isWifiActive
                ? Settings.PrimaryWifiDisplayName
                : $"#{bestRoute.InterfaceIndex}";

        var key = CreateRouteKey(bestRoute.InterfaceIndex, bestRoute.NextHop);

        if (_primaryTrafficMonitor is not null && string.Equals(_primaryTrafficMonitor.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            UpdateTrayToolTipText();
            return;
        }

        _primaryTrafficMonitor = new TrafficMonitorViewModel(
            key,
            bestRoute.InterfaceIndex,
            _primaryTrafficName,
            string.Empty);
        UpdateTrayToolTipText();
    }

    private async Task<AdapterStatusResult> GetWifiAdapterStatusAsync()
    {
        try
        {
            return await _adapterController.GetAdapterStatusAsync(
                Settings.PrimaryWifiInterfaceAlias,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            return new AdapterStatusResult(
                Exists: false,
                Name: Settings.PrimaryWifiInterfaceAlias,
                Status: "Unknown",
                InterfaceIndex: Settings.PrimaryWifiInterfaceIndex,
                Message: exception.Message);
        }
    }

    private string FormatWifiStatusText(AdapterStatusResult adapterStatus, bool isWifiActive)
    {
        if (!adapterStatus.Exists)
        {
            return Texts.Unknown;
        }

        if (isWifiActive)
        {
            return Texts.InUse;
        }

        return adapterStatus.Status switch
        {
            "Up" => Texts.Connected,
            "Disconnected" => Texts.NotConnected,
            "Disabled" => Texts.Disabled,
            "Not Present" => Texts.NotPresent,
            _ => FormatAdapterStatus(adapterStatus.Status)
        };
    }

    private string FormatWifiDetailText(AdapterStatusResult adapterStatus, DefaultRouteInfo? wifiRoute)
    {
        var adapterIndex = adapterStatus.InterfaceIndex ?? Settings.PrimaryWifiInterfaceIndex;
        var adapterText = adapterStatus.Exists
            ? string.Format(Texts.InterfaceFormat, FormatIndex(adapterIndex))
            : $"{string.Format(Texts.InterfaceFormat, FormatIndex(adapterIndex))} ・ {adapterStatus.Message}";

        return wifiRoute is null
            ? adapterText
            : adapterText;
    }

    private string FormatAdapterStatus(string status)
    {
        return status switch
        {
            "Up" => Texts.Connected,
            "Disconnected" => Texts.NotConnected,
            "Disabled" => Texts.Disabled,
            "Not Present" => Texts.NotPresent,
            _ => string.IsNullOrWhiteSpace(status) ? Texts.Unknown : status
        };
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
        _isSwitchingWifiAdapter = true;
        var requestedStateText = enabled ? Texts.EnableAction : Texts.DisableAction;
        AdapterControlStatusText = string.Format(Texts.WifiUpdatingFormat, requestedStateText);
        WifiStatusText = Texts.Updating;

        try
        {
            var result = await _adapterController.SetAdapterEnabledAsync(
                Settings.PrimaryWifiInterfaceAlias,
                enabled,
                Settings,
                CancellationToken.None);

            var commandResultText = result.IsDryRun
                ? Texts.AdapterDryRunNotice
                : Texts.SettingsSaved;
            RouteControlText = commandResultText;
            AdapterControlStatusText = commandResultText;

            await Task.Delay(TimeSpan.FromSeconds(2));
            _isSwitchingWifiAdapter = false;
            await RunCheckAsync();

            var refreshedStatus = await GetWifiAdapterStatusAsync();
            var expectedStateMatched = enabled
                ? refreshedStatus.IsEnabled
                : string.Equals(refreshedStatus.Status, "Disabled", StringComparison.OrdinalIgnoreCase);

            if (!result.IsDryRun && !expectedStateMatched)
            {
                AdapterControlStatusText =
                    string.Format(
                        Texts.AdapterStateMismatchFormat,
                        requestedStateText,
                        FormatAdapterStatus(refreshedStatus.Status));
            }
        }
        catch (Exception exception)
        {
            RouteControlText = exception.Message;
            AdapterControlStatusText = exception.Message;
            _isSwitchingWifiAdapter = false;
            await RunCheckAsync();
        }
        finally
        {
            _isSwitchingWifiAdapter = false;
            IsBusy = false;
        }
    }

    private Task SetWifiEnabledAsync(bool? enabled)
    {
        return SetWifiEnabledAsync(enabled ?? false);
    }

    private IReadOnlyList<DefaultRouteInfo> ApplySavedRoutePriorities(IReadOnlyList<DefaultRouteInfo> routes)
    {
        if (Settings.RoutePriorities.Count == 0)
        {
            return routes;
        }

        return routes
            .Select((route, index) => new
            {
                Route = route,
                DefaultIndex = index,
                Priority = Settings.RoutePriorities.TryGetValue(CreateRouteKey(route.InterfaceIndex, route.NextHop), out var priority)
                    ? priority
                    : int.MaxValue
            })
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.DefaultIndex)
            .Select(item => item.Route)
            .ToList();
    }

    private Task MoveSelectedRouteUpAsync()
    {
        return MoveSelectedRouteAsync(offset: -1);
    }

    private Task MoveSelectedRouteDownAsync()
    {
        return MoveSelectedRouteAsync(offset: 1);
    }

    private async Task MoveSelectedRouteAsync(int offset)
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
        RenumberRoutes();
        SaveRoutePreferences();
        SyncTrafficMonitors();
        await ApplyRoutePrioritiesAsync();
    }

    private void RenumberRoutes()
    {
        for (var index = 0; index < Routes.Count; index++)
        {
            Routes[index].Priority = index + 1;
        }
    }

    private void SaveRoutePreferences()
    {
        Settings.RoutePriorities = Routes
            .Select((route, index) => new { route.RouteKey, Priority = index + 1 })
            .ToDictionary(
                item => item.RouteKey,
                item => item.Priority,
                StringComparer.OrdinalIgnoreCase);

        var monitoredRouteKeys = Routes
            .Where(route => route.IsMonitored)
            .Select(route => route.RouteKey)
            .ToList();

        Settings.MonitoredRouteKeys = monitoredRouteKeys;
        _monitoredRouteKeys.Clear();
        _monitoredRouteKeys.UnionWith(monitoredRouteKeys);
        TraySettingsLoader.Save(Settings);
    }

    private async Task ApplyRoutePrioritiesAsync()
    {
        IsBusy = true;

        try
        {
            var result = await _routeController.ApplyDefaultRoutePrioritiesAsync(
                Routes.Select(route => route.Route).ToList(),
                Settings,
                CancellationToken.None);

            RouteControlText = result.IsDryRun
                ? Texts.RoutePrioritySaved
                : Texts.RoutePriorityApplied;

            if (!result.IsDryRun && result.ChangedRouteCount > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                await RunCheckAsync();
            }
        }
        catch (Exception exception)
        {
            RouteControlText = string.Format(Texts.RoutePriorityApplyFailedFormat, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
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

        SaveRoutePreferences();
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
        var owner = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current.MainWindow;

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
            OnPropertyChanged(nameof(Texts));
            OnPropertyChanged(nameof(RouterLineLabel));
            OnPropertyChanged(nameof(EnableWifiMenuText));
            OnPropertyChanged(nameof(DisableWifiMenuText));
            UpdateAdapterControlStatus();
            RouteControlText = Texts.SettingsSaved;
            _ = RunCheckAsync();
        }
    }

    private void UpdateAdapterControlStatus()
    {
        AdapterControlStatusText = Settings.EnableAdapterChanges
            ? Texts.AdapterEnabledNotice
            : Texts.AdapterDryRunNotice;
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
        RouteControlText = Texts.SettingsSaved;
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
        RouteControlText = Texts.NameSavedNotice;
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
        if (_primaryTrafficMonitor is not null)
        {
            SampleTraffic(_primaryTrafficMonitor);
            UpdateTrayToolTipText();
        }

        foreach (var monitor in TrafficMonitors)
        {
            SampleTraffic(monitor);
        }
    }

    private void UpdateTrayToolTipText()
    {
        var rateText = _primaryTrafficMonitor?.RateText ?? string.Empty;
        TrayToolTipText = string.IsNullOrWhiteSpace(rateText)
            ? _primaryTrafficName
            : $"{_primaryTrafficName}  {rateText}";
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
            var bestRoute = Routes.FirstOrDefault(route => route.InterfaceIndex == bestInterfaceIndex);

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
