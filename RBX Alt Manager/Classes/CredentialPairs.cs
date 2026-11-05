using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class CredentialPair : IDisposable
    {
        internal string Username { get; }
        internal string Password { get; private set; }
        internal CredentialPair(string username, string password) { Username = username; Password = password; }
        public void Dispose() { Password = null; }
        public override string ToString() => Username;
    }
    internal static class CredentialPairs
    {
        internal static List<CredentialPair> Parse(string text)
        {
            if (text == null || text.Length > 200000) throw new InvalidDataException("Слишком большой список user:pass.");
            var pairs = new List<CredentialPair>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                int number = 0;
                foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                {
                    number++; if (string.IsNullOrWhiteSpace(line)) continue;
                    int separator = line.IndexOf(':');
                    string username = separator > 0 ? line.Substring(0, separator).Trim() : "";
                    string password = separator > 0 ? line.Substring(separator + 1) : "";
                    if (!Regex.IsMatch(username, "^[A-Za-z0-9_]{3,20}$") || password.Length == 0 || password.Length > 1024 || password.IndexOfAny(new[] { '\r', '\0' }) >= 0)
                        throw new InvalidDataException("Строка " + number + ": ожидается username:password. Пароль не выводится в сообщениях.");
                    if (!names.Add(username)) throw new InvalidDataException("Строка " + number + ": повторяющийся логин.");
                    pairs.Add(new CredentialPair(username, password));
                    if (pairs.Count > 100) throw new InvalidDataException("За один импорт можно добавить до 100 аккаунтов.");
                }
                if (pairs.Count == 0) throw new InvalidDataException("Вставь хотя бы одну строку username:password.");
                return pairs;
            }
            catch { foreach (var pair in pairs) pair.Dispose(); throw; }
        }
    }
}
