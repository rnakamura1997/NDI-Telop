# AGENTS.md — NdiTelop 開発仕様書（OpenAI Codex 専用）

> このファイルは OpenAI Codex が自律的に読み込み、実装を進めるための完全仕様書です。
> **Codex はこのファイルに記載された手順・ルールを厳守して実装を進めてください。**

---

## 🤖 エージェント指示（最重要）

あなたは上級ソフトウェアエンジニアです。以下の完全仕様に従い、ユーザーの介入を最小限に抑えながら自律的に実装を進めてください。

### 行動原則
1. **実装前に必ず仕様を確認**し、不明点があれば実装を止めて報告する
2. **各フェーズ完了後に必ず Git タグを付与**する
3. **破壊防止ルールを最優先**とし、既存コードの変更は原則禁止
4. **並列実装可能なタスクは並列で実行**する（Codex の並列タスク機能を活用）
5. **`dotnet build && dotnet test` が通過しない限りコミット禁止**
6. 各作業完了後に **完了報告と次ステップ** を提示する

### 完成の定義（Definition of Done）
- [ ] `dotnet build` が警告ゼロで成功する
- [ ] `dotnet test` が全テストパスで成功する
- [ ] `dotnet publish -c Release -r win-x64 --self-contained true` で `.exe` が生成される
- [ ] GitHub Actions の `build-windows.yml` が成功し、インストーラー `.exe` がアーティファクトとして生成される
- [ ] インストーラーを Windows PC にコピーして実行するとアプリが起動する
- [ ] NDI ソースが「NdiTelop Program」「NdiTelop Preview」として登録される
- [ ] Web UI にブラウザからアクセスしてテロップ操作ができる
- [ ] OSC でテロップの送出・切替ができる

---

## 🔧 ビルド・テストコマンド

```bash
# ビルド
dotnet build src/NdiTelop/NdiTelop.csproj

# テスト
dotnet test tests/NdiTelop.Tests/NdiTelop.Tests.csproj

# Windows 向けパブリッシュ
dotnet publish src/NdiTelop/NdiTelop.csproj \
  -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/win-x64

# コードフォーマット
dotnet format src/NdiTelop/NdiTelop.csproj

# コミット前フルチェック（必須）
dotnet build && dotnet test && dotnet format --verify-no-changes
```

---

## 🚫 破壊防止ルール（最優先・絶対厳守）

### Rule 1: インターフェース変更禁止
`src/NdiTelop/Interfaces/` 配下のファイルは **定義後変更禁止**。
変更が必要な場合は新しいインターフェースを作成して継承させること。
変更が避けられない場合は **必ずユーザーに報告してから**変更すること。

### Rule 2: COMPLETED マーク尊重
ファイル冒頭に `// [COMPLETED]` が付いたファイルは **読み取り専用**。
変更する場合は必ずユーザーに報告し、承認を得てから変更すること。

### Rule 3: 新機能は新ファイルに実装
既存の実装済みファイルへの追記ではなく、必ず新しいファイルに実装する。
既存メソッドへの変更は **バグ修正のみ** 許容。

### Rule 4: ビルド・テスト必須
変更を加える前後に必ず `dotnet build && dotnet test` を実行し、全て成功することを確認する。

### Rule 5: 依存方向の遵守
依存は必ず一方向のみ：
```
Views → ViewModels → Services(Interface) → Services(Implementation)
```
逆方向の依存は禁止。

### Rule 6: Git タグ戦略
各フェーズ完了時に必ず Git タグを付与する：
- `v0.0.1-arch` : Phase 0 完了（アーキテクチャ確定）
- `v0.1.0`      : Phase 1 完了（コア NDI・テロップ）
- `v0.2.0`      : Phase 2 完了（リモート・外部制御）
- `v0.3.0`      : Phase 3 完了（映像出力拡張）
- `v0.4.0`      : Phase 4 完了（入力デバイス・データ管理）

### Rule 7: コミット前チェックリスト
コミット前に以下を全て確認すること：
- [ ] `dotnet build` 成功（警告ゼロ）
- [ ] `dotnet test` 全テストパス
- [ ] `dotnet format --verify-no-changes` 成功
- [ ] `// [COMPLETED]` マークのファイルを変更していない
- [ ] インターフェースファイルを変更していない
- [ ] 新機能が新ファイルに実装されている

---

## 📋 プロジェクト概要

**アプリ名**: NdiTelop  
**用途**: ライブイベント・配信現場向け NDI テロップ送出ソフトウェア  
**開発環境**: macOS（Codex クラウド環境）  
**実行環境**: Windows 10/11 x64  
**配布形式**: Windows インストーラー（`.exe`）、または自己完結型 `.exe`  

