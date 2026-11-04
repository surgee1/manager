using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class BrowserIntents
    {
        private sealed class Intent { internal long Id; internal DateTime Expires; }
        private readonly Dictionary<string, Intent> pending = new Dictionary<string, Intent>();
        private readonly Func<DateTime> now;
        internal BrowserIntents(Func<DateTime> now = null) { this.now = now ?? (() => DateTime.UtcNow); }
        internal string Create(long id)
        {
            if (id <= 0) throw new InvalidOperationException("У аккаунта нет ID. Обнови его через расширение.");
            lock (pending)
            {
                foreach (string old in pending.Where(pair => pair.Value.Expires <= now()).Select(pair => pair.Key).ToList()) pending.Remove(old);
                if (pending.Count >= 8) pending.Remove(pending.OrderBy(pair => pair.Value.Expires).First().Key);
                var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                string nonce = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                pending.Add(nonce, new Intent { Id = id, Expires = now().AddMinutes(2) });
                return nonce;
            }
        }
        internal long Consume(string nonce)
        {
            lock (pending)
            {
                if (nonce == null || !Regex.IsMatch(nonce, "^[a-f0-9]{64}$") || !pending.TryGetValue(nonce, out var intent))
                    throw new InvalidOperationException("Ссылка входа уже использована или недействительна. Нажми «Браузер» в менеджере ещё раз.");
                pending.Remove(nonce);
                if (intent.Expires <= now()) throw new InvalidOperationException("Срок ссылки входа истёк. Нажми «Браузер» ещё раз.");
                return intent.Id;
            }
        }
    }
}
