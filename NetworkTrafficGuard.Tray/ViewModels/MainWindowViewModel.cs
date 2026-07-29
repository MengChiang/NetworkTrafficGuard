using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly INetworkPolicyEngine _policyEngine;

    [ObservableProperty]
    private NetworkGuardSettings _settings;

    [ObservableProperty]
    private ObservableCollection<RouteRowViewModel> _routes = [];

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
    private bool _isBusy;

    [ObservableProperty]
    private DateTimeOffset? _lastCheckedAt;

    public MainWindowViewModel()
        : this(
            TraySettingsLoader.Load(),
            new PowerShellRouteReader(NullLogger<PowerShellRouteReader>.Instance),
            new PowerShellRouteController(NullLogger<PowerShellRouteController>.Instance),
            new NetworkPolicyEngine())
    {
    }

    public MainWindowViewModel(
        NetworkGuardSettings settings,
        IRouteReader routeReader,
        IRouteController routeController,
        INetworkPolicyEngine policyEngine)
    {
        Settings = settings;
        _routeReader = routeReader;
        _routeController = routeController;
        _policyEngine = policyEngine;
        RunCheckCommand = new AsyncRelayCommand(RunCheckAsync, () => !IsBusy);
    }

    public IAsyncRelayCommand RunCheckCommand { get; }

    public string SettingsSummary =>
        $"Wi-Fi {Settings.PrimaryWifiDisplayName} ({Settings.PrimaryWifiInterfaceAlias} #{FormatIndex(Settings.PrimaryWifiInterfaceIndex)}) | " +
        $"Mobile {Settings.SimDisplayName} / {Settings.SimCarrierName} ({Settings.SimInterfaceAlias} #{FormatIndex(Settings.SimInterfaceIndex)}) | " +
        $"Mode {Settings.Mode} | Route changes {(Settings.EnableRouteChanges ? "enabled in config" : "disabled")}";

    public string WifiDisplayName => Settings.PrimaryWifiDisplayName;

    public string MobileDataDisplayName => Settings.SimDisplayName;

    public string MobileDataCarrierName => Settings.SimCarrierName;

    public string OptionsSummary =>
        $"優先: Wi-Fi | SIM接管: {Settings.Mode} | Route changes: {(Settings.EnableRouteChanges ? "enabled" : "dry-run")}";

    partial void OnIsBusyChanged(bool value)
    {
        RunCheckCommand.NotifyCanExecuteChanged();
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
                ? $"現在の主回線: モバイルデータ通信 ({Settings.SimDisplayName})"
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
}