---

## 🛠 技術スタック（固定・変更禁止）

| 項目 | 技術 | バージョン |
|------|------|-----------|
| 言語 | C# | 12.0 |
| フレームワーク | .NET | 8.0 |
| UI フレームワーク | Avalonia UI | 11.x 最新 |
| MVVM | CommunityToolkit.Mvvm | 8.x 最新 |
| 描画 | SkiaSharp | 最新安定版 |
| NDI SDK | NewTek.NDI (NuGet) | 最新 |
| Web サーバー | ASP.NET Core (Kestrel) | .NET 8 内蔵 |
| Web UI フロント | HTML/CSS/JavaScript (バニラ) | — |
| OSC | OscCore または SharpOSC | 最新 |
| JSON 設定 | System.Text.Json | .NET 8 内蔵 |
| テスト | xUnit + NSubstitute | 最新 |
| インストーラー | Inno Setup | 6.x |
| CI/CD | GitHub Actions | — |

### NuGet パッケージ一覧（`src/NdiTelop/NdiTelop.csproj` に追加すること）
```xml
<PackageReference Include="Avalonia" Version="11.*" />
<PackageReference Include="Avalonia.Desktop" Version="11.*" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
<PackageReference Include="Avalonia.ReactiveUI" Version="11.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="SkiaSharp" Version="*" />
<PackageReference Include="SkiaSharp.Views.Avalonia" Version="*" />
<PackageReference Include="NewTek.NDI" Version="*" />
<PackageReference Include="Microsoft.AspNetCore.App" />
<PackageReference Include="OscCore" Version="*" />
```

---

## 📐 全機能要件

### 1. テロップ表示・編集

#### 1.1 テキスト基本機能
- 複数行テキスト対応（行数制限なし）
- **行ごとに以下を独立して設定可能**：
  - フォントファミリー（Windows インストール済みフォントから選択）
  - フォントサイズ（8〜300px）
  - フォントスタイル（Bold / Italic / BoldItalic / Regular）
  - 文字色（カラーピッカー + RGBA）
  - アウトライン（色・太さ 0〜20px）
  - ドロップシャドウ（色・X/Y オフセット・ぼかし）
  - 行間隔（ピクセル指定）
  - 水平揃え（左 / 中央 / 右）

#### 1.2 テキスト位置・レイアウト
- 9 点ポジション（左上/中上/右上/左中/中央/右中/左下/中下/右下）
- X/Y 座標の直接数値入力
- マージン設定（上下左右 ピクセル）
- テキストブロック全体のサイズ（幅・高さ 固定 or 自動）

#### 1.3 背景（座布団）
- 透明（アルファ 0）
- 単色（カラーピッカー + アルファ）
- グラデーション（2 色、角度指定）
- 画像（PNG/JPG/GIF、タイル/フィット/ストレッチ）
- 背景のみの角丸設定（0〜50px）
- 背景のみのパディング設定

#### 1.4 オーバーレイ（ロゴ・素材）
- 同時表示最大 8 レイヤー
- 対応形式：PNG / JPG / GIF（アニメーション GIF 対応）
- 位置：9 点 + XY 直接指定
- サイズ：幅/高さ 個別指定、アスペクト比固定オプション
- 透明度（アルファ）

### 2. アニメーション

以下のアニメーション種別を実装すること：

| 種別 | イン | アウト |
|------|------|--------|
| フェード | ◯ | ◯ |
| スライドイン（上/下/左/右） | ◯ | ◯ |
| ズームイン/アウト | ◯ | ◯ |
| ワイプ（上/下/左/右） | ◯ | ◯ |
| カット（即時）| ◯ | ◯ |

- アニメーション速度：0.1〜5.0 秒（プリセットごとに設定）
- イージング：Linear / EaseIn / EaseOut / EaseInOut
- プレビューモードでは PGM に影響しない

### 3. NDI 出力

#### 3.1 NDI チャンネル
- **Program (PGM)** チャンネル：ソース名「NdiTelop Program」
- **Preview (PVW)** チャンネル：ソース名「NdiTelop Preview」
- 各チャンネル独立して ON/OFF 可能
- NDI 送出状態インジケーター（ON 時は赤、OFF 時はグレー）

#### 3.2 解像度・フレームレート
- 解像度：1280×720 / 1920×1080 / 2560×1440 / 3840×2160（UI で選択）
- フレームレート：23.976 / 24 / 25 / 29.97 / 30 / 50 / 59.94 / 60（UI で選択）
- アルファチャンネル（BGRA）対応

