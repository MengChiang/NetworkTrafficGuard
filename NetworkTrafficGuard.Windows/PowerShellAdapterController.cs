using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using NetworkTrafficGuard.Core.Adapters;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Windows;

public sealed class PowerShellAdapterController(ILogger<PowerShellAdapterController> logger) : IAdapterController
{
    public async Task<AdapterStatusResult> GetAdapterStatusAsync(
        string interfaceAlias,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceAlias);

        using var process = CreateAdapterStatusProcess(interfaceAlias);
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await WaitForExitWithTimeoutAsync(process, TimeSpan.FromSeconds(8), cancellationToken);

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return new AdapterStatusResult(
                Exists: false,
                Name: interfaceAlias,
                Status: "Unknown",
                InterfaceIndex: null,
                Message: string.IsNullOrWhiteSpace(error)
                    ? $"找不到網卡 '{interfaceAlias}'。"
                    : error);
        }

        var parts = output.Split('\t');
        var name = parts.ElementAtOrDefault(0) ?? interfaceAlias;
        var status = parts.ElementAtOrDefault(1) ?? "Unknown";
        var indexText = parts.ElementAtOrDefault(2);
        var interfaceIndex = int.TryParse(indexText, out var parsedIndex)
            ? parsedIndex
            : (int?)null;

        return new AdapterStatusResult(
            Exists: true,
            Name: name,
            Status: status,
            InterfaceIndex: interfaceIndex,
            Message: $"網卡 {name}: {status}");
    }

    public async Task<AdapterControlResult> SetAdapterEnabledAsync(
        string interfaceAlias,
        bool enabled,
        NetworkGuardSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceAlias);
        ArgumentNullException.ThrowIfNull(settings);

        var action = enabled ? "enable" : "disable";
        var actionText = enabled ? "開啟" : "關閉";

        if (!settings.EnableAdapterChanges)
        {
            logger.LogWarning(
                "Dry-run: would {Action} network adapter {InterfaceAlias}.",
                action,
                interfaceAlias);

            return new AdapterControlResult(
                IsDryRun: true,
                Changed: false,
                Message: $"Dry-run：只會預演，不會真的{actionText}網卡 '{interfaceAlias}'。");
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
                Message: $"{actionText}網卡 '{interfaceAlias}' 的系統管理員權限確認已取消。");
        }

        await WaitForExitWithTimeoutAsync(process, TimeSpan.FromSeconds(20), cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell adapter {action} failed with exit code {process.ExitCode}.");
        }

        var status = await GetAdapterStatusAsync(interfaceAlias, cancellationToken);

        return new AdapterControlResult(
            IsDryRun: false,
            Changed: true,
            Message: $"{actionText}網卡 '{interfaceAlias}' 指令已完成，目前狀態：{status.Status}。");
    }

    private static Process CreateAdapterStatusProcess(string interfaceAlias)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $adapter = Get-NetAdapter -Name '{{EscapePowerShell(interfaceAlias)}}' -ErrorAction Stop
            "$($adapter.Name)`t$($adapter.Status)`t$($adapter.ifIndex)"
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

    private static async Task WaitForExitWithTimeoutAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(timeout);

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
