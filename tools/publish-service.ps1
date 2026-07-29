param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = "$PSScriptRoot\..\artifacts\service"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\.."
$projectPath = Join-Path $repoRoot "NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj"

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    --output $OutputPath

Write-Host "Service published to $OutputPath"
