using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private PortableBackup removedAccounts;
        private void RemoveAccountsCore(IEnumerable<Account> selection)
        {
            var selected = selection.Where(account => AccountsList.Contains(account)).Distinct().ToList();
            if (selected.Count == 0) return;
            var undo = PortableBackups.Capture(selected, AllFolderNames());
            // Keep an independent copy even if the app closes before the user clicks Undo.
            PortableBackups.Write(PortableBackups.NewPath(BackupDirectory, "before-delete"), CapturePortableBackup());
            var prior = AccountsList;
            try
            {
                AccountsList = prior.Except(selected).ToList();
                SaveAccounts(true, true);
            }
            catch { AccountsList = prior; throw; }
            removedAccounts = undo;
            LastValidAccount = AccountsList.OrderByDescending(account => account.LastUse).FirstOrDefault();
            SelectedAccounts.Clear(); SelectedAccount = null; SelectedAccountItem = null;
            AccountsView.SelectedObjects = new List<Account>();
            AccountsView.SetObjects(AccountsList); RefreshView();
        }
        private void UndoLastRemoval()
        {
            if (removedAccounts == null) { MessageBox.Show(this, "В этом запуске ещё нечего возвращать. Более старые записи можно восстановить из Backups в настройках.", "Вернуть аккаунты"); return; }
            var prior = AccountsList;
            var next = PortableBackups.Merge(prior, removedAccounts, out int added);
            try { AccountsList = next; SaveAccounts(true, true); }
            catch { AccountsList = prior; throw; }
            removedAccounts = null;
            LastValidAccount = AccountsList.OrderByDescending(account => account.LastUse).FirstOrDefault();
            AccountsView.SetObjects(AccountsList); RefreshView();
        }
    }
}
