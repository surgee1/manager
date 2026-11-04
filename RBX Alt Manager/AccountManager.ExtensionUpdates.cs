using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private readonly SemaphoreSlim extensionUpdateGate = new SemaphoreSlim(1);
        private async Task<object> UpdateInstalledExtensionAsync(JObject payload)
        {
            string id = payload?.Value<string>("extensionId"), currentVersion = payload?.Value<string>("version");
            if (!Regex.IsMatch(id ?? "", "^[a-p]{32}$") || !Version.TryParse(currentVersion, out var current)) throw new InvalidOperationException("Неверные данные расширения.");
            if (!await extensionUpdateGate.WaitAsync(0)) throw new InvalidOperationException("Обновление расширения уже выполняется.");
            try
            {
                var release = await ManagerUpdates.CheckFreshAsync().ConfigureAwait(false);
                if (!Version.TryParse(release.ExtensionVersion, out var latest)) throw new InvalidOperationException("В релизе не указана версия расширения.");
                if (latest <= current) return new { ok = true, updated = false, version = currentVersion };
                string target = await Task.Run(() => ExtensionUpdates.FindInstalledDirectory(id)).ConfigureAwait(false);
                if (target == null) target = await ChooseExtensionFolderAsync(id).ConfigureAwait(false);
                string archive = await ManagerUpdates.DownloadExtensionAsync(release).ConfigureAwait(false);
                var files = await Task.Run(() => ExtensionUpdates.ReadPackage(archive, release.ExtensionVersion)).ConfigureAwait(false);
                await Task.Run(() => ExtensionUpdates.Apply(target, files)).ConfigureAwait(false);
                return new { ok = true, updated = true, version = release.ExtensionVersion };
            }
            catch (InvalidOperationException) { throw; }
            catch { throw new InvalidOperationException("Расширение не обновлено. Проверь соединение с GitHub и доступ к папке расширения. Прежние файлы сохранены."); }
            finally { extensionUpdateGate.Release(); }
        }
        private Task<string> ChooseExtensionFolderAsync(string id)
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (PasswordPanel.Visible) throw new InvalidOperationException("Разблокируй менеджер перед обновлением расширения.");
                    string key = "ExtensionFolder_" + id;
                    string saved = General.Get(key);
                    if (!string.IsNullOrEmpty(saved) && ExtensionUpdates.IsExtensionFolder(saved)) { completion.SetResult(saved); return; }
                    completion.SetException(new InvalidOperationException("В менеджере открыто окно выбора папки расширения. Выбери установленную папку, затем нажми «Обновить расширение» ещё раз."));
                    using var dialog = new FolderBrowserDialog { Description = "Один раз выбери установленную папку расширения Spectrum Manager (с manifest.json). Браузер показывает её в режиме разработчика.", SelectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Browser Extension") };
                    Activate();
                    if (dialog.ShowDialog(this) != DialogResult.OK) throw new InvalidOperationException("Обновление отменено: папка расширения не выбрана.");
                    if (!ExtensionUpdates.IsExtensionFolder(dialog.SelectedPath)) throw new InvalidOperationException("В выбранной папке нет расширения Spectrum Manager.");
                    General.Set(key, dialog.SelectedPath); IniSettings.Save("RAMSettings.ini");
                }
                catch (Exception error) { if (!completion.Task.IsCompleted) completion.SetException(error); }
            }));
            return completion.Task;
        }
    }
}
