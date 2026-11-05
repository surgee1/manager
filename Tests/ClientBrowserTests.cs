using PuppeteerSharp;
using RBX_Alt_Manager.Classes;
using System;
using System.Linq;
using System.Threading.Tasks;

internal static class ClientBrowserTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        var browser = new IsolatedAccountBrowser();
        try
        {
            var first = await browser.CreateWindowAsync(); var second = await browser.CreateWindowAsync();
            check(first.BrowserContext.Id != second.BrowserContext.Id, "Chromium accounts have independent browser contexts");
            await first.SetCookieAsync(new CookieParam { Name = "fixture", Value = "account-one", Domain = "example.test", Path = "/", HttpOnly = true, Secure = true });
            await second.SetCookieAsync(new CookieParam { Name = "fixture", Value = "account-two", Domain = "example.test", Path = "/", HttpOnly = true, Secure = true });
            var one = await first.GetCookiesAsync("https://example.test/"); var two = await second.GetCookiesAsync("https://example.test/");
            check(one.Single(cookie => cookie.Name == "fixture").Value == "account-one" && two.Single(cookie => cookie.Name == "fixture").Value == "account-two", "Sessions do not mix between separate windows");
            var sessionOne = await first.Target.CreateCDPSessionAsync(); var sessionTwo = await second.Target.CreateCDPSessionAsync();
            var windowOne = await sessionOne.SendAsync("Browser.getWindowForTarget"); var windowTwo = await sessionTwo.SendAsync("Browser.getWindowForTarget");
            check(windowOne.Value<int>("windowId") != windowTwo.Value<int>("windowId"), "Each account opens in its own native Chromium window");
            await sessionOne.DetachAsync(); await sessionTwo.DetachAsync();
            await first.BrowserContext.CloseAsync();
            check((await second.GetCookiesAsync("https://example.test/")).Single(cookie => cookie.Name == "fixture").Value == "account-two", "Closing one account leaves the other session intact");
            using (var capture = new BrowserLoginCapture())
            {
                await capture.AttachAsync(second);
                await second.SetRequestInterceptionAsync(true);
                second.Request += async (sender, args) => await args.Request.RespondAsync(new ResponseData { Status = System.Net.HttpStatusCode.OK, ContentType = "text/html", Body = "<form onsubmit='return false'><input id=login-username><input id=login-password type=password><button id=login-button type=submit>Login</button></form>" });
                await second.GoToAsync("https://www.roblox.com/login");
                await second.TypeAsync("#login-username", "Fixture_User");
                await second.TypeAsync("#login-password", "SYNTHETIC_PASSWORD");
                await second.ClickAsync("#login-button");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (capture.PasswordFor("Fixture_User") == null && DateTime.UtcNow < deadline) await Task.Delay(50);
                check(capture.PasswordFor("Fixture_User") == "SYNTHETIC_PASSWORD", "Real Chromium login submission captures password before navigation");
                check(capture.PasswordFor("Other_User") == null, "Chromium login capture rejects a different authenticated identity");
            }
        }
        finally { await browser.CloseAsync(); }
    }
}
