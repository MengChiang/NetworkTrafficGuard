using CommunityToolkit.Mvvm.ComponentModel;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed partial class NetworkNameMappingRowViewModel(
    RouteRowViewModel route,
    NetworkGuardSettings settings)
    : ObservableObject
{
    public int InterfaceIndex { get; } = route.InterfaceIndex;

    public string InterfaceAlias { get; } = route.InterfaceAlias;

    public string RawGateway { get; } = route.RawGateway;

    public string DetectedNetwork { get; } = route.InterfaceAlias;

    public string Gateway { get; } = string.IsNullOrWhiteSpace(route.RawGateway)
        ? route.Gateway
        : route.RawGateway;

    public string AddressFamily { get; } = route.AddressFamily;

    [ObservableProperty]
    private string _displayName = ResolveDisplayName(route, settings);

    private static string ResolveDisplayName(RouteRowViewModel route, NetworkGuardSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(route.RawGateway)
            && settings.GatewayDisplayNames.TryGetValue(route.RawGateway, out var gatewayDisplayName)
            && !string.IsNullOrWhiteSpace(gatewayDisplayName))
        {
            return gatewayDisplayName;
        }

        if (route.InterfaceIndex == settings.PrimaryWifiInterfaceIndex
            || string.Equals(route.InterfaceAlias, settings.PrimaryWifiInterfaceAlias, StringComparison.OrdinalIgnoreCase))
        {
            return settings.PrimaryWifiDisplayName;
        }

        if (route.InterfaceIndex == settings.SecondaryInterfaceIndex
            || string.Equals(route.InterfaceAlias, settings.SecondaryInterfaceAlias, StringComparison.OrdinalIgnoreCase))
        {
            return settings.SecondaryDisplayName;
        }

        return route.NetworkName.Split(" / ", StringSplitOptions.None)[0];
    }
}
