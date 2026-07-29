using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using NetworkTrafficGuard.Core.Adapters;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Windows;

public sealed class PowerShellAdapterController(ILogger<PowerShellAdapterController> logger) : IAdapterController
{
    public async Task<AdapterControlResult> SetAdapterEnabledAsync(
        string interfaceAlias,
        bool enabled,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceAlias);
        ArgumentNullException.ThrowIfNull(settings);

        var action = enabled ? "enable" : "disable";

        if (!settings.EnableAdapterChanges)
        {
            logger.LogWarning(
                "Dry-run: would {Action} network adapter {InterfaceAlias}.",
                action,
                interfaceAlias);

            return new AdapterControlResult(
                IsDryRun: true,
                Changed: false,
                Message: $"Dry-run only. Would {action} adapter '{interfaceAlias}'.");
        }

        using var process = CreatePowerShellProcess(interfaceAlias, enabled);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await WaitForExitWithTimeoutAsync(process, cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell adapter {action} failed with exit code {process.ExitCode}: {error}{output}");
        }

        return new AdapterControlResult(
            IsDryRun: false,
            Changed: true,
            Message: $"Adapter '{interfaceAlias}' {action} command completed.");
    }

    private static Process CreatePowerShellProcess(string interfaceAlias, bool enabled)
    {
        var command = enabled ? "Enable-NetAdapter" : "Disable-NetAdapter";
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            {{command}} -Name '{{EscapePowerShell(interfaceAlias)}}' -Confirm:$false
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
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            await process.WaitForExitAsync(timeoutTokenSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("PowerShell adapter command timed out.");
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
