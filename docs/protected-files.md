# 保護ファイル一覧

以下は Codex が原則変更してはいけない、または最小変更に留めるファイル。

## 変更禁止
- `src/NdiTelop/Interfaces/INdiService.cs`
- `src/NdiTelop/Interfaces/IRenderService.cs`
- `src/NdiTelop/Interfaces/IPresetService.cs`
- `src/NdiTelop/Interfaces/ISetlistService.cs`
- `src/NdiTelop/Interfaces/IWebApiService.cs`
- `src/NdiTelop/Interfaces/IOscService.cs`
- `src/NdiTelop/Interfaces/IOutputService.cs`
- `src/NdiTelop/Interfaces/ISettingsService.cs`

## 最小変更のみ
- `src/NdiTelop/Program.cs`
- `src/NdiTelop/App.axaml`
- `src/NdiTelop/App.axaml.cs`
- `.github/workflows/build-windows.yml`
- `src/NdiTelop/NdiTelop.csproj`

## 原則新規ファイルで対応
- `Services/` 配下の新機能
- `ViewModels/` の機能追加
- `Views/` の新規UI
- `tests/` の追加テスト

## 競合時の優先
- Interface の整合性を最優先
- build/test が通ることを最優先
- 大規模編集より新規ファイル追加を優先
