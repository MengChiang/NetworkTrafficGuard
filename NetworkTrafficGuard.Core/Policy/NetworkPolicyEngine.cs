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
                shouldBlockSecondaryRoute: false,
                settings.CultureName);
        }

        if (IsSecondaryRoute(bestRoute, settings))
        {
            return CreateResult(
                NetworkRiskLevel.SecondaryRouteActive,
                PolicyMessageKeys.SecondaryRouteActive,
                shouldNotify: true,
                shouldBlockSecondaryRoute: settings.Mode == GuardMode.BlockSecondaryWhenWifiDown,
                settings.CultureName);
        }

        return CreateResult(
            NetworkRiskLevel.Normal,
            PolicyMessageKeys.NormalRoute,
            shouldNotify: false,
            shouldBlockSecondaryRoute: false,
            settings.CultureName);
    }

    private static bool IsSecondaryRoute(DefaultRouteInfo route, NetworkGuardSettings settings)
    {
        if (settings.SecondaryInterfaceIndex is { } secondaryInterfaceIndex
            && route.InterfaceIndex == secondaryInterfaceIndex)
        {
            return true;
        }

        return string.Equals(
            route.InterfaceAlias,
            settings.SecondaryInterfaceAlias,
            StringComparison.OrdinalIgnoreCase);
    }

    private static NetworkPolicyResult CreateResult(
        NetworkRiskLevel riskLevel,
        string messageKey,
        bool shouldNotify,
        bool shouldBlockSecondaryRoute,
        string cultureName)
    {
        return new NetworkPolicyResult(
            riskLevel,
            LocalizedMessages.Get(messageKey, cultureName),
            shouldNotify,
            shouldBlockSecondaryRoute);
    }
}
