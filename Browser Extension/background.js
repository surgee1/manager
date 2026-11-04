import { importActiveAccount, managerRequest, checkUpdates } from './core.js';

chrome.storage.local.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id || sender.url !== chrome.runtime.getURL('popup.html')) return false;
  if (!['connect', 'import', 'updates'].includes(message?.action)) return false;
  (async () => {
    try {
      let result;
      if (message.action === 'connect') {
        const token = String(message.code || '').trim();
        result = await managerRequest('/v1/status', token);
        await chrome.storage.local.set({ pairingCode: token });
      } else if (message.action === 'updates') result = await checkUpdates(chrome);
      else result = await importActiveAccount(chrome);
      respond({ ok: true, result });
    } catch (error) { respond({ ok: false, error: error.message || 'Не удалось выполнить действие.' }); }
  })();
  return true;
});
