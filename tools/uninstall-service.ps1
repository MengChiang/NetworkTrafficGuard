param(
    [string]$ServiceName = "NetworkTrafficGuard"
)

$ErrorActionPreference = "Stop"
$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Administrator permission is required to uninstall the Windows Service."
}

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if (-not $existingService) {
    Write-Host "Service '$ServiceName' is not installed."
    exit 0
}

Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
sc.exe delete $ServiceName | Out-Null

Write-Host "Uninstalled service '$ServiceName'."
