import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';
import { armPasswordLogin, capturePasswordLogin, finishPasswordLogins, clearPasswordLogins, passwordLoginSender } from '../Browser Extension/passwords.js';

const sender = { frameId: 0, tab: { id: 7 }, url: 'https://www.roblox.com/login' };
function fixture(options = {}) {
  const session = {}, calls = [], local = { pairingCode: 'a'.repeat(64), saveLoginPasswords: !options.disabled };
  let cookie = 'OLD_FIXTURE_SESSION', tab = { url: 'https://www.roblox.com/home' };
  const api = { storage: {
    local: { get: async () => ({ ...local }) },
    session: { get: async key => key == null ? { ...session } : { [key]: session[key] }, set: async values => Object.assign(session, values),
      remove: async keys => { for (const key of Array.isArray(keys) ? keys : [keys]) delete session[key]; } }
  }, cookies: { getAllCookieStores: async () => [{ id: 'profile', tabIds: [7, 8] }], get: async () => ({ value: cookie }) },
  tabs: { get: async () => tab } };
  const fetcher = async (url, init) => {
    calls.push({ url, init });
    if (url.endsWith('/status')) return { ok: true, json: async () => ({ ok: true, passwordImport: !options.oldManager, savePasswords: !options.managerOptOut }) };
    assert.equal(url, 'http://127.0.0.1:7964/v1/accounts/import-password');
    if (options.offline) throw Error('offline');
    if (options.wrongIdentity) return { ok: false, status: 409, json: async () => ({ error: 'Roblox открыл другой аккаунт.' }) };
    return { ok: true, json: async () => ({ ok: true, username: 'Fixture_User', passwordSaved: true }) };
  };
  return { api, fetcher, session, calls, local, newCookie: () => { cookie = 'NEW_FIXTURE_SESSION'; }, setTab: value => { tab = value; } };
}
async function submitted(f, source = sender) {
  await armPasswordLogin(f.api, source, f.fetcher, 1000);
  await capturePasswordLogin(f.api, source, { username: 'Fixture_User', password: 'PRIVATE_FIXTURE_PASSWORD' }, 2000);
}
test('password capture requires exact top-level Roblox login and a normal tab', () => {
  assert.equal(passwordLoginSender(sender), true);
  for (const bad of [{ ...sender, frameId: 1 }, { ...sender, tab: { id: 7, incognito: true } }, { ...sender, url: 'https://www.roblox.com.evil.test/login' },
    { ...sender, url: 'http://www.roblox.com/login' }, { ...sender, url: 'https://www.roblox.com/signup' }]) assert.equal(passwordLoginSender(bad), false);
});
test('disabled option never reads or stores a password; older managers and manager opt-out are rejected', async () => {
  const f = fixture({ disabled: true });
  assert.deepEqual(await armPasswordLogin(f.api, sender, f.fetcher), { enabled: false });
  await assert.rejects(capturePasswordLogin(f.api, sender, { username: 'Fixture_User', password: 'secret' }));
  assert.deepEqual(f.session, {}); assert.equal(f.calls.length, 0);
  for (const option of ['oldManager', 'managerOptOut']) {
    const f = fixture({ [option]: true });
    await assert.rejects(armPasswordLogin(f.api, sender, f.fetcher)); assert.deepEqual(f.session, {});
  }
});
test('submitted credentials wait for a new session and go only to paired loopback manager', async () => {
  const f = fixture(); await submitted(f);
  assert.equal(await finishPasswordLogins(f.api, f.fetcher, 3000), null);
  f.newCookie();
  const result = await finishPasswordLogins(f.api, f.fetcher, 4000);
  assert.equal(result.passwordSaved, true);
  const sent = JSON.parse(f.calls.at(-1).init.body);
  assert.deepEqual(sent, { cookie: 'NEW_FIXTURE_SESSION', username: 'Fixture_User', password: 'PRIVATE_FIXTURE_PASSWORD' });
  assert.equal(f.calls.at(-1).init.credentials, 'omit');
  assert.equal(JSON.stringify(result).includes('PASSWORD'), false);
  assert.equal(JSON.stringify(f.session).includes('PASSWORD'), false);
  assert.equal(await finishPasswordLogins(f.api, f.fetcher, 5000), null);
});
test('candidate survives worker memory loss in session storage and is purged after expiry or opt-out', async () => {
  for (const reason of ['expiry', 'disabled', 'closed', 'navigated']) {
    const f = fixture(); await submitted(f); f.newCookie();
    if (reason === 'disabled') f.local.saveLoginPasswords = false;
    if (reason === 'closed') f.api.tabs.get = async () => { throw Error('closed'); };
    if (reason === 'navigated') f.setTab({ url: 'https://example.test/' });
    assert.equal(await finishPasswordLogins(f.api, f.fetcher, reason === 'expiry' ? 700000 : 4000), null);
    assert.equal(JSON.stringify(f.session).includes('PRIVATE_FIXTURE_PASSWORD'), false);
  }
});
test('wrong identity discards password; offline import keeps a bounded retry without logging credentials', async () => {
  for (const option of ['wrongIdentity', 'offline']) {
    const f = fixture({ [option]: true }); await submitted(f); f.newCookie();
    await assert.rejects(finishPasswordLogins(f.api, f.fetcher, 4000));
    assert.equal(JSON.stringify(f.session).includes('PRIVATE_FIXTURE_PASSWORD'), option === 'offline');
    assert.equal(JSON.stringify(f.session.passwordLoginStatus).includes('PRIVATE'), false);
  }
});
test('a newer login in the same profile supersedes another tab and tab closing clears its secrets', async () => {
  const f = fixture(); await submitted(f);
  await submitted(f, { ...sender, tab: { id: 8 } });
  assert.equal(f.session['spectrumPasswordLogin:7'], undefined);
  await clearPasswordLogins(f.api, 8); assert.deepEqual(f.session, {});
});
test('content capture ignores synthetic events, unrelated buttons, frames and opted-out forms', async () => {
  const script = await readFile(new URL('../Browser Extension/capture.js', import.meta.url), 'utf8');
  const listeners = {}, messages = [], username = { value: 'Fixture_User' }, password = { value: 'PRIVATE_FIXTURE_PASSWORD' };
  const win = {}; win.top = win;
  const context = { window: win, location: { origin: 'https://www.roblox.com', pathname: '/login' },
    document: { querySelector: selector => selector === '#login-username' ? username : password, addEventListener: (type, listener) => { listeners[type] = listener; } },
    chrome: { runtime: { sendMessage: async message => { messages.push(message); return { ok: true, enabled: true }; }, onMessage: { addListener() {} } } } };
  vm.runInNewContext(script, context); await Promise.resolve();
  const event = { type: 'click', isTrusted: false, target: { closest: () => true } };
  listeners.click(event); assert.equal(messages.length, 1);
  listeners.click({ ...event, isTrusted: true, target: { closest: () => false } }); assert.equal(messages.length, 1);
  listeners.click({ ...event, isTrusted: true }); await Promise.resolve();
  assert.equal(messages[1].password, 'PRIVATE_FIXTURE_PASSWORD');
});
test('background handles real login messages and imports automatically on a fresh Roblox cookie', async () => {
  const f = fixture(); const id = 'a'.repeat(32); let listener, cookieChanged;
  f.api.cookies.onChanged = { addListener: value => { cookieChanged = value; } };
  f.api.tabs.onRemoved = { addListener() {} };
  f.api.runtime = { id, getURL: path => `chrome-extension://${id}/${path}`, onMessage: { addListener: value => { listener = value; } } };
  const oldChrome = globalThis.chrome, oldFetch = globalThis.fetch;
  globalThis.chrome = f.api; globalThis.fetch = f.fetcher;
  const source = { ...sender, id };
  const send = message => new Promise(resolve => { assert.equal(listener(message, source, resolve), true); });
  try {
    await import('../Browser Extension/background.js');
    assert.equal((await send({ action: 'password-capture-state' })).enabled, true);
    const response = await send({ action: 'password-capture-submit', username: 'Fixture_User', password: 'PRIVATE_FIXTURE_PASSWORD' });
    assert.equal(response.captured, true); assert.equal(JSON.stringify(response).includes('PASSWORD'), false);
    f.newCookie(); cookieChanged({ removed: false, cookie: { name: '.ROBLOSECURITY', domain: '.roblox.com' } });
    for (let attempt = 0; attempt < 100 && !f.session.passwordLoginStatus; attempt++) await new Promise(resolve => setTimeout(resolve, 5));
    assert.equal(f.session.passwordLoginStatus?.passwordSaved, true);
    assert.equal(JSON.stringify(f.session).includes('PRIVATE_FIXTURE_PASSWORD'), false);
    assert.equal(listener({ action: 'password-capture-submit', password: 'SECRET' }, { ...source, url: 'https://evil.test/login' }, () => assert.fail()), false);
  } finally { globalThis.chrome = oldChrome; globalThis.fetch = oldFetch; }
});
