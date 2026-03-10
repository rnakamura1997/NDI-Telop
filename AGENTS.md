# AGENTS.md

このリポジトリでは、Codex は以下を必ず守ること。

## 0. 作業開始条件
- このタスクは **main ブランチ起点の最新スナップショット** から開始されている前提とする。
- **シェルから `git fetch` / `git pull` を必須条件にしないこと。**
  - Codex Cloud では agent phase でネットワーク制限があり得るため、GitHub 上で選択されたベースブランチのスナップショットを信頼すること。
  - もし task のベースが `main` ではないと判断した場合は、変更せず停止して「main 起点でタスクを作り直してください」と報告すること。
- `docs/spec.md` `docs/phase-plan.md` `docs/protected-files.md` を最初に読むこと。

## 1. 基本ルール
1. 今回対象のフェーズだけを実装する。
2. 未マージ PR のブランチを作業ベースにしない。
3. `Interfaces/` 配下は変更禁止。
4. `Program.cs` と `App.axaml` は最小変更のみ許可。
5. 新機能は新規ファイル中心で追加する。
6. 既存テストの削除・緩和は禁止。
7. 競合しそうなら無理に進めず、変更を止めて報告する。

## 2. ビルドとテスト
- 変更後は必ず以下を実行する。

```bash
dotnet build src/NdiTelop/NdiTelop.csproj
dotnet test tests/NdiTelop.Tests/NdiTelop.Tests.csproj
```

- build/test が通らない場合はコミット・PR作成禁止。
- `dotnet` が存在しない場合は、環境不備として停止し、必要な setup 条件を報告する。

## 3. 実装方針
- 依存方向は以下のみ許可。

```text
Views -> ViewModels -> Interfaces -> Services/Implementations
```

- 既存の共通ファイルを広く書き換えず、拡張ポイントや新規ファイルで実装する。
- 1 PR 1 フェーズ（または 1 サブフェーズ）を厳守する。
- 変更量が大きくなる場合は、まず計画を短く提示してから実装する。

## 4. Phase 1 再実装時の優先順
Phase 1 をやり直す場合は、以下の順で進める。
1. 既存骨格の確認
2. `RenderService`
3. `PresetService`
4. `MainWindowViewModel`
5. `MainWindow` の最小 UI
6. 組み込みプリセット
7. テスト追加・修正
8. build/test 通過確認
9. PR 作成

## 5. 禁止事項
- `git fetch origin` を前提条件にして停止しないこと。
- docs 不在を理由に停止しないこと（このリポジトリでは docs を同梱する）。
- conflict 中の古い PR を直接修復しないこと。
- 仕様外の大規模リファクタをしないこと。
- build/test 未実行で PR を作らないこと。

## 6. 完了条件
- 対象フェーズの実装が完了している。
- `dotnet build` と `dotnet test` が通っている。
- 変更範囲が対象フェーズ内に収まっている。
- PR 説明に「何をしたか」「何をしていないか」「確認コマンド」を書くこと。
