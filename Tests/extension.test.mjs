import test from 'node:test';
import assert from 'node:assert/strict';
import { importActiveAccount, managerRequest, checkUpdates, validReleaseUrl, MANAGER_URL, handoffNonce, openAccount, listAccounts, updateExtension, loginNonce, loginWithCredentials, takeLoginCredentials, recoverCredentialLogin } from '../Browser Extension/core.js';

const code = 'a'.repeat(64);
const loginToken = 'b'.repeat(64);
function credentialFixture(options = {}) {
  const calls = [], sets = [], session = {};
  const old = { name: '.ROBLOSECURITY', value: 'OLD_FIXTURE_SESSION', domain: '.roblox.com', path: '/', secure: true, httpOnly: true, sameSite: 'no_restriction', storeId: 'profile', session: true };
  let current = old, delivered = false;
  const sender = { frameId: 0, tab: { id: 77 }, url: 'https://www.roblox.com/login' };
  const api = {
    storage: { local: { get: async () => ({ pairingCode: code }) }, session: {
      get: async () => ({ ...session }), set: async values => Object.assign(session, values), remove: async key => { delete session[key]; }
    } },
    tabs: { create: async details => { assert.equal(details.url, 'https://www.roblox.com/login'); return { id: 77 }; },
      get: async () => ({ url: sender.url }), update: async (id, details) => { assert.equal(id, 77); calls.push({ refreshed: details.url }); } },
    cookies: { getAllCookieStores: async () => [{ id: 'profile', tabIds: [41, 77] }],
      getAll: async () => current ? [current] : [], remove: async () => { current = null; return {}; },
      get: async () => ({ value: 'NEW_FIXTURE_SESSION' }),
      set: async details => { sets.push(details); current = { ...details }; return current; } }
  };
  const fetcher = async (url, init) => {
    calls.push({ url, init });
    let value = { ok: true };
    if (url.endsWith('/v1/login/consume')) value = { ok: true, username: 'Fixture_User', password: 'PRIVATE_FIXTURE_PASSWORD' };
    if (url.endsWith('/v1/login/state')) {
      assert.equal(takeLoginCredentials({ ...sender, tab: { id: 78 } }), null);
      assert.equal(takeLoginCredentials({ ...sender, frameId: 1 }), null);
      assert.equal(takeLoginCredentials({ ...sender, url: 'https://evil.test/login' }), null);
      if (!delivered) { const data = takeLoginCredentials(sender); assert.equal(data.password, 'PRIVATE_FIXTURE_PASSWORD'); delivered = true; }
      assert.equal(takeLoginCredentials(sender), null);
      value = { ok: true, active: !options.cancelled };
    }
    if (url.endsWith('/users/authenticated')) {
      assert.equal(init.credentials, 'include');
      return { ok: !options.timeout, status: options.timeout ? 401 : 200, json: async () => ({ id: 42, name: options.wrong ? 'Wrong_User' : 'Fixture_User' }) };
    }
    if (url.endsWith('/v1/login/import')) {
      assert.deepEqual(JSON.parse(init.body), { nonce: loginToken, cookie: 'NEW_FIXTURE_SESSION' });
      value = { ok: true, username: 'Fixture_User', userId: '42', cookie: 'SHOULD_NOT_RETURN' };
    }
    return { ok: true, json: async () => value };
  };
  return { api, fetcher, calls, sets, session, current: () => current };
}
test('login handoff accepts only exact loopback URL', () => {
  assert.equal(loginNonce(MANAGER_URL + '/browser/login/' + loginToken), loginToken);
  for (const url of ['https://evil.test/browser/login/' + loginToken, MANAGER_URL + '/browser/login/' + loginToken + '?x=1', MANAGER_URL + '/browser/login/' + loginToken + '#x', MANAGER_URL + '/browser/open/' + loginToken]) assert.equal(loginNonce(url), null);
});
test('credential login verifies identity, passes password once to exact login tab and restores previous session', async () => {
  const f = credentialFixture();
  const result = await loginWithCredentials(f.api, loginToken, { id: 41 }, f.fetcher, async () => {});
  assert.deepEqual(result, { username: 'Fixture_User', userId: '42', restoreFailed: false });
  assert.equal(f.current().value, 'OLD_FIXTURE_SESSION'); assert.equal(f.current().httpOnly, true);
  assert.deepEqual(f.session, {}); assert.equal(takeLoginCredentials({}), null);
  assert.equal(f.calls.some(call => call.init?.body?.includes('PRIVATE_FIXTURE_PASSWORD')), false);
  assert.ok(f.calls.some(call => call.url?.endsWith('/v1/login/finish')));
});
for (const option of ['wrong', 'cancelled', 'timeout']) test('credential login restores previous session on ' + option, async () => {
  const f = credentialFixture({ [option]: true });
  await assert.rejects(loginWithCredentials(f.api, loginToken, { id: 41 }, f.fetcher, async () => {}));
  assert.equal(f.current().value, 'OLD_FIXTURE_SESSION'); assert.deepEqual(f.session, {});
  assert.equal(f.calls.some(call => call.url?.endsWith('/v1/login/import')), false);
});
test('interrupted credential login restores session after worker restart', async () => {
  const f = credentialFixture();
  await f.api.storage.session.set({ spectrumCredentialLoginRestore: { storeId: 'profile', cookies: [{ ...f.current() }] } });
  await f.api.cookies.remove(); await recoverCredentialLogin(f.api);
  assert.equal(f.current().value, 'OLD_FIXTURE_SESSION'); assert.deepEqual(f.session, {});
});
function fixture(options = {}) {
  const calls = [];
  let reads = 0;
  const api = {
    storage: { local: { get: async () => ({ pairingCode: code }) } },
    tabs: { query: async () => [{ id: 41, url: options.url || 'https://www.roblox.com/home' }] },
    cookies: {
      getAllCookieStores: async () => [{ id: 'wrong-profile', tabIds: [99] }, { id: 'active-profile', tabIds: [41] }],
      get: async details => {
        reads++;
        assert.equal(details.storeId, 'active-profile');
        assert.equal(details.name, '.ROBLOSECURITY');
        assert.equal(details.url, 'https://www.roblox.com/');
        return options.noCookie ? null : { value: 'SYNTHETIC_TEST_SESSION' };
      }
    }
  };
  const fetcher = async (url, init) => {
    calls.push({ url, init });
    assert.ok(url.startsWith(MANAGER_URL + '/'));
    assert.equal(init.credentials, 'omit');
    assert.equal(init.redirect, 'error');
    if (options.offline) throw Error('offline');
    if (options.unauthorized) return { ok: false, json: async () => ({ error: 'Неверный код подключения.' }) };
    return { ok: true, json: async () => url.endsWith('/status') ? { ok: true } : { ok: true, username: 'VerifiedName', displayName: 'Display', updated: false } };
  };
  return { api, calls, fetcher, reads: () => reads };
}
test('only active Roblox session is sent to loopback and verified nickname is returned', async () => {
  const f = fixture();
  const result = await importActiveAccount(f.api, f.fetcher);
  assert.equal(result.username, 'VerifiedName');
  assert.equal(f.calls.length, 2);
  assert.equal(f.calls[1].url, 'http://127.0.0.1:7964/v1/accounts/import');
  assert.deepEqual(JSON.parse(f.calls[1].init.body), { cookie: 'SYNTHETIC_TEST_SESSION' });
  assert.equal(f.reads(), 1);
});
test('rejects another active website before reading cookies', async () => {
  const f = fixture({ url: 'https://example.com/' });
  await assert.rejects(importActiveAccount(f.api, f.fetcher), /Roblox/);
  assert.equal(f.reads(), 0);
});
test('rejects hostname lookalikes', async () => {
  const f = fixture({ url: 'https://www.roblox.com.example.org/' });
  await assert.rejects(importActiveAccount(f.api, f.fetcher), /Roblox/);
  assert.equal(f.reads(), 0);
});
test('does not extract cookies when manager is offline or pairing is incorrect', async () => {
  for (const options of [{ offline: true }, { unauthorized: true }]) {
    const f = fixture(options);
    await assert.rejects(importActiveAccount(f.api, f.fetcher));
    assert.equal(f.reads(), 0);
  }
});
test('reports a missing Roblox session without attempting import', async () => {
  const f = fixture({ noCookie: true });
  await assert.rejects(importActiveAccount(f.api, f.fetcher), /Сессия/);
  assert.equal(f.calls.length, 1);
});
test('requires valid pairing and rejects arbitrary endpoints', async () => {
  await assert.rejects(managerRequest('/v1/status', 'bad'), /код/);
  await assert.rejects(managerRequest('https://example.com/', code), /Неизвестная/);
});

