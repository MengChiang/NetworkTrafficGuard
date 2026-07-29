# Windows 網路流量守門員需求與開發計畫

## 1. 背景

目前電腦同時連接兩個網路：

- Wi-Fi：主要上網來源，有時會帶出門使用。
- 有線網路：連接家中的 SIM 路由器，流量有限，希望作為受控備援。

問題是當 Wi-Fi 中斷時，Windows 會自動改走有線 SIM 路由器，導致大量流量在未察覺的情況下被消耗。

目標是開發一個 Windows 常駐程式，像手機的行動數據保護機制一樣：

- 優先使用指定 Wi-Fi。
- 偵測目前 Internet 流量是否正在走 SIM 路由器。
- 在切換到 SIM 網路時警告。
- 必要時自動阻止 SIM 網路承接 Internet 流量。

## 2. 核心目標

### 必要目標

1. 偵測目前 Windows 的主要 Internet default route。
2. 判斷目前是否正在使用 SIM 有線網路上網。
3. 當 Wi-Fi 中斷且流量改走 SIM 有線網路時，立即通知使用者。
4. 提供設定讓使用者選擇：
   - 只警告。
   - 自動封鎖 SIM Internet。
   - 詢問後允許 SIM 臨時接管。
5. 程式需可開機自動啟動。

### 延伸目標

1. 監控 SIM 網路本月使用量。
2. 設定流量警戒值，例如 1GB、5GB、10GB。
3. 提供「允許 SIM 使用 10 分鐘 / 30 分鐘 / 直到下次重開機」。
4. 顯示 tray icon 狀態：
   - 綠色：Wi-Fi 正常。
   - 黃色：Wi-Fi 中斷，但 SIM 尚未接管。
   - 紅色：目前正在使用 SIM 上網。
5. 允許指定安全 Wi-Fi SSID 清單。
6. 匯出診斷報告，方便確認 Windows routing 狀態。

## 3. 建議 MVP 範圍

第一版先不要做太大，建議只做以下功能：

1. 設定主要 Wi-Fi 介面。
2. 設定 SIM 有線網路介面。
3. 每 2 至 5 秒檢查目前 default route。
4. 若 default route 指向 SIM 有線網路：
   - 顯示 Windows toast notification。
   - tray icon 變成紅色。
   - 若啟用「自動封鎖」，則移除或降低 SIM default route。
5. 提供簡單設定檔 `appsettings.json`。
6. 寫入文字 log。

## 4. 重要概念

### 4.1 Interface Metric

Windows 會依照 route metric 與 interface metric 選擇出口網路。

當 Wi-Fi 和有線 SIM 都可連 Internet 時，可以讓 Wi-Fi metric 較低、SIM metric 較高。這會讓 Windows 優先使用 Wi-Fi。

但這不會防止 Wi-Fi 斷線後 SIM 自動接手，因為 Windows 仍然會找到另一條可用 default route。

### 4.2 Default Route

真正要監控的是 default route：

```powershell
Get-NetRoute -DestinationPrefix "0.0.0.0/0"
```

若目前最佳 default route 的 `InterfaceAlias` 或 `InterfaceIndex` 是 SIM 有線網卡，就代表 Internet 流量可能正在走 SIM。

### 4.3 封鎖策略

有三種可選策略：

| 策略 | 說明 | 優點 | 缺點 |
| --- | --- | --- | --- |
| 警告 | 只通知使用者 | 最安全，不改系統設定 | 仍可能吃流量 |
| 移除 SIM default route | 保留 LAN，但不讓 SIM 當 Internet 出口 | 最符合需求 | 需要系統管理員權限 |
| 停用 SIM 網卡 | 直接停用網路介面 | 很有效 | 可能影響連線到 SIM 路由器管理頁 |

建議 MVP 使用「偵測 + 警告 + 可選移除 SIM default route」。

## 5. 建議技術架構

### 5.1 第一階段架構

先做單一 WPF tray app：

```text
NetworkTrafficGuard.App
├─ Tray UI
├─ Settings
├─ Network Monitor
├─ Route Controller
└─ Logging
```

