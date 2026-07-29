using CommunityToolkit.Mvvm.ComponentModel;

namespace NetworkTrafficGuard.Tray.ViewModels;

public sealed partial class NetworkNameMappingRowViewModel(RouteRowViewModel route) : ObservableObject
{
    public int InterfaceIndex { get; } = route.InterfaceIndex;

    public string InterfaceAlias { get; } = route.InterfaceAlias;

    public string RawGateway { get; } = route.RawGateway;

    public string CurrentNetwork { get; } = $"{route.Gateway} / {route.Interface} / {route.AddressFamily}";

    [ObservableProperty]
    private string _displayName = route.NetworkName.Split(" / ", StringSplitOptions.None)[0];
}
