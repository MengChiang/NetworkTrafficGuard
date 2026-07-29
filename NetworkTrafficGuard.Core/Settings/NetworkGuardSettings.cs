using NetworkTrafficGuard.Core.Localization;
using NetworkTrafficGuard.Core.Models;

namespace NetworkTrafficGuard.Core.Settings;

public sealed class NetworkGuardSettings
{
    public const int DefaultCheckIntervalSeconds = 3;

    public string PrimaryWifiInterfaceAlias { get; set; } = "Wi-Fi";

    public int? PrimaryWifiInterfaceIndex { get; set; }

    public string SimInterfaceAlias { get; set; } = "Ethernet";

    public int? SimInterfaceIndex { get; set; }

    public GuardMode Mode { get; set; } = GuardMode.WarnOnly;

    public int CheckIntervalSeconds { get; set; } = DefaultCheckIntervalSeconds;

    public string CultureName { get; set; } = SupportedCultures.DefaultCultureName;

    public List<string> AllowedWifiSsids { get; set; } = [];
}
