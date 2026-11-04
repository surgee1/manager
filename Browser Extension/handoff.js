(() => {
  if (window.top !== window || location.origin !== 'http://127.0.0.1:7964' || location.search || location.hash || !/^\/browser\/open\/[a-f0-9]{64}$/.test(location.pathname)) return;
  const status = document.getElementById('surgee-status');
  if (status) status.textContent = 'Проверяем выбранный аккаунт и открываем Roblox…';
  chrome.runtime.sendMessage({ action: 'handoff' }).then(response => {
    if (!response?.ok && status) status.textContent = response?.error || 'Нет ответа от менеджера. Проверь подключение в расширении.';
  }).catch(() => { if (status) status.textContent = 'Расширение перезагружено или не подключено. Открой его, проверь код и снова нажми «Браузер».'; });
})();
