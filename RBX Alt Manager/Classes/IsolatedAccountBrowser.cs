using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class IsolatedAccountBrowser
    {
        private IBrowser browser;
        private string profile;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        internal static string FindEngine()
        {
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) };
            return roots.SelectMany(root => new[] { Path.Combine(root, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(root, @"Google\Chrome\Application\chrome.exe"), Path.Combine(root, @"Chromium\Application\chrome.exe") }).FirstOrDefault(File.Exists);
        }
        private async Task EnsureBrowserAsync()
        {
            if (browser?.IsConnected == true) return;
            string engine = FindEngine();
            if (engine == null) throw new InvalidOperationException("Для отдельных окон нужен Chrome или Microsoft Edge. Установи один из них и повтори запуск.");
            profile = Path.Combine(Path.GetTempPath(), "Spectrum-Browser-" + Guid.NewGuid().ToString("N"));
            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = engine, Headless = false, UserDataDir = profile, DefaultViewport = null, Timeout = 45000,
                Args = new[] { "--no-first-run", "--no-default-browser-check", "--disable-extensions" }
            }).ConfigureAwait(false);
            // Credentials live only in separate incognito contexts, never the user's browser profile.
        }
        internal async Task<IPage> CreateWindowAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await EnsureBrowserAsync().ConfigureAwait(false);
                var context = await browser.CreateIncognitoBrowserContextAsync().ConfigureAwait(false);
                try
                {
                    var targetWait = context.WaitForTargetAsync(target => target.Type == TargetType.Page, new WaitForOptions { Timeout = 30000 });
                    var session = await browser.Target.CreateCDPSessionAsync().ConfigureAwait(false);
                    try { await session.SendAsync("Target.createTarget", new { url = "about:blank", browserContextId = context.Id, newWindow = true }).ConfigureAwait(false); }
                    finally { await session.DetachAsync().ConfigureAwait(false); }
                    var page = await (await targetWait.ConfigureAwait(false)).PageAsync().ConfigureAwait(false);
                    // Remove the initial empty normal window without touching any account context.
                    foreach (var blank in await browser.DefaultContext.PagesAsync().ConfigureAwait(false)) await blank.CloseAsync().ConfigureAwait(false);
                    return page;
                }
                catch { await context.CloseAsync().ConfigureAwait(false); throw; }
            }
            finally { gate.Release(); }
        }
        internal async Task OpenAsync(long userId, string cookie)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(cookie) || cookie.Length > 6000 || cookie.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
                throw new InvalidOperationException("Нет пригодной сессии. Обнови вход через расширение.");
            var page = await CreateWindowAsync().ConfigureAwait(false);
            try
            {
                await page.SetCookieAsync(new CookieParam { Name = ".ROBLOSECURITY", Value = cookie, Domain = ".roblox.com", Path = "/", Secure = true, HttpOnly = true }).ConfigureAwait(false);
                var response = await page.GoToAsync("https://users.roblox.com/v1/users/authenticated", new NavigationOptions { Timeout = 30000, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).ConfigureAwait(false);
                if (response == null || response.Status != System.Net.HttpStatusCode.OK || JObject.Parse(await response.TextAsync().ConfigureAwait(false)).Value<long?>("id") != userId)
                    throw new InvalidOperationException("Roblox не подтвердил выбранный аккаунт. Обнови вход через расширение.");
                await page.GoToAsync("https://www.roblox.com/home", new NavigationOptions { Timeout = 45000, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).ConfigureAwait(false);
                await page.BringToFrontAsync().ConfigureAwait(false);
            }
            catch
            {
                try { await page.BrowserContext.CloseAsync().ConfigureAwait(false); } catch { }
                throw new InvalidOperationException("Окно не открыто: сессия истекла, Roblox недоступен или требует повторного входа. Аккаунт сохранён в менеджере.");
            }
        }
        internal void Disconnect() { try { browser?.Disconnect(); } catch { } }
        internal async Task CloseAsync()
        {
            if (browser != null) await browser.CloseAsync().ConfigureAwait(false);
            browser = null;
            if (profile != null && Path.GetFileName(profile).StartsWith("Spectrum-Browser-", StringComparison.Ordinal) && Path.GetDirectoryName(profile) == Path.GetTempPath().TrimEnd('\\'))
                try { Directory.Delete(profile, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
