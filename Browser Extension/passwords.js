import { managerRequest } from './core.js';

const PREFIX = 'spectrumPasswordLogin:';
export function passwordLoginSender(sender) {
  try {
    const url = new URL(sender?.url);
    return sender.frameId === 0 && Number.isInteger(sender.tab?.id) && !sender.tab.incognito &&
      url.origin === 'https://www.roblox.com' && /^\/login\/?$/.test(url.pathname) && !url.username && !url.password;
  } catch { return false; }
}
export async function armPasswordLogin(api, sender, fetcher = fetch, now = Date.now()) {
  if (!passwordLoginSender(sender) || !api.storage.session) return { enabled: false };
  const { saveLoginPasswords, pairingCode } = await api.storage.local.get(['saveLoginPasswords', 'pairingCode']);
  if (!saveLoginPasswords) return { enabled: false };
  const status = await managerRequest('/v1/status', pairingCode, undefined, fetcher);
  if (!status.passwordImport) throw new Error('Для сохранения пароля обнови менеджер до 6.2.13+.');
  if (status.savePasswords === false) throw new Error('Включи сохранение паролей в настройках менеджера.');
  const stores = await api.cookies.getAllCookieStores();
  const storeId = stores.find(store => store.tabIds.includes(sender.tab.id))?.id;
  if (!storeId) return { enabled: false };
  const existing = (await api.storage.session.get(PREFIX + sender.tab.id))[PREFIX + sender.tab.id];
  if (existing?.storeId === storeId && existing.expires > now) return { enabled: true };
  const baseline = await api.cookies.get({ url: 'https://www.roblox.com/', name: '.ROBLOSECURITY', storeId });
  await api.storage.session.set({ [PREFIX + sender.tab.id]: { tabId: sender.tab.id, storeId, baseline: baseline?.value || '', expires: now + 600000 } });
  return { enabled: true };
}
export async function capturePasswordLogin(api, sender, message, now = Date.now()) {
  if (!passwordLoginSender(sender)) throw new Error('Пароль принимается только с формы входа Roblox.');
  const key = PREFIX + sender.tab.id;
  const candidate = (await api.storage.session.get(key))[key];
  const { saveLoginPasswords } = await api.storage.local.get('saveLoginPasswords');
  if (!saveLoginPasswords || !candidate || candidate.expires < now) throw new Error('Сохранение пароля выключено.');
  if (!/^[A-Za-z0-9_]{3,20}$/.test(message.username || '') || typeof message.password !== 'string' || !message.password || message.password.length > 1024)
    throw new Error('Используй ник Roblox и пароль в форме входа.');
  // A browser profile has one Roblox session. A newer submission supersedes other tabs in that profile.
  const all = await api.storage.session.get(null);
  const superseded = Object.keys(all).filter(other => other.startsWith(PREFIX) && other !== key && all[other].storeId === candidate.storeId);
  if (superseded.length) await api.storage.session.remove(superseded);
  await api.storage.session.set({ [key]: { ...candidate, username: message.username, password: message.password, expires: now + 600000 } });
  return { captured: true };
}
export async function clearPasswordLogins(api, tabId) {
  if (!api.storage.session) return;
  const keys = Object.keys(await api.storage.session.get(null)).filter(key => key.startsWith(PREFIX) && (tabId === undefined || key === PREFIX + tabId));
  if (keys.length) await api.storage.session.remove(keys);
}
export async function finishPasswordLogins(api, fetcher = fetch, now = Date.now(), onlyTabId) {
  if (!api.storage.session) return null;
  const { pairingCode, saveLoginPasswords } = await api.storage.local.get(['pairingCode', 'saveLoginPasswords']);
  if (!saveLoginPasswords) { await clearPasswordLogins(api); return null; }
  const pending = await api.storage.session.get(null);
  for (const [key, candidate] of Object.entries(pending)) {
    if (!key.startsWith(PREFIX) || (onlyTabId !== undefined && candidate.tabId !== onlyTabId)) continue;
    if (candidate.expires < now) { await api.storage.session.remove(key); continue; }
    if (!candidate.password) continue;
    let tab;
    try { tab = await api.tabs.get(candidate.tabId); }
    catch { await api.storage.session.remove(key); continue; }
    let origin;
    try { origin = new URL(tab.url).origin; } catch { origin = ''; }
    if (tab.incognito || origin !== 'https://www.roblox.com') { await api.storage.session.remove(key); continue; }
    const cookie = await api.cookies.get({ url: 'https://www.roblox.com/', name: '.ROBLOSECURITY', storeId: candidate.storeId });
    if (!cookie?.value || cookie.value === candidate.baseline) continue;
    // The manager verifies the new cookie against the submitted username before saving either secret.
    try {
      const result = await managerRequest('/v1/accounts/import-password', pairingCode,
        { cookie: cookie.value, username: candidate.username, password: candidate.password }, fetcher);
      await api.storage.session.remove(key);
      await api.storage.session.set({ passwordLoginStatus: { ok: true, username: result.username, passwordSaved: result.passwordSaved === true } });
      return result;
    } catch (error) {
      if ([400, 401, 403, 409].includes(error.status)) await api.storage.session.remove(key);
      await api.storage.session.set({ passwordLoginStatus: { ok: false, error: 'Не удалось добавить аккаунт с паролем. Проверь менеджер и нажми «Добавить в менеджер» ещё раз.' } });
      throw error;
    }
  }
  return null;
}