#### 3.3 フィル＆キー出力
- **フィル出力**：テロップ+背景のフルコンポジット映像
- **キー出力**：テロップのアルファマスク（グレースケール輝度でアルファを表現）
- **黒背景出力**：アルファ非対応機器向けに黒背景合成
- 出力モードを UI で切替可能：「Fill+Key」「Fill+Black」「Fill Only」

#### 3.4 パフォーマンス適応
- CPU 使用率が 80% 超 or 空きメモリが 1GB 未満の場合、フレームレートを自動で半減
- 設定で自動適応の ON/OFF 切替可能
- 現在の CPU / メモリ使用率をステータスバーに表示

### 4. Program / Preview モード

- **Preview モード**：
  - 次に送出するプリセットを PVW チャンネルで確認
  - PGM への影響なし
  - 「TAKE」ボタン（または設定したショートカット）で PGM に送出
- **即時送出モード**：
  - プリセット選択と同時に PGM に即時送出
- モード切替ボタンを UI のメインエリアに配置

### 5. 映像出力（NDI 以外）

#### 5.1 マルチモニター全画面出力
- 接続されているモニターを選択して全画面表示
- 表示内容：Program / Preview / 設定画面を選択可能

#### 5.2 仮想カメラ（DirectShow Virtual Camera）
- Windows の仮想カメラデバイスとして登録
- OBS / vMix 等から映像ソースとして認識
- デバイス名：「NdiTelop Virtual Camera」
- 実装：`DirectShow.NET` ライブラリまたは Windows Media Foundation を使用

#### 5.3 DeckLink 出力
- Blackmagic Design DeckLink SDK を使用
- インストール済みの DeckLink デバイスを自動検出
- デバイス選択 UI を設定画面に追加
- SDK が存在しない場合は該当 UI をグレーアウト（エラーにしない）

#### 5.4 Spout2 出力
- `Spout2` ライブラリを使用してテクスチャ共有
- VMAX / MadMapper / Resolume 等の VJ ソフトに映像を渡す
- 送信者名：「NdiTelop」
- ライブラリが存在しない場合は UI をグレーアウト

### 6. 自動テロップ消去

- プリセットごとに「自動消去タイマー」を設定可能（0 = 無効、1〜999 秒）
- タイマー発動時は **その時点でプリセットに設定されているアウトアニメーション** に従って動作（手動操作と同じ挙動）
- タイマー残り時間をプリセットカード上に表示
- タイマーカウント中は視覚的インジケーターを表示

### 7. プリセット管理

#### 7.1 プリセット基本
- 上限なし（無制限）
- プリセットに含まれる情報：
  - テロップ全設定（テキスト、フォント、色、位置、背景、オーバーレイ）
  - アニメーション設定（イン/アウト種別・速度・イージング）
  - 自動消去タイマー
  - プリセット名・サムネイル（自動生成）
  - タグ・カラーラベル

#### 7.2 組み込みプリセット（初期状態で存在すること）
| プリセット名 | 用途 |
|---|---|
| アーティスト名 | 白文字・中央配置・黒座布団 |
| 曲名 | 黄色文字・左下配置・透明背景 |
| イベント名 | 大きな白文字・中央上配置 |
| MC テロップ | グレー背景・中央配置 |
| カウントダウン | 数字大きく表示 |
| 透明カット | 完全透明（送出なし状態） |

#### 7.3 プリセット表示
- グリッドビュー（サムネイル付き）
- リストビュー（名前のみ）
- 検索・タグフィルター

### 8. セットリスト機能

ProPresenter / Resolume Avenue / vMix の Playlist を参考に実装する。

#### 8.1 セットリスト基本
- セットリスト = プリセットを順番に並べたリスト
- 複数のセットリストを作成・保存可能
- セットリスト名・説明・色ラベル

#### 8.2 セットリスト操作
- ドラッグ＆ドロップによる並び替え
- プリセットの追加・削除・複製
- 前へ / 次へ ナビゲーション（キーボード / ボタン / OSC / Stream Deck 対応）
- 現在位置インジケーター（ハイライト表示）
- ループ設定（セットリスト末尾で最初に戻る）
- キュー機能（次のプリセットを事前にプレビュー）

#### 8.3 CSV インポート/エクスポート
- セットリストを CSV でエクスポート
- CSV からインポートしてプリセットを一括生成
- サンプル CSV エクスポート機能（空のテンプレート）
- CSV カラム仕様：

