using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SurgeeUpdating
{
    internal static class FileUpdateTransaction
    {
        internal static string Hash(string path)
        { using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }

        internal static bool Allowed(string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Contains("\\") || relative.Contains(":") || relative.StartsWith("/") || relative.Contains("..")) return false;
            if (relative.StartsWith("Themes/", StringComparison.Ordinal)) return relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && relative.Split('/').Length == 2;
            if (relative.StartsWith("Browser Extension/", StringComparison.Ordinal))
                return relative.Split('/').Length == 2 && (relative.EndsWith(".js") || relative.EndsWith(".html") || relative.EndsWith(".css") || relative == "Browser Extension/manifest.json");
            return !relative.Contains("/") && (relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || relative == "Roblox Account Manager.exe" ||
                relative == "SurgeeUpdater.exe" || relative == "Roblox Account Manager.exe.config" || relative == "log4.config");
        }

        internal static string SafePath(string root, string relative)
        {
            if (!Allowed(relative)) throw new InvalidDataException("Недопустимый файл обновления: " + relative);
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Файл за пределами папки приложения.");
            for (string check = Path.GetDirectoryName(path); !string.IsNullOrEmpty(check); check = Path.GetDirectoryName(check))
                if (Directory.Exists(check) && (File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0) throw new IOException("Папка обновления содержит перенаправление.");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Файл обновления содержит перенаправление.");
            return path;
        }

        internal static void Apply(string stage, string destination, IDictionary<string, string> files)
        {
            string backup = Path.Combine(stage, "backup");
            var written = new List<string>();
            // Validate every file and create all backups before replacing the first executable.
            foreach (var file in files)
            {
                string source = SafePath(stage, file.Key), target = SafePath(destination, file.Key);
                if (!string.Equals(Hash(source), file.Value, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Контрольная сумма не совпала: " + file.Key);
                if (File.Exists(target))
                {
                    string saved = SafePath(backup, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved));
                    File.Copy(target, saved, true);
                }
            }
            try
            {
                foreach (var file in files)
                {
                    string target = SafePath(destination, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    AtomicCopy(SafePath(stage, file.Key), target);
                    written.Add(file.Key);
                }
            }
            catch
            {
                foreach (string relative in written)
                {
                    string target = SafePath(destination, relative), saved = SafePath(backup, relative);
                    if (File.Exists(saved)) AtomicCopy(saved, target);
                    else if (File.Exists(target)) File.Delete(target);
                }
                throw;
            }
        }

        private static void AtomicCopy(string source, string target)
        {
            string temporary = target + ".surgee-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(source, temporary);
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
