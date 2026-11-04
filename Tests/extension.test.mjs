import test from 'node:test';
import assert from 'node:assert/strict';
import { importActiveAccount, managerRequest, checkUpdates, validReleaseUrl, MANAGER_URL } from '../Browser Extension/core.js';

const code = 'a'.repeat(64);
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

