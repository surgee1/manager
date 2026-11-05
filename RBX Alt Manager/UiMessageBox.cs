using RBX_Alt_Manager.Classes;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    internal static class UiMessageBox
    {
        private static string Caption(string text) => text == "Roblox Account Manager" ? "Spectrum Manager" : UiText.Translate(text);
        internal static DialogResult Show(string text, string caption = "Spectrum Manager", MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1, MessageBoxOptions options = 0)
            => MessageBox.Show(UiText.Translate(text), Caption(caption), buttons, icon, defaultButton, options);
        internal static DialogResult Show(IWin32Window owner, string text, string caption = "Spectrum Manager", MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1, MessageBoxOptions options = 0)
            => MessageBox.Show(owner, UiText.Translate(text), Caption(caption), buttons, icon, defaultButton, options);
    }
}
