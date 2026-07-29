# Network Traffic Guard

A small Windows tray tool for watching the active network route, realtime traffic, and monthly usage. It is designed for PCs connected to more than one network, where one connection should be preferred and another connection may have limited data.

Languages: [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja-JP.md)

## Features

- Tray UI for Windows network status.
- Native Windows IP Helper API route reading.
- Realtime per-interface traffic monitor.
- Monthly traffic usage accounting.
- Route priority ordering with optional Windows apply.
- Wi-Fi enable and disable command from the settings menu.
- Traffic threshold alerts through Windows notifications.
- Custom display names for detected networks and gateways.
- English, Traditional Chinese, Simplified Chinese, and Japanese UI.
- Windows Service publish, install, uninstall, and startup scripts.
- Inno Setup installer script.

## Requirements

- Windows 10 or later.
- .NET 10 SDK for development.
- Administrator permission for Windows Service installation and system route or adapter changes.
- Inno Setup 6 if you want to build the installer.

## Development

```powershell
dotnet build .\NetworkTrafficGuard.slnx
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

Close the tray app before rebuilding if Windows locks the output files.

## Windows Service

Publish and install the service:

```powershell
.\tools\publish-service.ps1
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\install-service.ps1`""
```

Uninstall the service:

```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\uninstall-service.ps1`""
```

Run one service check locally:

```powershell
dotnet run --project .\NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj -- RunOnce=true
```

## Tray Startup

Publish and register the tray app for the current Windows user:

```powershell
.\tools\publish-tray.ps1
.\tools\register-tray-startup.ps1
```

Remove startup registration:

```powershell
.\tools\unregister-tray-startup.ps1
```

## Installer

Build release files first:

```powershell
.\tools\publish-tray.ps1
.\tools\publish-service.ps1
```

Then compile `installer\NetworkTrafficGuard.iss` with Inno Setup.

## Data

- App settings: project `appsettings.json` files during development.
- Monthly usage: `%LOCALAPPDATA%\NetworkTrafficGuard\traffic-usage.json`.
- Service name: `NetworkTrafficGuard`.