```csv
name,line1_text,line1_font,line1_size,line1_color,line2_text,line2_font,line2_size,line2_color,bg_color,bg_alpha,position,anim_in,anim_out,anim_speed,auto_clear_sec
アーティスト名,山田太郎,Meiryo,80,#FFFFFF,,,,,,#000000,0.8,center,fade,fade,0.5,0
曲名,愛の歌,Meiryo,60,#FFFF00,,,,,,transparent,0,bottom-left,slide-up,slide-down,0.3,10
```

### 9. 設定管理

#### 9.1 エクスポート/インポート
- 全設定を JSON ファイルにエクスポート
- JSON ファイルからインポートして設定を完全復元
- プリセット単体のエクスポート/インポートも可能
- エクスポートファイル形式：`NdiTelop_settings_YYYYMMDD.json`

#### 9.2 設定保存場所
- Windows: `%APPDATA%\NdiTelop\`
  - `settings.json` : アプリ設定
  - `presets/` : プリセット JSON ファイル群
  - `setlists/` : セットリスト JSON ファイル群
  - `assets/` : ユーザー素材（画像等）

### 10. リモートコントロール（HTTP REST API）

Kestrel（ASP.NET Core 内蔵）でローカル HTTP サーバーを起動する。

#### 10.1 API 仕様

| メソッド | エンドポイント | 説明 |
|---------|--------------|------|
| GET | `/api/status` | 現在の状態（PGM プリセット、PVW プリセット、NDI 状態等） |
| GET | `/api/presets` | 全プリセット一覧 |
| GET | `/api/presets/{id}` | プリセット詳細 |
| POST | `/api/presets/{id}/send` | プリセットを PGM に即時送出 |
| POST | `/api/presets/{id}/preview` | プリセットを PVW に送出 |
| POST | `/api/take` | PVW → PGM に TAKE |
| POST | `/api/cut` | 透明カット（現在のテロップを消す） |
| GET | `/api/setlists` | 全セットリスト一覧 |
| POST | `/api/setlists/{id}/load` | セットリストをロード |
| POST | `/api/setlists/current/next` | 次のプリセットへ |
| POST | `/api/setlists/current/prev` | 前のプリセットへ |
| GET | `/api/preview/thumbnail` | 現在のプレビュー画像（JPEG, 320px幅） |
| POST | `/api/ndi/toggle` | NDI 送出 ON/OFF |

#### 10.2 デフォルトポート
- HTTP: `8080`（設定画面で変更可能）

#### 10.3 レスポンス形式
全て JSON。成功時は `{"success": true, "data": {...}}`、エラー時は `{"success": false, "error": "message"}`

### 11. Web UI

Kestrel で静的ファイルを配信し、ブラウザからアクセス可能にする。

#### 11.1 簡易版 Web UI（`/`）
スマートフォン・タブレット向け。最小限の操作に特化。

- プリセット一覧（タッチ操作でタップして即送出）
- セットリストナビゲーション（前へ / 次へ ボタン）
- CUT ボタン（テロップ消去）
- TAKE ボタン（PVW → PGM）
- 現在送出中テロップのサムネイル表示
- レスポンシブデザイン（iPhone / iPad / Android タブレット対応）
- タッチ操作最適化（ボタン最小サイズ 44×44px）

#### 11.2 フル機能 Web UI（`/full`）
PC ブラウザ向け。デスクトップアプリと同等の操作性。

- テロップ編集（テキスト・フォント・色・位置）
- プリセット管理（作成・編集・削除）
- セットリスト管理
- NDI 設定
- CSV インポート/エクスポート
- 設定画面

#### 11.3 認証
- パスワード認証なし（ローカルネットワーク利用前提）
- 設定でパスワード認証を有効化する選択肢（オプション）

### 12. OSC コントロール

#### 12.1 OSC 設定
- 受信ポート：デフォルト `57120`（設定で変更可能）
- 送信先：IP アドレス + ポート（設定で変更可能）
- フィードバック（状態通知）送信：ON/OFF 設定可能

#### 12.2 受信コマンド一覧

| OSC アドレス | 引数 | 説明 |
|-------------|------|------|
| `/nditelop/send` | `string presetId` | プリセットを PGM に送出 |
| `/nditelop/preview` | `string presetId` | プリセットを PVW に送出 |
| `/nditelop/take` | — | PVW → PGM TAKE |
| `/nditelop/cut` | — | テロップ消去 |
| `/nditelop/next` | — | セットリスト次へ |
| `/nditelop/prev` | — | セットリスト前へ |
| `/nditelop/setlist/load` | `string setlistId` | セットリストをロード |
| `/nditelop/ndi/toggle` | — | NDI 送出 ON/OFF |
| `/nditelop/preset/{index}` | — | インデックス番号でプリセット送出 |

#### 12.3 送信フィードバック

| OSC アドレス | 引数 | タイミング |
|-------------|------|-----------|
| `/nditelop/status/pgm` | `string presetId` | PGM プリセット変更時 |
| `/nditelop/status/pvw` | `string presetId` | PVW プリセット変更時 |
| `/nditelop/status/ndi` | `int 0or1` | NDI ON/OFF 変化時 |

### 13. Stream Deck 連携

#### 13.1 Phase 2 以降：HTTP API 経由
HTTP API（ポート 8080）を叩く Webhook アクションで操作可能。
Stream Deck の「システム / ウェブサイトを開く」や「API ツール」アクションから利用。

#### 13.2 Phase 4：公式プラグイン
Elgato Stream Deck SDK（Node.js / TypeScript）を使用した公式プラグインを作成。

プラグイン構成：
```
com.nditelop.streamdeck.sdPlugin/
  manifest.json
  src/
    plugin.ts        # プラグインエントリポイント
    actions/
      sendPreset.ts  # プリセット送出アクション
      take.ts        # TAKE アクション
      cut.ts         # CUT アクション
      nextPreset.ts  # セットリスト次へ
      prevPreset.ts  # セットリスト前へ
  ui/
    sendPreset.html  # プロパティインスペクタ
  package.json
