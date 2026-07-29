using System.Diagnostics;
using System.Text;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Service.Windows;

public sealed class PowerShellRouteController(ILogger<PowerShellRouteController> logger) : IRouteController
{
    public async Task<RouteControlResult> RemoveSimDefaultRoutesAsync(
        IReadOnlyCollection<DefaultRouteInfo> defaultRoutes,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaultRoutes);
        ArgumentNullException.ThrowIfNull(settings);

        var matchedRoutes = defaultRoutes
            .Where(route => DefaultRouteSelector.IsDefaultRoute(route.DestinationPrefix))
            .Where(route => IsSimRoute(route, settings))
            .OrderBy(route => route.TotalMetric)
            .ToList();

        if (matchedRoutes.Count == 0)
        {
            return new RouteControlResult(
                IsDryRun: !settings.EnableRouteChanges,
                MatchedRouteCount: 0,
                ChangedRouteCount: 0,
                MatchedRoutes: matchedRoutes,
                Message: "No SIM default routes matched.");
        }

        if (!settings.EnableRouteChanges)
        {
            logger.LogWarning(
                "Dry-run: would remove {RouteCount} SIM default route(s): {Routes}",
                matchedRoutes.Count,
                DescribeRoutes(matchedRoutes));

            return new RouteControlResult(
                IsDryRun: true,
                MatchedRouteCount: matchedRoutes.Count,
                ChangedRouteCount: 0,
                MatchedRoutes: matchedRoutes,
                Message: "Dry-run only. No Windows routes were changed.");
        }

        var changedRouteCount = 0;

        foreach (var route in matchedRoutes)
        {
            await RemoveDefaultRouteAsync(route, cancellationToken);
            changedRouteCount++;
        }

        return new RouteControlResult(
            IsDryRun: false,
            MatchedRouteCount: matchedRoutes.Count,
            ChangedRouteCount: changedRouteCount,
            MatchedRoutes: matchedRoutes,
            Message: $"Removed {changedRouteCount} SIM default route(s).");
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

    private static string DescribeRoutes(IEnumerable<DefaultRouteInfo> routes)
    {
        return string.Join(
            "; ",
            routes.Select(route =>
                $"{route.DestinationPrefix} via {route.NextHop} on {route.InterfaceAlias}#{route.InterfaceIndex} metric {route.TotalMetric}"));
    }

    private static async Task RemoveDefaultRouteAsync(
        DefaultRouteInfo route,
        CancellationToken cancellationToken)
    {
        using var process = CreatePowerShellProcess(route);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await WaitForExitWithTimeoutAsync(process, cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell route removal failed with exit code {process.ExitCode}: {error}{output}");
        }
    }

    private static Process CreatePowerShellProcess(DefaultRouteInfo route)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            Get-NetRoute -DestinationPrefix '{{EscapePowerShell(route.DestinationPrefix)}}' -InterfaceIndex {{route.InterfaceIndex}} |
                Where-Object { $_.NextHop -eq '{{EscapePowerShell(route.NextHop)}}' } |
                Remove-NetRoute -Confirm:$false
            """;

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);

        return new Process
        {
            StartInfo = startInfo
        };
    }

    private static async Task WaitForExitWithTimeoutAsync(Process process, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await process.WaitForExitAsync(timeoutTokenSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("PowerShell route removal timed out.");
        }
    }

    private static string EscapePowerShell(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
