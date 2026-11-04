using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class RoundedWindow
    {
        private static readonly ConditionalWeakTable<Form, RoundedWindow> Windows = new ConditionalWeakTable<Form, RoundedWindow>();
        private readonly Form form;
        private bool nativeCorners;
        private Size lastSize;
        private int lastDpi;
        private FormWindowState lastState;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        internal static void Attach(Form form) => Windows.GetValue(form, window => new RoundedWindow(window));

        private RoundedWindow(Form form)
        {
            this.form = form;
            form.HandleCreated += (sender, args) => InitializeHandle();
            form.SizeChanged += (sender, args) => UpdateShape();
            form.DpiChanged += (sender, args) => UpdateShape(true);
            if (form.IsHandleCreated) InitializeHandle();
        }

        private void InitializeHandle()
        {
            int preference = 2; // DWMWCP_ROUND; Windows 10 uses the region fallback below.
            try { nativeCorners = DwmSetWindowAttribute(form.Handle, 33, ref preference, sizeof(int)) == 0; }
            catch (DllNotFoundException) { nativeCorners = false; }
            catch (EntryPointNotFoundException) { nativeCorners = false; }
            UpdateShape(true);
        }

        private void UpdateShape(bool force = false)
        {
            if (form.IsDisposed || !form.IsHandleCreated) return;
            int dpi = form.DeviceDpi;
            if (!force && form.Size == lastSize && form.WindowState == lastState && dpi == lastDpi) return;
            lastSize = form.Size; lastState = form.WindowState; lastDpi = dpi;

            Region replacement = null;
            if (!nativeCorners && form.TopLevel && form.WindowState == FormWindowState.Normal && form.Width > 0 && form.Height > 0)
                using (var path = NeonButton.Round(new RectangleF(0, 0, form.Width, form.Height), 18f * dpi / 96f))
                    replacement = new Region(path);

            // Keep maximized windows rectangular and release the previous GDI region on every resize.
            var previous = form.Region;
            form.Region = replacement;
            previous?.Dispose();
        }
    }
}