```

アクション一覧：
- **Send Preset**：プリセット ID を指定して送出
- **TAKE**：PVW → PGM
- **CUT**：テロップ消去
- **Next**：セットリスト次へ
- **Prev**：セットリスト前へ
- **NDI Toggle**：NDI ON/OFF

### 14. キーボード・入力デバイス

#### 14.1 デフォルトショートカット（全て設定画面で変更可能）

| キー | 動作 |
|-----|------|
| `Space` / `Enter` | 現在選択プリセットを PGM に送出 |
| `Esc` | テロップ消去（CUT） |
| `T` | TAKE（PVW → PGM） |
| `→` / `Page Down` | セットリスト次へ |
| `←` / `Page Up` | セットリスト前へ |
| `F1` 〜 `F12` | プリセット 1〜12 を直接送出 |
| `Num 0` 〜 `Num 9` | テンキー番号 = プリセットインデックス |
| `Ctrl+S` | 設定保存 |
| `Ctrl+Z` | 直前操作をアンドゥ |
| `Tab` | プリセット選択を次へ |

#### 14.2 テンキーショートカット
- テンキー `0`〜`9` でプリセット番号を入力して送出（2 桁入力タイムアウト：1 秒）
- テンキー `*` で PVW に送出（TAKE で確定）
- テンキー `+` で CUT

#### 14.3 フットスイッチ対応
- フットスイッチは通常 USB HID デバイス（キーボード入力として動作）
- キーボードショートカットのカスタマイズにより自動対応
- 設定画面に「フットスイッチ設定」セクションを追加
- `F1`/`F2`/`Space` 等にバインド可能

### 15. UI/UX 設計

#### 15.1 テーマ
- **ライトテーマ** / **ダークテーマ** の切替（デフォルト：ダーク）
- アクセントカラー選択

#### 15.2 レイアウト（デスクトップアプリ）
```
┌─────────────────────────────────────────────────────────┐
│  メニューバー（File / Edit / View / Settings / Help）     │
├──────────────┬──────────────────────────────────────────┤
│              │  プレビューキャンバス (16:9 比率)          │
│  プリセット   │  ┌────────────────────────────────────┐   │
│  パネル      │  │   PVW | PGM 切替タブ               │   │
│  （一覧）    │  │   テロップ表示エリア                 │   │
│              │  └────────────────────────────────────┘   │
│              │  ─────────────────────────────────────   │
│              │  テキスト編集パネル                       │
│              │  スタイル設定パネル                       │
│              │  アニメーション設定パネル                  │
│  セットリスト │  ─────────────────────────────────────   │
│  パネル      │  送出コントロール                         │
│              │  [CUT] [PVW] [TAKE/SEND] [NDI●]          │
├──────────────┴──────────────────────────────────────────┤
│  ステータスバー: CPU使用率 / メモリ / NDI状態 / FPS       │
└─────────────────────────────────────────────────────────┘
```

#### 15.3 タッチ対応
- 全ボタンの最小タップサイズ：44×44px
- スクロール・スワイプによるプリセット切替
- ピンチズームでプレビュー拡大縮小

#### 15.4 フォント・可読性
- UI フォント：14px 以上
- ステータス・ラベル：12px 以上（最小）
- 低解像度ディスプレイでも視認性を確保

#### 15.5 マルチモニター
- 任意のモニターでアプリを全画面表示可能
- 設定で「コントロール画面」「プレビュー画面」「セットリスト画面」を割り当て可能

### 16. カウントダウンタイマー

- プリセット種別「タイマー」を実装
- カウントダウン：任意の秒数（1〜99:59）
- カウントアップ：経過時間表示
- ゼロ到達時の動作：停止 / 次のプリセットへ / テロップ消去（設定可能）
- フォント・色・背景はテロップと同様に設定可能

---

## 🗂 プロジェクト構造

```
NdiTelop/
├── AGENTS.md                          # このファイル（変更禁止）
├── README.md
├── .github/
│   └── workflows/
│       ├── build-windows.yml          # メイン CI（インストーラー生成）
│       └── pr-check.yml              # PR チェック（ビルド・テスト）
├── src/
│   └── NdiTelop/
│       ├── NdiTelop.csproj
│       ├── Program.cs                 # エントリポイント [COMPLETED 後変更禁止]
│       ├── App.axaml                  # アプリ基本設定 [COMPLETED 後変更禁止]
│       ├── App.axaml.cs
│       ├── Interfaces/               # [全ファイル変更禁止]
│       │   ├── INdiService.cs
│       │   ├── IRenderService.cs
│       │   ├── IPresetService.cs
│       │   ├── ISetlistService.cs
│       │   ├── IWebApiService.cs
│       │   ├── IOscService.cs
│       │   ├── IOutputService.cs
│       │   └── ISettingsService.cs
│       ├── Models/
│       │   ├── Preset.cs
│       │   ├── TextLine.cs
│       │   ├── BackgroundStyle.cs
│       │   ├── OverlayItem.cs
│       │   ├── AnimationConfig.cs
│       │   ├── Setlist.cs
│       │   ├── AppSettings.cs
│       │   └── NdiConfig.cs
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── PresetEditorViewModel.cs
│       │   ├── SetlistViewModel.cs
│       │   ├── NdiSettingsViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Views/
│       │   ├── MainWindow.axaml
│       │   ├── MainWindow.axaml.cs
│       │   ├── PresetEditorView.axaml
│       │   ├── SetlistView.axaml
│       │   └── SettingsView.axaml
│       ├── Services/
│       │   ├── NdiService.cs
│       │   ├── RenderService.cs
│       │   ├── PresetService.cs
│       │   ├── SetlistService.cs
│       │   ├── WebApiService.cs
│       │   ├── OscService.cs
│       │   ├── OutputService.cs
│       │   └── SettingsService.cs
│       ├── Controls/
│       │   ├── PreviewCanvas.axaml
│       │   └── PresetCard.axaml
│       └── Assets/
│           ├── icon.ico
│           └── DefaultPresets/
│               └── default_presets.json
├── tests/
│   └── NdiTelop.Tests/
│       ├── NdiTelop.Tests.csproj
│       ├── Services/
│       │   ├── RenderServiceTests.cs
│       │   ├── PresetServiceTests.cs
│       │   └── SetlistServiceTests.cs
│       └── Mocks/
│           └── MockServices.cs
├── web/                               # Web UI 静的ファイル
│   ├── index.html                    # 簡易版 Web UI
│   ├── full/
│   │   └── index.html               # フル機能 Web UI
│   ├── css/
│   │   └── style.css
│   └── js/
│       └── app.js
├── installer/
│   └── setup.iss                     # Inno Setup スクリプト
└── streamdeck-plugin/               # Phase 4
    └── com.nditelop.streamdeck.sdPlugin/
        ├── manifest.json
        ├── package.json
        └── src/
