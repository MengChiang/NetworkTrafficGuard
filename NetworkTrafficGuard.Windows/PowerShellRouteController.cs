using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Windows;

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

    public async Task<RouteControlResult> ApplyDefaultRoutePrioritiesAsync(
        IReadOnlyList<DefaultRouteInfo> defaultRoutesInPriorityOrder,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaultRoutesInPriorityOrder);
        ArgumentNullException.ThrowIfNull(settings);

        var defaultRoutes = defaultRoutesInPriorityOrder
            .Where(route => DefaultRouteSelector.IsDefaultRoute(route.DestinationPrefix))
            .ToList();

        if (defaultRoutes.Count == 0)
        {
            return new RouteControlResult(
                IsDryRun: !settings.EnableRouteChanges,
                MatchedRouteCount: 0,
                ChangedRouteCount: 0,
                MatchedRoutes: defaultRoutes,
                Message: "No default routes matched.");
        }

        if (!settings.EnableRouteChanges)
        {
            logger.LogWarning(
                "Dry-run: would apply default route priorities: {Routes}",
                DescribeRoutes(defaultRoutes));

            return new RouteControlResult(
                IsDryRun: true,
                MatchedRouteCount: defaultRoutes.Count,
                ChangedRouteCount: 0,
                MatchedRoutes: defaultRoutes,
                Message: "已紀錄優先順序。Dry-run：Windows route metric 未變更。");
        }

        using var process = CreateApplyPrioritiesProcess(defaultRoutes);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new RouteControlResult(
                IsDryRun: false,
                MatchedRouteCount: defaultRoutes.Count,
                ChangedRouteCount: 0,
                MatchedRoutes: defaultRoutes,
                Message: "已紀錄優先順序，但 Windows 權限確認已取消。");
        }

        await WaitForExitWithTimeoutAsync(process, cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell route priority update failed with exit code {process.ExitCode}.");
        }

        return new RouteControlResult(
            IsDryRun: false,
            MatchedRouteCount: defaultRoutes.Count,
            ChangedRouteCount: defaultRoutes.Count,
            MatchedRoutes: defaultRoutes,
            Message: $"已紀錄並要求 Windows 套用 {defaultRoutes.Count} 條 route 優先度。");
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

    private static Process CreateApplyPrioritiesProcess(IReadOnlyList<DefaultRouteInfo> routes)
    {
        var scriptBuilder = new StringBuilder();
        scriptBuilder.AppendLine("$ErrorActionPreference = 'Stop'");
        scriptBuilder.AppendLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");

        for (var index = 0; index < routes.Count; index++)
        {
            var routeMetric = (index + 1) * 10;
            var route = routes[index];

            scriptBuilder.AppendLine(
                $"Get-NetRoute -DestinationPrefix '{EscapePowerShell(route.DestinationPrefix)}' -InterfaceIndex {route.InterfaceIndex} |");
            scriptBuilder.AppendLine(
                $"    Where-Object {{ $_.NextHop -eq '{EscapePowerShell(route.NextHop)}' }} |");
            scriptBuilder.AppendLine(
                $"    Set-NetRoute -RouteMetric {routeMetric} -Confirm:$false");
        }

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(scriptBuilder.ToString()));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        return new Process
        {
            StartInfo = startInfo
        };
    }

    private static async Task WaitForExitWithTimeoutAsync(Process process, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(60));

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
