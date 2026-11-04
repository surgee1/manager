using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    // Package destinations follow Roblox's deployment layout, also documented by Bloxstrap:
    // https://github.com/bloxstraplabs/bloxstrap/blob/main/Bloxstrap/AppData/CommonAppData.cs
    internal sealed class OfficialRobloxInstaller
    {
        internal sealed class Package { internal string Name, Hash, Destination; internal long Packed, Unpacked; }
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, string> Destinations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RobloxApp.zip"] = "", ["Libraries.zip"] = "", ["redist.zip"] = "", ["WebView2.zip"] = "",
            ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller", ["shaders.zip"] = "shaders", ["ssl.zip"] = "ssl",
            ["content-avatar.zip"] = @"content\avatar", ["content-configs.zip"] = @"content\configs", ["content-fonts.zip"] = @"content\fonts",
            ["content-sky.zip"] = @"content\sky", ["content-sounds.zip"] = @"content\sounds", ["content-models.zip"] = @"content\models",
            ["content-textures2.zip"] = @"content\textures", ["content-textures3.zip"] = @"PlatformContent\pc\textures",
            ["content-terrain.zip"] = @"PlatformContent\pc\terrain", ["content-platform-fonts.zip"] = @"PlatformContent\pc\fonts",
            ["content-platform-dictionaries.zip"] = @"PlatformContent\pc\shared_compression_dictionaries",
            ["extracontent-places.zip"] = @"ExtraContent\places", ["extracontent-luapackages.zip"] = @"ExtraContent\LuaPackages",
            ["extracontent-translations.zip"] = @"ExtraContent\translations", ["extracontent-models.zip"] = @"ExtraContent\models",
            ["extracontent-textures.zip"] = @"ExtraContent\textures"
        };
        private readonly HttpClient http;
        internal OfficialRobloxInstaller(HttpClient http) { this.http = http; }
        internal static string VersionId(string json)
        {
            string version = JObject.Parse(json).Value<string>("clientVersionUpload");
            if (version == null || !Regex.IsMatch(version, @"\Aversion-[a-f0-9]{16}\z")) throw new IOException("Roblox вернул неизвестный номер сборки.");
            return version;
        }
        internal static List<Package> ParseManifest(string manifest)
        {
            var lines = manifest.Replace("\r", "").TrimEnd('\n').Split('\n');
            if (lines.Length < 5 || lines[0] != "v0" || (lines.Length - 1) % 4 != 0) throw new IOException("Неизвестный формат пакетов Roblox.");
            var packages = new List<Package>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i += 4)
            {
                string name = lines[i];
                if (!names.Add(name) || !Regex.IsMatch(lines[i + 1], @"\A[a-fA-F0-9]{32}\z") ||
                    !long.TryParse(lines[i + 2], out long packed) || !long.TryParse(lines[i + 3], out long unpacked) ||
                    packed <= 0 || packed > 1024L * 1024 * 1024 || unpacked <= 0 || unpacked > 2L * 1024 * 1024 * 1024)
                    throw new IOException("Повреждён список пакетов Roblox.");
                if (name == "RobloxPlayerInstaller.exe" || name == "RobloxPlayerLauncher.exe") continue;
                if (!Destinations.TryGetValue(name, out string destination)) throw new IOException("Новый пакет Roblox «" + name + "». Обнови Spectrum Manager.");
                packages.Add(new Package { Name = name, Hash = lines[i + 1], Packed = packed, Unpacked = unpacked, Destination = destination });
            }
            if (!packages.Any(package => package.Name == "RobloxApp.zip") || packages.Sum(package => package.Unpacked) > 4L * 1024 * 1024 * 1024)
                throw new IOException("В списке нет полного клиента Roblox.");
            return packages;
        }
        internal static void VerifyPackage(string file, Package package)
        {
            using (var input = File.OpenRead(file))
            using (var md5 = MD5.Create())
                if (input.Length != package.Packed || !string.Equals(BitConverter.ToString(md5.ComputeHash(input)).Replace("-", ""), package.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Проверка загрузки не пройдена: " + package.Name + ". Повтори скачивание.");
        }
        internal static void ExtractPackage(string file, string root, Package package, CancellationToken cancel)
        {
            string destination = Path.GetFullPath(Path.Combine(root, package.Destination)) + Path.DirectorySeparatorChar;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long size = 0;
            using (var zip = ZipFile.OpenRead(file))
            foreach (var entry in zip.Entries)
            {
                cancel.ThrowIfCancellationRequested();
                // Roblox content archives include rooted, zero-length directory markers.
                // File parents are created below; markers never cause filesystem writes.
                if (entry.Name.Length == 0 && entry.Length == 0) continue;
                if ((size += entry.Length) > package.Unpacked + 1024 * 1024 || entry.FullName.IndexOf(':') >= 0 || Path.IsPathRooted(entry.FullName))
                    throw new IOException("Неверный размер или путь в пакете Roblox.");
                string path = Path.GetFullPath(Path.Combine(destination, entry.FullName.Replace('/', '\\')));
                if (!path.StartsWith(destination, StringComparison.OrdinalIgnoreCase) || !names.Add(path) || (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                    throw new IOException("Небезопасный путь в пакете Roblox.");
                if (entry.Name.Length == 0) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var input = entry.Open()) using (var output = new FileStream(path, FileMode.Create, FileAccess.Write)) input.CopyTo(output);
            }
        }
        internal async Task<string> InstallAsync(string root, IProgress<string> progress, CancellationToken cancel)
        {
            await Gate.WaitAsync(cancel).ConfigureAwait(false); string staging = null;
            try
            {
                progress?.Report("Проверка версии Roblox…");
                string version = VersionId(await GetText("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer/channel/LIVE", cancel).ConfigureAwait(false));
                string prefix = "https://setup.rbxcdn.com/" + version + "-";
                var packages = ParseManifest(await GetText(prefix + "rbxPkgManifest.txt", cancel).ConfigureAwait(false));
                Directory.CreateDirectory(root);
                long required = packages.Sum(package => package.Packed + package.Unpacked);
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root)));
                if (drive.AvailableFreeSpace < required + 256L * 1024 * 1024) throw new IOException("Для отдельного Roblox нужно не менее " + ((required / 1024 / 1024) + 256) + " МБ свободного места.");
                staging = Path.Combine(Path.GetFullPath(root), ".download-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                string client = Path.Combine(staging, "client"); Directory.CreateDirectory(client);
                long total = packages.Sum(package => package.Packed), done = 0; int lastPercent = -1;
                foreach (var package in packages)
                {
                    cancel.ThrowIfCancellationRequested();
                    string archive = Path.Combine(staging, package.Name);
                    using (var response = await http.GetAsync(prefix + package.Name, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            var buffer = new byte[81920]; long received = 0; int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancel).ConfigureAwait(false)) > 0)
                            {
                                received += count;
                                if (received > package.Packed) throw new IOException("Размер пакета Roblox не совпал.");
                                await output.WriteAsync(buffer, 0, count, cancel).ConfigureAwait(false);
                                int percent = (int)((done + received) * 100 / total);
                                if (percent != lastPercent) { progress?.Report("Скачивание Legit · " + percent + "%"); lastPercent = percent; }
                            }
                        }
                    }
                    VerifyPackage(archive, package);
                    progress?.Report("Распаковка · " + package.Name);
                    await Task.Run(() => ExtractPackage(archive, client, package, cancel), cancel).ConfigureAwait(false);
                    File.Delete(archive); done += package.Packed;
                }
                cancel.ThrowIfCancellationRequested();
                if (!File.Exists(Path.Combine(client, RobloxClients.ExecutableName))) throw new IOException("Клиент Roblox не найден в официальных пакетах.");
                File.WriteAllText(Path.Combine(client, "AppSettings.xml"), "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Settings><ContentFolder>content</ContentFolder><BaseUrl>https://www.roblox.com</BaseUrl></Settings>");
                File.WriteAllText(Path.Combine(client, "spectrum-install.json"), new JObject { ["version"] = version, ["source"] = "https://setup.rbxcdn.com/" }.ToString());
                string installed = Path.Combine(Path.GetFullPath(root), version + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.Move(client, installed);
                progress?.Report("Legit готов · " + version);
                return Path.Combine(installed, RobloxClients.ExecutableName);
            }
            finally
            {
                // Only our fresh private staging directory can be removed; existing clients stay intact.
                if (staging != null && Path.GetFullPath(staging).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\.download-", StringComparison.OrdinalIgnoreCase))
                    try { Directory.Delete(staging, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                Gate.Release();
            }
        }
        private async Task<string> GetText(string url, CancellationToken cancel)
        {
            using (var response = await http.GetAsync(url, cancel).ConfigureAwait(false))
            { response.EnsureSuccessStatusCode(); if (response.Content.Headers.ContentLength > 1024 * 1024) throw new IOException("Слишком большой ответ Roblox."); return await response.Content.ReadAsStringAsync().ConfigureAwait(false); }
        }
    }
}
