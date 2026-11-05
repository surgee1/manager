import { importActiveAccount, managerRequest, checkUpdates, listAccounts, openAccount, handoffNonce, updateExtension, loginNonce, loginWithCredentials, takeLoginCredentials, recoverCredentialLogin } from './core.js';
import { armPasswordLogin, capturePasswordLogin, finishPasswordLogins, clearPasswordLogins, passwordLoginSender } from './passwords.js';

chrome.storage.local.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
chrome.storage.session?.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
let recoveryError = null;
const recovered = recoverCredentialLogin(chrome).catch(() => { recoveryError = 'Прежний вход Roblox восстановлен не полностью после прерывания. Войди в нужный аккаунт вручную.'; });
let sessionActionBusy = false;
let passwordWork = Promise.resolve();
function passwordTask(action) {
  const result = passwordWork.then(action);
  passwordWork = result.catch(() => {});
  return result;
}
chrome.cookies.onChanged.addListener(change => {
  if (!change.removed && change.cookie.name === '.ROBLOSECURITY' && /(^|\.)roblox\.com$/.test(change.cookie.domain) && !sessionActionBusy)
    passwordTask(async () => { await recovered; if (!sessionActionBusy) await finishPasswordLogins(chrome); }).catch(() => {});
});
chrome.tabs.onRemoved.addListener(tabId => { passwordTask(() => clearPasswordLogins(chrome, tabId)).catch(() => {}); });
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id) return false;
  if (['password-capture-state', 'password-capture-submit'].includes(message?.action)) {
    if (!passwordLoginSender(sender)) return false;
    passwordTask(async () => {
      await recovered;
      if (sessionActionBusy) return { enabled: false };
      if (message.action === 'password-capture-state') return armPasswordLogin(chrome, sender);
      const result = await capturePasswordLogin(chrome, sender, message);
      await finishPasswordLogins(chrome);
      return result;
    }).then(result => respond({ ok: true, ...result })).catch(() => respond({ ok: false, error: 'Проверь подключение и включи сохранение паролей в менеджере.' }));
    return true;
  }
  if (message?.action === 'login-form-ready') {
    const credentials = takeLoginCredentials(sender);
    if (!credentials) return false;
    respond({ ok: true, credentials }); return false;
  }
  const popup = sender.url === chrome.runtime.getURL('popup.html');
  const nonce = message?.action === 'handoff' && sender.frameId === 0 ? handoffNonce(sender.url) : null;
  const login = message?.action === 'login-handoff' && sender.frameId === 0 ? loginNonce(sender.url) : null;
  if (!popup && !nonce && !login) return false;
  if ((nonce || login) && !Number.isInteger(sender.tab?.id)) return false;
  if (popup ? !['connect', 'import', 'updates', 'accounts', 'open', 'install-update', 'password-setting'].includes(message?.action) : !['handoff', 'login-handoff'].includes(message?.action)) return false;
  const exclusive = ['import', 'open', 'handoff', 'login-handoff', 'install-update'].includes(message.action);
  if (exclusive && sessionActionBusy) { respond({ ok: false, error: 'Дождись завершения переключения или обновления расширения.' }); return false; }
  if (exclusive) sessionActionBusy = true;
  (async () => {
    let reloading = false;
    try {
      await recovered;
      if (recoveryError) { const problem = recoveryError; recoveryError = null; throw new Error(problem); }
      let result;
      if (message.action === 'connect') {
        const token = String(message.code || '').trim();
        result = await managerRequest('/v1/status', token);
        await chrome.storage.local.set({ pairingCode: token });
      } else if (message.action === 'password-setting') {
        const enabled = message.enabled === true;
        if (enabled) {
          const { pairingCode } = await chrome.storage.local.get('pairingCode');
          const status = await managerRequest('/v1/status', pairingCode);
          if (!status.passwordImport) throw new Error('Для сохранения пароля обнови менеджер до 6.2.13+.');
          if (status.savePasswords === false) throw new Error('Включи сохранение паролей в настройках менеджера.');
        }
        await passwordTask(async () => {
          await chrome.storage.local.set({ saveLoginPasswords: enabled });
          if (!enabled) await clearPasswordLogins(chrome);
        });
        for (const tab of await chrome.tabs.query({ url: 'https://www.roblox.com/login*' }))
          chrome.tabs.sendMessage(tab.id, { action: 'password-capture-setting', enabled }).catch(() => {});
        result = { enabled };
      } else if (message.action === 'updates') result = await checkUpdates(chrome);
      else if (message.action === 'accounts') result = await listAccounts(chrome);
      else if (message.action === 'open') result = await openAccount(chrome, { id: String(message.id || '') });
      else if (message.action === 'handoff') result = await openAccount(chrome, { nonce }, sender.tab);
      else if (message.action === 'login-handoff') result = await loginWithCredentials(chrome, login, sender.tab);
      else if (message.action === 'install-update') result = await updateExtension(chrome);
      else {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        result = await passwordTask(() => finishPasswordLogins(chrome, fetch, Date.now(), tab?.id));
        if (!result) result = await importActiveAccount(chrome);
      }
      respond({ ok: true, result });
      if (message.action === 'install-update' && result.updated) {
        reloading = true;
        setTimeout(() => { try { chrome.runtime.reload(); } catch { sessionActionBusy = false; } }, 700);
      }
    } catch (error) { respond({ ok: false, error: error.message || 'Не удалось выполнить действие.' }); }
    finally { if (exclusive && !reloading) sessionActionBusy = false; }
  })();
  return true;
});
