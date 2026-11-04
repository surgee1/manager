export const MANAGER_URL = 'http://127.0.0.1:7964';

export async function managerRequest(path, token, body, fetcher = fetch) {
  if (!/^[a-f0-9]{64}$/i.test(token || '')) throw new Error('Вставь код из Настройки → Расширение в менеджере.');
  if (!['/v1/status', '/v1/accounts/import', '/v1/updates'].includes(path)) throw new Error('Неизвестная команда.');
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 20000);
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
      throw new Error('Менеджер недоступен. Открой обычную версию менеджера и проверь Настройки → Расширение.');
    }
    let result;
    try { result = await response.json(); } catch { throw new Error('Менеджер вернул непонятный ответ. Обнови сборку.'); }
    if (!response.ok || !result.ok) throw new Error(result.error || 'Не удалось выполнить действие.');
    return result;
  } finally { clearTimeout(timer); }
}

export async function checkUpdates(api, fetcher = fetch) {
  const { pairingCode } = await api.storage.local.get('pairingCode');
  return managerRequest('/v1/updates', pairingCode, undefined, fetcher);
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
  try { url = new URL(tab?.url || ''); } catch { throw new Error('Открой вкладку Roblox со своим аккаунтом.'); }
  if (url.protocol !== 'https:' || !['www.roblox.com', 'roblox.com'].includes(url.hostname))
    throw new Error('Открой активную вкладку Roblox со своим аккаунтом.');
  const stores = await api.cookies.getAllCookieStores();
  const store = stores.find(value => value.tabIds.includes(tab.id));
  if (!store) throw new Error('Не найден профиль этой вкладки. Открой обычное окно браузера.');
  const cookie = await api.cookies.get({ url: 'https://www.roblox.com/', name: '.ROBLOSECURITY', storeId: store.id });
  if (!cookie?.value) throw new Error('Сессия Roblox не найдена. Сначала войди в аккаунт в этой вкладке.');
  return managerRequest('/v1/accounts/import', pairingCode, { cookie: cookie.value }, fetcher);
}
