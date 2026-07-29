using FluentAssertions;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;

namespace NetworkTrafficGuard.Tests.Routes;

public sealed class DefaultRouteSelectorTests
{
    [Fact]
    public void GetDefaultRoutes_ShouldReturnOnlyDefaultRoutesOrderedByTotalMetric()
    {
        var routes = new[]
        {
            CreateRoute("192.168.0.0/24", interfaceIndex: 1, routeMetric: 1, interfaceMetric: 1),
            CreateRoute("::/0", interfaceIndex: 8, routeMetric: 16, interfaceMetric: 30),
            CreateRoute("0.0.0.0/0", interfaceIndex: 12, routeMetric: 50, interfaceMetric: 50),
            CreateRoute("0.0.0.0/0", interfaceIndex: 3, routeMetric: 5, interfaceMetric: 5)
        };

        var defaultRoutes = DefaultRouteSelector.GetDefaultRoutes(routes);

        defaultRoutes.Should().HaveCount(3);
        defaultRoutes.Select(route => route.InterfaceIndex).Should().Equal(3, 8, 12);
    }

    [Fact]
    public void GetBestDefaultRoute_ShouldReturnLowestMetricDefaultRoute()
    {
        var routes = new[]
        {
            CreateRoute("0.0.0.0/0", interfaceIndex: 12, routeMetric: 50, interfaceMetric: 50),
            CreateRoute("0.0.0.0/0", interfaceIndex: 8, routeMetric: 10, interfaceMetric: 30)
        };

        var bestRoute = DefaultRouteSelector.GetBestDefaultRoute(routes);

        bestRoute.Should().NotBeNull();
        bestRoute!.InterfaceIndex.Should().Be(8);
        bestRoute.TotalMetric.Should().Be(40);
    }

    private static DefaultRouteInfo CreateRoute(
        string destinationPrefix,
        int interfaceIndex,
        uint routeMetric,
        uint interfaceMetric)
    {
        return new DefaultRouteInfo(
            destinationPrefix,
            NextHop: "192.168.1.1",
            interfaceIndex,
            InterfaceAlias: $"Interface {interfaceIndex}",
            routeMetric,
            interfaceMetric);
    }
}
