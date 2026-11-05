using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class CredentialImportResult
    { internal string Username; internal long UserId; internal string Warning; }
    internal sealed class CredentialLoginJobs
    {
        internal sealed class Details { internal string Username, PasswordToSave; }
        private sealed class Job
        {
            internal string Username, Password, PasswordToSave; internal DateTime Expires;
            internal bool Consumed, Cancelled; internal CredentialImportResult Imported;
            internal readonly TaskCompletionSource<CredentialImportResult> Completion = new TaskCompletionSource<CredentialImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal void Wipe() { Password = null; PasswordToSave = null; }
        }
        private readonly Dictionary<string, Job> jobs = new Dictionary<string, Job>();
        private readonly Func<DateTime> clock;
        internal CredentialLoginJobs(Func<DateTime> clock = null) { this.clock = clock ?? (() => DateTime.UtcNow); }
        internal string Create(CredentialPair pair, bool savePassword)
        {
            lock (jobs)
            {
                foreach (string expired in jobs.Where(item => item.Value.Expires <= clock()).Select(item => item.Key).ToArray()) Remove(expired);
                if (jobs.Count >= 4) throw new InvalidOperationException("Дождись завершения текущего входа.");
                byte[] bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                string nonce = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                jobs.Add(nonce, new Job { Username = pair.Username, Password = pair.Password, PasswordToSave = savePassword ? pair.Password : null, Expires = clock().AddMinutes(5) });
                return nonce;
            }
        }
        private Job Active(string nonce)
        {
            if (nonce == null || !Regex.IsMatch(nonce, "^[a-f0-9]{64}$") || !jobs.TryGetValue(nonce, out var job) || job.Cancelled || job.Expires <= clock() || job.Completion.Task.IsCompleted)
                throw new InvalidOperationException("Вход отменён или ссылка устарела. Начни импорт ещё раз.");
            return job;
        }
        internal object Consume(string nonce)
        {
            lock (jobs)
            {
                var job = Active(nonce);
                if (job.Consumed) throw new InvalidOperationException("Ссылка входа уже использована.");
                job.Consumed = true;
                object result = new { ok = true, username = job.Username, password = job.Password };
                job.Password = null; return result;
            }
        }
        internal Details Inspect(string nonce)
        {
            lock (jobs)
            {
                var job = Active(nonce);
                if (!job.Consumed) throw new InvalidOperationException("Сначала открой страницу входа из менеджера.");
                if (job.Imported != null) throw new InvalidOperationException("Аккаунт уже импортирован.");
                return new Details { Username = job.Username, PasswordToSave = job.PasswordToSave };
            }
        }
        internal bool IsActive(string nonce)
        { lock (jobs) { try { Active(nonce); return true; } catch (InvalidOperationException) { return false; } } }
        internal void Imported(string nonce, CredentialImportResult result)
        { lock (jobs) { var job = Active(nonce); if (result == null || result.UserId <= 0 || !string.Equals(job.Username, result.Username, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Roblox открыл другой аккаунт. Импорт отменён."); job.Imported = result; job.Wipe(); } }
        internal void Finish(string nonce, bool restoreFailed)
        {
            lock (jobs)
            {
                var job = Active(nonce);
                if (job.Imported == null) throw new InvalidOperationException("Аккаунт ещё не подтверждён Roblox.");
                if (restoreFailed) job.Imported.Warning = "Аккаунт добавлен; проверь прежний вход в основном браузере.";
                job.Wipe(); job.Completion.TrySetResult(job.Imported);
            }
        }
        internal void Cancel(string nonce)
        {
            lock (jobs)
            {
                if (nonce == null || !jobs.TryGetValue(nonce, out var job)) return;
                job.Cancelled = true; job.Wipe();
                job.Completion.TrySetException(new InvalidOperationException("Вход не завершён. Аккаунты в менеджере сохранены."));
            }
        }
        internal async Task<CredentialImportResult> WaitAsync(string nonce, CancellationToken cancel)
        {
            Task<CredentialImportResult> completion;
            lock (jobs) completion = Active(nonce).Completion.Task;
            using (cancel.Register(() => Cancel(nonce)))
            {
                if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromMinutes(5), cancel)) != completion) Cancel(nonce);
                cancel.ThrowIfCancellationRequested();
                return await completion;
            }
        }
        internal void Remove(string nonce)
        { lock (jobs) { if (!jobs.TryGetValue(nonce, out var job)) return; Cancel(nonce); job.Wipe(); jobs.Remove(nonce); } }
    }
}
