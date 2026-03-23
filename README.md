# NdiTelop

NDI テロップ送出アプリ **NdiTelop** のリリース準備版 README です。複数テキストブロック、USK/DSK 階層、外部制御、プレイリスト、外部データ連携を含む現行機能をまとめています。

## 主な機能

- **8 キーヤー構成**: USK1-4 / DSK1-4 を個別制御。
- **複数テキストブロック**: 1 プリセット内に複数ブロックを配置し、キーヤー単位で管理。
- **背景 + オーバーレイ合成**: 単色/透過/画像背景、PNG オーバーレイ、透過付きロゴ対応。
- **Preview / Program 運用**: Preview 選択、TAKE、CUT、CLEAR をサポート。
- **キーヤー AUTO**: 各キーヤーごとに in/out タイプと速度を設定可能。
- **プレイリスト送出**: Next Cue、Auto-Advance、残り秒数表示。
- **外部制御**: Web API / OSC / Tally 連携。
- **外部データソース**: JSON / CSV / HTTP API の内容を `{{placeholder}}` で差し込み。
- **ログビューア**: フィルタ、キーワード検索、エクスポートに対応。

## セットアップ

### 必要環境

- .NET SDK 8.0
- Windows / Linux での Avalonia 実行環境
- NDI 出力を使う場合はネットワークと NDI ランタイム相当の実行環境

### 初回セットアップ

```bash
chmod +x setup.sh
./setup.sh
```

### ビルド / テスト

```bash
dotnet build src/NdiTelop/NdiTelop.csproj
dotnet test tests/NdiTelop.Tests/NdiTelop.Tests.csproj
```

### CI と手動起動確認

- GitHub Actions では **ビルド、GUI に依存しないユニットテスト、publish 成果物確認、インストーラー生成** までを検証します。
- GitHub Actions のような非対話環境では `ClassicDesktopLifetime` を使う GUI アプリを起動しません。`dotnet run`、`NdiTelop.exe` の起動、生成済みインストーラーの実行による起動確認は CI では行いません。
- 最終的な起動確認は、**対話型 Windows デスクトップ環境** でユーザーが手動実施してください。

手動確認手順:

1. GitHub Actions 実行結果から `NdiTelop-Installer-v*` アーティファクトをダウンロードする。
2. そのインストーラーを、GitHub Actions / Windows サービス / 非対話タスクではない **通常の Windows デスクトップ環境** に配置する。
3. `NdiTelop-Setup-v*.exe` を手動で実行してインストールする。
4. スタートメニューまたはインストール先の `NdiTelop.exe` からアプリを起動する。
5. `System.PlatformNotSupportedException` が発生せず、メインウィンドウが表示されることを確認する。

## 画面構成と基本ワークフロー

### 1. プリセットを選ぶ

左カラムの Presets からプリセットを選択します。

- `Set Preview`: Preview バスに送る候補を選択。
- `Show`: 即座に Program へ表示。
- `Duplicate/Delete/Save`: プリセット管理。

### 2. Preview / Program を運用する

中央 Preview キャンバスと右カラムの操作で送出します。

- **Preview preset selected**: 送出前の確認状態。
- **TAKE**: Preview を Program にトランジション付きで反映。
- **CUT**: 即時反映。
- **Clear Program**: Program / Preview を透明でクリア。

### 3. プレイリストで自動進行する

Playlist パネルでは以下を操作できます。

- `Add to Playlist`: 選択中プリセットを追加。
- `Cue`: 任意項目を即時再生。
- `Next Cue`: 次のキューへ進行。
- `Auto-Advance`: 各アイテムの表示秒数で自動送り。
- `RemainingSeconds`: 次アイテムまでの残り秒数を確認。

## プリセット設計

### 背景

各プリセットは以下の背景モードを持ちます。

- `solid`: 単色背景
- `transparent`: 透過背景
- `image`: アセット画像を全面表示

### 複数テキストブロック

1 プリセットに複数の Text Block を定義できます。各ブロックには以下があります。

- ブロック名
- 送出先キーヤー
- 複数行テキスト
- 共通テキストスタイル
- レイアウト（左右/上下揃え、オフセット）
- データソース設定

### USK / DSK 階層構造

キーヤーは描画順と役割で分かれます。

- **USK1-4**: メインの lower-third、見出し、速報帯など。
- **DSK1-4**: ロゴ、バグ、緊急帯、時計など常時重ねる要素。

推奨運用例:

- USK1: 主テロップ
- USK2: 補足や肩書き
- USK3: 速報/スコア変更
- USK4: フルフレーム見出し
- DSK1: チャンネルロゴ
- DSK2: 時計
- DSK3: 提供クレジット
- DSK4: 緊急帯

### キーヤー AUTO と優先度