優點是開發快，容易 debug。

缺點是若要改 route，程式需要以系統管理員權限執行。

### 5.2 穩定版架構

之後再拆成：

```text
NetworkTrafficGuard.Service
├─ 背景監控
├─ route 修改
├─ traffic counter
└─ event log

NetworkTrafficGuard.Tray
├─ tray icon
├─ notification
├─ settings UI
└─ 與 service 溝通

NetworkTrafficGuard.Core
├─ domain model
├─ network abstraction
├─ policy engine
└─ shared DTO

NetworkTrafficGuard.Tests
└─ unit tests
```

Service 以系統管理員權限安裝一次即可，tray app 用一般權限執行。

## 6. 建議專案建立步驟

### 6.1 建立 solution

```powershell
mkdir NetworkTrafficGuard
cd NetworkTrafficGuard

dotnet new sln -n NetworkTrafficGuard

dotnet new classlib -n NetworkTrafficGuard.Core
dotnet new wpf -n NetworkTrafficGuard.Tray
dotnet new worker -n NetworkTrafficGuard.Service
dotnet new xunit -n NetworkTrafficGuard.Tests

dotnet sln add .\NetworkTrafficGuard.Core\NetworkTrafficGuard.Core.csproj
dotnet sln add .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
dotnet sln add .\NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj
dotnet sln add .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj

dotnet add .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj reference .\NetworkTrafficGuard.Core\NetworkTrafficGuard.Core.csproj
dotnet add .\NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj reference .\NetworkTrafficGuard.Core\NetworkTrafficGuard.Core.csproj
dotnet add .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj reference .\NetworkTrafficGuard.Core\NetworkTrafficGuard.Core.csproj
```

### 6.2 建議 NuGet 套件

```powershell
dotnet add .\NetworkTrafficGuard.Tray package Hardcodet.NotifyIcon.Wpf
dotnet add .\NetworkTrafficGuard.Tray package CommunityToolkit.Mvvm
dotnet add .\NetworkTrafficGuard.Core package Microsoft.Extensions.Logging.Abstractions
dotnet add .\NetworkTrafficGuard.Service package Microsoft.Extensions.Hosting.WindowsServices
dotnet add .\NetworkTrafficGuard.Tests package FluentAssertions
```

若想做 toast notification，可再評估：

```powershell
dotnet add .\NetworkTrafficGuard.Tray package CommunityToolkit.WinUI.Notifications
```

## 7. Core 專案設計

### 7.1 Domain Models

```csharp
public sealed record NetworkAdapterInfo(
    int InterfaceIndex,
    string InterfaceAlias,
    string Description,
    bool IsWireless,
    bool IsUp);

public sealed record DefaultRouteInfo(
    string DestinationPrefix,
    string NextHop,
    int InterfaceIndex,
    string InterfaceAlias,
    uint RouteMetric,
    uint InterfaceMetric);

public enum NetworkRiskLevel
{
    Normal,
    WifiUnavailable,
    SimRouteActive,
    Unknown
}

public sealed record NetworkPolicyResult(
    NetworkRiskLevel RiskLevel,
    string Message,
    bool ShouldNotify,
    bool ShouldBlockSimRoute);
```

### 7.2 設定檔

```json
{
  "PrimaryWifiInterfaceAlias": "Wi-Fi",
  "SimInterfaceAlias": "Ethernet",
  "Mode": "WarnOnly",
  "CheckIntervalSeconds": 3,
  "AllowedWifiSsids": [
    "HomeWifi",
    "PhoneHotspot"
  ]
}
```

`Mode` 建議先支援：

- `WarnOnly`
- `BlockSimWhenWifiDown`
- `AskBeforeUsingSim`

### 7.3 Policy Engine

核心判斷應該獨立於 Windows API，方便測試：

