using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace RBX_Alt_Manager.Classes
{
    internal static class ExtensionUpdates
    {
        private static readonly string[] Names = { "manifest.json", "background.js", "core.js", "popup.js", "popup.html", "popup.css", "handoff.js" };
        private const string Product = "Spectrum Manager";
        internal static bool IsExtensionFolder(string directory)
        {
            try { var manifest = JObject.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json"))); return ((string)manifest["name"] == Product || (string)manifest["name"] == "Surgee • Roblox Manager") && (int?)manifest["manifest_version"] == 3; }
            catch { return false; }
        }
        internal static string FindInstalledDirectory(string id, IEnumerable<string> preferences = null)
        {
            foreach (string path in preferences ?? PreferenceFiles())
                try
                {
                    if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024 * 1024) continue;
                    var entry = JObject.Parse(File.ReadAllText(path))["extensions"]?["settings"]?[id];
                    if (entry == null || (int?)entry["location"] != 4) continue; // Unpacked extensions only.
                    string directory = (string)entry["path"];
                    if (string.IsNullOrWhiteSpace(directory)) continue;
                    if (!Path.IsPathRooted(directory)) directory = Path.Combine(Path.GetDirectoryName(path), "Extensions", directory);
                    directory = Path.GetFullPath(directory);
                    if (IsExtensionFolder(directory)) return directory;
                }
                catch { /* Browser preferences are read-only; a locked or damaged file is skipped. */ }
            return null;
        }
        private static IEnumerable<string> PreferenceFiles()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (string root in new[] { Path.Combine(roaming, "Opera Software", "Opera GX Stable"), Path.Combine(roaming, "Opera Software", "Opera Stable"),
                Path.Combine(local, "Google", "Chrome", "User Data"), Path.Combine(local, "Microsoft", "Edge", "User Data"), Path.Combine(local, "Chromium", "User Data") })
            {
                yield return Path.Combine(root, "Preferences"); yield return Path.Combine(root, "Secure Preferences");
                string[] profiles; try { profiles = Directory.GetDirectories(root); } catch { continue; }
                foreach (string profile in profiles.Where(path => Path.GetFileName(path) == "Default" || Path.GetFileName(path).StartsWith("Profile ", StringComparison.Ordinal)))
                { yield return Path.Combine(profile, "Preferences"); yield return Path.Combine(profile, "Secure Preferences"); }
            }
        }
        internal static Dictionary<string, byte[]> ReadPackage(string archive, string expectedVersion)
        {
            if (!Version.TryParse(expectedVersion, out var version)) throw new InvalidDataException("Нет версии расширения в релизе.");
            if (new FileInfo(archive).Length > 10 * 1024 * 1024) throw new InvalidDataException("Архив расширения слишком большой.");
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using var zip = ZipFile.OpenRead(archive);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (!Names.Contains(entry.FullName, StringComparer.Ordinal) || files.ContainsKey(entry.FullName) || entry.Length > 2 * 1024 * 1024 || (total += entry.Length) > 5 * 1024 * 1024)
                    throw new InvalidDataException("Недопустимое содержимое обновления расширения.");
                using var input = entry.Open(); using var memory = new MemoryStream();
                byte[] buffer = new byte[8192]; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                { if (memory.Length + count > entry.Length || memory.Length + count > 2 * 1024 * 1024) throw new InvalidDataException("Повреждённый файл расширения."); memory.Write(buffer, 0, count); }
                files.Add(entry.FullName, memory.ToArray());
            }
            if (Names.Any(name => !files.ContainsKey(name))) throw new InvalidDataException("Неполный пакет расширения.");
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(files["manifest.json"]));
            if ((string)manifest["name"] != Product || (int?)manifest["manifest_version"] != 3 || (string)manifest["version"] != expectedVersion ||
                (string)manifest["background"]?["service_worker"] != "background.js") throw new InvalidDataException("Неверный манифест расширения.");
            return files;
        }
        internal static void Apply(string directory, Dictionary<string, byte[]> files)
        {
            directory = Path.GetFullPath(directory);
            if (!IsExtensionFolder(directory) || files.Keys.Any(name => !Names.Contains(name, StringComparer.Ordinal))) throw new InvalidDataException("Выбрана не папка расширения Spectrum Manager.");
            for (string parent = directory; parent != null; parent = Path.GetDirectoryName(parent))
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException("Папка расширения содержит перенаправление.");
            var current = JObject.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            var incoming = JObject.Parse(System.Text.Encoding.UTF8.GetString(files["manifest.json"]));
            if (!JToken.DeepEquals(current["key"], incoming["key"])) throw new InvalidDataException("Обновление не должно менять идентификатор расширения.");
            var originals = new Dictionary<string, byte[]>();
            foreach (string name in files.Keys)
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Файл расширения содержит перенаправление.");
                originals[name] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            var written = new List<string>();
            try
            {
                // Manifest last, so reload never observes a new version with old scripts after a failed write.
                foreach (string name in files.Keys.OrderBy(name => name == "manifest.json" ? 1 : 0))
                { PortableBackups.AtomicWrite(Path.Combine(directory, name), files[name]); written.Add(name); }
            }
            catch
            {
                foreach (string name in written.AsEnumerable().Reverse())
                { if (originals[name] != null) PortableBackups.AtomicWrite(Path.Combine(directory, name), originals[name]); else File.Delete(Path.Combine(directory, name)); }
                throw;
            }
        }
    }
}
