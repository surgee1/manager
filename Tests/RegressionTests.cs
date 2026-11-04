using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using SurgeeUpdating;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class RegressionTests
{
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
    private static void Reject(Action action, string name) { try { action(); } catch { Check(true, name); return; } throw new Exception(name); }
    private sealed class Fake : HttpMessageHandler
    {
        internal int Calls;
        internal Func<HttpRequestMessage, int, HttpResponseMessage> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        { Calls++; return Task.FromResult(Reply(request, Calls)); }
    }
    private static HttpResponseMessage Reply(int status, string name = null, string value = null, string body = "{}")
    {
        var reply = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
        if (name != null) reply.Headers.TryAddWithoutValidation(name, value);
        return reply;
    }
    static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("Passed " + checks + " regression checks."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
    static async Task Run()
    {
        PortableBackupChecks();
        BrowserBridgeTests.Run(Check, Reject);
        ExtensionUpdateTests.Run(Check, Reject);
        AccountImportTests.Run(Check, Reject);
        var fake = new Fake { Reply = (request, call) =>
        {
            Check(request.RequestUri.Host == "auth.roblox.com", "ticket sent only to Roblox auth");
            Check(request.Headers.Contains("RBXAuthenticationNegotiation") && request.Headers.Referrer.Host == "www.roblox.com" && request.Headers.Contains("Origin"), "ticket negotiation has required headers");
            if (call == 1) return Reply(403, "X-CsRf-ToKeN", "synthetic-csrf");
            Check(System.Linq.Enumerable.First(request.Headers.GetValues("X-CSRF-TOKEN")) == "synthetic-csrf", "fresh CSRF used in retry");
            return Reply(200, "Rbx-Authentication-Ticket", "synthetic-ticket");
        } };
        using (var client = new RobloxSessionClient(fake))
        { var response = await client.RequestAsync("SYNTHETIC_SESSION", null, true); Check(response.Success && response.Ticket == "synthetic-ticket" && fake.Calls == 2, "mixed-case response headers produce usable ticket"); }
        foreach (int status in new[] { 401, 403, 429, 500 })
        {
            fake = new Fake { Reply = (request, call) => Reply(status) };
            using var client = new RobloxSessionClient(fake);
            var result = await client.RequestAsync("SYNTHETIC_SESSION", null, true);
            Check(!result.Success && fake.Calls == 1 && result.Error.Contains(status.ToString()), "precise failure without retry loop: HTTP " + status);
            Check(!result.Error.Contains("SYNTHETIC_SESSION"), "diagnostics omit session: HTTP " + status);
        }
        fake = new Fake { Reply = (request, call) => { var reply = Reply(403, "x-csrf-token", "fresh"); reply.Headers.Add("rblx-challenge-id", "synthetic"); return reply; } };
        using (var client = new RobloxSessionClient(fake))
        { var result = await client.RequestAsync("SYNTHETIC_SESSION", null, true); Check(!result.Success && fake.Calls == 1 && result.Error.Contains("проверку"), "additional verification goes back to official website"); }
        fake = new Fake { Reply = (request, call) => Reply(403, "X-CSRF-TOKEN", "fresh") };
        using (var client = new RobloxSessionClient(fake))
        { var result = await client.RequestAsync("SYNTHETIC_SESSION", null, false); Check(result.Success && result.Csrf == "fresh" && fake.Calls == 1, "CSRF-only request accepts mixed-case header"); }
        fake = new Fake { Reply = (request, call) => Reply(200) };
        using (var client = new RobloxSessionClient(fake))
        { var result = await client.RequestAsync("SYNTHETIC_SESSION", null, true); Check(!result.Success, "HTTP 200 without ticket is not successful authentication"); }
        using (var client = new RobloxSessionClient(new Fake()))
        { var result = await client.RequestAsync("bad\r\nheader", null, true); Check(!result.Success, "session header injection rejected before network"); }

        string uuid = "12345678-1234-1234-1234-123456789abc";
        var target = await RobloxLaunchTarget.ResolveAsync(42, uuid, "synthetic", "csrf");
        Check(target.JobId == uuid && !target.IsPrivate, "job ID remains a public-server target");
        target = await RobloxLaunchTarget.ResolveAsync(42, "https://www.roblox.com/games/42/test?privateServerLinkCode=12345&extra=1", "synthetic", "csrf");
        Check(target.IsPrivate && target.LinkCode == "12345" && target.JobId == "", "legacy private URL extracts only its link code");
        target = await RobloxLaunchTarget.ResolveAsync(42, "https://www.roblox.com/games/99?privateServerLinkCode=12345", "synthetic", "csrf");
        Check(target.Error.Length > 0, "private URL cannot silently launch another place");
        target = await RobloxLaunchTarget.ResolveAsync(42, "https://roblox.com.example.org/share?code=12345678&type=Server", "synthetic", "csrf", new Fake());
        Check(target.Error.Length > 0, "lookalike invite URL rejected before sending session");
        fake = new Fake { Reply = (request, call) =>
        {
            Check(request.RequestUri.AbsoluteUri == "https://apis.roblox.com/sharelinks/v1/resolve-link", "share invitation resolves only at fixed Roblox API");
            return Reply(200, body: "{\"privateServerInviteData\":{\"placeId\":42,\"linkCode\":\"12345\",\"accessCode\":\"" + uuid + "\"}}");
        } };
        target = await RobloxLaunchTarget.ResolveAsync(42, "https://www.roblox.com/share?code=12345678&type=Server", "synthetic", "csrf", fake);
        Check(target.IsPrivate && target.AccessCode == uuid && target.LinkCode == "12345" && target.JobId == "", "modern share URL becomes private launch parameters instead of gameId URL");
        Check(RobloxLaunchTarget.FromShareReply(42, JObject.Parse("{\"privateServerInviteData\":{\"placeId\":99,\"linkCode\":123}} ")).Error.Length > 0, "share response place mismatch is explicit");
        Check(RobloxLaunchTarget.FromShareReply(42, JObject.Parse("{\"data\":\"bad\"}")).Error.Length > 0, "malformed invite data handled");
        const string numericInvite = "12345678901234567890123456789012";
        target = RobloxLaunchTarget.FromShareReply(42, JObject.Parse("{\"privateServerInviteData\":{\"placeId\":42,\"linkCode\":\"" + numericInvite + "\"}}"));
        Check(target.LinkCode == numericInvite && target.AccessCode == "", "32-digit invite stays linkCode instead of being mistaken for GUID format N");
        string accessCode;
        Check(RobloxLaunchTarget.TryReadAccessCode("Roblox.GameLauncher.joinPrivateGame(42, '" + uuid + "');", out accessCode) && accessCode == uuid, "private page provides the separate access GUID");
        var launchQuery = System.Web.HttpUtility.ParseQueryString(new Uri(RobloxLaunchTarget.PrivateLauncherUrl(42, accessCode, target.LinkCode)).Query);
        Check(launchQuery["request"] == "RequestPrivateGame" && launchQuery["linkCode"] == numericInvite && launchQuery["accessCode"] == uuid, "private launch keeps both distinct codes exactly as resolved");
        Check(!RobloxLaunchTarget.TryReadAccessCode(null, out accessCode), "missing private page handled without crash");
        Reject(() => RobloxLaunchTarget.PrivateLauncherUrl(42, "", numericInvite), "incomplete private parameters fail before client launch");

        foreach (string name in new[] { "../bad.dll", "/bad.dll", "C:/bad.dll", "Themes/../../bad.png", "AccountData.json", "RAMSettings.ini", "RAMTheme.ini", "FavoritePlaces.json", "AccountFolders.json" })
            Check(!FileUpdateTransaction.Allowed(name), "updater excludes traversal and personal data: " + name);
        Check(!ManagerUpdates.IsAssetUrl("https://github.com/other/repo/releases/download/v9/app.zip") && !ManagerUpdates.IsAssetUrl("https://github.com.example.org/surgee1/manager/releases/download/v9/app.zip"), "updates confined to configured GitHub repository");
        var release = ManagerUpdates.ParseRelease(JObject.Parse("{\"tag_name\":\"v6.1.0\",\"body\":\"<!-- surgee-extension:0.4.0 -->\",\"assets\":[{\"name\":\"Surgee-Manager-v6.1.zip\",\"browser_download_url\":\"https://github.com/surgee1/manager/releases/download/v6.1.0/Surgee-Manager-v6.1.zip\"}]}"));
        Check(release.Version == "6.1.0" && release.ExtensionVersion == "0.4.0", "release versions parsed for app and extension");
        release = ManagerUpdates.ParseRelease(JObject.Parse("{\"tag_name\":\"v6.2.0\",\"body\":\"<!-- surgee-extension:0.4.0 -->\",\"assets\":[{\"name\":\"Spectrum-Manager-v6.2.0.zip\",\"browser_download_url\":\"https://github.com/surgee1/manager/releases/download/v6.2.0/Spectrum-Manager-v6.2.0.zip\"},{\"name\":\"Surgee-Manager-v6.2.0.zip\",\"browser_download_url\":\"https://github.com/surgee1/manager/releases/download/v6.2.0/Surgee-Manager-v6.2.0.zip\"},{\"name\":\"Spectrum-Browser-Extension.zip\",\"digest\":\"sha256:" + new string('a', 64) + "\",\"browser_download_url\":\"https://github.com/surgee1/manager/releases/download/v6.2.0/Spectrum-Browser-Extension.zip\"},{\"name\":\"Surgee-Browser-Extension.zip\",\"browser_download_url\":\"https://github.com/surgee1/manager/releases/download/v6.2.0/Surgee-Browser-Extension.zip\"}]}"));
        Check(release.ManagerUrl.Contains("Spectrum-Manager") && release.ExtensionUrl.EndsWith("Spectrum-Browser-Extension.zip") && release.ExtensionHash == new string('a', 64), "Spectrum package names take precedence while legacy assets remain compatible");

        string root = Path.Combine(Path.GetTempPath(), "SurgeeTests", Guid.NewGuid().ToString("N"));
        string stage = Path.Combine(root, "stage"), destination = Path.Combine(root, "app");
        Directory.CreateDirectory(stage); Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(stage, "Roblox Account Manager.exe"), "new-version");
        File.WriteAllText(Path.Combine(stage, "test.dll"), "new-library");
        File.WriteAllText(Path.Combine(destination, "Roblox Account Manager.exe"), "old-version");
        File.WriteAllText(Path.Combine(destination, "test.dll"), "old-library");
        File.WriteAllText(Path.Combine(destination, "AccountData.json"), "synthetic-user-data");
        File.WriteAllText(Path.Combine(destination, "RAMSettings.ini"), "synthetic-settings");
        var files = new Dictionary<string, string> { ["Roblox Account Manager.exe"] = FileUpdateTransaction.Hash(Path.Combine(stage, "Roblox Account Manager.exe")), ["test.dll"] = FileUpdateTransaction.Hash(Path.Combine(stage, "test.dll")) };
        FileUpdateTransaction.Apply(stage, destination, files);
        Check(File.ReadAllText(Path.Combine(destination, "Roblox Account Manager.exe")) == "new-version" && File.ReadAllText(Path.Combine(destination, "AccountData.json")) == "synthetic-user-data" && File.ReadAllText(Path.Combine(destination, "RAMSettings.ini")) == "synthetic-settings", "transaction updates app and preserves accounts and settings");
        File.WriteAllText(Path.Combine(destination, "Roblox Account Manager.exe"), "before-rollback");
        using (var locked = new FileStream(Path.Combine(destination, "test.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
            Reject(() => FileUpdateTransaction.Apply(stage, destination, files), "locked DLL aborts update");
        Check(File.ReadAllText(Path.Combine(destination, "Roblox Account Manager.exe")) == "before-rollback", "failed update rolls back already replaced executable");
        files["test.dll"] = new string('a', 64);
        Reject(() => FileUpdateTransaction.Apply(stage, destination, files), "hash mismatch aborts before mutation");
        Check(File.ReadAllText(Path.Combine(destination, "Roblox Account Manager.exe")) == "before-rollback", "bad hash leaves previous app untouched");

        var manifest = new JObject { ["kind"] = "surgee-manager", ["version"] = "6.0.0", ["files"] = new JObject { ["Roblox Account Manager.exe"] = FileUpdateTransaction.Hash(Path.Combine(stage, "Roblox Account Manager.exe")), ["SurgeeUpdater.exe"] = FileUpdateTransaction.Hash(Path.Combine(stage, "test.dll")) } };
        string archive = Path.Combine(root, "package.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("Manager/surgee-release.json").Open())) writer.Write(manifest.ToString());
            zip.CreateEntryFromFile(Path.Combine(stage, "Roblox Account Manager.exe"), "Manager/Roblox Account Manager.exe");
            zip.CreateEntryFromFile(Path.Combine(stage, "test.dll"), "Manager/SurgeeUpdater.exe");
        }
        string prepared = ManagerUpdates.Stage(archive);
        Check(File.Exists(Path.Combine(prepared, "update-plan.tsv")), "valid update ZIP is verified and staged");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        { zip.GetEntry("Manager/SurgeeUpdater.exe").Delete(); using (var writer = new StreamWriter(zip.CreateEntry("Manager/SurgeeUpdater.exe").Open())) writer.Write("corrupted"); }
        Reject(() => ManagerUpdates.Stage(archive), "corrupted ZIP content rejected");
    }
    static void PortableBackupChecks()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SurgeeBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var record = new JObject { ["Username"] = "SyntheticUser", ["UserID"] = 42, ["SecurityToken"] = "SYNTHETIC_SESSION",
            ["Alias"] = "Основной", ["Description"] = "Заметки 🌙", ["Password"] = "SYNTHETIC_PASSWORD", ["Group"] = "Папка",
            ["LastUse"] = "2026-11-04T00:00:00Z", ["Fields"] = new JObject { ["custom"] = "Тест" } };
        var account = RBX_Alt_Manager.Account.FromPortableRecord(record);
        var backup = PortableBackups.Capture(new[] { account }, new[] { "Папка", "Пустая папка" });
        string export = Path.Combine(directory, "manual" + PortableBackups.Extension);
        PortableBackups.Write(export, backup);
        var read = PortableBackups.Read(export);
        Check(read.Hash == backup.Hash && read.Accounts.Count == 1, "portable backup survives JSON roundtrip without Windows keys");
        Check(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(export)).Contains("SYNTHETIC_SESSION"), "portable copy is readable independently of DPAPI");
        int added;
        var restored = PortableBackups.Merge(new RBX_Alt_Manager.Account[0], read, out added);
        Check(added == 1 && restored[0].SecurityToken == "SYNTHETIC_SESSION" && restored[0].Password == "SYNTHETIC_PASSWORD", "restore on empty manager keeps session and saved password");
        Check(restored[0].Alias == "Основной" && restored[0].Description == "Заметки 🌙" && restored[0].Fields["custom"] == "Тест" && restored[0].Group == "Папка", "restore preserves unicode metadata, custom fields and folder");
        Check(read.Folders.Count == 2, "empty folders included in portable copy");
        var freshRecord = (JObject)record.DeepClone(); freshRecord["SecurityToken"] = "FRESH_SESSION"; freshRecord["Username"] = "RenamedUser"; freshRecord["Alias"] = "";
        var fresh = RBX_Alt_Manager.Account.FromPortableRecord(freshRecord);
        var merged = PortableBackups.Merge(new[] { fresh }, read, out added);
        Check(added == 0 && merged.Count == 1 && merged[0].SecurityToken == "FRESH_SESSION" && merged[0].Username == "RenamedUser" && merged[0].Alias == "Основной", "merge matches ID, preserves current session and fills missing metadata");
        merged = PortableBackups.Merge(merged, read, out added);
        Check(added == 0 && merged.Count == 1, "repeated restore is idempotent");
        var noId = (JObject)record.DeepClone(); noId["UserID"] = 0; noId["Username"] = "syntheticuser";
        Check(PortableBackups.Merge(new[] { RBX_Alt_Manager.Account.FromPortableRecord(noId) }, read, out added).Count == 1 && added == 0, "missing ID falls back to case-insensitive username");
        string corrupted = Path.Combine(directory, "bad.json");
        File.WriteAllText(corrupted, File.ReadAllText(export).Replace("SYNTHETIC_SESSION", "DAMAGED_SESSION"));
        Reject(() => PortableBackups.Read(corrupted), "tampered or damaged portable copy rejected");
        File.WriteAllText(corrupted, "{\"format\":"); Reject(() => PortableBackups.Read(corrupted), "truncated backup rejected");
        File.WriteAllText(corrupted, "{\"format\":\"surgee-portable-backup\",\"version\":99}"); Reject(() => PortableBackups.Read(corrupted), "unknown backup version rejected");
        File.WriteAllText(corrupted, "{\"format\":\"a\",\"format\":\"b\"}"); Reject(() => PortableBackups.Read(corrupted), "duplicate JSON keys rejected");
        var badRecord = (JObject)record.DeepClone(); badRecord["Alias"] = new string('a', 51);
        Reject(() => RBX_Alt_Manager.Account.FromPortableRecord(badRecord), "overlong metadata rejected before database mutation");
        string prior = File.ReadAllText(export);
        using (var locked = new FileStream(export, FileMode.Open, FileAccess.Read, FileShare.Read))
            Reject(() => PortableBackups.Write(export, read), "locked export fails safely");
        Check(File.ReadAllText(export) == prior, "failed atomic export leaves previous backup intact");
        string automatic = PortableBackups.WriteAutomatic(directory, read, 3);
        Check(PortableBackups.WriteAutomatic(directory, read, 3) == automatic, "unchanged same-day data does not create duplicate automatic copies");
        File.WriteAllText(Path.Combine(directory, "auto-do-not-delete" + PortableBackups.Extension), "user file");
        for (int i = 0; i < 5; i++)
        {
            var changed = PortableBackups.Capture(new[] { account }, new[] { "Папка", "Changed " + i });
            PortableBackups.WriteAutomatic(directory, changed, 3);
        }
        Check(Directory.GetFiles(directory, "auto-*" + PortableBackups.Extension).Length == 4, "automatic retention keeps three generated copies and ignores unrelated files");
        Check(File.Exists(export) && File.ReadAllText(export) == prior, "retention never deletes manual export");
        var profile = new LaunchProfile { Name = "Вечер", PlaceId = "123456789", Server = "https://www.roblox.com/share?code=SYNTHETIC&type=Server", Folder = "Папка" };
        string profilesPath = Path.Combine(directory, "LaunchProfiles.json");
        LaunchProfiles.Save(profilesPath, new List<LaunchProfile> { profile });
        var profiles = LaunchProfiles.Read(profilesPath);
        Check(profiles.Count == 1 && profiles[0].Server == profile.Server && profiles[0].Folder == profile.Folder, "launch profile preserves full private URL and folder after reload");
        Check(LaunchProfiles.Upsert(profiles, profile).Count == 1, "saving same launch profile replaces instead of duplicating");
        var invalidProfile = new LaunchProfile { Name = "Invalid", PlaceId = "not-an-id" };
        Reject(() => LaunchProfiles.Upsert(profiles, invalidProfile), "invalid launch profile rejected before saving");
        Check(LaunchProfiles.Read(profilesPath).Count == 1, "invalid profile leaves saved profiles unchanged");
    }
}