```

---

## 🔌 インターフェース設計（Phase 0 で全て定義・以降変更禁止）

```csharp
// Interfaces/INdiService.cs
namespace NdiTelop.Interfaces;
public interface INdiService
{
    bool IsInitialized { get; }
    bool IsProgramActive { get; }
    bool IsPreviewActive { get; }
    Task InitializeAsync(NdiConfig config);
    Task SendFrameAsync(NdiChannelType channel, SKBitmap frame);
    Task SetActiveAsync(NdiChannelType channel, bool active);
    void Dispose();
}

// Interfaces/IRenderService.cs
public interface IRenderService
{
    SKBitmap Render(Preset preset, int width, int height);
    SKBitmap RenderTransition(Preset from, Preset to, float progress, AnimationConfig config);
}

// Interfaces/IPresetService.cs
public interface IPresetService
{
    IReadOnlyList<Preset> Presets { get; }
    Task LoadPresetsAsync();
    Task SavePresetAsync(Preset preset);
    Task DeletePresetAsync(string id);
    Task ImportFromCsvAsync(string filePath);
    Task ExportToCsvAsync(string filePath);
}

// Interfaces/ISetlistService.cs
public interface ISetlistService
{
    IReadOnlyList<Setlist> Setlists { get; }
    Setlist? CurrentSetlist { get; }
    int CurrentIndex { get; }
    Task LoadSetlistAsync(string id);
    Preset? Next();
    Preset? Previous();
}

