$ErrorActionPreference = "Stop"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

Remove-ItemProperty -Path $runKey -Name "NetworkTrafficGuard" -ErrorAction SilentlyContinue

Write-Host "Removed Network Traffic Guard tray app from user startup."
