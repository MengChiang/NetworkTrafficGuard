param(
    [string]$TrayExePath = "$PSScriptRoot\..\artifacts\tray\NetworkTrafficGuard.Tray.exe"
)

$ErrorActionPreference = "Stop"
$resolvedTrayExePath = Resolve-Path $TrayExePath
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name "NetworkTrafficGuard" -Value "`"$resolvedTrayExePath`""

Write-Host "Registered Network Traffic Guard tray app for user startup."
