# Network Traffic Guard 规划

英文是本项目的默认文档语言。

其他语言版本：

- [English](README.md)
- [繁體中文](README.zh-TW.md)
- [日本語](README.ja-JP.md)

## 1. 目的

Network Traffic Guard 是一个 Windows 常驻程序，用来避免电脑在 Wi-Fi 断开后，静默切换到昂贵或流量有限的备用网络。

原始使用场景是电脑同时连接：

- Wi-Fi：主要上网来源。
- 有线网络：连接到家中的路由器，可能使用有限流量。

当 Wi-Fi 断开时，Windows 可能会自动把 Internet default route 切到另一个可用网络。本工具会监控路由状态、显示当前使用中的连接、显示实时流量，并在用户勾选的路由流量超过阈值时发送通知。

## 2. 当前范围

当前 MVP 聚焦于本机 Windows 监控与 WPF tray UI。

已完成：

- 通过 PowerShell 读取 Windows default routes。
- 根据 route metric 与 interface metric 判断最佳 default route。
- 显示最高优先级的 Wi-Fi route 与最高优先级的非 Wi-Fi 网络接口。
- 断开或禁用的网络接口不会显示在状态卡与流量监看中。
- 以紧凑表格显示路由优先顺序。
- 可上下调整路由优先顺序并保存。
- 启用权限后，可把路由优先顺序应用到 Windows。
- 启用接口变更权限后，可从设置菜单启用或停用 Wi-Fi。
- 可针对勾选的路由显示实时流量。
- 支持多个实时流量监看卡。
- 可针对每条路由勾选警示。
- 当警示路由的流量超过阈值时，显示 Windows tray notification。
- tray tooltip 显示主要连接与当前流量。
- 可为检测到的网络设置自定义显示名称。
- 警示设置已拆成独立窗口。
- 支持 UI 语言：英文、繁体中文、简体中文、日文。

尚未完成：

- 每月流量统计。
- 临时允许规则，例如 10 分钟或直到重启。
- 完整 Windows Service 部署流程。
- 使用原生 Windows IP Helper API 读取路由。
- Wi-Fi SSID allow-list 强制检查。
- 安装程序与开机自动启动注册。

## 3. 术语

本工具使用通用网络术语，不假设备用连接一定是移动网络。

- Primary Wi-Fi：首选 Wi-Fi 连接。
- Secondary network：设置为备用或非首选的网络接口。
- Network interface：Windows 检测到的任一网络接口。
- Gateway：default route 使用的 next-hop 地址。
- Display name：用户自定义、显示在 UI 上的名称。
- Alert route：被勾选用来监控流量阈值的路由。

系统消息、log 与代码标识符使用英文。UI 文字支持多语言。

## 4. 项目结构

```text
NetworkTrafficGuard.Core
  Domain models、settings、route selection 与 policy logic。

NetworkTrafficGuard.Windows
  Windows 专用 PowerShell route 与 adapter controller。

NetworkTrafficGuard.Tray
  WPF tray app、多语言 UI、流量监看、设置窗口与通知。

NetworkTrafficGuard.Service
  后台监控用 worker service prototype。

NetworkTrafficGuard.Tests
  Policy 与 Windows command generation 相关单元测试。
```

## 5. 设置

示例：

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
  "CultureName": "zh-CN",
  "AllowedWifiSsids": []
}
```

重要开关：

- `EnableRouteChanges`：为 `false` 时，路由变更只会模拟，不会真的修改 Windows。
- `EnableAdapterChanges`：为 `false` 时，Wi-Fi 启用/停用只会模拟。
- `AlertThresholdKbps`：路由流量通知阈值。
- `CultureName`：UI 语言，例如 `en-US`、`zh-TW`、`zh-CN` 或 `ja-JP`。

## 6. UI 行为

主窗口：

- 上方卡片显示 Wi-Fi 与最高优先级的非 Wi-Fi 网络接口。
- 路由表格显示是否显示、是否警示、优先级、网络名称、网关与类型。
- 上移与下移按钮可调整路由优先顺序。
- 实时流量区会针对每个勾选显示的路由显示一张监看卡。

自定义名称设置：

- 检测到的网络、网关与类型是只读列。
- 显示名称是可编辑列。
- 窗口再次打开时，会从已保存的设置读取名称。

警示设置：

- 警示阈值放在独立设置窗口。
- 每条路由是否启用警示仍在主窗口路由表格中勾选。

## 7. 开发流程

Build：

```powershell
dotnet build .\NetworkTrafficGuard.slnx
```

Test：

```powershell
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
```

运行 tray app：

```powershell
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

如果 tray app 已经在运行，建议先关闭再 build，因为 Windows 可能会锁住输出 DLL。

## 8. 测试重点

现有测试覆盖：

- Wi-Fi route 优先时的 policy 行为。
- Secondary route active 的 policy 行为。
- Block mode 的 policy result。
- 没有 default route 时的行为。
- 通过 interface index 判断 secondary interface。
- 英文系统 policy message。
- PowerShell route-control dry-run 行为。

手动测试建议：

- 从 Windows 禁用与重新启用 Wi-Fi，确认 UI 实时更新。
- 新增或移除网络接口，确认上方状态卡更新。
- 修改检测到的网络名称、保存、重新打开设置，确认名称仍存在。
- 勾选多个流量监看，确认右侧显示多张监看卡。
- 勾选警示路由、超过阈值，确认 tray notification。

## 9. 下一步

建议下一阶段：

1. 更清楚地区分 interface display name 与 gateway display name。
2. 加入每月流量统计。
3. 加入安装程序与开机自动启动。
4. 将长时间监控移到 Windows Service。
5. 需要更稳定时，以原生 Windows API 取代 PowerShell route 读取。
