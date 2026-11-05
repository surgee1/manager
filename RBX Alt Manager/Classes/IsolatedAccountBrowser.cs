using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        internal Task<IPage> CreateWindowAsync() => Task.Run(CreateWindowCoreAsync);
        private async Task<IPage> CreateWindowCoreAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            string stage = "запуск браузера";
            try
            {
                await EnsureBrowserAsync().ConfigureAwait(false);
                stage = "создание отдельной сессии";
                var context = await browser.CreateIncognitoBrowserContextAsync().ConfigureAwait(false);
                try
                {
                    stage = "создание окна";
                    var targetWait = context.WaitForTargetAsync(target => target.Type == TargetType.Page, new WaitForOptions { Timeout = 30000 });
                    var session = await browser.Target.CreateCDPSessionAsync().ConfigureAwait(false);
                    try { await session.SendAsync("Target.createTarget", new { url = "about:blank", browserContextId = context.Id, newWindow = true }).ConfigureAwait(false); }
                    finally { await session.DetachAsync().ConfigureAwait(false); }
                    var page = await (await targetWait.ConfigureAwait(false)).PageAsync().ConfigureAwait(false);
                    // Remove the initial empty normal window without touching any account context.
                    foreach (var blank in await browser.DefaultContext.PagesAsync().ConfigureAwait(false))
                        if (blank.Url == "about:blank") await blank.CloseAsync().ConfigureAwait(false);
                    return page;
                }
                catch { try { await context.CloseAsync().ConfigureAwait(false); } catch { } throw; }
            }
            catch (Exception error)
            {
                // Do not include protocol payloads, cookies or browser stderr in user-facing errors.
                throw new InvalidOperationException("Не удалось открыть Chromium. Этап: " + stage + ". Код: " + error.GetType().Name + ".", error);
            }
            finally { gate.Release(); }
        }
        internal async Task<string> SignInAsync(CredentialPair credentials, IProgress<string> progress, CancellationToken cancel)
        {
            var page = await CreateWindowAsync().ConfigureAwait(false);
            using var closeOnCancel = cancel.Register(() => { _ = CloseLoginContextQuietlyAsync(page); });
            try
            {
                progress?.Report("Открываем официальный вход Roblox…");
                await page.GoToAsync("https://www.roblox.com/login", new NavigationOptions { Timeout = 45000, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).ConfigureAwait(false);
                if (credentials != null)
                {
                    await page.WaitForSelectorAsync("#login-password, input[type=password]", new WaitForSelectorOptions { Timeout = 30000 }).ConfigureAwait(false);
                    await page.EvaluateFunctionAsync(@"(username,password) => {
                        const u=document.querySelector('#login-username,input[name=username],input[autocomplete=username]');
                        const p=document.querySelector('#login-password,input[type=password]');
                        if(!u||!p) throw new Error('Login form not found');
                        const setter=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;
                        for(const [element,value] of [[u,username],[p,password]]) { setter.call(element,value); element.dispatchEvent(new Event('input',{bubbles:true})); element.dispatchEvent(new Event('change',{bubbles:true})); }
                    }", credentials.Username, credentials.Password).ConfigureAwait(false);
                    await page.WaitForFunctionAsync("() => { const b=document.querySelector('#login-button'); return b && !b.disabled; }", new WaitForFunctionOptions { Timeout = 10000 }).ConfigureAwait(false);
                    await page.ClickAsync("#login-button").ConfigureAwait(false);
                }
                progress?.Report("Заверши вход в окне Chromium. CAPTCHA и подтверждение выполняются вручную.");
                await page.BringToFrontAsync().ConfigureAwait(false);
                using (var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
                using (var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) })
                {
                    DateTime deadline = DateTime.UtcNow.AddMinutes(4);
                    while (DateTime.UtcNow < deadline)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (page.IsClosed) throw new InvalidOperationException("Окно входа закрыто. Аккаунты в менеджере сохранены.");
                        var cookies = await page.GetCookiesAsync("https://www.roblox.com/").ConfigureAwait(false);
                        string cookie = cookies.FirstOrDefault(item => item.Name == ".ROBLOSECURITY")?.Value;
                        if (!string.IsNullOrWhiteSpace(cookie) && cookie.Length <= 6000 && cookie.IndexOfAny(new[] { ';', '\r', '\n', '\0' }) < 0)
                        {
                            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated"))
                            {
                                request.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                                using (var response = await http.SendAsync(request, cancel).ConfigureAwait(false))
                                {
                                    if (response.StatusCode == HttpStatusCode.OK)
                                    {
                                        var identity = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                                        if (identity.Value<long>("id") > 0 && (credentials == null || string.Equals(credentials.Username, identity.Value<string>("name"), StringComparison.OrdinalIgnoreCase))) return cookie;
                                        throw new InvalidOperationException("Roblox открыл другой аккаунт. Импорт отменён.");
                                    }
                                    if ((int)response.StatusCode == 429) await Task.Delay(15000, cancel).ConfigureAwait(false);
                                }
                            }
                        }
                        await Task.Delay(1500, cancel).ConfigureAwait(false);
                    }
                }
                throw new InvalidOperationException("Вход не завершён за 4 минуты. Проверь логин, пароль и подтверждение Roblox.");
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException) { throw; }
            catch { throw new InvalidOperationException("Не удалось завершить вход в Chromium. Проверь соединение и попробуй снова."); }
            finally { try { await page.BrowserContext.CloseAsync().ConfigureAwait(false); } catch { } }
        }
        private static async Task CloseLoginContextQuietlyAsync(IPage page)
        { try { await page.BrowserContext.CloseAsync().ConfigureAwait(false); } catch { } }
        internal async Task OpenAsync(long userId, string cookie)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(cookie) || cookie.Length > 6000 || cookie.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
                throw new InvalidOperationException("Нет пригодной сессии. Обнови вход через расширение.");
            var page = await CreateWindowAsync().ConfigureAwait(false);
            string stage = "установка сессии";
            try
            {
                await page.SetCookieAsync(new CookieParam { Name = ".ROBLOSECURITY", Value = cookie, Domain = ".roblox.com", Path = "/", Secure = true, HttpOnly = true }).ConfigureAwait(false);
                stage = "проверка аккаунта Roblox";
                var response = await page.GoToAsync("https://users.roblox.com/v1/users/authenticated", new NavigationOptions { Timeout = 30000, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).ConfigureAwait(false);
                if (response == null) throw new InvalidOperationException("Roblox не ответил на проверку входа.");
                if (response.Status == HttpStatusCode.Unauthorized || response.Status == HttpStatusCode.Forbidden)
                    throw new InvalidOperationException("Roblox не подтвердил выбранный аккаунт. Обнови вход через расширение.");
                if ((int)response.Status == 429) throw new InvalidOperationException("Roblox ограничил частоту запросов. Повтори чуть позже.");
                if (response.Status != HttpStatusCode.OK) throw new InvalidOperationException("Проверка входа Roblox: HTTP " + (int)response.Status + ". Повтори позже.");
                if (JObject.Parse(await response.TextAsync().ConfigureAwait(false)).Value<long?>("id") != userId)
                    throw new InvalidOperationException("Roblox открыл другой аккаунт. Окно закрыто; аккаунты в менеджере сохранены.");
                stage = "открытие главной страницы Roblox";
                await page.GoToAsync("https://www.roblox.com/home", new NavigationOptions { Timeout = 45000, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).ConfigureAwait(false);
                await page.BringToFrontAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                try { await page.BrowserContext.CloseAsync().ConfigureAwait(false); } catch { }
                if (error is InvalidOperationException) throw;
                throw new InvalidOperationException("Не удалось открыть Chromium. Этап: " + stage + ". Код: " + error.GetType().Name + ". Аккаунт сохранён.", error);
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
