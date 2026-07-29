# Network Traffic Guard

Windows の常駐ツールです。現在使われているネットワークルート、リアルタイム通信量、月間通信量を確認できます。複数のネットワークに同時接続していて、優先したい回線と通信量を抑えたい回線がある環境向けです。

言語: [English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md)

## 機能

- Windows タスクトレイ UI。
- Windows IP Helper API によるネイティブルート読み取り。
- ネットワークインターフェイス別のリアルタイム通信量表示。
- 月間通信量の集計。
- 接続優先順位の並べ替えと、Windows への任意適用。
- 設定メニューから Wi-Fi を有効化または無効化。
- Windows 通知による通信量アラート。
- 検出したネットワークとゲートウェイの表示名カスタマイズ。
- 英語、繁体字中国語、簡体字中国語、日本語 UI。
- Windows Service の発行、インストール、削除、スタートアップ登録スクリプト。
- Inno Setup インストーラースクリプト。

## 要件

- Windows 10 以降。
- 開発には .NET 10 SDK。
- Windows Service のインストール、システムルート変更、アダプター状態変更には管理者権限。
- インストーラーを作成する場合は Inno Setup 6。

## 開発

```powershell
dotnet build .\NetworkTrafficGuard.slnx
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

タスクトレイアプリが実行中の場合、Windows が出力ファイルをロックすることがあるため、再ビルド前に終了してください。

## Windows Service

Service を発行してインストール:

```powershell
.\tools\publish-service.ps1
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\install-service.ps1`""
```

Service を削除:

```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\tools\uninstall-service.ps1`""
```

Service をローカルで 1 回だけ実行:

```powershell
dotnet run --project .\NetworkTrafficGuard.Service\NetworkTrafficGuard.Service.csproj -- RunOnce=true
```

## スタートアップ

トレイアプリを発行し、現在の Windows ユーザーのスタートアップに登録:

```powershell
.\tools\publish-tray.ps1
.\tools\register-tray-startup.ps1
```

スタートアップ登録を削除:

```powershell
.\tools\unregister-tray-startup.ps1
```

## インストーラー

先に発行ファイルを作成:

```powershell
.\tools\publish-tray.ps1
.\tools\publish-service.ps1
```

その後、Inno Setup で `installer\NetworkTrafficGuard.iss` をコンパイルします。

## データ

- 開発時の設定: 各プロジェクトの `appsettings.json`。
- 月間通信量: `%LOCALAPPDATA%\NetworkTrafficGuard\traffic-usage.json`。
- Service 名: `NetworkTrafficGuard`。