// Interfaces/IWebApiService.cs
public interface IWebApiService
{
    int Port { get; set; }
    Task StartAsync();
    Task StopAsync();
}

// Interfaces/IOscService.cs
public interface IOscService
{
    int ReceivePort { get; set; }
    Task StartAsync();
    Task StopAsync();
    Task SendFeedbackAsync(string address, params object[] args);
}

// Interfaces/IOutputService.cs
public interface IOutputService
{
    Task StartVirtualCameraAsync();
    Task StopVirtualCameraAsync();
    Task StartDeckLinkOutputAsync(int deviceIndex);
    Task StopDeckLinkOutputAsync();
    Task StartSpoutAsync(string senderName);
    Task StopSpoutAsync();
    IReadOnlyList<string> GetAvailableDeckLinkDevices();
}

// Interfaces/ISettingsService.cs
public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task ExportAsync(string filePath);
    Task ImportAsync(string filePath);
}
```

---

## 📦 フェーズ実装計画

### Phase 0: アーキテクチャ確定（最初に実施）
**目標**: ビルドが通る骨格を作り、git タグ `v0.0.1-arch` を付ける

実施内容：
1. `dotnet new avalonia.app -n NdiTelop -o src/NdiTelop`
2. `dotnet new xunit -n NdiTelop.Tests -o tests/NdiTelop.Tests`
3. 全インターフェースファイル作成（`Interfaces/` 配下）
4. 全モデルクラス作成（空実装）
5. `App.axaml` / `MainWindow.axaml` の基本構造
6. DI コンテナ設定（`Program.cs`）
7. `MockServices.cs`（全サービスのスタブ）
8. `dotnet build` が通ることを確認
9. GitHub Actions `build-windows.yml` 作成
10. `git tag v0.0.1-arch`

**並列タスク可能**：3, 4 は並列実行可

---

### Phase 1: コア NDI・テロップ
**目標**: テロップ表示・NDI 送出の基本動作、git タグ `v0.1.0`

実施内容：
1. `RenderService.cs`（SkiaSharp でテロップ描画）
2. `NdiService.cs`（NDI SDK P/Invoke でフレーム送出）
3. `PresetService.cs`（JSON 読み書き）
4. `MainWindow.axaml`（プレビュー・送出ボタン UI）
5. `MainWindowViewModel.cs`（基本 MVVM）
6. 組み込みプリセット 6 種の JSON 作成
7. アニメーション実装（フェード、スライド）
8. `dotnet test` 全パス確認
9. GitHub Actions でインストーラー生成確認
10. `git tag v0.1.0`

**並列タスク可能**：1, 2, 3 は並列実行可

---

### Phase 2: リモート・外部制御
**目標**: Web UI / OSC / HTTP API、git タグ `v0.2.0`

実施内容：
1. `WebApiService.cs`（Kestrel + REST API 全エンドポイント）
2. `OscService.cs`（OSC 受信・送信）
3. `web/index.html`（簡易版 Web UI）
4. `web/full/index.html`（フル機能 Web UI）
5. Program/Preview モード実装
6. 全アニメーション種別実装（ズーム、ワイプ等）
7. `git tag v0.2.0`

---

### Phase 3: 映像出力拡張
**目標**: 仮想カメラ / DeckLink / Spout2、git タグ `v0.3.0`

実施内容：
1. `OutputService.cs`（仮想カメラ DirectShow）
2. DeckLink SDK 連携
3. Spout2 連携
4. マルチモニター全画面出力
5. フィル＆キー NDI 出力完全対応
6. パフォーマンス自動適応（CPU/メモリ監視）
7. `git tag v0.3.0`

---

### Phase 4: 入力デバイス・データ管理
**目標**: Stream Deck / セットリスト / CSV / フットスイッチ、git タグ `v0.4.0`

実施内容：
1. `SetlistService.cs`（セットリスト全機能）
2. CSV インポート/エクスポート
3. 設定エクスポート/インポート
4. キーボードショートカットカスタマイズ画面
5. テンキーショートカット
6. Stream Deck 公式プラグイン（`streamdeck-plugin/`）
7. 自動テロップ消去タイマー
8. カウントダウンタイマープリセット
9. `git tag v0.4.0`

---

## ⚙️ GitHub Actions 設定

### `.github/workflows/build-windows.yml`

```yaml
name: Build Windows Installer

