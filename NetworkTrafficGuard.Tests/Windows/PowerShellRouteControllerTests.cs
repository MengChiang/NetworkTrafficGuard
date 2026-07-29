using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Windows;

namespace NetworkTrafficGuard.Tests.Windows;

public sealed class PowerShellRouteControllerTests
{
    [Fact]
    public async Task RemoveSecondaryDefaultRoutesAsync_WhenRouteChangesAreDisabled_ShouldReturnDryRunResult()
    {
        var controller = new PowerShellRouteController(NullLogger<PowerShellRouteController>.Instance);
        var routes = new[]
        {
            new DefaultRouteInfo(
                "0.0.0.0/0",
                "192.168.100.1",
                InterfaceIndex: 12,
                InterfaceAlias: "Ethernet",
                RouteMetric: 50,
                InterfaceMetric: 50)
        };

        var settings = new NetworkGuardSettings
        {
            SecondaryInterfaceAlias = "Ethernet",
            SecondaryInterfaceIndex = 12,
            EnableRouteChanges = false
        };

        var result = await controller.RemoveSecondaryDefaultRoutesAsync(
            routes,
            settings,
            CancellationToken.None);

        result.IsDryRun.Should().BeTrue();
        result.MatchedRouteCount.Should().Be(1);
        result.ChangedRouteCount.Should().Be(0);
        result.MatchedRoutes.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveSecondaryDefaultRoutesAsync_WhenNoSecondaryDefaultRoutesMatch_ShouldNotChangeRoutes()
    {
        var controller = new PowerShellRouteController(NullLogger<PowerShellRouteController>.Instance);
        var routes = new[]
        {
            new DefaultRouteInfo(
                "0.0.0.0/0",
                "192.168.188.1",
                InterfaceIndex: 8,
                InterfaceAlias: "Wi-Fi",
                RouteMetric: 10,
                InterfaceMetric: 30)
        };

        var settings = new NetworkGuardSettings
        {
            SecondaryInterfaceAlias = "Ethernet",
            SecondaryInterfaceIndex = 12,
            EnableRouteChanges = false
        };

        var result = await controller.RemoveSecondaryDefaultRoutesAsync(
            routes,
            settings,
            CancellationToken.None);

        result.IsDryRun.Should().BeTrue();
        result.MatchedRouteCount.Should().Be(0);
        result.ChangedRouteCount.Should().Be(0);
        result.MatchedRoutes.Should().BeEmpty();
    }
}
