using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetworkTrafficGuard.Core.Models;
using NetworkTrafficGuard.Core.Routes;

namespace NetworkTrafficGuard.Service.Windows;

public sealed class PowerShellRouteReader(ILogger<PowerShellRouteReader> logger) : IRouteReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string RouteScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        $routes = Get-NetRoute | Where-Object {
            $_.DestinationPrefix -eq '0.0.0.0/0' -or $_.DestinationPrefix -eq '::/0'
        }
        $ipInterfaces = Get-NetIPInterface
        $results = foreach ($route in $routes) {
            $ipInterface = $ipInterfaces |
                Where-Object { $_.InterfaceIndex -eq $route.InterfaceIndex } |
                Select-Object -First 1

            [pscustomobject]@{
                DestinationPrefix = [string]$route.DestinationPrefix
                NextHop = [string]$route.NextHop
                InterfaceIndex = [int]$route.InterfaceIndex
                InterfaceAlias = [string]$route.InterfaceAlias
                RouteMetric = [uint32]$route.RouteMetric
                InterfaceMetric = if ($ipInterface) { [uint32]$ipInterface.InterfaceMetric } else { [uint32]0 }
            }
        }

        @($results) | ConvertTo-Json -Depth 4
        """;

    public async Task<IReadOnlyList<DefaultRouteInfo>> GetDefaultRoutesAsync(CancellationToken cancellationToken)
    {
        using var process = CreatePowerShellProcess();

        logger.LogDebug("Reading Windows default routes with PowerShell.");

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await WaitForExitWithTimeoutAsync(process, cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell route query failed with exit code {process.ExitCode}: {error}");
        }

        return ParseRoutes(output);
    }

    private static Process CreatePowerShellProcess()
    {
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
        startInfo.ArgumentList.Add(RouteScript);

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
            throw new TimeoutException("PowerShell route query timed out.");
        }
    }

    private static IReadOnlyList<DefaultRouteInfo> ParseRoutes(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var routeDtos = JsonSerializer.Deserialize<List<RouteDto>>(json, JsonOptions) ?? [];

        return routeDtos
            .Where(route => !string.IsNullOrWhiteSpace(route.DestinationPrefix))
            .Select(route => new DefaultRouteInfo(
                route.DestinationPrefix ?? string.Empty,
                route.NextHop ?? string.Empty,
                route.InterfaceIndex,
                route.InterfaceAlias ?? string.Empty,
                route.RouteMetric,
                route.InterfaceMetric))
            .ToList();
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

    private sealed record RouteDto(
        string? DestinationPrefix,
        string? NextHop,
        int InterfaceIndex,
        string? InterfaceAlias,
        uint RouteMetric,
        uint InterfaceMetric);
}