各キーヤーは次を持ちます。

- `KeyOn`: 現在の ON/OFF 状態
- `Opacity`: 透明度
- `Priority`: 同一バス内の描画順
- `Animation`: `cut / fade / slide / wipe / wipe-vertical / zoom`

AUTO 実行時は、そのキーヤーだけを安全にトランジションさせます。

## テキスト編集

### 行単位の設定

Text Line ごとに以下を上書きできます。

- 表示文字列
- フォント
- サイズ
- 色

### ブロック共通スタイル

Text Style では以下を制御します。

- FontFamily / FontSize / Color
- OutlineThickness / OutlineColor
- ShadowOffsetX / ShadowOffsetY / ShadowBlur / ShadowColor

### レイアウト

Text Layout では以下を設定できます。

- HorizontalAlignment: Left / Center / Right
- VerticalAlignment: Top / Center / Bottom
- OffsetX / OffsetY

## 外部データ連携

各 Text Block は外部データソースを持てます。

### 対応ソース

- ローカル JSON ファイル
- ローカル CSV ファイル
- HTTP / HTTPS JSON API

### 使い方

1. Text Block の `DataSource.IsEnabled` を有効化。
2. `Source` にファイルパスまたは URL を設定。
3. `RefreshIntervalSeconds` を設定。
4. テキストに `{{title}}` や `{{score.home}}` のようなプレースホルダを記述。

### 例

```text
{{headline}}
{{player.name}} - {{player.number}}
HOME {{score.home}} : {{score.away}} AWAY
```

CSV は 1 行目をヘッダ、2 行目を値として読み込みます。

## 外部制御

### Web API

Settings で Host / Port を設定し、起動後に次の API を利用できます。

#### 主なエンドポイント

- `GET /api/presets`
- `POST /api/presets/{id}/activate`
- `POST /take`
- `POST /api/program/clear`
- `GET /api/status/ndi`
- `GET /api/playlist/status`
- `POST /api/playlist/next-cue`
- `POST /api/keyers/{usk1|dsk1}/{on|off|toggle|auto}`
- `POST /api/tally`
- `POST /api/tally/ndi-metadata`

#### 例: TAKE

```bash
curl -X POST http://127.0.0.1:5000/take \
  -H 'Content-Type: application/json' \
  -d '{"presetId":"news-opening"}'
```

#### 例: キーヤー ON

```bash
curl -X POST http://127.0.0.1:5000/api/keyers/usk2/on \
  -H 'Content-Type: application/json' \
  -d '{"opacity":0.85}'
```

### OSC

OSC では以下のようなアドレスを利用できます。

- `/telop/show/{presetId}`
- `/preset/{presetId}`
- `/take`, 引数に presetId
- `/keyer/usk1/on`
- `/keyer/dsk2/auto`
- tally 系 OSC メッセージ

送信側が切断しても、受信ループはログを残して継続する設計です。

### Tally / NDI Metadata

Remote Control 設定では以下を管理できます。

- `EnableTallyAutoTake`
- `TallyPartnerIpAddress`
- `TallyPartnerName`
- `TallyAutoTakeKeyer`
- `AcceptNdiMetadataTally`

Program の立ち上がりエッジを検知すると、指定キーヤーで AUTO を実行できます。

## サンプルプリセット

`src/NdiTelop/Assets/DefaultPresets/default_presets.json` には、以下の実運用向けプリセットを同梱しています。

- **ニュース**: Breaking News / Headline Stack / Weather Update
- **スポーツ**: Match Score / Lineup Board
- **バラエティ**: Variety Pop / Guest Talk
- **共通素材**: Clock Bug / Transparent Cut

これらは複数テキストブロックと USK/DSK 分離、AUTO トランジション、プレイリスト運用を想定しています。

## パフォーマンスと安定性

今回の最終調整では以下を強化しています。

- 背景/オーバーレイ画像のキャッシュで再デコードを抑制
- 同一 Program / Preview フレームの再利用で不要な再レンダリングを削減
- トランジション進行を固定フレーム値ではなく実時間ベースで制御
- Web API / OSC の例外をログ化し、外部切断時もアプリ全体は継続
- 外部データ連携エラーをブロック単位で表示し、クラッシュを回避

## ログと障害対応

ログは以下に出力されます。

- ファイル: `data/logs/nditelop-*.log`
- UI: MainWindow の Log Viewer

確認ポイント:

1. Web API が起動しているか
2. OSC ポートが競合していないか
3. 外部データソース URL / ファイルパスが有効か
4. NDI Program / Preview が Active か

## 今回 README で説明していないこと

- インストーラー固有の配布手順
- 外部ハードウェア固有の詳細設定
- 実運用における社内テンプレート命名ルール

上記は運用チーム側のリリース手順に合わせて補完してください。
