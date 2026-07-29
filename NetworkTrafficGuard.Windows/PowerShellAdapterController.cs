using System.Diagnostics;
using System.ComponentModel;
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

        try
        {
            process.Start();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new AdapterControlResult(
                IsDryRun: false,
                Changed: false,
                Message: $"Adapter '{interfaceAlias}' {action} was cancelled by UAC.");
        }

        await WaitForExitWithTimeoutAsync(process, cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell adapter {action} failed with exit code {process.ExitCode}.");
        }

        return new AdapterControlResult(
            IsDryRun: false,
            Changed: true,
            Message: $"Adapter '{interfaceAlias}' {action} command completed. Windows may take a few seconds to update routes.");
    }

    private static Process CreatePowerShellProcess(string interfaceAlias, bool enabled)
    {
        var command = enabled ? "Enable-NetAdapter" : "Disable-NetAdapter";
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            {{command}} -Name '{{EscapePowerShell(interfaceAlias)}}' -Confirm:$false
            """;
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

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