```csharp
public sealed class NetworkPolicyEngine
{
    public NetworkPolicyResult Evaluate(
        IReadOnlyList<DefaultRouteInfo> defaultRoutes,
        string primaryWifiAlias,
        string simAlias,
        GuardMode mode)
    {
        var bestRoute = defaultRoutes
            .OrderBy(route => route.RouteMetric + route.InterfaceMetric)
            .FirstOrDefault();

        if (bestRoute is null)
        {
            return new NetworkPolicyResult(
                NetworkRiskLevel.Unknown,
                "找不到 default route。",
                ShouldNotify: true,
                ShouldBlockSimRoute: false);
        }

        if (string.Equals(bestRoute.InterfaceAlias, simAlias, StringComparison.OrdinalIgnoreCase))
        {
            return new NetworkPolicyResult(
                NetworkRiskLevel.SimRouteActive,
                "目前 Internet default route 指向 SIM 有線網路。",
                ShouldNotify: true,
                ShouldBlockSimRoute: mode == GuardMode.BlockSimWhenWifiDown);
        }

        return new NetworkPolicyResult(
            NetworkRiskLevel.Normal,
            "目前未使用 SIM 有線網路作為主要 Internet 出口。",
            ShouldNotify: false,
            ShouldBlockSimRoute: false);
    }
}
```

## 8. Windows 網路資訊取得方式

### 8.1 MVP：呼叫 PowerShell

第一版可以先用 PowerShell，開發速度最快：

```powershell
Get-NetRoute -DestinationPrefix "0.0.0.0/0" |
  Sort-Object { $_.RouteMetric + (Get-NetIPInterface -InterfaceIndex $_.InterfaceIndex).InterfaceMetric } |
  Select-Object -First 1
```

C# 可用 `ProcessStartInfo` 呼叫 PowerShell 並讀 JSON：

```powershell
Get-NetRoute -DestinationPrefix "0.0.0.0/0" |
  Select-Object DestinationPrefix,NextHop,InterfaceIndex,InterfaceAlias,RouteMetric |
  ConvertTo-Json
```

### 8.2 穩定版：使用 Windows API / WMI / CIM

後續可改成：

- `System.Net.NetworkInformation.NetworkInterface`
- WMI / CIM 查 route table
- Windows IP Helper API

建議先不要太早碰 P/Invoke。先把產品行為做對，再優化資料來源。

## 9. 封鎖 SIM Internet 的實作方式

### 9.1 移除 SIM default route

```powershell
Get-NetRoute -DestinationPrefix "0.0.0.0/0" -InterfaceAlias "Ethernet" |
  Remove-NetRoute -Confirm:$false
```

這通常需要系統管理員權限。

### 9.2 提高 SIM interface metric

```powershell
Set-NetIPInterface -InterfaceAlias "Ethernet" -InterfaceMetric 9000
```

這只能降低優先權，無法防止 Wi-Fi 斷線後 SIM 接管。

### 9.3 停用 SIM 網卡

```powershell
Disable-NetAdapter -Name "Ethernet" -Confirm:$false
```

比較激進，不建議作為預設。

## 10. 測試策略

### 10.1 Unit Tests

先測 `NetworkPolicyEngine`：

- Wi-Fi 是 best route 時，狀態為 `Normal`。
- SIM 是 best route 且模式為 `WarnOnly` 時，只警告不封鎖。
- SIM 是 best route 且模式為 `BlockSimWhenWifiDown` 時，應要求封鎖。
- 沒有 default route 時，狀態為 `Unknown`。

範例：

```csharp
[Fact]
public void Evaluate_WhenSimRouteIsBestAndModeIsBlock_ShouldRequestBlock()
{
    var engine = new NetworkPolicyEngine();
    var routes = new[]
    {
        new DefaultRouteInfo("0.0.0.0/0", "192.168.8.1", 12, "Ethernet", 10, 10)
    };

    var result = engine.Evaluate(
        routes,
        primaryWifiAlias: "Wi-Fi",
        simAlias: "Ethernet",
        mode: GuardMode.BlockSimWhenWifiDown);

    result.RiskLevel.Should().Be(NetworkRiskLevel.SimRouteActive);
    result.ShouldNotify.Should().BeTrue();
    result.ShouldBlockSimRoute.Should().BeTrue();
}
```

### 10.2 Integration Tests

不要在一般測試中真的改 Windows route。

