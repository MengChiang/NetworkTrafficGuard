using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed partial class RouteRowViewModel(
    DefaultRouteInfo route,
    int priority,
    bool isMonitored,
    NetworkGuardSettings settings)
    : ObservableObject
{
    public DefaultRouteInfo Route { get; } = route;

    public string RouteKey { get; } = $"{route.InterfaceIndex}|{route.NextHop}";

    public int InterfaceIndex { get; } = route.InterfaceIndex;

    public string InterfaceAlias { get; } = route.InterfaceAlias;

    public string RawGateway { get; } = route.NextHop;

    [ObservableProperty]
    private int _priority = priority;

    public string NetworkName { get; } = FormatNetworkName(route, settings);

    public string Gateway { get; } = FormatNextHop(route.NextHop, settings);

    public string Interface { get; } = $"{route.InterfaceAlias} #{route.InterfaceIndex}";

    public string AddressFamily { get; } = route.DestinationPrefix == "::/0" ? "IPv6" : "IPv4";

    [ObservableProperty]
    private bool _isMonitored = isMonitored;

    private static string FormatNextHop(string nextHop, NetworkGuardSettings settings)
    {
        return settings.GatewayDisplayNames.TryGetValue(nextHop, out var displayName)
            ? $"{displayName} ({nextHop})"
            : nextHop;
    }

    private static string FormatNetworkName(DefaultRouteInfo route, NetworkGuardSettings settings)
    {
        if (settings.PrimaryWifiInterfaceIndex == route.InterfaceIndex
            || string.Equals(route.InterfaceAlias, settings.PrimaryWifiInterfaceAlias, StringComparison.OrdinalIgnoreCase))
        {
            return settings.PrimaryWifiDisplayName;
        }

        if (settings.SimInterfaceIndex == route.InterfaceIndex
            || string.Equals(route.InterfaceAlias, settings.SimInterfaceAlias, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(settings.SimCarrierName)
                ? settings.SimDisplayName
                : $"{settings.SimDisplayName} / {settings.SimCarrierName}";
        }

        return route.InterfaceAlias;
    }
}