on:
  push:
    branches: [ main ]
    tags:
      - 'v*'
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET 8
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore src/NdiTelop/NdiTelop.csproj

    - name: Build
      run: dotnet build src/NdiTelop/NdiTelop.csproj --no-restore -c Release

    - name: Test
      run: dotnet test tests/NdiTelop.Tests/NdiTelop.Tests.csproj --no-build -c Release --verbosity normal

    - name: Publish Windows x64
      run: |
        dotnet publish src/NdiTelop/NdiTelop.csproj `
          -c Release -r win-x64 `
          --self-contained true `
          -p:PublishSingleFile=true `
          -o ./publish/win-x64

    - name: Install Inno Setup
      run: choco install innosetup -y

    - name: Get version
      id: version
      run: |
        $tag = if ("${{ github.ref }}" -match "refs/tags/v(.+)") { $matches[1] } else { "0.0.0-dev" }
        echo "VERSION=$tag" >> $env:GITHUB_ENV

    - name: Build Installer
      run: |
        iscc /DMyAppVersion="${{ env.VERSION }}" installer/setup.iss

    - name: Upload Installer Artifact
      uses: actions/upload-artifact@v4
      with:
        name: NdiTelop-Installer-v${{ env.VERSION }}
        path: installer/Output/NdiTelop-Setup-v*.exe

    - name: Create GitHub Release
      if: startsWith(github.ref, 'refs/tags/v')
      uses: softprops/action-gh-release@v2
      with:
        files: installer/Output/NdiTelop-Setup-v*.exe
```

### `installer/setup.iss`

```pascal
[Setup]
AppName=NdiTelop
AppVersion={#MyAppVersion}
AppPublisher=NakamuraService
AppPublisherURL=https://github.com/your-repo/NdiTelop
DefaultDirName={autopf}\NdiTelop
DefaultGroupName=NdiTelop
OutputBaseFilename=NdiTelop-Setup-v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=src\NdiTelop\Assets\icon.ico

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにアイコンを作成"; GroupDescription: "追加アイコン:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NdiTelop"; Filename: "{app}\NdiTelop.exe"
Name: "{group}\NdiTelop のアンインストール"; Filename: "{uninstallexe}"
Name: "{commondesktop}\NdiTelop"; Filename: "{app}\NdiTelop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NdiTelop.exe"; Description: "{cm:LaunchProgram,NdiTelop}"; Flags: nowait postinstall skipifsilent
```

---

## 📝 コード品質・規約

- **コメント言語**：日本語（クラス・メソッド・複雑ロジックに必須）
- **命名規則**：C# 標準（PascalCase クラス/メソッド、camelCase フィールド、`_` プレフィックス private フィールド）
- **非同期処理**：全 I/O 操作は `async/await`、UI スレッドへの返却は Dispatcher を使用
- **NDI スレッド**：NDI 送出は専用バックグラウンドスレッドで実行
- **エラーハンドリング**：全 catch ブロックにログ出力、UI にはユーザーフレンドリーなメッセージ
- **ログ**：`Microsoft.Extensions.Logging` を使用、デバッグ時はコンソール出力
- **Nullable**：`<Nullable>enable</Nullable>` を必ず有効化

---

## 🧪 テスト戦略

```csharp
// tests/NdiTelop.Tests/Mocks/MockServices.cs
// 全インターフェースのモック実装（NSubstitute 使用）

// テストカバレッジ必須箇所：
// - RenderService: テロップ描画が例外なく完了すること
// - PresetService: JSON の読み書きが正しく動作すること
// - SetlistService: Next/Previous が境界値で正しく動作すること
// - WebApiService: API エンドポイントが正しいレスポンスを返すこと
// - OscService: コマンドが正しくパースされ対応メソッドが呼ばれること
```

---

## 🚀 Codex への起動指示

このファイル（AGENTS.md）を GitHub リポジトリのルートに配置した上で、
ChatGPT の Codex に以下のように指示してください：

```
リポジトリ [YOUR_REPO_URL] に AGENTS.md があります。
その仕様書に従い、NdiTelop を Phase 0 から順番に実装してください。
各フェーズ完了後に git タグを付与し、完了報告をしてください。
並列実装可能なタスクは並列で実行し、破壊防止ルールを最優先にしてください。
```

---

*このファイルは NakamuraService / NdiTelop プロジェクト仕様書です。*
*最終更新: 2026-03-09*
