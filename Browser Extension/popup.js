import { validReleaseUrl, verifyExtensionReload } from './core.js';

const status = document.getElementById('status');
const add = document.getElementById('import');
const connect = document.getElementById('connect');
const code = document.getElementById('code');
const pairing = document.getElementById('pairing');
const refresh = document.getElementById('refresh-accounts');
const search = document.getElementById('account-search');
const accounts = document.getElementById('accounts');
const saveLoginPasswords = document.getElementById('save-login-passwords');
chrome.storage.local.get('saveLoginPasswords').then(value => { saveLoginPasswords.checked = value.saveLoginPasswords === true; });
saveLoginPasswords.addEventListener('change', async () => {
  saveLoginPasswords.disabled = true;
  try {
    const response = await chrome.runtime.sendMessage({ action: 'password-setting', enabled: saveLoginPasswords.checked });
    if (!response?.ok) throw new Error(response?.error || 'Не удалось изменить настройку.');
    status.className = '';
    status.textContent = response.result.enabled ? 'Теперь войди в Roblox по нику и паролю. После входа аккаунт сохранится автоматически.' : 'Сохранение паролей через расширение выключено.';
  } catch (error) { saveLoginPasswords.checked = !saveLoginPasswords.checked; status.className = 'error'; status.textContent = error.message; }
  finally { saveLoginPasswords.disabled = false; }
});
let accountItems = [], busy = false;

function setBusy(value) {
  busy = value;
  add.disabled = connect.disabled = refresh.disabled = value;
  for (const button of accounts.querySelectorAll('button')) button.disabled = value;
}
function renderAccounts() {
  accounts.replaceChildren();
  const query = search.value.trim().toLocaleLowerCase();
  const filtered = accountItems.filter(item => `${item.username} ${item.alias} ${item.folder}`.toLocaleLowerCase().includes(query));
  for (const account of filtered.slice(0, 100)) {
    const button = document.createElement('button');
    button.className = 'account'; button.disabled = busy;
    const name = document.createElement('strong'); name.textContent = account.username;
    const detail = document.createElement('span'); detail.textContent = [account.alias, account.folder].filter(Boolean).join(' · ');
    button.append(name, detail);
    button.addEventListener('click', () => run({ action: 'open', id: account.id }));
    accounts.append(button);
  }
  if (!filtered.length) { const empty = document.createElement('p'); empty.className = 'intro'; empty.textContent = query ? 'Ничего не найдено.' : 'Нет аккаунтов. Добавь их в менеджер.'; accounts.append(empty); }
  if (filtered.length > 100) { const hint = document.createElement('p'); hint.className = 'intro'; hint.textContent = 'Показаны первые 100. Уточни поиск.'; accounts.append(hint); }
}
async function loadAccounts(preserveStatus = false) {
  if (busy) return;
  setBusy(true);
  try {
    const response = await chrome.runtime.sendMessage({ action: 'accounts' });
    if (!response?.ok) throw new Error(response?.error || 'Нет ответа от менеджера.');
    accountItems = response.result.accounts; renderAccounts();
    if (preserveStatus !== true) {
      const saved = await chrome.storage.session?.get('passwordLoginStatus');
      const result = saved?.passwordLoginStatus;
      status.className = result ? (result.ok ? 'success' : 'error') : '';
      status.textContent = result ? (result.ok ? `Добавлен: ${result.username}${result.passwordSaved ? ' · пароль сохранён' : ''}` : result.error)
        : `Аккаунтов в менеджере: ${accountItems.length}. Выбери нужный для входа.`;
    }
  } catch (error) { status.className = 'error'; status.textContent = error.message; }
  finally { setBusy(false); }
}
refresh.addEventListener('click', loadAccounts);
search.addEventListener('input', renderAccounts);

async function run(message) {
  if (busy) return;
  setBusy(true);
  status.className = '';
  status.textContent = message.action === 'open' ? 'Переключаем и проверяем аккаунт Roblox…' : message.action === 'import' ? 'Проверяем аккаунт и добавляем в менеджер…' : 'Подключаем менеджер…';
  try {
    const response = await chrome.runtime.sendMessage(message);
    if (!response?.ok) throw new Error(response?.error || 'Нет ответа от расширения. Перезагрузи его.');
    const result = response.result;
    status.className = 'success';
    if (message.action === 'open') status.textContent = `Открыт аккаунт: ${result.username}`;
    else if (message.action === 'import') {
      const display = result.displayName && result.displayName !== result.username ? ` (${result.displayName})` : '';
      status.textContent = `${result.updated ? 'Обновлён' : 'Добавлен'}: ${result.username}${display}${result.passwordSaved ? ' · пароль сохранён' : ''}`;
    } else { status.textContent = 'Менеджер подключён. Теперь можно добавлять аккаунты.'; code.value = ''; pairing.open = false; }
  } catch (error) { status.className = 'error'; status.textContent = error.message; }
  finally { setBusy(false); }
  if (message.action === 'connect' || message.action === 'import') await loadAccounts(true);
}
add.addEventListener('click', () => run({ action: 'import' }));
connect.addEventListener('click', () => run({ action: 'connect', code: code.value.trim() }));
chrome.storage.local.get('pairingCode').then(({ pairingCode }) => {
  pairing.open = !pairingCode;
  if (pairingCode) loadAccounts();
});

document.getElementById('version').textContent = chrome.runtime.getManifest().version;
verifyExtensionReload(chrome).then(result => {
  if (!result) return;
  document.getElementById('update-status').textContent = result.updated ? `Расширение обновлено: ${result.version}.`
    : `Браузер всё ещё использует ${result.version}, ожидается ${result.expectedVersion}. Перезагрузи расширение на странице расширений браузера.`;
}).catch(() => {});
document.getElementById('reload-extension').addEventListener('click', () => chrome.runtime.reload());
document.getElementById('install-update').addEventListener('click', async () => {
  const button = document.getElementById('install-update');
  const text = document.getElementById('update-status');
  button.disabled = true; setBusy(true);
  text.textContent = 'Менеджер проверяет GitHub и устанавливает обновление…';
  try {
    const response = await chrome.runtime.sendMessage({ action: 'install-update' });
    if (!response?.ok) throw new Error(response?.error || 'Обнови менеджер до 6.2.0 или новее.');
    text.textContent = response.result.updated ? `Файлы версии ${response.result.version} обновлены. Перезагружаем расширение…` : `Уже установлена актуальная версия ${response.result.version}.`;
  } catch (error) { text.textContent = error.message; }
  finally { button.disabled = false; setBusy(false); }
});
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
