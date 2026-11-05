(() => {
  if (window.top !== window) return;
  if (location.origin === 'http://127.0.0.1:7964' && !location.search && !location.hash && /^\/browser\/(open|login)\/[a-f0-9]{64}$/.test(location.pathname)) {
    const login = location.pathname.startsWith('/browser/login/');
    const status = document.getElementById('surgee-status');
    if (status) status.textContent = login ? 'Вход Roblox открыт в отдельной вкладке. Заверши проверку, если она появится.' : 'Проверяем выбранный аккаунт и открываем Roblox…';
    chrome.runtime.sendMessage({ action: login ? 'login-handoff' : 'handoff' }).then(response => {
      if (status) status.textContent = response?.ok ? (login ? 'Аккаунт добавлен. Прежний вход браузера восстановлен.' : 'Аккаунт открыт.') : response?.error || 'Нет ответа от менеджера. Проверь подключение в расширении.';
      if (response?.ok && response.result?.restoreFailed && status) status.textContent = 'Аккаунт добавлен. Прежний вход нужно восстановить вручную на Roblox.';
    }).catch(() => { if (status) status.textContent = 'Расширение перезагружено или не подключено. Обнови его и повтори вход из менеджера.'; });
    return;
  }
  if (location.origin !== 'https://www.roblox.com' || !/^\/login\/?$/.test(location.pathname)) return;
  (async () => {
    for (let attempt = 0; attempt < 40; attempt++) {
      const username = document.querySelector('#login-username,input[name=username],input[autocomplete=username]');
      const password = document.querySelector('#login-password,input[type=password]');
      if (!username || !password) { await new Promise(resolve => setTimeout(resolve, 250)); continue; }
      let response;
      try { response = await chrome.runtime.sendMessage({ action: 'login-form-ready' }); } catch { await new Promise(resolve => setTimeout(resolve, 250)); continue; }
      if (!response?.ok || !response.credentials) { await new Promise(resolve => setTimeout(resolve, 250)); continue; }
      const data = response.credentials;
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
      for (const [element, value] of [[username, data.username], [password, data.password]]) {
        setter.call(element, value); element.dispatchEvent(new Event('input', { bubbles: true })); element.dispatchEvent(new Event('change', { bubbles: true }));
      }
      data.password = null;
      await new Promise(resolve => setTimeout(resolve, 150));
      const button = document.querySelector('#login-button');
      if (button && !button.disabled) button.click();
      return;
    }
  })();
})();
