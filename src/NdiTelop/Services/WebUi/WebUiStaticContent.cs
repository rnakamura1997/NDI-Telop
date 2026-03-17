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
    <h1>NDI Telop Control</h1>

    <section class="card">
      <h2>NDI出力ステータス</h2>
      <p id="ndiStatus" class="status-badge status-inactive">Inactive</p>
    </section>

    <section class="card">
      <h2>基本設定</h2>
      <dl id="basicSettings" class="settings-grid">
        <dt>NDI Source</dt><dd>-</dd>
        <dt>Resolution</dt><dd>-</dd>
        <dt>Frame Rate</dt><dd>-</dd>
        <dt>Web API Port</dt><dd>-</dd>
        <dt>OSC Port</dt><dd>-</dd>
      </dl>
    </section>

    <section class="card">
      <div class="actions-row">
        <h2>プリセット一覧</h2>
        <button id="reloadButton" type="button">再取得</button>
      </div>
      <ul id="presetList" class="preset-list"></ul>
    </section>

    <section class="card">
      <h2>プログラム出力</h2>
      <button id="clearButton" type="button" class="danger">クリア</button>
    </section>

    <p id="statusMessage" class="message" role="status">読み込み待機中...</p>
  </main>

  <script src="/web-ui.js"></script>
</body>
</html>
""";

    public const string StylesCss = """
:root {
  color-scheme: light dark;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  font-family: system-ui, sans-serif;
  background: #1c1f26;
  color: #f4f6fc;
}

.container {
  max-width: 860px;
  margin: 0 auto;
  padding: 1.25rem;
  display: grid;
  gap: 0.75rem;
}

.card {
  background: #2a2f3a;
  border-radius: 8px;
  padding: 0.85rem;
}

.actions-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
}

button {
  border: 0;
  border-radius: 6px;
  background: #2f80ed;
  color: #fff;
  padding: 0.45rem 0.9rem;
  cursor: pointer;
}

button.danger {
  background: #cf3f3f;
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
  border-radius: 6px;
  background: #1f2430;
  padding: 0.55rem 0.7rem;
}

.preset-name {
  font-weight: 600;
}

.settings-grid {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 0.4rem 0.7rem;
  margin: 0;
}

.settings-grid dt {
  color: #c8d0e4;
}

.settings-grid dd {
  margin: 0;
}

.status-badge {
  display: inline-block;
  margin: 0;
  padding: 0.3rem 0.6rem;
  border-radius: 999px;
  font-weight: 700;
}

.status-active {
  background: #3cb371;
  color: #0e2217;
}

.status-inactive {
  background: #888888;
  color: #111111;
}

.status-error {
  background: #e74c3c;
  color: #260f0c;
}

.message {
  margin: 0.1rem 0 0;
}

.message.error {
  color: #ff8080;
}
""";

    public const string ScriptJs = """
const presetList = document.getElementById('presetList');
const reloadButton = document.getElementById('reloadButton');
const clearButton = document.getElementById('clearButton');
const statusMessage = document.getElementById('statusMessage');
const ndiStatus = document.getElementById('ndiStatus');
const basicSettings = document.getElementById('basicSettings');

function setMessage(message, isError = false) {
  statusMessage.textContent = message;
  statusMessage.classList.toggle('error', isError);
}

async function readJsonOrThrow(response) {
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }

  return response.json();
}

function setNdiStatus(status) {
  const normalized = String(status || 'Inactive');
  ndiStatus.textContent = normalized;
  ndiStatus.classList.remove('status-active', 'status-inactive', 'status-error');

  if (normalized === 'Active') {
    ndiStatus.classList.add('status-active');
  } else if (normalized === 'Error') {
    ndiStatus.classList.add('status-error');
  } else {
    ndiStatus.classList.add('status-inactive');
  }
}

function renderBasicSettings(settings) {
  const values = [
    settings.ndiSourceName || '-',
    `${settings.resolutionWidth} x ${settings.resolutionHeight}`,
    `${settings.frameRateN}/${settings.frameRateD}`,
    String(settings.webApiPort ?? '-'),
    String(settings.oscPort ?? '-')
  ];

  const entries = basicSettings.querySelectorAll('dd');
  entries.forEach((dd, index) => {
    dd.textContent = values[index] || '-';
  });
}

async function activatePreset(id) {
  const response = await fetch(`/api/presets/${encodeURIComponent(id)}/activate`, { method: 'POST' });
  if (!response.ok) {
    throw new Error(`Activate failed (${response.status})`);
  }

  setMessage(`プリセット ${id} をProgramに切り替えました。`);
}

function renderPresets(presets) {
  presetList.innerHTML = '';

  if (!Array.isArray(presets) || presets.length === 0) {
    setMessage('利用可能なプリセットがありません。');
    return;
  }

  for (const preset of presets) {
    const item = document.createElement('li');
    item.className = 'preset-item';

    const name = document.createElement('span');
    name.className = 'preset-name';
    name.textContent = preset.name || preset.id;

    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Programへ';
    button.addEventListener('click', async () => {
      try {
        await activatePreset(preset.id);
        await refreshNdiStatus();
      } catch (error) {
        setMessage(error.message || 'プリセット切り替え中にエラーが発生しました。', true);
      }
    });

    item.append(name, button);
    presetList.append(item);
  }

  setMessage(`${presets.length} 件のプリセットを表示しています。`);
}

async function refreshNdiStatus() {
  const data = await readJsonOrThrow(await fetch('/api/status/ndi'));
  setNdiStatus(data.status);
}

async function refreshBasicSettings() {
  const data = await readJsonOrThrow(await fetch('/api/settings/basic'));
  renderBasicSettings(data);
}

async function loadPresets() {
  const presets = await readJsonOrThrow(await fetch('/api/presets'));
  renderPresets(presets);
}

async function clearProgram() {
  const response = await fetch('/api/program/clear', { method: 'POST' });
  if (!response.ok) {
    throw new Error(`Clear failed (${response.status})`);
  }

  setMessage('プログラム出力をクリアしました。');
}

async function refreshAll() {
  try {
    await Promise.all([
      loadPresets(),
      refreshNdiStatus(),
      refreshBasicSettings()
    ]);
  } catch (error) {
    setMessage(`読み込みに失敗しました: ${error.message}`, true);
  }
}

reloadButton.addEventListener('click', refreshAll);
clearButton.addEventListener('click', async () => {
  try {
    await clearProgram();
    await refreshNdiStatus();
  } catch (error) {
    setMessage(error.message || 'クリアに失敗しました。', true);
  }
});

refreshAll();
""";
}
