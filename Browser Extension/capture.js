(() => {
  if (window.top !== window || location.origin !== 'https://www.roblox.com' || !/^\/login\/?$/.test(location.pathname)) return;
  let enabled = false, pending = false;
  chrome.runtime.sendMessage({ action: 'password-capture-state' }).then(result => { enabled = result?.ok && result.enabled; }).catch(() => {});
  function capture(event) {
    if (!enabled || !event.isTrusted || pending) return;
    const username = document.querySelector('#login-username');
    const password = document.querySelector('#login-password');
    if (!username || !password) return;
    if (event.type === 'click' && !event.target.closest?.('#login-button')) return;
    if (event.type === 'submit' && !event.target.contains(password)) return;
    if (event.type === 'keydown' && (event.key !== 'Enter' || ![username, password].includes(event.target))) return;
    const name = username.value.trim(), secret = password.value;
    if (!/^[A-Za-z0-9_]{3,20}$/.test(name) || !secret || secret.length > 1024) return;
    pending = true;
    // Only the submitted Roblox login is handed to the extension worker; never write it into page storage.
    chrome.runtime.sendMessage({ action: 'password-capture-submit', username: name, password: secret })
      .catch(() => {}).finally(() => { pending = false; });
  }
  document.addEventListener('click', capture, true);
  document.addEventListener('submit', capture, true);
  document.addEventListener('keydown', capture, true);
  chrome.runtime.onMessage.addListener(message => {
    if (message?.action === 'password-capture-setting') {
      enabled = false;
      if (message.enabled === true) chrome.runtime.sendMessage({ action: 'password-capture-state' }).then(result => { enabled = result?.ok && result.enabled; }).catch(() => {});
    }
  });
})();
