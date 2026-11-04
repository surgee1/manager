import { validReleaseUrl } from './core.js';

const status = document.getElementById('status');
const add = document.getElementById('import');
const connect = document.getElementById('connect');
const code = document.getElementById('code');
const pairing = document.getElementById('pairing');

async function run(message) {
  add.disabled = connect.disabled = true;
  status.className = '';
  status.textContent = message.action === 'import' ? 'Проверяем аккаунт и добавляем в менеджер…' : 'Подключаем менеджер…';
  try {
    const response = await chrome.runtime.sendMessage(message);
    if (!response?.ok) throw new Error(response?.error || 'Нет ответа от расширения. Перезагрузи его.');
    const result = response.result;
    status.className = 'success';
    if (message.action === 'import') {
      const display = result.displayName && result.displayName !== result.username ? ` (${result.displayName})` : '';
      status.textContent = `${result.updated ? 'Обновлён' : 'Добавлен'}: ${result.username}${display}`;
    } else { status.textContent = 'Менеджер подключён. Теперь можно добавлять аккаунты.'; code.value = ''; pairing.open = false; }
  } catch (error) { status.className = 'error'; status.textContent = error.message; }
  finally { add.disabled = connect.disabled = false; }
}
add.addEventListener('click', () => run({ action: 'import' }));
connect.addEventListener('click', () => run({ action: 'connect', code: code.value.trim() }));
chrome.storage.local.get('pairingCode').then(({ pairingCode }) => {
  pairing.open = !pairingCode;
  if (pairingCode) status.textContent = 'Готово. Открой Roblox в активной вкладке.';
});

document.getElementById('version').textContent = chrome.runtime.getManifest().version;
document.getElementById('reload-extension').addEventListener('click', () => chrome.runtime.reload());
document.getElementById('check-updates').addEventListener('click', async () => {
  const button = document.getElementById('check-updates');
  const text = document.getElementById('update-status');
  const link = document.getElementById('extension-download');
  button.disabled = true; link.hidden = true; text.textContent = 'Проверяем GitHub Releases…';
  try {
    const response = await chrome.runtime.sendMessage({ action: 'updates' });
    if (!response?.ok) throw new Error(response?.error || 'Обнови менеджер до версии 6 или новее.');
    const info = response.result;
    const version = info.latestExtensionVersion || 'из последнего релиза';
    text.textContent = `Расширение: ${chrome.runtime.getManifest().version}; в релизе: ${version}. Менеджер: ${info.managerVersion}; в релизе: ${info.latestManagerVersion}. Для обновления менеджера открой Настройки → Обновления.`;
    if (validReleaseUrl(info.extensionDownloadUrl)) { link.href = info.extensionDownloadUrl; link.hidden = false; }
  } catch (error) { text.textContent = error.message; }
  finally { button.disabled = false; }
});
