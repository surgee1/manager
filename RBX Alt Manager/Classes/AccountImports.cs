using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class ImportPasswordRequiredException : InvalidOperationException
    { internal ImportPasswordRequiredException() : base("Файл защищён мастер-паролем исходного менеджера.") { } }
    internal static class AccountImports
    {
        internal static PortableBackup Read(string path, byte[] entropy, string password = null)
        {
            if (new FileInfo(path).Length > 50 * 1024 * 1024) throw new InvalidDataException("Файл больше 50 МБ.");
            byte[] raw = File.ReadAllBytes(path), plain = null;
            if (raw.Length == 0) throw new InvalidDataException("Файл аккаунтов пуст.");
            try
            {
                if (raw.Take(Cryptography.RAMHeader.Length).SequenceEqual(Cryptography.RAMHeader))
                {
                    if (password == null) throw new ImportPasswordRequiredException();
                    try { plain = Cryptography.Decrypt(raw, Sodium.CryptoHash.Hash(password)); }
                    catch { throw new InvalidDataException("Не удалось открыть файл с этим мастер-паролем."); }
                }
                else
                {
                    int offset = raw.Length >= 3 && raw[0] == 239 && raw[1] == 187 && raw[2] == 191 ? 3 : 0;
                    while (offset < raw.Length && (raw[offset] == 32 || raw[offset] == 9 || raw[offset] == 10 || raw[offset] == 13)) offset++;
                    if (offset < raw.Length && (raw[offset] == '[' || raw[offset] == '{')) plain = raw;
                    else
                    {
                        try { plain = ProtectedData.Unprotect(raw, entropy, DataProtectionScope.CurrentUser); }
                        catch (CryptographicException) { throw new InvalidDataException("Windows не смогла расшифровать файл. Нужны исходная Windows и её ключи либо переносимая копия/экспорт из работающего менеджера."); }
                    }
                }
                string json = new UTF8Encoding(false, true).GetString(plain).TrimStart('\uFEFF');
                if (json.TrimStart().StartsWith("{"))
                {
                    var document = JObject.Parse(json);
                    if ((string)document["format"] == "surgee-portable-backup") return PortableBackups.Read(path);
                    throw new InvalidDataException("Выбери AccountData.json исходного менеджера или переносимую копию Spectrum Manager.");
                }
                var accounts = AccountManager.ReadStoredAccounts(json);
                var folders = accounts.Select(account => account.Group).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
                string folderPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "AccountFolders.json");
                if (File.Exists(folderPath) && new FileInfo(folderPath).Length < 2 * 1024 * 1024)
                {
                    try { folders.AddRange(JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(folderPath)) ?? new List<string>()); }
                    catch { /* Account groups remain available even if the optional empty-folder list is damaged. */ }
                }
                return PortableBackups.Capture(accounts, folders);
            }
            catch (JsonException) { throw new InvalidDataException("Файл не содержит подходящий список аккаунтов. Исходный файл не изменён."); }
            catch (DecoderFallbackException) { throw new InvalidDataException("Файл повреждён или это не оригинальный AccountData.json. Текстовая вставка зашифрованного файла не подходит."); }
            finally { if (plain != null && !ReferenceEquals(plain, raw)) Array.Clear(plain, 0, plain.Length); }
        }
        private static bool Empty(JToken value) => value == null || value.Type == JTokenType.Null || (value.Type == JTokenType.String && string.IsNullOrEmpty((string)value));
        private static JObject Fields(JObject account)
        { if (!(account["Fields"] is JObject fields)) account["Fields"] = fields = new JObject(); return fields; }
        internal static List<Account> Merge(IEnumerable<Account> current, PortableBackup source, bool replaceSessions, out int added, out int duplicates)
        {
            var records = current.Select(JObject.FromObject).ToList(); added = duplicates = 0;
            foreach (JObject incoming in source.Accounts)
            {
                long id = (long?)incoming["UserID"] ?? 0;
                var found = records.FirstOrDefault(item => id > 0 && (long?)item["UserID"] == id)
                    ?? records.FirstOrDefault(item => (id == 0 || (long?)item["UserID"] == 0) && string.Equals((string)item["Username"], (string)incoming["Username"], StringComparison.OrdinalIgnoreCase));
                if (found == null) { records.Add((JObject)incoming.DeepClone()); added++; continue; }
                duplicates++;
                foreach (var property in incoming.Properties()) if (Empty(found[property.Name])) found[property.Name] = property.Value.DeepClone();
                if (incoming["Fields"] is JObject fields)
                    foreach (var field in fields.Properties()) if (Empty(Fields(found)[field.Name])) Fields(found)[field.Name] = field.Value.DeepClone();
                if ((long?)found["UserID"] == 0 && id > 0) found["UserID"] = id;
                if (((string)found["Group"] == "Default" || Empty(found["Group"])) && !Empty(incoming["Group"])) found["Group"] = incoming["Group"].DeepClone();
                string oldNote = (string)incoming["Description"], note = (string)found["Description"];
                if (!string.IsNullOrEmpty(oldNote) && !string.IsNullOrEmpty(note) && note != oldNote && !note.Contains(oldNote))
                {
                    string combined = note + "\n\nИз старого менеджера:\n" + oldNote;
                    if (combined.Length <= 5000) found["Description"] = combined; else Fields(found)["ImportedDescription"] = oldNote;
                }
                if (!Empty(incoming["Alias"]) && !Empty(found["Alias"]) && (string)incoming["Alias"] != (string)found["Alias"])
                    Fields(found)["ImportedAlias"] = incoming["Alias"].DeepClone();
                if (replaceSessions && !Empty(incoming["SecurityToken"]))
                {
                    found["SecurityToken"] = incoming["SecurityToken"].DeepClone(); found["Valid"] = incoming["Valid"] ?? false;
                    if (!Empty(incoming["BrowserTrackerID"])) found["BrowserTrackerID"] = incoming["BrowserTrackerID"].DeepClone();
                }
            }
            return records.Select(Account.FromPortableRecord).ToList();
        }
    }
}
