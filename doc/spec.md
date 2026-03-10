# NdiTelop 仕様書

## 1. 目的
Windows 10/11 x64 上で動作する、ライブイベント・配信向けの NDI テロップ送出アプリを構築する。開発は Codex 主導で継続できるよう、段階的に実装する。

## 2. 技術スタック
- C# / .NET 8
- Avalonia UI 11.x
- CommunityToolkit.Mvvm
- SkiaSharp
- NDI SDK / NewTek.NDI
- ASP.NET Core (Kestrel)
- OSC ライブラリ
- xUnit
- GitHub Actions
- Windows インストーラー生成

## 3. コア要件
### 3.1 テロップ編集
- 複数行テキスト対応
- 行ごとにフォント、サイズ、色、アウトライン、シャドウ変更可能
- 背景（座布団）対応
- 画像/ロゴオーバーレイ対応
- フェード/スライドなど複数アニメーション対応
- 自動消去タイマー対応

### 3.2 出力
- NDI Program / Preview の2系統
- Fill / Key / 黒背景モード
- HD〜4K 対応
- フレームレート任意設定
- 低スペックPC向けに負荷時は FPS を落とせる
- 将来的に仮想カメラ / DeckLink / Spout2 / マルチモニター対応

### 3.3 操作
- アプリ本体から操作可能
- 即時送出モード
- OBS 風 Preview / Take モード
- Web UI（簡易版 + フル版）
- スマホ・タブレット対応
- OSC 制御
- Stream Deck 連携（早期は HTTP、後期で公式プラグイン）
- キーボードショートカット変更可能
- テンキー操作
- フットスイッチ対応を考慮

### 3.4 データ管理
- プリセット無制限
- セットリスト機能
- CSV インポート/エクスポート
- サンプル CSV エクスポート
- JSON 設定エクスポート/インポート

## 4. 今回の Phase 1 の対象
Phase 1 は「UI骨格 + 描画 + プリセット管理の最小実装」に限定する。

### Phase 1 でやること
- Avalonia アプリ骨格の確認
- `RenderService` 実装
- `PresetService` 実装
- 組み込みプリセットの読込
- `MainWindowViewModel` の基本動作
- 最小限の `MainWindow` UI
- 可能ならプレビュー描画表示
- 単体テストの追加

### Phase 1 でやらないこと
- Web UI
- OSC
- Stream Deck
- Virtual Camera
- DeckLink
- Spout2
- 4K最適化の詰め
- 高度なアニメーション全種
- PGM/PVW 完全切替制御

## 5. UI の最低要件（Phase 1）
- 左: プリセット一覧
- 中央: プレビュー領域
- 右または下: テキスト編集の最小項目
- 送出ボタンはダミー可、ただし UI 上に配置
- テーマは仮でダーク固定でも可

## 6. プリセット最低要件（Phase 1）
- アーティスト名
- 曲名
- イベント名
- MC テロップ
- カウントダウン（表示のみでも可）
- 透明カット

## 7. 品質条件
- テストが落ちないこと
- 既存の Interface を変更しないこと
- 共通ファイルの大規模改変をしないこと
- PR は小さく、目的が明確であること
