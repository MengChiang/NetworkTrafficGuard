using FluentAssertions;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Tests.Policy;

public sealed class NetworkPolicyEngineTests
{
    private readonly NetworkPolicyEngine _engine = new();

    [Fact]
    public void Evaluate_WhenWifiRouteIsBest_ShouldReturnNormal()
    {
        var routes = new[]
        {
            CreateRoute(interfaceIndex: 10, interfaceAlias: "Wi-Fi", routeMetric: 10, interfaceMetric: 10),
            CreateRoute(interfaceIndex: 12, interfaceAlias: "Ethernet", routeMetric: 50, interfaceMetric: 50)
        };

        var result = _engine.Evaluate(routes, CreateSettings());

        result.RiskLevel.Should().Be(NetworkRiskLevel.Normal);
        result.ShouldNotify.Should().BeFalse();
        result.ShouldBlockSecondaryRoute.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WhenSecondaryRouteIsBestAndModeIsWarnOnly_ShouldNotifyWithoutBlocking()
    {
        var routes = new[]
        {
            CreateRoute(interfaceIndex: 10, interfaceAlias: "Wi-Fi", routeMetric: 50, interfaceMetric: 50),
            CreateRoute(interfaceIndex: 12, interfaceAlias: "Ethernet", routeMetric: 10, interfaceMetric: 10)
        };

        var result = _engine.Evaluate(routes, CreateSettings(mode: GuardMode.WarnOnly));

        result.RiskLevel.Should().Be(NetworkRiskLevel.SecondaryRouteActive);
        result.ShouldNotify.Should().BeTrue();
        result.ShouldBlockSecondaryRoute.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WhenSecondaryRouteIsBestAndModeIsBlock_ShouldNotifyAndRequestBlock()
    {
        var routes = new[]
        {
            CreateRoute(interfaceIndex: 12, interfaceAlias: "Ethernet", routeMetric: 10, interfaceMetric: 10)
        };

        var result = _engine.Evaluate(routes, CreateSettings(mode: GuardMode.BlockSecondaryWhenWifiDown));

        result.RiskLevel.Should().Be(NetworkRiskLevel.SecondaryRouteActive);
        result.ShouldNotify.Should().BeTrue();
        result.ShouldBlockSecondaryRoute.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WhenNoDefaultRouteExists_ShouldReturnUnknownAndNotify()
    {
        var routes = Array.Empty<DefaultRouteInfo>();

        var result = _engine.Evaluate(routes, CreateSettings());

        result.RiskLevel.Should().Be(NetworkRiskLevel.Unknown);
        result.ShouldNotify.Should().BeTrue();
        result.ShouldBlockSecondaryRoute.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WhenSecondaryInterfaceIndexMatches_ShouldTreatRouteAsSecondaryEvenIfAliasChanged()
    {
        var routes = new[]
        {
            CreateRoute(interfaceIndex: 12, interfaceAlias: "Secondary Ethernet", routeMetric: 10, interfaceMetric: 10)
        };

        var settings = CreateSettings(secondaryInterfaceAlias: "Ethernet", secondaryInterfaceIndex: 12);

        var result = _engine.Evaluate(routes, settings);

        result.RiskLevel.Should().Be(NetworkRiskLevel.SecondaryRouteActive);
    }

    [Fact]
    public void Evaluate_WhenCultureIsTraditionalChinese_ShouldReturnEnglishSystemMessage()
    {
        var routes = new[]
        {
            CreateRoute(interfaceIndex: 12, interfaceAlias: "Ethernet", routeMetric: 10, interfaceMetric: 10)
        };

        var result = _engine.Evaluate(routes, CreateSettings(cultureName: "zh-TW"));

        result.Message.Should().Be("The current Internet default route points to the secondary network.");
    }

    private static DefaultRouteInfo CreateRoute(
        int interfaceIndex,
        string interfaceAlias,
        uint routeMetric,
        uint interfaceMetric,
        string destinationPrefix = "0.0.0.0/0")
    {
        return new DefaultRouteInfo(
            destinationPrefix,
            NextHop: "192.168.8.1",
            interfaceIndex,
            interfaceAlias,
            routeMetric,
            interfaceMetric);
    }

    private static NetworkGuardSettings CreateSettings(
        GuardMode mode = GuardMode.WarnOnly,
        string secondaryInterfaceAlias = "Ethernet",
        int? secondaryInterfaceIndex = null,
        string cultureName = "en-US")
    {
        return new NetworkGuardSettings
        {
            PrimaryWifiInterfaceAlias = "Wi-Fi",
            SecondaryInterfaceAlias = secondaryInterfaceAlias,
            SecondaryInterfaceIndex = secondaryInterfaceIndex,
            Mode = mode,
            CultureName = cultureName
        };
    }
}
