using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
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
        $"Wi-Fi {Settings.PrimaryWifiInterfaceAlias} #{FormatIndex(Settings.PrimaryWifiInterfaceIndex)} | " +
        $"SIM {Settings.SimInterfaceAlias} #{FormatIndex(Settings.SimInterfaceIndex)} | " +
        $"Mode {Settings.Mode} | Route changes {(Settings.EnableRouteChanges ? "enabled in config" : "disabled")}";

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
                orderedRoutes.Select(route => new RouteRowViewModel(route, route == bestRoute)));

            BestRouteText = bestRoute is null
                ? "No default route found."
                : $"{bestRoute.DestinationPrefix} via {bestRoute.NextHop} on {bestRoute.InterfaceAlias} #{bestRoute.InterfaceIndex} (total metric {bestRoute.TotalMetric})";

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
            Mode = source.Mode,
            EnableRouteChanges = false,
            CheckIntervalSeconds = source.CheckIntervalSeconds,
            CultureName = source.CultureName,
            AllowedWifiSsids = [.. source.AllowedWifiSsids]
        };
    }

    private static string FormatIndex(int? interfaceIndex)
    {
        return interfaceIndex?.ToString() ?? "auto";
    }
}
