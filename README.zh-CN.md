# Network Traffic Guard

一个 Windows 常驻工具，用来查看当前使用中的网络路由、实时流量和每月流量。适合电脑同时连接多个网络时使用，例如一个网络是主要连接，另一个网络有流量限制。

语言：[English](README.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja-JP.md)

## 功能

- Windows 系统托盘界面。
- 使用原生 Windows IP Helper API 读取路由。
- 按网络接口显示实时流量。
- 统计每月流量使用量。
- 调整连接优先顺序，并可选择是否应用到 Windows。
- 从设置菜单启用或停用 Wi-Fi。
- 使用 Windows 通知显示流量警示。
- 自定义检测到的网络和网关显示名称。
- 支持英文、繁体中文、简体中文、日文界面。
- 提供 Windows Service 发布、安装、移除和开机启动脚本。
- 提供 Inno Setup 安装程序脚本。

## 需求

- Windows 10 或更新版本。
- 开发时需要 .NET 10 SDK。
- 安装 Windows Service、调整系统路由或网卡状态时需要管理员权限。
- 若要生成安装程序，需要 Inno Setup 6。

## 开发

```powershell
dotnet build .\NetworkTrafficGuard.slnx
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

如果系统托盘程序正在运行，重新构建前请先关闭，避免 Windows 锁定输出文件。

## Windows Service

发布并安装 Service：

```powershell
.\tools\publish-service.ps1
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\install-service.ps1`""
```

移除 Service：

```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\uninstall-service.ps1`""
```

本机执行一次 Service 检查：

```powershell
dotnet run --project .\NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj -- RunOnce=true
```

## 开机启动

发布并注册当前用户的系统托盘程序开机启动：

```powershell
.\tools\publish-tray.ps1
.\tools\register-tray-startup.ps1
```

移除开机启动：

```powershell
.\tools\unregister-tray-startup.ps1
```

## 安装程序

先生成发布文件：

```powershell
.\tools\publish-tray.ps1
.\tools\publish-service.ps1
```

然后使用 Inno Setup 编译 `installer\NetworkTrafficGuard.iss`。

## 数据

- 开发期间设置：各项目的 `appsettings.json`。
- 每月流量：`%LOCALAPPDATA%\NetworkTrafficGuard\traffic-usage.json`。
- Service 名称：`NetworkTrafficGuard`。
