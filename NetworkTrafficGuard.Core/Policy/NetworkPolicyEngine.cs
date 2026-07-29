using NetworkTrafficGuard.Core.Localization;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Core.Policy;

public sealed class NetworkPolicyEngine : INetworkPolicyEngine
{
    public NetworkPolicyResult Evaluate(
        IReadOnlyCollection<DefaultRouteInfo> defaultRoutes,
        NetworkGuardSettings settings)
    {
        ArgumentNullException.ThrowIfNull(defaultRoutes);
        ArgumentNullException.ThrowIfNull(settings);

        var bestRoute = DefaultRouteSelector.GetBestDefaultRoute(defaultRoutes);

        if (bestRoute is null)
        {
            return CreateResult(
                NetworkRiskLevel.Unknown,
                PolicyMessageKeys.NoDefaultRoute,
                shouldNotify: true,
                shouldBlockSimRoute: false,
                settings.CultureName);
        }

        if (IsSimRoute(bestRoute, settings))
        {
            return CreateResult(
                NetworkRiskLevel.SimRouteActive,
                PolicyMessageKeys.SimRouteActive,
                shouldNotify: true,
                shouldBlockSimRoute: settings.Mode == GuardMode.BlockSimWhenWifiDown,
                settings.CultureName);
        }

        return CreateResult(
            NetworkRiskLevel.Normal,
            PolicyMessageKeys.NormalRoute,
            shouldNotify: false,
            shouldBlockSimRoute: false,
            settings.CultureName);
    }

    private static bool IsSimRoute(DefaultRouteInfo route, NetworkGuardSettings settings)
    {
        if (settings.SimInterfaceIndex is { } simInterfaceIndex
            && route.InterfaceIndex == simInterfaceIndex)
        {
            return true;
        }

        return string.Equals(
            route.InterfaceAlias,
            settings.SimInterfaceAlias,
            StringComparison.OrdinalIgnoreCase);
    }

    private static NetworkPolicyResult CreateResult(
        NetworkRiskLevel riskLevel,
        string messageKey,
        bool shouldNotify,
        bool shouldBlockSimRoute,
        string cultureName)
    {
        return new NetworkPolicyResult(
            riskLevel,
            LocalizedMessages.Get(messageKey, cultureName),
            shouldNotify,
            shouldBlockSimRoute);
    }
}