test('checking updates uses paired manager without reading any account cookie', async () => {
  const f = fixture();
  await checkUpdates(f.api, f.fetcher);
  assert.equal(f.reads(), 0);
  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0].url, MANAGER_URL + '/v1/updates');
  assert.equal(f.calls[0].init.method, 'GET');
});
test('extension download links stay within the configured repository', () => {
  assert.equal(validReleaseUrl('https://github.com/surgee1/manager/releases/download/v6.0.0/Surgee-Browser-Extension.zip'), true);
  for (const url of ['https://github.com.example.org/surgee1/manager/releases/download/v6/a.zip', 'https://github.com/other/repo/releases/download/v6/a.zip', 'javascript:alert(1)', 'https://user:pass@github.com/surgee1/manager/releases/download/v6/a.zip'])
    assert.equal(validReleaseUrl(url), false);
});

function switchFixture(options = {}) {
  const calls = [], changes = [], opened = [];
  const old = { name: '.ROBLOSECURITY', value: 'SYNTHETIC_OLD_SESSION', domain: '.roblox.com', path: '/', storeId: 'active-profile', secure: true, httpOnly: true, sameSite: 'no_restriction', session: false, expirationDate: 1999999999, hostOnly: false };
  const original = [old, ...(options.shadow ? [{ ...old, domain: 'www.roblox.com', hostOnly: true, value: 'SYNTHETIC_HOST_SHADOW' }] : [])];
  let cookies = structuredClone(original);
  const api = {
    storage: { local: { get: async () => ({ pairingCode: code }) } },
    tabs: {
      query: async () => [{ id: 41, url: 'https://example.com/', incognito: !!options.incognito }],
      create: async details => { opened.push({ create: details }); },
      update: async (id, details) => { opened.push({ update: details, id }); }
    },
    cookies: {
      getAllCookieStores: async () => [{ id: 'other-profile', tabIds: [99] }, { id: 'active-profile', tabIds: [41] }],
      getAll: async details => { assert.equal(details.storeId, 'active-profile'); assert.equal(details.name, '.ROBLOSECURITY'); return structuredClone(cookies); },
      remove: async details => {
        assert.equal(details.storeId, 'active-profile'); changes.push('remove');
        const host = new URL(details.url).hostname;
        const index = cookies.findIndex(cookie => cookie.hostOnly ? cookie.domain === host : host === cookie.domain.slice(1) || host.endsWith(cookie.domain));
        if (index < 0) return null;
        cookies.splice(index, 1); return { url: details.url };
      },
      set: async details => {
        assert.equal(details.storeId, 'active-profile'); changes.push('set');
        if (options.setFailure && details.value === 'SYNTHETIC_SELECTED_SESSION') throw new Error('set failed');
        const cookie = { ...details, domain: details.domain || new URL(details.url).hostname, hostOnly: !details.domain, session: details.expirationDate === undefined };
        delete cookie.url; cookies.push(cookie); return cookie;
      }
    }
  };
  const fetcher = async (url, init) => {
    calls.push({ url, init });
    if (url.startsWith(MANAGER_URL)) {
      assert.equal(init.credentials, 'omit');
      if (options.unpaired) return { ok: false, json: async () => ({ error: 'Неверный код подключения.' }) };
      if (url.endsWith('/list')) return { ok: true, json: async () => ({ ok: true, accounts: [{ id: '42', username: 'ChosenAccount', alias: 'Основной', folder: 'Папка', cookie: 'SHOULD_NOT_PASS_THROUGH' }] }) };
      return { ok: true, json: async () => ({ ok: true, userId: options.badManagerId ? '99' : '42', username: 'ChosenAccount', cookie: 'SYNTHETIC_SELECTED_SESSION' }) };
    }
    assert.equal(url, 'https://users.roblox.com/v1/users/authenticated');
    assert.equal(init.credentials, 'include'); assert.equal(init.redirect, 'error');
    if (options.networkFailure) throw new Error('offline');
    return { ok: !options.expired, status: options.expired ? 401 : 200, json: async () => ({ id: options.wrongIdentity ? 99 : 42, name: 'ChosenAccount' }) };
  };
  return { api, fetcher, calls, changes, opened, original, cookies: () => cookies };
}
test('handoff accepts only exact loopback origin and one-use nonce-shaped path', () => {
  const nonce = 'b'.repeat(64);
  assert.equal(handoffNonce(MANAGER_URL + '/browser/open/' + nonce), nonce);
  for (const url of ['https://example.com/browser/open/' + nonce, 'http://127.0.0.1:7965/browser/open/' + nonce,
    'http://127.0.0.1.example.org:7964/browser/open/' + nonce, MANAGER_URL + '/browser/open/' + nonce + '?x=1',
    MANAGER_URL + '/browser/open/' + nonce + '#x', MANAGER_URL + '/browser/open/bad']) assert.equal(handoffNonce(url), null);
});
test('account list exposes only metadata and does not read browser cookies', async () => {
  const f = switchFixture();
  const result = await listAccounts(f.api, f.fetcher);
  assert.deepEqual(result.accounts[0], { id: '42', username: 'ChosenAccount', alias: 'Основной', folder: 'Папка' });
  assert.equal(JSON.stringify(result).includes('SHOULD_NOT_PASS_THROUGH'), false);
  assert.equal(f.changes.length, 0);
});
test('selecting an account installs an HttpOnly Secure cookie in the active store and verifies its ID', async () => {
  const f = switchFixture({ shadow: true });
  const result = await openAccount(f.api, { id: '42' }, undefined, f.fetcher);
  assert.deepEqual(result, { userId: '42', username: 'ChosenAccount' });
  assert.equal(JSON.stringify(result).includes('SESSION'), false);
  assert.equal(f.cookies().length, 1);
  const cookie = f.cookies()[0];
  assert.equal(cookie.value, 'SYNTHETIC_SELECTED_SESSION'); assert.equal(cookie.httpOnly, true); assert.equal(cookie.secure, true);
  assert.ok(cookie.expirationDate > Date.now() / 1000 && cookie.expirationDate < Date.now() / 1000 + 31 * 86400);
  assert.deepEqual(f.opened, [{ create: { url: 'https://www.roblox.com/home', active: true } }]);
});
test('manager handoff uses nonce and replaces only the handoff tab after verification', async () => {
  const f = switchFixture(); const nonce = 'c'.repeat(64);
  await openAccount(f.api, { nonce }, { id: 41, url: MANAGER_URL + '/browser/open/' + nonce }, f.fetcher);
  assert.equal(f.calls[0].url, MANAGER_URL + '/v1/browser/consume');
  assert.deepEqual(JSON.parse(f.calls[0].init.body), { nonce });
  assert.deepEqual(f.opened, [{ id: 41, update: { url: 'https://www.roblox.com/home' } }]);
});
for (const [flag, message] of [['expired', /завершилась/], ['wrongIdentity', /другой аккаунт/], ['networkFailure', /соединение/], ['setFailure', /Браузер/]]) {
  test('failed switch rolls back previous root and host-only sessions: ' + flag, async () => {
    const f = switchFixture({ [flag]: true, shadow: true });
    await assert.rejects(openAccount(f.api, { id: '42' }, undefined, f.fetcher), message);
    assert.deepEqual(f.cookies().map(c => [c.domain, c.value, c.path, c.httpOnly, c.secure, c.expirationDate]), f.original.map(c => [c.domain, c.value, c.path, c.httpOnly, c.secure, c.expirationDate]));
    assert.equal(f.opened.length, 0);
  });
}
test('unpaired, mismatched manager response and incognito cannot modify browser sessions', async () => {
  for (const options of [{ unpaired: true }, { badManagerId: true }, { incognito: true }]) {
    const f = switchFixture(options);
    await assert.rejects(openAccount(f.api, { id: '42' }, undefined, f.fetcher));
    assert.equal(f.changes.length, 0);
  }
});
test('invalid account selection is rejected before contacting manager', async () => {
  for (const selection of [{ id: '../42' }, { id: '0' }, { nonce: 'bad' }, {}]) {
    const f = switchFixture();
    await assert.rejects(openAccount(f.api, selection, undefined, f.fetcher));
    assert.equal(f.calls.length, 0);
  }
});
test('extension updates request desktop installation with runtime identity and never read browser cookies', async () => {
  const f = fixture();
  f.api.runtime = { id: 'a'.repeat(32), getManifest: () => ({ version: '0.4.0' }) };
  await updateExtension(f.api, f.fetcher);
  assert.equal(f.calls.length, 1); assert.equal(f.reads(), 0);
  assert.equal(f.calls[0].url, MANAGER_URL + '/v1/extension/update');
  assert.deepEqual(JSON.parse(f.calls[0].init.body), { extensionId: 'a'.repeat(32), version: '0.4.0' });
});
test('background accepts switching only from its own popup or exact top-level loopback handoff', async () => {
  const f = switchFixture(); let listener;
  const extensionId = 'a'.repeat(32);
  f.api.cookies.onChanged = { addListener() {} };
  f.api.tabs.onRemoved = { addListener() {} };
  f.api.runtime = { id: extensionId, getURL: path => `chrome-extension://${extensionId}/${path}`, getManifest: () => ({ version: '0.4.0' }), onMessage: { addListener: value => { listener = value; } } };
  const originalChrome = globalThis.chrome, originalFetch = globalThis.fetch;
  globalThis.chrome = f.api; globalThis.fetch = f.fetcher;
  try {
    await import('../Browser Extension/background.js');
    const nonce = 'b'.repeat(64);
    const good = { id: extensionId, url: MANAGER_URL + '/browser/open/' + nonce, frameId: 0, tab: { id: 41 } };
    for (const sender of [{ ...good, id: 'b'.repeat(32) }, { ...good, frameId: 1 }, { ...good, tab: undefined }, { ...good, url: 'https://example.com/' }, { ...good, url: 'http://127.0.0.1:7965/browser/open/' + nonce }])
      assert.equal(listener({ action: 'handoff' }, sender, () => assert.fail('Rejected sender received session action')), false);
    assert.equal(f.calls.length, 0);
    const response = await new Promise(resolve => { assert.equal(listener({ action: 'handoff', nonce: 'ignored-payload' }, good, resolve), true); });
    assert.equal(response.ok, true); assert.equal(response.result.userId, '42'); assert.equal(JSON.stringify(response).includes('SESSION'), false);
    assert.deepEqual(JSON.parse(f.calls[0].init.body), { nonce });
    let release;
    const gate = new Promise(resolve => { release = resolve; });
    globalThis.fetch = async (url, init) => { if (url.endsWith('/v1/accounts/open')) await gate; return f.fetcher(url, init); };
    const popup = { id: extensionId, url: f.api.runtime.getURL('popup.html') };
    const opening = new Promise(resolve => listener({ action: 'open', id: '42' }, popup, resolve));
    await new Promise(resolve => setTimeout(resolve, 0));
    let blocked;
    assert.equal(listener({ action: 'install-update' }, popup, response => { blocked = response; }), false);
    assert.equal(blocked.ok, false); assert.match(blocked.error, /Дождись/);
    release(); assert.equal((await opening).ok, true);
    assert.equal(f.calls.some(call => call.url.endsWith('/v1/extension/update')), false);
  } finally { globalThis.chrome = originalChrome; globalThis.fetch = originalFetch; }
});

