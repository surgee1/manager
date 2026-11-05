using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

static class ExtensionUpdateTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string root = Path.Combine(Path.GetTempPath(), "SurgeeExtensionTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var files = new Dictionary<string, byte[]>();
        foreach (string name in new[] { "manifest.json", "background.js", "core.js", "popup.js", "popup.html", "popup.css", "handoff.js" }) files[name] = Encoding.UTF8.GetBytes("synthetic-new-file");
        files["manifest.json"] = Manifest("0.4.0");
        string zip = Path.Combine(root, "extension.zip"); WriteZip(zip, files);
        var unpacked = ExtensionUpdates.ReadPackage(zip, "0.4.0");
        check(unpacked.Count == 7, "extension ZIP validates required flat files and expected version");
        reject(() => ExtensionUpdates.ReadPackage(zip, "0.5.0"), "extension package version mismatch rejected");
        var withPasswords = new Dictionary<string, byte[]>(files) { ["manifest.json"] = Manifest("0.6.0"), ["capture.js"] = Encoding.UTF8.GetBytes("synthetic-capture"), ["passwords.js"] = Encoding.UTF8.GetBytes("synthetic-password-import") };
        string passwordZip = Path.Combine(root, "password-extension.zip"); WriteZip(passwordZip, withPasswords);
        check(ExtensionUpdates.ReadPackage(passwordZip, "0.6.0").Count == 9, "extension 0.6.0 update accepts both password modules");
        withPasswords.Remove("capture.js"); string incompleteZip = Path.Combine(root, "incomplete-password.zip"); WriteZip(incompleteZip, withPasswords);
        reject(() => ExtensionUpdates.ReadPackage(incompleteZip, "0.6.0"), "extension 0.6.0 requires complete password modules before updating");
        string target = Path.Combine(root, "installed"); Directory.CreateDirectory(target);
        File.WriteAllBytes(Path.Combine(target, "manifest.json"), Manifest("0.3.0"));
        foreach (string name in files.Keys.Where(name => name != "manifest.json" && name != "handoff.js")) File.WriteAllText(Path.Combine(target, name), "synthetic-old-file");
        File.WriteAllText(Path.Combine(target, "user-settings.txt"), "keep-local-data");
        using (var locked = new FileStream(Path.Combine(target, "popup.js"), FileMode.Open, FileAccess.Read, FileShare.Read))
            reject(() => ExtensionUpdates.Apply(target, unpacked), "locked extension file aborts update");
        check(File.ReadAllText(Path.Combine(target, "background.js")) == "synthetic-old-file" && File.ReadAllText(Path.Combine(target, "core.js")) == "synthetic-old-file"
            && (string)JObject.Parse(File.ReadAllText(Path.Combine(target, "manifest.json")))["version"] == "0.3.0", "failed extension update restores already replaced scripts and old manifest");
        ExtensionUpdates.Apply(target, unpacked);
        check((string)JObject.Parse(File.ReadAllText(Path.Combine(target, "manifest.json")))["version"] == "0.4.0" && File.Exists(Path.Combine(target, "handoff.js")), "extension files update in place without changing installation path");
        check(File.ReadAllText(Path.Combine(target, "user-settings.txt")) == "keep-local-data", "extension update preserves unrelated local files");
        var bad = new Dictionary<string, byte[]>(files) { ["AccountData.json"] = Encoding.UTF8.GetBytes("bad") };
        reject(() => ExtensionUpdates.Apply(target, bad), "extension installer cannot write arbitrary account or settings files");
        var keyed = JObject.Parse(Encoding.UTF8.GetString(files["manifest.json"])); keyed["key"] = "different-extension-id";
        bad = new Dictionary<string, byte[]>(files) { ["manifest.json"] = Encoding.UTF8.GetBytes(keyed.ToString()) };
        reject(() => ExtensionUpdates.Apply(target, bad), "update cannot change extension key or identity");
        var malicious = new Dictionary<string, byte[]>(files) { ["../outside.js"] = Encoding.UTF8.GetBytes("bad") };
        string badZip = Path.Combine(root, "bad.zip"); WriteZip(badZip, malicious);
        reject(() => ExtensionUpdates.ReadPackage(badZip, "0.4.0"), "extension archive traversal rejected before extraction");
        malicious = new Dictionary<string, byte[]>(files); malicious.Remove("handoff.js"); File.Delete(badZip); WriteZip(badZip, malicious);
        reject(() => ExtensionUpdates.ReadPackage(badZip, "0.4.0"), "incomplete extension archive rejected");
        string id = new string('a', 32), preferences = Path.Combine(root, "Preferences");
        var entry = new JObject { ["path"] = target, ["location"] = 4 };
        var document = new JObject { ["extensions"] = new JObject { ["settings"] = new JObject { [id] = entry } } };
        File.WriteAllText(preferences, document.ToString()); string original = File.ReadAllText(preferences);
        check(ExtensionUpdates.FindInstalledDirectory(id, new[] { preferences }) == target, "installed unpacked extension path found by exact browser extension ID");
        check(File.ReadAllText(preferences) == original, "browser preferences are never modified while locating extension");
        check(ExtensionUpdates.FindInstalledDirectory(new string('b', 32), new[] { preferences }) == null, "different extension ID cannot select installation path");
        entry["location"] = 1; File.WriteAllText(preferences, document.ToString());
        check(ExtensionUpdates.FindInstalledDirectory(id, new[] { preferences }) == null, "web-store managed extensions are not edited by unpacked updater");
    }
    private static byte[] Manifest(string version) => Encoding.UTF8.GetBytes(new JObject { ["name"] = version == "0.3.0" ? "Surgee • Roblox Manager" : "Spectrum Manager", ["manifest_version"] = 3, ["version"] = version,
        ["background"] = new JObject { ["service_worker"] = "background.js" } }.ToString());
    private static void WriteZip(string path, Dictionary<string, byte[]> files)
    { using var zip = ZipFile.Open(path, ZipArchiveMode.Create); foreach (var file in files) { using var output = zip.CreateEntry(file.Key).Open(); output.Write(file.Value, 0, file.Value.Length); } }
}
