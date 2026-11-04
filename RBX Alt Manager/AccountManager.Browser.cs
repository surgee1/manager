using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private readonly IsolatedAccountBrowser isolatedBrowser = new IsolatedAccountBrowser();
        private bool openingBrowserWindows;
        private async void OpenSelectedInChromium()
        {
            if (openingBrowserWindows) return;
            var accounts = AccountsView.SelectedObjects.Cast<Account>().Select(account => new { account.UserID, account.Username, Cookie = account.SecurityToken }).ToArray();
            if (accounts.Length == 0) { MessageBox.Show(this, "Выбери аккаунты для открытия.", "Браузер"); return; }
            openingBrowserWindows = true; string caption = OpenBrowser.Text;
            var failures = new System.Collections.Generic.List<string>();
            try
            {
                for (int index = 0; index < accounts.Length; index++)
                {
                    if (IsDisposed) break;
                    OpenBrowser.Text = "Открытие " + (index + 1) + "/" + accounts.Length;
                    try { await isolatedBrowser.OpenAsync(accounts[index].UserID, accounts[index].Cookie); }
                    catch (Exception error) { failures.Add(accounts[index].Username + ": " + (error is InvalidOperationException ? error.Message : "Не удалось открыть отдельное окно Chromium.")); }
                }
            }
            finally { openingBrowserWindows = false; if (!IsDisposed) OpenBrowser.Text = caption; }
            if (failures.Count > 0 && !IsDisposed) MessageBox.Show(this, string.Join("\n\n", failures.Take(8)) + (failures.Count > 8 ? "\n… ещё " + (failures.Count - 8) : ""), "Открытие аккаунтов", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private BrowserBridge browserBridge;
        private readonly BrowserIntents browserIntents = new BrowserIntents();
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
            FormClosed += (sender, e) => isolatedBrowser.Disconnect();
            try
            {
                browserBridge = new BrowserBridge(7964, BrowserPairingCode, ImportBrowserSessionAsync, CheckBrowserUpdatesAsync, HandleBrowserActionAsync);
                browserBridge.Start();
                BrowserBridgeStatus = "Готово • 127.0.0.1:7964";
                FormClosed += (sender, e) => browserBridge.Dispose();
            }
            catch { BrowserBridgeStatus = "Порт 7964 занят. Закрой другую копию менеджера и перезапусти эту."; }
        }
        private Task<object> HandleBrowserActionAsync(string action, JObject payload)
        {
            if (action == "/v1/extension/update") return UpdateInstalledExtensionAsync(payload);
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (IsDisposed || !IsHandleCreated) throw new InvalidOperationException("Открой менеджер.");
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (PasswordPanel.Visible) throw new InvalidOperationException("Сначала разблокируй базу в менеджере.");
                    if (accountFileUnreadable) throw new InvalidOperationException("Сначала восстанови файл аккаунтов.");
                    if (action == "/v1/accounts/list")
                    {
                        completion.SetResult(new { ok = true, accounts = AccountsList.Where(account => account.UserID > 0)
                            .Select(account => new { id = account.UserID.ToString(), username = account.Username, alias = account.Alias, folder = FolderName(account.Group) }).ToArray() });
                        return;
                    }
                    long id;
                    if (action == "/v1/browser/consume") id = browserIntents.Consume(payload?.Value<string>("nonce"));
                    else if (!long.TryParse(payload?.Value<string>("id"), out id) || id <= 0) throw new InvalidOperationException("Выбери аккаунт из списка менеджера.");
                    var selected = AccountsList.FirstOrDefault(account => account.UserID == id);
                    if (selected == null) throw new InvalidOperationException("Аккаунт уже удалён из менеджера. Обнови список.");
                    string cookie = selected.SecurityToken;
                    if (string.IsNullOrWhiteSpace(cookie) || cookie.Length > 6000 || cookie.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
                        throw new InvalidOperationException("У аккаунта нет пригодной сессии. Добавь его через расширение после входа Roblox.");
                    completion.SetResult(new { ok = true, userId = id.ToString(), username = selected.Username, cookie });
                }
                catch (Exception error) { completion.SetException(error); }
            }));
            return completion.Task;
        }
        private void AccountsView_ItemActivate(object sender, EventArgs e)
        {
            if (!(AccountsView.FocusedObject is Account account)) return;
            AccountsView.SelectedObject = account;
            OpenSelectedAccountBrowser();
        }

        private void OpenSelectedAccountBrowser()
        {
            var selection = AccountsView.SelectedObjects.Cast<Account>().ToList();
            if (selection.Count > 1) { OpenSelectedInChromium(); return; }
            if (selection.Count == 0) { MessageBox.Show(this, "Выбери аккаунт, который нужно открыть в браузере.", "Браузер"); return; }
            if (browserBridge == null) { MessageBox.Show(this, BrowserBridgeStatus, "Подключение расширения"); return; }
            try
            {
                string nonce = browserIntents.Create(selection[0].UserID);
                SystemBrowser.Open("http://127.0.0.1:7964/browser/open/" + nonce);
            }
            catch (InvalidOperationException error) { MessageBox.Show(this, error.Message, "Браузер"); }
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
