param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = "$PSScriptRoot\..\artifacts\tray"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\.."
$projectPath = Join-Path $repoRoot "NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj"

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    --output $OutputPath

Write-Host "Tray app published to $OutputPath"
