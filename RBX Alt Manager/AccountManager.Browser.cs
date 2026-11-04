using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private BrowserBridge browserBridge;
        public string BrowserBridgeStatus { get; private set; } = "Подключение ещё не запущено";
        public string BrowserPairingCode
        {
            get
            {
                string key = General.Get("BrowserPairingCode");
                if (string.IsNullOrWhiteSpace(key))
                {
                    var bytes = new byte[32];
                    using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                    key = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                    General.Set("BrowserPairingCode", key);
                    IniSettings.Save("RAMSettings.ini");
                }
                return key;
            }
        }
        private void StartBrowserBridge()
        {
            try
            {
                browserBridge = new BrowserBridge(7964, BrowserPairingCode, ImportBrowserSessionAsync, CheckBrowserUpdatesAsync);
                browserBridge.Start();
                BrowserBridgeStatus = "Готово • 127.0.0.1:7964";
                FormClosed += (sender, e) => browserBridge.Dispose();
            }
            catch { BrowserBridgeStatus = "Порт 7964 занят. Закрой другую копию менеджера и перезапусти эту."; }
        }
        private async Task<object> CheckBrowserUpdatesAsync()
        {
            var release = await ManagerUpdates.CheckAsync();
            return new { ok = true, managerVersion = ManagerUpdates.Version, latestManagerVersion = release.Version,
                latestExtensionVersion = release.ExtensionVersion, extensionDownloadUrl = release.ExtensionUrl,
                managerDownloadUrl = release.ManagerUrl, releasesUrl = ManagerUpdates.Repository + "/releases" };
        }
        private async Task<object> ImportBrowserSessionAsync(string cookie)
        {
            if (IsDisposed || !IsHandleCreated) throw new InvalidOperationException("Открой окно менеджера.");
            using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            request.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
            using var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Сессия Roblox завершилась. Войди в аккаунт в браузере и повтори.");
            if ((int)response.StatusCode == 429) throw new InvalidOperationException("Roblox ограничил частоту запросов. Повтори чуть позже.");
            response.EnsureSuccessStatusCode();
            var user = JObject.Parse(await response.Content.ReadAsStringAsync());
            long id = user.Value<long>("id");
            string name = user.Value<string>("name");
            if (id <= 0 || string.IsNullOrEmpty(name)) throw new InvalidOperationException("Roblox не вернул данные аккаунта.");
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (PasswordPanel.Visible) throw new InvalidOperationException("Сначала разблокируй базу в менеджере.");
                    completion.SetResult(StoreBrowserSession(cookie, id, name, user.Value<string>("displayName")));
                }
                catch (Exception e) { completion.SetException(e); }
            }));
            return await completion.Task;
        }

        private object StoreBrowserSession(string cookie, long id, string name, string displayName)
        {
            var account = AccountsList.FirstOrDefault(item => item.UserID == id);
            bool updated = account != null;
            if (account == null)
            {
                account = new Account { UserID = id, Group = FolderForNewAccount };
                AccountsList.Add(account);
            }
            account.SecurityToken = cookie;
            account.Username = name;
            account.Valid = true;
            account.LastUse = DateTime.Now;
            account.CSRFToken = null;
            account.TokenSet = DateTime.MinValue;
            LastValidAccount = account;
            SaveAccounts(true);
            RefreshView(account);
            return new { ok = true, username = name, displayName, userId = id, updated };
        }
    }
}
