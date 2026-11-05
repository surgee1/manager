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
                if (target == null) throw new InvalidOperationException("Не удалось однозначно определить установленную копию расширения. Перезапусти браузер и повтори обновление. Другая папка не будет изменена.");
                string archive = await ManagerUpdates.DownloadExtensionAsync(release).ConfigureAwait(false);
                var files = await Task.Run(() => ExtensionUpdates.ReadPackage(archive, release.ExtensionVersion)).ConfigureAwait(false);
                await Task.Run(() => ExtensionUpdates.Apply(target, files)).ConfigureAwait(false);
                return new { ok = true, updated = true, version = release.ExtensionVersion };
            }
            catch (InvalidOperationException) { throw; }
            catch { throw new InvalidOperationException("Расширение не обновлено. Проверь соединение с GitHub и доступ к папке расширения. Прежние файлы сохранены."); }
            finally { extensionUpdateGate.Release(); }
        }
    }
}
