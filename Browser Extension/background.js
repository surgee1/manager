import { importActiveAccount, managerRequest, checkUpdates, listAccounts, openAccount, handoffNonce, updateExtension } from './core.js';

chrome.storage.local.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
let sessionActionBusy = false;
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id) return false;
  const popup = sender.url === chrome.runtime.getURL('popup.html');
  const nonce = message?.action === 'handoff' && sender.frameId === 0 ? handoffNonce(sender.url) : null;
  if (!popup && !nonce) return false;
  if (nonce && !Number.isInteger(sender.tab?.id)) return false;
  if (popup ? !['connect', 'import', 'updates', 'accounts', 'open', 'install-update'].includes(message?.action) : message?.action !== 'handoff') return false;
  const exclusive = ['import', 'open', 'handoff', 'install-update'].includes(message.action);
  if (exclusive && sessionActionBusy) { respond({ ok: false, error: 'Дождись завершения переключения или обновления расширения.' }); return false; }
  if (exclusive) sessionActionBusy = true;
  (async () => {
    let reloading = false;
    try {
      let result;
      if (message.action === 'connect') {
        const token = String(message.code || '').trim();
        result = await managerRequest('/v1/status', token);
        await chrome.storage.local.set({ pairingCode: token });
      } else if (message.action === 'updates') result = await checkUpdates(chrome);
      else if (message.action === 'accounts') result = await listAccounts(chrome);
      else if (message.action === 'open') result = await openAccount(chrome, { id: String(message.id || '') });
      else if (message.action === 'handoff') result = await openAccount(chrome, { nonce }, sender.tab);
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
