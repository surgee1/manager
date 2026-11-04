class UserError extends Error {}
export const MANAGER_URL = 'http://127.0.0.1:7964';

export async function managerRequest(path, token, body, fetcher = fetch) {
  if (!/^[a-f0-9]{64}$/i.test(token || '')) throw new UserError('Вставь код из Настройки → Расширение в менеджере.');
  if (!['/v1/status', '/v1/accounts/import', '/v1/updates', '/v1/accounts/list', '/v1/accounts/open', '/v1/browser/consume', '/v1/extension/update'].includes(path)) throw new UserError('Неизвестная команда.');
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), path === '/v1/extension/update' ? 90000 : 20000);
  try {
    let response;
    try {
      response = await fetcher(MANAGER_URL + path, {
        method: body === undefined ? 'GET' : 'POST',
        headers: { Authorization: `Bearer ${token}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
        ...(body === undefined ? {} : { body: JSON.stringify(body) }),
        credentials: 'omit', cache: 'no-store', redirect: 'error', referrerPolicy: 'no-referrer', signal: controller.signal
      });
    } catch {
      throw new UserError('Менеджер недоступен. Открой обычную версию менеджера и проверь Настройки → Расширение.');
    }
    let result;
    try { result = await response.json(); } catch { throw new UserError('Менеджер вернул непонятный ответ. Обнови сборку.'); }
    if (!response.ok || !result.ok) throw new UserError(result.error || 'Не удалось выполнить действие.');
    return result;
  } finally { clearTimeout(timer); }
}

export async function checkUpdates(api, fetcher = fetch) {
  const { pairingCode } = await api.storage.local.get('pairingCode');
  return managerRequest('/v1/updates', pairingCode, undefined, fetcher);
}
export async function updateExtension(api, fetcher = fetch) {
  const { pairingCode } = await api.storage.local.get('pairingCode');
  return managerRequest('/v1/extension/update', pairingCode, { extensionId: api.runtime.id, version: api.runtime.getManifest().version }, fetcher);
}

export function validReleaseUrl(value) {
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && url.hostname === 'github.com' && !url.username && !url.password &&
      url.pathname.startsWith('/surgee1/manager/releases/download/');
  } catch { return false; }
}

export async function importActiveAccount(api, fetcher = fetch) {
  const { pairingCode } = await api.storage.local.get('pairingCode');
  await managerRequest('/v1/status', pairingCode, undefined, fetcher);
  const tabs = await api.tabs.query({ active: true, currentWindow: true });
  const tab = tabs[0];
  let url;
  try { url = new URL(tab?.url || ''); } catch { throw new UserError('Открой вкладку Roblox со своим аккаунтом.'); }
  if (url.protocol !== 'https:' || !['www.roblox.com', 'roblox.com'].includes(url.hostname))
    throw new UserError('Открой активную вкладку Roblox со своим аккаунтом.');
  const stores = await api.cookies.getAllCookieStores();
  const store = stores.find(value => value.tabIds.includes(tab.id));
  if (!store) throw new UserError('Не найден профиль этой вкладки. Открой обычное окно браузера.');
  const cookie = await api.cookies.get({ url: 'https://www.roblox.com/', name: '.ROBLOSECURITY', storeId: store.id });
  if (!cookie?.value) throw new UserError('Сессия Roblox не найдена. Сначала войди в аккаунт в этой вкладке.');
  return managerRequest('/v1/accounts/import', pairingCode, { cookie: cookie.value }, fetcher);
}

export function handoffNonce(value) {
  try {
    const url = new URL(value);
    if (url.origin !== MANAGER_URL || url.search || url.hash || url.username || url.password) return null;
    return /^\/browser\/open\/([a-f0-9]{64})$/.exec(url.pathname)?.[1] || null;
  } catch { return null; }
}

export async function listAccounts(api, fetcher = fetch) {
  const { pairingCode } = await api.storage.local.get('pairingCode');
  const result = await managerRequest('/v1/accounts/list', pairingCode, undefined, fetcher);
  if (!Array.isArray(result.accounts)) throw new UserError('Обнови менеджер до 6.2.0 или новее.');
  return { accounts: result.accounts.map(item => ({ id: String(item.id), username: String(item.username || ''), alias: String(item.alias || ''), folder: String(item.folder || '') })) };
}

function cookieDetails(cookie) {
  const host = String(cookie.domain || '').replace(/^\./, '');
  if (host !== 'roblox.com' && !host.endsWith('.roblox.com')) throw new UserError('Неверный домен cookie Roblox.');
  return { url: `https://${host}${cookie.path || '/'}`, name: '.ROBLOSECURITY', storeId: cookie.storeId,
    ...(cookie.partitionKey ? { partitionKey: cookie.partitionKey } : {}) };
}
async function clearSessions(api, storeId) {
  // remove() selects a matching URL cookie, not an exact domain. Re-read to remove host-only shadows too.
  for (let attempt = 0; attempt < 64; attempt++) {
    const cookies = await api.cookies.getAll({ domain: 'roblox.com', name: '.ROBLOSECURITY', storeId });
    if (!cookies.length) return;
    if (!await api.cookies.remove(cookieDetails(cookies[0]))) throw new UserError('Браузер не смог заменить прежнюю сессию Roblox.');
  }
  throw new UserError('Слишком много конфликтующих сессий Roblox. Выйди из Roblox в этом профиле браузера и повтори.');
}
async function verifyIdentity(expectedId, fetcher) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 15000);
  try {
    let response;
    try { response = await fetcher('https://users.roblox.com/v1/users/authenticated', { credentials: 'include', cache: 'no-store', redirect: 'error', signal: controller.signal }); }
    catch { throw new UserError('Не удалось проверить вход Roblox. Проверь соединение и повтори.'); }
    if (response.status === 401) throw new UserError('Сессия выбранного аккаунта завершилась. Войди заново и добавь его в менеджер через расширение.');
    if (response.status === 429) throw new UserError('Roblox ограничил запросы. Повтори чуть позже.');
    if (!response.ok) throw new UserError('Roblox не подтвердил вход. Открой сайт и пройди проверку, если он её запрашивает.');
    let identity;
    try { identity = await response.json(); } catch { throw new UserError('Roblox вернул непонятный ответ при проверке входа.'); }
    if (String(identity.id) !== expectedId) throw new UserError('Roblox открыл другой аккаунт. Переключение отменено; повтори вход через расширение.');
    return { userId: expectedId, username: String(identity.name || '') };
  } finally { clearTimeout(timer); }
}

let switching = false;
export async function openAccount(api, selection, contextTab, fetcher = fetch) {
  if (switching) throw new UserError('Дождись завершения предыдущего переключения.');
  if (!selection || (selection.id !== undefined ? !/^[1-9]\d{0,18}$/.test(String(selection.id)) : !/^[a-f0-9]{64}$/.test(selection.nonce || '')))
    throw new UserError('Выбери аккаунт или снова нажми «Браузер» в менеджере.');
  switching = true;
  let oldCookies, storeId, changed = false;
  try {
    const { pairingCode } = await api.storage.local.get('pairingCode');
    const tab = contextTab || (await api.tabs.query({ active: true, currentWindow: true }))[0];
    if (!Number.isInteger(tab?.id)) throw new UserError('Открой обычное окно браузера.');
    if (tab.incognito) throw new UserError('Переключение аккаунтов работает в обычном окне браузера. Открой его вместо инкогнито.');
    const stores = await api.cookies.getAllCookieStores();
    storeId = stores.find(store => store.tabIds.includes(tab.id))?.id;
    if (!storeId) throw new UserError('Не найден профиль вкладки. Открой обычное окно браузера.');
    const session = await managerRequest(selection.id !== undefined ? '/v1/accounts/open' : '/v1/browser/consume', pairingCode, selection, fetcher);
    const expectedId = String(session.userId || '');
    if (!/^[1-9]\d{0,18}$/.test(expectedId) || (selection.id !== undefined && expectedId !== String(selection.id)) ||
      typeof session.cookie !== 'string' || !session.cookie || session.cookie.length > 6000 || /[\r\n;]/.test(session.cookie))
      throw new UserError('Менеджер не вернул пригодную сессию выбранного аккаунта.');
    oldCookies = await api.cookies.getAll({ domain: 'roblox.com', name: '.ROBLOSECURITY', storeId });
    if (oldCookies.length > 64) throw new UserError('Слишком много конфликтующих сессий Roblox. Открой обычный профиль браузера.');
    changed = true;
    await clearSessions(api, storeId);
    const cookie = await api.cookies.set({ url: 'https://www.roblox.com/', name: '.ROBLOSECURITY', value: session.cookie,
      domain: '.roblox.com', path: '/', secure: true, httpOnly: true, sameSite: 'no_restriction',
      expirationDate: Math.floor(Date.now() / 1000) + 30 * 86400, storeId });
    if (!cookie) throw new UserError('Браузер не сохранил сессию Roblox.');
    const identity = await verifyIdentity(expectedId, fetcher);
    if (contextTab) await api.tabs.update(tab.id, { url: 'https://www.roblox.com/home' });
    else await api.tabs.create({ url: 'https://www.roblox.com/home', active: true });
    return identity; // Never return a cookie to the popup or to the content script.
  } catch (error) {
    if (changed) {
      try {
        await clearSessions(api, storeId);
        for (const cookie of oldCookies) {
          const details = cookieDetails(cookie);
          const restored = await api.cookies.set({ ...details, value: cookie.value, path: cookie.path, secure: cookie.secure, httpOnly: cookie.httpOnly, sameSite: cookie.sameSite,
            ...(!cookie.hostOnly ? { domain: cookie.domain } : {}), ...(!cookie.session && cookie.expirationDate ? { expirationDate: cookie.expirationDate } : {}) });
          if (!restored) throw new UserError();
        }
      } catch { throw new UserError('Переключение не удалось; прежняя сессия восстановлена не полностью. Открой Roblox и войди в нужный аккаунт вручную.'); }
    }
    throw error instanceof UserError ? error : new UserError('Браузер не смог завершить переключение. Прежний вход восстановлен; повтори попытку.');
  } finally { switching = false; }
}