建議建立 interface：

```csharp
public interface IRouteReader
{
    Task<IReadOnlyList<DefaultRouteInfo>> GetDefaultRoutesAsync(CancellationToken cancellationToken);
}

public interface IRouteController
{
    Task RemoveDefaultRouteAsync(string interfaceAlias, CancellationToken cancellationToken);
}
```

測試時使用 fake implementation。

### 10.3 手動測試

測試場景：

1. Wi-Fi 與 SIM 有線網路都連上。
2. 確認 default route 優先走 Wi-Fi。
3. 關閉 Wi-Fi。
4. 檢查程式是否偵測到 SIM route active。
5. 啟用自動封鎖。
6. 再次關閉 Wi-Fi，確認 SIM default route 被移除或警告彈出。

建議測試前先保存目前 route：

```powershell
Get-NetRoute -DestinationPrefix "0.0.0.0/0" | Format-Table -AutoSize
Get-NetIPInterface | Sort-Object InterfaceMetric | Format-Table InterfaceAlias,InterfaceIndex,InterfaceMetric,ConnectionState
```

## 11. 開發里程碑

### Milestone 1：CLI Prototype

目標：先做 console app，不做 UI。

功能：

- 列出 default routes。
- 找出目前 best route。
- 顯示是否正在使用 SIM。
- 支援 `--watch` 每幾秒檢查一次。

完成條件：

- 能正確在 Wi-Fi / SIM 切換時輸出狀態。

### Milestone 2：Policy + Tests

目標：把判斷邏輯抽到 Core，並補 unit tests。

完成條件：

- `dotnet test` 通過。
- policy 不依賴真實 Windows 網路狀態。

### Milestone 3：Tray App

目標：做出常駐 tray icon。

功能：

- 顯示目前狀態。
- 右鍵選單：
  - 開啟設定。
  - 暫停監控。
  - 允許 SIM 30 分鐘。
  - 離開。
- 偵測到 SIM 接管時跳通知。

### Milestone 4：Admin Action

目標：加入封鎖 SIM Internet。

功能：

- 手動按鈕移除 SIM default route。
- 自動封鎖模式。
- 權限不足時提示以系統管理員執行。

### Milestone 5：Windows Service

目標：把監控和 route 修改移到 service。

功能：

- service 開機啟動。
- tray app 只負責 UI。
- 使用 named pipe 或 local HTTP IPC 與 service 溝通。

## 12. 建議先做的最小程式流程

```text
啟動程式
  ↓
讀取設定
  ↓
每 3 秒讀取 default routes
  ↓
找出 best route
  ↓
判斷 best route 是否為 SIM interface
  ↓
若否：tray icon 顯示正常
  ↓
若是：跳通知
  ↓
若模式為自動封鎖：移除 SIM default route
```

## 13. 風險與注意事項

1. 不同 Windows 語系的網卡名稱可能不同，不要硬寫 `Wi-Fi` 或 `Ethernet`。
2. 使用者可能改名網卡，所以設定應保存 `InterfaceIndex` 和 `InterfaceAlias`，並允許重新選擇。
3. route 操作需要系統管理員權限。
4. 如果 SIM 路由器也承擔區網用途，停用網卡可能太激進。
5. Windows 更新或 VPN 軟體可能改變 route table，應避免做過度假設。
6. VPN 也可能成為 default route，policy 需要清楚定義 VPN 時的行為。
7. 若使用 IPv6，還要監控 `::/0` default route。

## 14. 建議下一步

建議你先從 CLI Prototype 開始：

1. 建立 solution。
2. 新增 `NetworkTrafficGuard.Core` 和 `NetworkTrafficGuard.Cli`。
3. 用 PowerShell JSON 讀取 default route。
4. 把 route 判斷邏輯寫成可測試的 `NetworkPolicyEngine`。
5. 確認能偵測到 Wi-Fi 斷線後 SIM 接管。
6. 再加入 tray app。

這樣可以避免一開始就卡在 WPF tray、Windows Service、權限提升和安裝程式。先把「判斷正不正確」做出來，後面 UI 與 service 都只是包裝。

