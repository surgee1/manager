using Newtonsoft.Json.Linq;
using SurgeeUpdating;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class ReleaseInfo
    {
        public string Version, ManagerUrl, ExtensionUrl, ManagerHash, ExtensionVersion, ExtensionHash;
    }
    internal static class ManagerUpdates
    {
        internal const string Version = "6.2.7";
        internal const string ExtensionVersion = "0.4.0";
        internal const string Repository = "https://github.com/surgee1/manager";
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1);
        private static ReleaseInfo cached;
        private static DateTime checkedAt;

        internal static Task<ReleaseInfo> CheckAsync() => CheckCoreAsync(false);
        internal static Task<ReleaseInfo> CheckFreshAsync() => CheckCoreAsync(true);
        private static async Task<ReleaseInfo> CheckCoreAsync(bool fresh)
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!fresh && cached != null && DateTime.UtcNow - checkedAt < TimeSpan.FromMinutes(1)) return cached;
                using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Spectrum-Manager/" + Version);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                using var response = await client.GetAsync("https://api.github.com/repos/surgee1/manager/releases/latest").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("В surgee1/manager ещё нет опубликованных релизов.");
                if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429) throw new InvalidOperationException("GitHub временно ограничил запросы. Повтори позже.");
                response.EnsureSuccessStatusCode();
                var release = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                cached = ParseRelease(release);
                checkedAt = DateTime.UtcNow;
                return cached;
            }
            finally { Gate.Release(); }
        }

        internal static ReleaseInfo ParseRelease(JObject release)
        {
            string version = ((string)release["tag_name"] ?? "").TrimStart('v');
            System.Version parsed;
            if (!System.Version.TryParse(version, out parsed) || (bool?)release["draft"] == true || (bool?)release["prerelease"] == true)
                throw new InvalidDataException("Неверная версия опубликованного релиза.");
            var result = new ReleaseInfo { Version = version };
            var extension = System.Text.RegularExpressions.Regex.Match((string)release["body"] ?? "", @"surgee-extension:\s*(\d+\.\d+\.\d+)");
            if (extension.Success) result.ExtensionVersion = extension.Groups[1].Value;
            foreach (var asset in release["assets"] ?? new JArray())
            {
                string name = (string)asset["name"] ?? "", url = (string)asset["browser_download_url"] ?? "";
                if (!IsAssetUrl(url)) continue;
                if ((name.StartsWith("Spectrum-Manager-", StringComparison.Ordinal) || (result.ManagerUrl == null && name.StartsWith("Surgee-Manager-", StringComparison.Ordinal))) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                { result.ManagerUrl = url; result.ManagerHash = ((string)asset["digest"] ?? "").Replace("sha256:", ""); }
                if (name == "Spectrum-Browser-Extension.zip" || (result.ExtensionUrl == null && name == "Surgee-Browser-Extension.zip")) { result.ExtensionUrl = url; result.ExtensionHash = ((string)asset["digest"] ?? "").Replace("sha256:", ""); }
            }
            if (result.ManagerUrl == null) throw new InvalidDataException("В релизе отсутствует ZIP менеджера.");
            return result;
        }

        internal static bool IsAssetUrl(string value)
        {
            Uri url;
            return Uri.TryCreate(value, UriKind.Absolute, out url) && url.Scheme == "https" && url.Host == "github.com" &&
                url.AbsolutePath.StartsWith("/surgee1/manager/releases/download/", StringComparison.Ordinal) && string.IsNullOrEmpty(url.UserInfo);
        }

        internal static async Task<string> DownloadAsync(ReleaseInfo release)
        {
            if (!IsAssetUrl(release.ManagerUrl)) throw new InvalidDataException("Неверный адрес обновления.");
            string folder = NewStage();
            string path = Path.Combine(folder, "release.zip");
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await client.GetAsync(release.ManagerUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            const long limit = 300L * 1024 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Архив слишком большой.");
            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = File.Create(path))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                { total += count; if (total > limit) throw new InvalidDataException("Архив слишком большой."); await output.WriteAsync(buffer, 0, count).ConfigureAwait(false); }
            }
            if (!string.IsNullOrEmpty(release.ManagerHash) && !string.Equals(FileUpdateTransaction.Hash(path), release.ManagerHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Контрольная сумма архива не совпала. Установка отменена.");
            return path;
        }
        internal static async Task<string> DownloadExtensionAsync(ReleaseInfo release)
        {
            if (!IsAssetUrl(release.ExtensionUrl) || !System.Text.RegularExpressions.Regex.IsMatch(release.ExtensionHash ?? "", "^[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("GitHub не вернул проверяемый ZIP расширения.");
            string path = Path.Combine(NewStage(), "extension.zip");
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(60) };
            using var response = await client.GetAsync(release.ExtensionUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            const long limit = 10 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("ZIP расширения слишком большой.");
            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = File.Create(path))
            {
                byte[] buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                { total += count; if (total > limit) throw new InvalidDataException("ZIP расширения слишком большой."); await output.WriteAsync(buffer, 0, count).ConfigureAwait(false); }
            }
            if (!string.Equals(FileUpdateTransaction.Hash(path), release.ExtensionHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Контрольная сумма расширения не совпала.");
            return path;
        }

        private static string NewStage()
        { string path = Path.Combine(Path.GetTempPath(), "SurgeeUpdate", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }

        internal static string Stage(string archivePath)
        {
            if (new FileInfo(archivePath).Length > 300L * 1024 * 1024) throw new InvalidDataException("Архив слишком большой.");
            using var zip = ZipFile.OpenRead(archivePath);
            if (zip.Entries.Count > 5000 || zip.Entries.Sum(e => e.Length) > 800L * 1024 * 1024) throw new InvalidDataException("Архив слишком большой.");
            var manifests = zip.Entries.Where(e => e.Name == "surgee-release.json").ToArray();
            if (manifests.Length != 1 || manifests[0].Length > 100000) throw new InvalidDataException("Это не пакет обновления Spectrum Manager / Surgee 6 или новее.");
            var manifestEntry = manifests[0];
            JObject manifest;
            using (var reader = new StreamReader(manifestEntry.Open())) manifest = JObject.Parse(reader.ReadToEnd());
            System.Version version;
            if ((string)manifest["kind"] != "surgee-manager" || !System.Version.TryParse((string)manifest["version"], out version)) throw new InvalidDataException("Неверный манифест обновления.");
            var files = manifest["files"] as JObject;
            if (files == null || files.Count > 256 || files["Roblox Account Manager.exe"] == null || files["SurgeeUpdater.exe"] == null) throw new InvalidDataException("Неполный пакет обновления.");
            string root = manifestEntry.FullName.Substring(0, manifestEntry.FullName.Length - manifestEntry.Name.Length);
            string stage = NewStage();
            var plan = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files.Properties())
            {
                string relative = file.Name, hash = (string)file.Value;
                if (!seen.Add(relative) || !FileUpdateTransaction.Allowed(relative) || !System.Text.RegularExpressions.Regex.IsMatch(hash ?? "", "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Недопустимый файл в обновлении.");
                var matches = zip.Entries.Where(e => e.FullName == root + relative).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("В архиве отсутствует файл: " + relative);
                string destination = FileUpdateTransaction.SafePath(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (var input = matches[0].Open()) using (var output = File.Create(destination)) input.CopyTo(output);
                if (!string.Equals(FileUpdateTransaction.Hash(destination), hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Повреждён файл обновления: " + relative);
                plan.Append(relative).Append('\t').Append(hash).AppendLine();
            }
            File.WriteAllText(Path.Combine(stage, "update-plan.tsv"), plan.ToString(), Encoding.UTF8);
            return stage;
        }

        internal static void StartInstaller(string stage)
        {
            // Run the currently shipped helper, not an unchecked executable from an arbitrary ZIP.
            string helper = Path.Combine(stage, "UpdateRunner.exe");
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SurgeeUpdater.exe"), helper, true);
            string destination = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var start = new ProcessStartInfo(helper, Process.GetCurrentProcess().Id + " \"" + destination + "\" \"" + stage + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = stage };
            Process.Start(start);
        }
    }
}
