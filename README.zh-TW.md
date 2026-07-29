# Network Traffic Guard 規劃

英文是本專案的預設文件語言。

其他語言版本：

- [English](README.md)
- [简体中文](README.zh-CN.md)
- [日本語](README.ja-JP.md)

## 1. 目的

Network Traffic Guard 是一個 Windows 常駐程式，用來避免電腦在 Wi-Fi 中斷後，無聲地改走昂貴或流量有限的備援網路。

原始使用情境是電腦同時連接：

- Wi-Fi：主要上網來源。
- 有線網路：連到家中的路由器，可能使用有限流量。

當 Wi-Fi 中斷時，Windows 可能會自動把 Internet default route 切到另一個可用網路。本工具會監控路由狀態、顯示目前使用中的連線、顯示即時流量，並在使用者勾選的路由流量超過門檻時送出通知。

## 2. 目前範圍

目前 MVP 聚焦在本機 Windows 監控與 WPF tray UI。

已完成：

- 透過 PowerShell 讀取 Windows default routes。
- 依 route metric 與 interface metric 判斷最佳 default route。
- 顯示最高優先度的 Wi-Fi route 與最高優先度的非 Wi-Fi 網路介面。
- 斷線或停用的網路介面不顯示在狀態卡與流量監看中。
- 以精簡表格顯示路由優先順序。
- 可上下調整路由優先順序並保存。
- 啟用權限後，可把路由優先順序套用到 Windows。
- 啟用介面變更權限後，可從設定選單啟用或停用 Wi-Fi。
- 可針對勾選的路由顯示即時流量。
- 支援多個即時流量監看卡。
- 可針對每條路由勾選警示。
- 當警示路由的流量超過門檻時，顯示 Windows tray notification。
- tray tooltip 顯示主要連線與目前流量。
- 可替偵測到的網路設定自訂顯示名稱。
- 警示設定已獨立成單獨視窗。
- 支援 UI 語言：英文、繁體中文、簡體中文、日文。

尚未完成：

- 每月流量統計。
- 臨時允許規則，例如 10 分鐘或直到重開機。
- 完整 Windows Service 部署流程。
- 使用原生 Windows IP Helper API 讀取路由。
- Wi-Fi SSID allow-list 強制檢查。
- 安裝程式與開機自動啟動註冊。

## 3. 用語

本工具使用通用網路用語，不假設備援連線一定是行動網路。

- Primary Wi-Fi：偏好的 Wi-Fi 連線。
- Secondary network：設定為備援或非優先的網路介面。
- Network interface：Windows 偵測到的任一網路介面。
- Gateway：default route 使用的 next-hop 位址。
- Display name：使用者自訂、顯示在 UI 上的名稱。
- Alert route：被勾選用來監控流量門檻的路由。

系統訊息、log 與程式識別字使用英文。UI 文字支援多國語系。

## 4. 專案結構

```text
NetworkTrafficGuard.Core
  Domain models、settings、route selection 與 policy logic。

NetworkTrafficGuard.Windows
  Windows 專用 PowerShell route 與 adapter controller。

NetworkTrafficGuard.Tray
  WPF tray app、多國語系 UI、流量監看、設定視窗與通知。

NetworkTrafficGuard.Service
  背景監控用 worker service prototype。

NetworkTrafficGuard.Tests
  Policy 與 Windows command generation 相關單元測試。
```

## 5. 設定

範例：

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
  "CultureName": "zh-TW",
  "AllowedWifiSsids": []
}
```

重要開關：

- `EnableRouteChanges`：為 `false` 時，路由變更只會模擬，不會真的修改 Windows。
- `EnableAdapterChanges`：為 `false` 時，Wi-Fi 啟用/停用只會模擬。
- `AlertThresholdKbps`：路由流量通知門檻。
- `CultureName`：UI 語言，例如 `en-US`、`zh-TW`、`zh-CN` 或 `ja-JP`。

## 6. UI 行為

主畫面：

- 上方卡片顯示 Wi-Fi 與最高優先度的非 Wi-Fi 網路介面。
- 路由表格顯示是否顯示、是否警示、優先度、網路名稱、閘道與類型。
- 上移與下移按鈕可調整路由優先順序。
- 即時流量區會針對每個勾選顯示的路由顯示一張監看卡。

自訂名稱設定：

- 偵測到的網路、閘道與類型是唯讀欄位。
- 顯示名稱是可編輯欄位。
- 視窗再次開啟時，會從已儲存的設定讀取名稱。

警示設定：

- 警示門檻放在獨立設定視窗。
- 每條路由是否啟用警示仍在主畫面路由表格中勾選。

## 7. 開發流程

Build：

```powershell
dotnet build .\NetworkTrafficGuard.slnx
```

Test：

```powershell
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
```

執行 tray app：

```powershell
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

如果 tray app 已經在執行，建議先關閉再 build，因為 Windows 可能會鎖住輸出 DLL。

## 8. 測試重點

既有測試涵蓋：

- Wi-Fi route 優先時的 policy 行為。
- Secondary route active 的 policy 行為。
- Block mode 的 policy result。
- 沒有 default route 時的行為。
- 透過 interface index 判斷 secondary interface。
- 英文系統 policy message。
- PowerShell route-control dry-run 行為。

手動測試建議：

- 從 Windows 停用與重新啟用 Wi-Fi，確認 UI 即時更新。
- 新增或移除網路介面，確認上方狀態卡更新。
- 修改偵測到的網路名稱、儲存、重新開啟設定，確認名稱仍存在。
- 勾選多個流量監看，確認右側顯示多張監看卡。
- 勾選警示路由、超過門檻，確認 tray notification。

## 9. 下一步

建議下一階段：

1. 更清楚區分 interface display name 與 gateway display name。
2. 加入每月流量統計。
3. 加入安裝程式與開機自動啟動。
4. 將長時間監控移到 Windows Service。
5. 需要更穩定時，以原生 Windows API 取代 PowerShell route 讀取。
