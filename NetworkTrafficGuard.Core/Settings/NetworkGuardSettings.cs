using NetworkTrafficGuard.Core.Localization;
using NetworkTrafficGuard.Core.Models;

namespace NetworkTrafficGuard.Core.Settings;

public sealed class NetworkGuardSettings
{
    public const int DefaultCheckIntervalSeconds = 3;

    public string PrimaryWifiInterfaceAlias { get; set; } = "Wi-Fi";

    public int? PrimaryWifiInterfaceIndex { get; set; }

    public string PrimaryWifiDisplayName { get; set; } = "Wi-Fi";

    public string SecondaryInterfaceAlias { get; set; } = "Ethernet";

    public int? SecondaryInterfaceIndex { get; set; }

    public string SecondaryDisplayName { get; set; } = "Secondary Network";

    public string SecondaryProviderName { get; set; } = string.Empty;

    public Dictionary<string, string> GatewayDisplayNames { get; set; } = [];

    public Dictionary<string, int> RoutePriorities { get; set; } = [];

    public List<string> MonitoredRouteKeys { get; set; } = [];

    public List<string> AlertRouteKeys { get; set; } = [];

    public int AlertThresholdKbps { get; set; } = 100;

    public bool ShowMonthlyTrafficUsage { get; set; }

    public GuardMode Mode { get; set; } = GuardMode.WarnOnly;

    public bool EnableRouteChanges { get; set; }

    public bool EnableAdapterChanges { get; set; }

    public int CheckIntervalSeconds { get; set; } = DefaultCheckIntervalSeconds;

    public string CultureName { get; set; } = SupportedCultures.DefaultCultureName;

    public List<string> AllowedWifiSsids { get; set; } = [];
}
