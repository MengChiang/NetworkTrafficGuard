# Network Traffic Guard 計画

このプロジェクトでは英語ドキュメントを既定とします。

他の言語:

- [English](windows-network-traffic-guard-plan.md)
- [繁體中文](windows-network-traffic-guard-plan.zh-TW.md)
- [简体中文](windows-network-traffic-guard-plan.zh-CN.md)

## 1. 目的

Network Traffic Guard は、Wi-Fi が切断されたあとに Windows が高コストまたは容量制限のあるバックアップ回線へ自動的に切り替わることを防ぐための常駐アプリです。

最初の利用シナリオは、PC が次の 2 つのネットワークに同時接続されている状態です。

- Wi-Fi: 優先して使いたい Internet 接続。
- 有線ネットワーク: 自宅ルーターにつながっており、通信量に制限がある可能性がある接続。

Wi-Fi が切断されると、Windows は Internet default route を別の利用可能なネットワークへ切り替えることがあります。このツールは route の状態を監視し、現在使われている接続、リアルタイム通信量、選択された route がしきい値を超えたときの通知を提供します。

## 2. 現在の範囲

現在の MVP は、ローカル Windows 監視と WPF tray UI に集中しています。

実装済み:

- PowerShell で Windows default routes を読み取る。
- route metric と interface metric で最適な default route を判定する。
- 優先度が最も高い Wi-Fi route と、優先度が最も高い非 Wi-Fi ネットワークインターフェイスを表示する。
- 切断または無効化されたネットワークインターフェイスを状態カードと通信量監視から除外する。
- route の優先順位をコンパクトな表で表示する。
- route 優先順位の並べ替えと保存に対応する。
- 許可されている場合、route 優先順位を Windows に適用する。
- adapter 変更が許可されている場合、設定メニューから Wi-Fi を有効化または無効化する。
- 選択された route ごとにリアルタイム通信量を表示する。
- 複数の通信量監視カードを表示する。
- route ごとに警告を有効化できる。
- 警告対象 route の通信量がしきい値を超えたとき Windows tray notification を表示する。
- tray tooltip に主要接続と現在の通信速度を表示する。
- 検出されたネットワークにカスタム表示名を設定できる。
- 警告設定は独立した設定ウィンドウで管理する。
- UI 言語として English、繁體中文、简体中文、日本語をサポートする。

未実装:

- 月間通信量の集計。
- 10 分間または再起動まで許可する一時ルール。
- 完全な Windows Service 配布フロー。
- Windows IP Helper API による native route 読み取り。
- Wi-Fi SSID allow-list の強制。
- installer と自動起動登録。

## 3. 用語

このツールでは、バックアップ接続がモバイルデータであるとは仮定せず、汎用的なネットワーク用語を使います。

- Primary Wi-Fi: 優先する Wi-Fi 接続。
- Secondary network: バックアップまたは非優先として設定されたネットワークインターフェイス。
- Network interface: Windows が検出した任意のネットワークインターフェイス。
- Gateway: default route が使う next-hop アドレス。
- Display name: UI に表示するユーザー定義名。
- Alert route: 通信量しきい値通知の対象として選択された route。

システムメッセージ、log、コード識別子は英語です。UI テキストは多言語化されています。

## 4. プロジェクト構成

```text
NetworkTrafficGuard.Core
  Domain models、settings、route selection、policy logic。

NetworkTrafficGuard.Windows
  Windows 用 PowerShell route controller と adapter controller。

NetworkTrafficGuard.Tray
  WPF tray app、多言語 UI、通信量監視、設定ウィンドウ、通知。

NetworkTrafficGuard.Service
  バックグラウンド監視用 worker service prototype。

NetworkTrafficGuard.Tests
  Policy と Windows command generation の unit tests。
```

## 5. 設定

例:

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
  "CultureName": "ja-JP",
  "AllowedWifiSsids": []
}
```

重要な設定:

- `EnableRouteChanges`: `false` の場合、route 変更は simulation のみで Windows は変更しません。
- `EnableAdapterChanges`: `false` の場合、Wi-Fi 有効化/無効化は simulation のみです。
- `AlertThresholdKbps`: route 通信量通知のしきい値。
- `CultureName`: UI 言語。例: `en-US`、`zh-TW`、`zh-CN`、`ja-JP`。

## 6. UI の動作

メインウィンドウ:

- 上部カードに Wi-Fi と、優先度が最も高い非 Wi-Fi ネットワークインターフェイスを表示する。
- route 表には、表示、警告、優先度、ネットワーク名、gateway、種類を表示する。
- 上へ/下へボタンで route 優先順位を変更する。
- リアルタイム通信量エリアには、表示対象として選択した route ごとに監視カードを表示する。

カスタム表示名設定:

- 検出されたネットワーク、gateway、種類は読み取り専用列です。
- 表示名だけが編集可能列です。
- ウィンドウを再度開いたとき、保存済み settings から表示名を読み込みます。

警告設定:

- 警告しきい値は独立した設定ウィンドウで管理します。
- route ごとの警告有効/無効は、メイン画面の route 表で選択します。

## 7. 開発フロー

Build:

```powershell
dotnet build .\NetworkTrafficGuard.slnx
```

Test:

```powershell
dotnet test .\NetworkTrafficGuard.Tests\NetworkTrafficGuard.Tests.csproj
```

tray app の実行:

```powershell
dotnet run --project .\NetworkTrafficGuard.Tray\NetworkTrafficGuard.Tray.csproj
```

tray app がすでに実行中の場合は、build 前に閉じてください。Windows が出力 DLL をロックすることがあります。

## 8. テスト観点

既存テストの対象:

- Wi-Fi route が優先される場合の policy 動作。
- Secondary route active の policy 動作。
- Block mode の policy result。
- default route がない場合の動作。
- interface index による secondary interface 判定。
- 英語の system policy message。
- PowerShell route-control dry-run 動作。

手動テストの推奨項目:

- Windows から Wi-Fi を無効化/再有効化し、UI が更新されることを確認する。
- ネットワークインターフェイスを追加または削除し、上部カードが更新されることを確認する。
- 検出されたネットワーク名を変更、保存、再度設定を開き、保存名が表示されることを確認する。
- 複数の通信量監視を選択し、右側に複数カードが表示されることを確認する。
- 警告 route を選択し、しきい値超過時に tray notification が表示されることを確認する。

## 9. 次のステップ

推奨される次の開発項目:

1. interface display name と gateway display name の区別をより明確にする。
2. 月間通信量集計を追加する。
3. installer と自動起動登録を追加する。
4. 長時間監視を Windows Service に移す。
5. 安定性が必要になったら、PowerShell route 読み取りを native Windows API に置き換える。
