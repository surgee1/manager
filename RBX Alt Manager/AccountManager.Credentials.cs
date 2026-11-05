using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private readonly CredentialLoginJobs credentialLogins = new CredentialLoginJobs();
        private CredentialImportForm credentialImportForm;
        private void ShowCredentialImport()
        {
            if (PasswordPanel.Visible || accountFileUnreadable) { UiMessageBox.Show(this, "Сначала разблокируй базу аккаунтов.", "Импорт аккаунтов"); return; }
            if (credentialImportForm?.Visible == true) { credentialImportForm.Activate(); return; }
            using (credentialImportForm = new CredentialImportForm(ImportCredentialAsync, General.Get<bool>("SavePasswords"))) credentialImportForm.ShowDialog(this);
            credentialImportForm = null;
        }
        private async Task<CredentialImportResult> ImportCredentialAsync(CredentialPair pair, bool chromium, bool savePassword, IProgress<string> progress, CancellationToken cancel)
        {
            if (chromium)
            {
                string cookie = await isolatedBrowser.SignInAsync(pair, progress, cancel);
                cancel.ThrowIfCancellationRequested();
                var result = JObject.FromObject(await ImportVerifiedBrowserSessionAsync(cookie, pair.Username, savePassword ? pair.Password : null, () => !cancel.IsCancellationRequested));
                return new CredentialImportResult { Username = result.Value<string>("username"), UserId = result.Value<long>("userId") };
            }
            if (browserBridge == null) throw new InvalidOperationException(BrowserBridgeStatus);
            string nonce = credentialLogins.Create(pair, savePassword);
            try
            {
                progress?.Report("Открываем основной браузер. Нужно расширение Spectrum 0.5.0+.");
                SystemBrowser.Open("http://127.0.0.1:7964/browser/login/" + nonce);
                return await credentialLogins.WaitAsync(nonce, cancel);
            }
            finally { credentialLogins.Remove(nonce); }
        }
        private async Task<object> HandleCredentialBrowserActionAsync(string action, JObject payload)
        {
            string nonce = payload?.Value<string>("nonce");
            await CredentialUiAsync(() => { if (PasswordPanel.Visible || accountFileUnreadable) throw new InvalidOperationException("Сначала разблокируй базу аккаунтов."); return true; });
            if (action == "/v1/login/consume") return credentialLogins.Consume(nonce);
            if (action == "/v1/login/state") return new { ok = true, active = credentialLogins.IsActive(nonce) };
            if (action == "/v1/login/fail") { credentialLogins.Cancel(nonce); return new { ok = true }; }
            if (action == "/v1/login/finish") { credentialLogins.Finish(nonce, payload.Value<bool>("restoreFailed")); return new { ok = true }; }
            if (action == "/v1/login/import")
            {
                var expected = credentialLogins.Inspect(nonce);
                var result = JObject.FromObject(await ImportVerifiedBrowserSessionAsync(payload.Value<string>("cookie"), expected.Username, expected.PasswordToSave, () => credentialLogins.IsActive(nonce)));
                credentialLogins.Imported(nonce, new CredentialImportResult { Username = result.Value<string>("username"), UserId = result.Value<long>("userId") });
                return new { ok = true, username = result.Value<string>("username"), userId = result.Value<long>("userId") };
            }
            throw new InvalidOperationException("Неизвестная команда входа.");
        }
        private Task<object> CredentialUiAsync(Func<object> action)
        {
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (IsDisposed || !IsHandleCreated) throw new InvalidOperationException("Открой менеджер.");
            BeginInvoke(new Action(() => { try { completion.SetResult(action()); } catch (Exception error) { completion.SetException(error); } }));
            return completion.Task;
        }
        private async void AddThroughChromium()
        {
            if (PasswordPanel.Visible || accountFileUnreadable) { UiMessageBox.Show(this, "Сначала разблокируй базу аккаунтов.", "Добавить аккаунт"); return; }
            using (var cancel = new CancellationTokenSource())
            {
                try { string cookie = await isolatedBrowser.SignInAsync(null, null, cancel.Token); await ImportBrowserSessionAsync(cookie); }
                catch (Exception error) { UiMessageBox.Show(this, error is InvalidOperationException ? error.Message : "Вход не завершён. Попробуй снова.", "Добавить аккаунт"); }
            }
        }
    }
}
