using Newtonsoft.Json.Linq;
using RBX_Alt_Manager;
using RBX_Alt_Manager.Classes;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

static class AccountImportTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string root = Path.Combine(Path.GetTempPath(), "SpectrumImportTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "AccountData.json"); byte[] entropy = { 1, 2, 3, 4 };
        var source = new JArray(new JObject { ["UserID"] = 1, ["Username"] = "OldName", ["SecurityToken"] = "SYNTHETIC_OLD_SESSION", ["Alias"] = "Из старого", ["Description"] = "Старая заметка", ["Group"] = "Группа", ["Fields"] = new JObject { ["SavedPlaceId"] = "42", ["empty"] = "filled" } },
            new JObject { ["UserID"] = 2, ["Username"] = "SecondUser", ["SecurityToken"] = "SYNTHETIC_SECOND_SESSION", ["Group"] = "Другая группа" });
        string json = source.ToString(); File.WriteAllText(path, json);
        File.WriteAllText(Path.Combine(root, "AccountFolders.json"), "[\"Пустая папка\"]");
        var batch = AccountImports.Read(path, entropy);
        check(batch.Accounts.Count == 2 && batch.Folders.Values<string>().Contains("Пустая папка"), "import reads original JSON and optional empty folders");
        var current = Account.FromPortableRecord(new JObject { ["UserID"] = 1, ["Username"] = "CurrentName", ["SecurityToken"] = "SYNTHETIC_FRESH_SESSION", ["Alias"] = "Текущий", ["Description"] = "Новая заметка", ["Group"] = "Default", ["Fields"] = new JObject { ["existing"] = "keep", ["empty"] = "" } });
        var merged = AccountImports.Merge(new[] { current }, batch, false, out int added, out int duplicates);
        check(added == 1 && duplicates == 1 && merged.Count == 2, "original manager import merges by ID without duplicates");
        var first = merged.First(account => account.UserID == 1);
        check(first.Username == "CurrentName" && first.SecurityToken == "SYNTHETIC_FRESH_SESSION" && first.Alias == "Текущий", "import preserves current session, current nickname and current alias");
        check(first.Description.Contains("Новая заметка") && first.Description.Contains("Старая заметка") && first.Group == "Группа", "import keeps both notes and restores original named group");
        check(first.Fields["existing"] == "keep" && first.Fields["SavedPlaceId"] == "42" && first.Fields["empty"] == "filled" && first.Fields["ImportedAlias"] == "Из старого", "import preserves custom fields, saved game and conflicting alias");
        var repeated = AccountImports.Merge(merged, batch, false, out added, out duplicates);
        check(added == 0 && repeated.Count == 2 && repeated[0].Description == first.Description, "repeated original import neither duplicates accounts nor repeats notes");
        var replaced = AccountImports.Merge(new[] { current }, batch, true, out added, out duplicates);
        check(replaced[0].SecurityToken == "SYNTHETIC_OLD_SESSION", "explicit replace-sessions option uses imported login only when requested");
        check(File.ReadAllText(path) == json, "import never modifies source account file");
        byte[] protectedFile = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), entropy, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(path, protectedFile);
        check(AccountImports.Read(path, entropy).Accounts.Count == 2, "import reads Windows-protected original manager file on the same Windows installation");
        check(File.ReadAllBytes(path).SequenceEqual(protectedFile), "protected source file remains byte-for-byte unchanged");
        reject(() => AccountImports.Read(path, new byte[] { 9, 9 }), "unavailable or incorrect Windows keys fail without changing target data");
        File.WriteAllBytes(path, Cryptography.RAMHeader.Concat(new byte[64]).ToArray());
        bool passwordRequested = false;
        try { AccountImports.Read(path, entropy); } catch (ImportPasswordRequiredException) { passwordRequested = true; }
        check(passwordRequested, "master-password protected imports request original file password instead of bypassing protection");
        string portable = Path.Combine(root, "portable.surgee-backup.json"); PortableBackups.Write(portable, batch);
        check(AccountImports.Read(portable, entropy).Accounts.Count == 2, "main Import button also accepts validated portable backup format");
        File.WriteAllText(path, "[{\"Username\":\"broken\""); reject(() => AccountImports.Read(path, entropy), "truncated account JSON rejected");
        File.WriteAllText(path, "{\"unrelated\":true}"); reject(() => AccountImports.Read(path, entropy), "unrelated JSON cannot be mistaken for an account database");
    }
}
