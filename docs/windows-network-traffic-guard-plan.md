# Network Traffic Guard Plan

English is the default documentation language for this project.

Localized versions:

- [Traditional Chinese](windows-network-traffic-guard-plan.zh-TW.md)
- [Simplified Chinese](windows-network-traffic-guard-plan.zh-CN.md)
- [Japanese](windows-network-traffic-guard-plan.ja-JP.md)

## 1. Purpose

Network Traffic Guard is a Windows resident application that helps prevent unexpected Internet usage through an expensive or limited backup network.

The original use case is a PC connected to both:

- Wi-Fi as the preferred Internet connection.
- A wired network connected to a home router that may use limited data.

When Wi-Fi disconnects, Windows may automatically switch the default Internet route to another available network. This app watches that routing state, shows which connection is active, displays live traffic, and alerts the user when selected routes exceed a threshold.

## 2. Current Scope

The current MVP focuses on local Windows monitoring and a WPF tray UI.

Implemented:

- Reads Windows default routes through PowerShell.
- Detects the best default route by route metric and interface metric.
- Shows the highest-priority Wi-Fi route and highest-priority non-Wi-Fi network interface.
- Hides disconnected or disabled network interfaces from the status cards and traffic monitor.
- Shows route priority in a compact table.
- Allows reordering route priority and saving the order.
- Allows route priority changes to be applied to Windows when enabled.
- Allows Wi-Fi enable/disable commands from the settings menu when adapter changes are enabled.
- Shows realtime traffic per selected route.
- Supports multiple selected traffic monitors.
- Supports alert checkboxes per route.
- Shows Windows tray notifications when monitored alert traffic exceeds the configured threshold.
- Shows tray tooltip text with the primary connection and current traffic rate.
- Provides custom display-name settings for detected networks.
- Provides separate alert settings.
- Supports UI languages: English, Traditional Chinese, Simplified Chinese, and Japanese.

Not implemented yet:

- Monthly data usage accounting.
- Temporary allow rules, such as 10 minutes or until restart.
- A full Windows Service deployment flow.
- Native Windows IP Helper API route reading.
- Wi-Fi SSID allow-list enforcement.
- Installer and auto-start registration.

## 3. Terminology

The app uses generic network terminology instead of assuming the backup connection is mobile data.

- Primary Wi-Fi: the preferred Wi-Fi connection.
- Secondary network: a configured backup or non-preferred network interface.
- Network interface: any detected Windows network interface.
- Gateway: the next-hop address used by a default route.
- Display name: a user-defined name shown in the UI.
- Alert route: a route selected for traffic-threshold notifications.

System messages, logs, and code identifiers are written in English. UI text is localized.

## 4. Project Structure

```text
NetworkTrafficGuard.Core
  Domain models, settings, route selection, and policy logic.

NetworkTrafficGuard.Windows
  Windows-specific PowerShell route and adapter controllers.

NetworkTrafficGuard.Tray
  WPF tray application, localized UI, traffic monitor, settings windows, and notifications.

NetworkTrafficGuard.Service
  Worker-service prototype for background monitoring.

NetworkTrafficGuard.Tests
  Unit tests for policy and Windows command generation behavior.
```

## 5. Settings

Example:

```json
{
  "PrimaryWifiInterfaceAlias": "Wi-Fi",
  "PrimaryWifiInterfaceIndex": null,
  "PrimaryWifiDisplayName": "Home Wi-Fi",
  "SecondaryInterfaceAlias": "Ethernet",
  "SecondaryInterfaceIndex": null,
  "SecondaryDisplayName": "Backup Router",
  "SecondaryProviderName": "",
  "GatewayDisplayNames": {
    "192.168.100.1": "Backup Router"
  },
  "RoutePriorities": {},
  "MonitoredRouteKeys": [],
  "AlertRouteKeys": [],
  "AlertThresholdKbps": 100,
  "Mode": "WarnOnly",
  "EnableRouteChanges": false,
  "EnableAdapterChanges": false,
  "CheckIntervalSeconds": 3,
  "CultureName": "en-US",
  "AllowedWifiSsids": []
}
```

Important flags:

- `EnableRouteChanges`: when `false`, route changes are simulation-only.
- `EnableAdapterChanges`: when `false`, Wi-Fi adapter enable/disable commands are simulation-only.
- `AlertThresholdKbps`: threshold for route traffic notifications.
- `CultureName`: UI language, such as `en-US`, `zh-TW`, `zh-CN`, or `ja-JP`.

## 6. UI Behavior

Main window:

- Top cards show Wi-Fi and the highest-priority non-Wi-Fi network interface.
- The route table shows visible routes, alert selection, priority, network name, gateway, and type.
- Up and down buttons reorder route priority.
- Realtime traffic shows one card per selected route.

Custom name settings:

- Detected network, gateway, and type are read-only columns.
- Display name is the editable column.
- Saved names are read from settings when the window is opened again.

Alert settings:

- Alert threshold is configured in a separate settings window.
- Per-route alert selection remains in the main route table.

## 7. Development Workflow

Build:

```powershell
dotnet build .\NetworkTrafficGuard.slnx
```

Test:

```powershell
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
```

Run tray app:

```powershell
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

If the tray app is already running, close it before building because Windows may lock output DLLs.

## 8. Testing Notes

Existing tests cover:

- Wi-Fi route preferred policy behavior.
- Secondary route active policy behavior.
- Block mode policy result.
- No-default-route behavior.
- Secondary interface matching by index.
- English system policy messages.
- PowerShell route-control dry-run behavior.

Manual testing should cover:

- Disable and re-enable Wi-Fi from Windows and verify UI refresh.
- Add or remove a network adapter and verify the top cards update.
- Rename a detected network, save, reopen settings, and verify the saved name appears.
- Select multiple traffic monitors and verify all selected routes show traffic cards.
- Enable an alert route, exceed the threshold, and verify tray notification behavior.

## 9. Next Steps

Recommended next development steps:

1. Add a clearer distinction between interface display names and gateway display names.
2. Add persistent monthly traffic accounting.
3. Add installer and startup registration.
4. Move long-running monitoring into the Windows Service.
5. Replace PowerShell route reads with native Windows APIs when stability requires it.
