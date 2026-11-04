using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RBX_Alt_Manager.Classes
{
    // Deliberately portable: no Windows keys, pairing codes or application API keys.
    internal sealed class PortableBackup
    {
        internal JObject Payload;
        internal DateTime CreatedUtc;
        internal string Hash => PortableBackups.Digest(Payload);
        internal JArray Accounts => (JArray)Payload["accounts"];
        internal JArray Folders => (JArray)Payload["folders"];
    }

    internal static class PortableBackups
    {
        internal const string Extension = ".surgee-backup.json";
        private const int MaxBytes = 50 * 1024 * 1024;
        private static readonly object IoLock = new object();
        internal static string Digest(JToken value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
        }
        internal static PortableBackup Capture(IEnumerable<Account> accounts, IEnumerable<string> folders)
        {
            // Parsing with dates disabled gives the same checksum before and after writing JSON.
            var names = folders.Select(name => name == "Default" ? "Без папки" : name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var payload = Parse(JObject.FromObject(new { accounts = accounts.ToList(), folders = names }).ToString());
            Validate(payload);
            return new PortableBackup { Payload = payload, CreatedUtc = DateTime.UtcNow };
        }
        private static JObject Parse(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 };
            var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.TokenType != JsonToken.EndObject || reader.Depth != 0 || reader.Read()) throw new InvalidDataException("Неполный JSON или лишние данные после него.");
            return value;
        }
        internal static PortableBackup Read(string path)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length == 0 || file.Length > MaxBytes) throw new InvalidDataException("Пустая или слишком большая копия (максимум 50 МБ).");
            using var reader = new StreamReader(file, new UTF8Encoding(false, true), true);
            var document = Parse(reader.ReadToEnd());
            if ((string)document["format"] != "surgee-portable-backup" || (int?)document["version"] != 1)
                throw new InvalidDataException("Это не переносимая копия Spectrum Manager поддерживаемой версии.");
            if (!(document["payload"] is JObject payload) || !string.Equals((string)document["sha256"], Digest(payload), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Копия повреждена: контрольная сумма не совпала.");
            Validate(payload);
            if (!DateTime.TryParse((string)document["createdUtc"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created))
                throw new InvalidDataException("В копии отсутствует дата создания.");
            return new PortableBackup { Payload = payload, CreatedUtc = created.ToUniversalTime() };
        }
        private static void Validate(JObject payload)
        {
            if (!(payload["accounts"] is JArray accounts) || accounts.Count > 10000 || !(payload["folders"] is JArray folders) || folders.Count > 10000)
                throw new InvalidDataException("Неверный список аккаунтов или папок.");
            foreach (var token in accounts)
            {
                if (!(token is JObject record)) throw new InvalidDataException("Неверная запись аккаунта.");
                Account.FromPortableRecord(record); // Validate without invoking setters or saving anything.
            }
            foreach (var folder in folders)
                if (folder.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)folder) || ((string)folder).Length > 1000)
                    throw new InvalidDataException("Неверное название папки.");
        }
        internal static void AtomicWrite(string path, byte[] bytes)
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static void Write(string path, PortableBackup backup)
        {
            Validate(backup.Payload);
            var document = new JObject { ["format"] = "surgee-portable-backup", ["version"] = 1,
                ["createdUtc"] = backup.CreatedUtc.ToString("o", CultureInfo.InvariantCulture),
                ["notice"] = "Содержит данные входа. Хранить приватно. Для восстановления ключи Windows не нужны.",
                ["sha256"] = backup.Hash, ["payload"] = backup.Payload.DeepClone() };
            byte[] bytes = new UTF8Encoding(false).GetBytes(document.ToString(Formatting.Indented));
            if (bytes.Length > MaxBytes) throw new InvalidDataException("Копия больше 50 МБ.");
            // Verify the complete temporary copy before replacing an existing export.
            string staged = path + ".verify-" + Guid.NewGuid().ToString("N");
            lock (IoLock)
                try { AtomicWrite(staged, bytes); Read(staged); AtomicWrite(path, bytes); }
                finally { if (File.Exists(staged)) File.Delete(staged); }
        }
        internal static string NewPath(string directory, string prefix) => Path.Combine(directory,
            prefix + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + Extension);
        internal static string WriteAutomatic(string directory, PortableBackup backup, int keep = 30)
        {
            lock (IoLock)
            {
                Directory.CreateDirectory(directory);
                var files = AutomaticFiles(directory);
                if (files.Length > 0)
                {
                    try
                    {
                        var latest = Read(files[0]);
                        if (latest.CreatedUtc > backup.CreatedUtc) return files[0];
                        if (latest.Hash == backup.Hash && latest.CreatedUtc.Date == backup.CreatedUtc.Date) return files[0];
                    }
                    catch { /* A damaged previous copy must not prevent a new valid one. */ }
                }
                string path = NewPath(directory, "auto");
                Write(path, backup);
                // Only exact generated names; manual exports and before-restore copies are never pruned.
                foreach (string old in AutomaticFiles(directory).Skip(Math.Max(1, keep))) File.Delete(old);
                return path;
            }
        }
        private static string[] AutomaticFiles(string directory) => Directory.GetFiles(directory, "auto-*" + Extension)
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), @"^auto-\d{8}-\d{6}-\d{3}-[a-f0-9]{8}\.surgee-backup\.json$"))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray();

        internal static List<Account> Merge(IEnumerable<Account> current, PortableBackup backup, out int added)
        {
            var result = current.Select(account => JObject.FromObject(account)).ToList();
            added = 0;
            foreach (JObject record in backup.Accounts)
            {
                long id = (long?)record["UserID"] ?? 0;
                string name = (string)record["Username"];
                var match = result.FirstOrDefault(item => id > 0 && (long?)item["UserID"] == id)
                    ?? result.FirstOrDefault(item => ((long?)item["UserID"] == 0 || id == 0) && string.Equals((string)item["Username"], name, StringComparison.OrdinalIgnoreCase));
                if (match == null) { result.Add((JObject)record.DeepClone()); added++; continue; }
                // Keep a current session and current metadata. Fill only missing information.
                foreach (var property in record.Properties())
                {
                    var existing = match[property.Name];
                    if (existing == null || existing.Type == JTokenType.Null || (existing.Type == JTokenType.String && string.IsNullOrEmpty((string)existing)))
                        match[property.Name] = property.Value.DeepClone();
                    else if (property.Name == "Fields" && existing is JObject fields && property.Value is JObject incoming)
                        foreach (var field in incoming.Properties()) if (fields[field.Name] == null) fields[field.Name] = field.Value.DeepClone();
                }
                if ((long?)match["UserID"] == 0 && id > 0) match["UserID"] = id;
            }
            return result.Select(Account.FromPortableRecord).ToList();
        }
    }
}
