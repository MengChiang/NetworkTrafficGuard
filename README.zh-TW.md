# Network Traffic Guard

一個 Windows 常駐工具，用來查看目前使用中的網路路由、即時流量與每月流量。適合電腦同時連上多個網路時使用，例如一個網路是主要連線，另一個網路有流量限制。

語言：[English](README.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja-JP.md)

## 功能

- Windows 系統匣介面。
- 使用原生 Windows IP Helper API 讀取路由。
- 依網路介面顯示即時流量。
- 統計每月流量使用量，並可選擇是否顯示在介面中。
- 調整連線優先順序，並可選擇是否套用到 Windows。
- 從設定選單啟用或停用 Wi-Fi。
- 使用 Windows 通知顯示流量警示。
- 自訂偵測到的網路與閘道顯示名稱。
- 支援英文、繁體中文、簡體中文、日文介面。
- 提供 Windows Service 發佈、安裝、移除與開機啟動腳本。
- 提供 Inno Setup 安裝程式腳本。

## 需求

- Windows 10 或更新版本。
- 開發時需要 .NET 10 SDK。
- 安裝 Windows Service、調整系統路由或網卡狀態時需要系統管理員權限。
- 若要產生安裝程式，需要 Inno Setup 6。

## 開發

```powershell
dotnet build .\NetworkTrafficGuard.slnx
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

如果系統匣程式正在執行，重建前請先關閉，避免 Windows 鎖住輸出檔。

## Windows Service

發佈並安裝 Service：

```powershell
.\tools\publish-service.ps1
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\install-service.ps1`""
```

移除 Service：

```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\uninstall-service.ps1`""
```

本機執行一次 Service 檢查：

```powershell
dotnet run --project .\NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj -- RunOnce=true
```

## 開機啟動

發佈並註冊目前使用者的系統匣程式開機啟動：

```powershell
.\tools\publish-tray.ps1
.\tools\register-tray-startup.ps1
```

移除開機啟動：

```powershell
.\tools\unregister-tray-startup.ps1
```

## 安裝程式

先產生發佈檔：

```powershell
.\tools\publish-tray.ps1
.\tools\publish-service.ps1
```

接著使用 Inno Setup 編譯 `installer\NetworkTrafficGuard.iss`。

## 資料

- 開發期間設定：各專案的 `appsettings.json`。
- 每月流量：`%LOCALAPPDATA%\NetworkTrafficGuard\traffic-usage.json`。
- Service 名稱：`NetworkTrafficGuard`。
