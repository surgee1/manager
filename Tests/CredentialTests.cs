using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using System;
using System.Threading;
using System.Threading.Tasks;

internal static class CredentialTests
{
    internal static async Task Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var parsed = CredentialPairs.Parse("\r\nFixture_One:first:second \r\nFixture_Two:other\n");
        check(parsed.Count == 2 && parsed[0].Username == "Fixture_One" && parsed[0].Password == "first:second ", "Credential parser preserves password colons and spaces");
        check(!parsed[0].ToString().Contains("first"), "Credential display omits password");
        foreach (var value in new[] { "", "a:password", "Fixture:abc\0", "Fixture:one\nfixture:two", "Fixture:" })
            reject(() => CredentialPairs.Parse(value), "Malformed or duplicate credential input rejected");
        try { CredentialPairs.Parse("Fixture:PRIVATE_SENTINEL\rbroken"); }
        catch (Exception error) { check(!error.ToString().Contains("PRIVATE_SENTINEL"), "Parser errors do not echo input credentials"); }
        DateTime now = DateTime.UtcNow;
        var jobs = new CredentialLoginJobs(() => now);
        string nonce = jobs.Create(parsed[0], false);
        check(nonce.Length == 64 && !nonce.Contains(parsed[0].Username), "Login URL uses random opaque nonce");
        reject(() => jobs.Inspect(nonce), "Import blocked before one-time credential handoff");
        var consumed = JObject.FromObject(jobs.Consume(nonce));
        check(consumed.Value<string>("username") == "Fixture_One" && consumed.Value<string>("password") == "first:second ", "One-time handoff gives requested credentials");
        reject(() => jobs.Consume(nonce), "Credential handoff cannot be replayed");
        check(jobs.Inspect(nonce).PasswordToSave == null, "Password storage obeys opt-out");
        reject(() => jobs.Imported(nonce, new CredentialImportResult { Username = "Wrong_User", UserId = 9 }), "Wrong identity cannot complete import");
        reject(() => jobs.Finish(nonce, false), "Login cannot finish before verified import");
        var wait = jobs.WaitAsync(nonce, CancellationToken.None);
        jobs.Imported(nonce, new CredentialImportResult { Username = "Fixture_One", UserId = 9 });
        reject(() => jobs.Inspect(nonce), "Verified import cannot be repeated");
        jobs.Finish(nonce, true);
        check((await wait).Warning != null && !jobs.IsActive(nonce), "Login completes with explicit browser restore warning");
        jobs.Remove(nonce);
        nonce = jobs.Create(parsed[1], true);
        jobs.Consume(nonce);
        check(jobs.Inspect(nonce).PasswordToSave == "other", "Password storage obeys explicit opt-in");
        jobs.Cancel(nonce);
        reject(() => jobs.Inspect(nonce), "Cancellation immediately revokes credential job");
        jobs.Remove(nonce);
        nonce = jobs.Create(parsed[1], false); now = now.AddMinutes(6);
        check(!jobs.IsActive(nonce), "Expired login nonce rejected");
        jobs.Remove(nonce);
        foreach (var pair in parsed) pair.Dispose();
        check(parsed[0].Password == null && parsed[1].Password == null, "Credential references cleared on disposal");
        using (var capture = new BrowserLoginCapture())
        {
            capture.Record("Fixture_User", "SYNTHETIC_PASSWORD");
            check(capture.PasswordFor("fixture_user") == "SYNTHETIC_PASSWORD", "Chromium password follows verified username ignoring case");
            check(capture.PasswordFor("Other_User") == null, "Chromium cannot assign a captured password to another identity");
            capture.Record("Fixture_User", "CORRECTED_PASSWORD");
            check(capture.PasswordFor("Fixture_User") == "CORRECTED_PASSWORD", "A corrected login replaces the failed password attempt");
            capture.Dispose(); capture.Record("Fixture_User", "LATE_PASSWORD");
            check(capture.PasswordFor("Fixture_User") == null, "Closing login revokes late callbacks and clears password references");
        }
    }
}
