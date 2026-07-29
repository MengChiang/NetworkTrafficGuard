param(
    [string]$ServiceName = "NetworkTrafficGuard",
    [string]$DisplayName = "Network Traffic Guard",
    [string]$PublishPath = "$PSScriptRoot\..\artifacts\service"
)

$ErrorActionPreference = "Stop"
$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Administrator permission is required to install the Windows Service."
}

$serviceExe = Resolve-Path (Join-Path $PublishPath "NetworkTrafficGuard.Service.exe")
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= "`"$serviceExe`"" start= auto DisplayName= "`"$DisplayName`"" | Out-Null
sc.exe description $ServiceName "Monitors Windows default routes and monthly network usage." | Out-Null
Start-Service -Name $ServiceName

Write-Host "Installed and started service '$ServiceName'."
