import { importActiveAccount, managerRequest, checkUpdates, listAccounts, openAccount, handoffNonce, updateExtension, loginNonce, loginWithCredentials, takeLoginCredentials, recoverCredentialLogin } from './core.js';

chrome.storage.local.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
chrome.storage.session?.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
let recoveryError = null;
const recovered = recoverCredentialLogin(chrome).catch(() => { recoveryError = 'Прежний вход Roblox восстановлен не полностью после прерывания. Войди в нужный аккаунт вручную.'; });
let sessionActionBusy = false;
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id) return false;
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
  if (popup ? !['connect', 'import', 'updates', 'accounts', 'open', 'install-update'].includes(message?.action) : !['handoff', 'login-handoff'].includes(message?.action)) return false;
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
      } else if (message.action === 'updates') result = await checkUpdates(chrome);
      else if (message.action === 'accounts') result = await listAccounts(chrome);
      else if (message.action === 'open') result = await openAccount(chrome, { id: String(message.id || '') });
      else if (message.action === 'handoff') result = await openAccount(chrome, { nonce }, sender.tab);
      else if (message.action === 'login-handoff') result = await loginWithCredentials(chrome, login, sender.tab);
      else if (message.action === 'install-update') result = await updateExtension(chrome);
      else result = await importActiveAccount(chrome);
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
