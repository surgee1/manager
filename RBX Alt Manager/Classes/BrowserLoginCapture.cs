using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class BrowserSignInResult : IDisposable
    {
        internal string Cookie, Username, Password;
        public void Dispose() { Cookie = null; Password = null; }
    }
    internal sealed class BrowserLoginCapture : IDisposable
    {
        private readonly object sync = new object();
        private string username, password;
        private bool disposed;
        internal async Task AttachAsync(IPage page)
        {
            await page.ExposeFunctionAsync("spectrumLoginSubmit", new Func<string, string, bool>((name, secret) =>
            {
                if (Uri.TryCreate(page.Url, UriKind.Absolute, out var url) && url.Scheme == "https" && url.Host == "www.roblox.com" && url.AbsolutePath.TrimEnd('/') == "/login")
                    Record(name, secret);
                return true;
            })).ConfigureAwait(false);
            await page.EvaluateFunctionOnNewDocumentAsync(@"() => {
                if (window.top !== window || location.origin !== 'https://www.roblox.com' || !/^\/login\/?$/.test(location.pathname)) return;
                const record = event => {
                    const u=document.querySelector('#login-username'), p=document.querySelector('#login-password');
                    if (!u || !p || !event.isTrusted) return;
                    if (event.type==='click' && !event.target.closest?.('#login-button')) return;
                    if (event.type==='submit' && !event.target.contains(p)) return;
                    if (event.type==='keydown' && (event.key!=='Enter' || ![u,p].includes(event.target))) return;
                    window.spectrumLoginSubmit(u.value.trim(),p.value).catch(()=>{});
                };
                for (const type of ['click','submit','keydown']) document.addEventListener(type,record,true);
            }").ConfigureAwait(false);
        }
        internal void Record(string name, string secret)
        {
            lock (sync)
            {
                if (disposed) return;
                username = null; password = null;
                if (!System.Text.RegularExpressions.Regex.IsMatch(name ?? "", "^[A-Za-z0-9_]{3,20}$") || string.IsNullOrEmpty(secret) || secret.Length > 1024) return;
                username = name; password = secret;
            }
        }
        internal string PasswordFor(string verifiedName)
        { lock (sync) return !disposed && string.Equals(username, verifiedName, StringComparison.OrdinalIgnoreCase) ? password : null; }
        public void Dispose() { lock (sync) { disposed = true; username = password = null; } }
    }
}
