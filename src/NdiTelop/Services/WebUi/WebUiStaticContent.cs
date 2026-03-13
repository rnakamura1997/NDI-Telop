namespace NdiTelop.Services.WebUi;

internal static class WebUiStaticContent
{
    public const string IndexHtml = """
<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>NDI Telop Web UI</title>
  <link rel="stylesheet" href="/web-ui.css">
</head>
<body>
  <main class="container">
    <h1>NDI Telop Presets</h1>
    <p class="help">プリセット一覧を取得し、選択したプリセットを有効化します。</p>

    <div class="actions">
      <button id="reloadButton" type="button">一覧を再取得</button>
    </div>

    <div id="status" class="status" role="status">読み込み待機中...</div>

    <ul id="presetList" class="preset-list"></ul>
  </main>

  <script src="/web-ui.js"></script>
</body>
</html>
""";

    public const string StylesCss = """
:root {
  color-scheme: light dark;
}

body {
  margin: 0;
  font-family: system-ui, sans-serif;
  background: #1c1f26;
  color: #f4f6fc;
}

.container {
  max-width: 720px;
  margin: 0 auto;
  padding: 1.25rem;
}

.actions {
  margin-bottom: 0.75rem;
}

button {
  border: 0;
  border-radius: 6px;
  background: #2f80ed;
  color: #fff;
  padding: 0.5rem 0.9rem;
  cursor: pointer;
}

.status {
  margin-bottom: 0.75rem;
}

.status.error {
  color: #ff8080;
}

.preset-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 0.5rem;
}

.preset-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #2a2f3a;
  border-radius: 8px;
  padding: 0.6rem 0.75rem;
}

.preset-name {
  font-weight: 600;
}
""";

    public const string ScriptJs = """
const presetList = document.getElementById('presetList');
const statusBox = document.getElementById('status');
const reloadButton = document.getElementById('reloadButton');

function setStatus(message, isError = false) {
  statusBox.textContent = message;
  statusBox.classList.toggle('error', isError);
}

async function readJsonOrThrow(response) {
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }

  return response.json();
}

async function activatePreset(id) {
  setStatus(`プリセット ${id} を有効化中...`);

  const response = await fetch(`/api/presets/${encodeURIComponent(id)}/activate`, {
    method: 'POST'
  });

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('プリセットが見つかりません (404)。');
    }

    throw new Error(`有効化に失敗しました (${response.status})。`);
  }

  setStatus(`プリセット ${id} を有効化しました。`);
}

function renderPresets(presets) {
  presetList.innerHTML = '';

  if (!Array.isArray(presets) || presets.length === 0) {
    setStatus('利用可能なプリセットがありません。');
    return;
  }

  setStatus(`${presets.length} 件のプリセットを読み込みました。`);

  for (const preset of presets) {
    const item = document.createElement('li');
    item.className = 'preset-item';

    const name = document.createElement('span');
    name.className = 'preset-name';
    name.textContent = preset.name || preset.id;

    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Activate';
    button.addEventListener('click', async () => {
      try {
        await activatePreset(preset.id);
      } catch (error) {
        setStatus(error.message || '有効化中にエラーが発生しました。', true);
      }
    });

    item.append(name, button);
    presetList.append(item);
  }
}

async function loadPresets() {
  setStatus('プリセット一覧を読み込み中...');

  try {
    const response = await fetch('/api/presets');
    const presets = await readJsonOrThrow(response);
    renderPresets(presets);
  } catch (error) {
    if (String(error.message).includes('404')) {
      setStatus('APIエンドポイントが見つかりません (404)。', true);
    } else {
      setStatus(`プリセット取得に失敗しました: ${error.message}`, true);
    }
  }
}

reloadButton.addEventListener('click', loadPresets);
loadPresets();
""";
}
