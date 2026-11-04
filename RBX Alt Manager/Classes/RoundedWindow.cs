using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RBX_Alt_Manager.Forms;

namespace RBX_Alt_Manager.Classes
{
    // Preserve native move, resize and system commands; replace the complete non-client appearance.
    internal sealed class RoundedWindow : NativeWindow
    {
        private static readonly ConditionalWeakTable<Form, RoundedWindow> Windows = new ConditionalWeakTable<Form, RoundedWindow>();
        private readonly Form form;
        private readonly Size contentSize, originalMinimum, originalFrame;
        private readonly int initialDpi;
        private bool initialized, stateUpdateQueued;
        private Size lastSize;
        private int lastDpi, hover, pressed;
        private FormWindowState lastState;
        private int Border => Math.Max(1, form.DeviceDpi / 96);
        private int Caption => Scale(42);
        private int MaximizedInset => Handle != IntPtr.Zero && IsZoomed(Handle) ? Math.Max(0, (int)Math.Round(originalFrame.Width * form.DeviceDpi / (initialDpi * 2f))) : 0;
        private Rectangle FrameBounds => new Rectangle(MaximizedInset, MaximizedInset, form.Width - MaximizedInset * 2, form.Height - MaximizedInset * 2);
        private int Scale(int value) => Math.Max(1, (int)Math.Round(value * form.DeviceDpi / 96f));
        private bool Resizable => form.FormBorderStyle == FormBorderStyle.Sizable || form.FormBorderStyle == FormBorderStyle.SizableToolWindow;
        internal static void Attach(Form form) => Windows.GetValue(form, window => new RoundedWindow(window));

        private RoundedWindow(Form form)
        {
            this.form = form;
            initialDpi = form.DeviceDpi;
            contentSize = form.ClientSize; originalMinimum = form.MinimumSize;
            originalFrame = new Size(form.Width - form.ClientSize.Width, form.Height - form.ClientSize.Height);
            form.HandleCreated += (sender, args) => InitializeHandle();
            form.HandleDestroyed += (sender, args) => ReleaseHandle();
            form.SizeChanged += (sender, args) => { UpdateShape(); PaintFrame(); QueueStateUpdate(); };
            form.DpiChanged += (sender, args) => { UpdateShape(true); RefreshMetrics(); };
            form.TextChanged += (sender, args) => PaintFrame();
            form.BackColorChanged += (sender, args) => PaintFrame();
            form.ForeColorChanged += (sender, args) => PaintFrame();
            if (form.IsHandleCreated) InitializeHandle();
        }

        private void InitializeHandle()
        {
            AssignHandle(form.Handle);
            int disabled = 1;
            try { DwmSetWindowAttribute(Handle, 2, ref disabled, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            if (!initialized)
            {
                initialized = true;
                if (!originalMinimum.IsEmpty)
                    form.MinimumSize = new Size(Math.Max(100, originalMinimum.Width - originalFrame.Width + Border * 2),
                        Math.Max(100, originalMinimum.Height - originalFrame.Height + Caption + Border));
                RefreshMetrics();
                form.Size = new Size(contentSize.Width + Border * 2, contentSize.Height + Caption + Border);
            }
            else RefreshMetrics();
            UpdateShape(true);
        }

        private void RefreshMetrics()
        {
            if (Handle == IntPtr.Zero) return;
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0020 | 0x0010 | 0x0004 | 0x0002 | 0x0001);
            PaintFrame();
        }

        private void UpdateShape(bool force = false)
        {
            if (form.IsDisposed || !form.IsHandleCreated) return;
            int dpi = form.DeviceDpi;
            if (!force && form.Size == lastSize && form.WindowState == lastState && dpi == lastDpi) return;
            lastSize = form.Size; lastState = form.WindowState; lastDpi = dpi;
            Region replacement = null;
            if (form.TopLevel && form.WindowState == FormWindowState.Normal && form.Width > 0 && form.Height > 0)
                using (var path = NeonButton.Round(new RectangleF(0, 0, form.Width, form.Height), Scale(16)))
                    replacement = new Region(path);
            var previous = form.Region;
            form.Region = replacement;
            previous?.Dispose();
        }

        private Rectangle ButtonBounds(int hit)
        {
            if (!form.ControlBox || (hit == 8 && !form.MinimizeBox) || (hit == 9 && !form.MaximizeBox)) return Rectangle.Empty;
            int index = hit == 20 ? 0 : hit == 9 ? 1 : 1 + (form.MaximizeBox ? 1 : 0);
            var frame = FrameBounds;
            return new Rectangle(frame.Right - Scale(10) - Scale(42) * (index + 1), frame.Top + Scale(6), Scale(38), Scale(30));
        }

        private int ButtonAt(Point point)
        {
            foreach (int hit in new[] { 20, 9, 8 }) if (ButtonBounds(hit).Contains(point)) return hit;
            return 0;
        }

        private int HitTest(Point point)
        {
            if (Resizable && form.WindowState == FormWindowState.Normal)
            {
                int edge = Scale(6);
                bool left = point.X < edge, right = point.X >= form.Width - edge;
                bool top = point.Y < edge, bottom = point.Y >= form.Height - edge;
                if (top && left) return 13; if (top && right) return 14;
                if (bottom && left) return 16; if (bottom && right) return 17;
                if (left) return 10; if (right) return 11; if (top) return 12; if (bottom) return 15;
            }
            int button = ButtonAt(point);
            return button != 0 ? button : point.Y < Caption + MaximizedInset ? 2 : 1;
        }

        private Point WindowPoint(IntPtr coordinates, bool client = false)
        {
            long value = coordinates.ToInt64();
            var point = new Point((short)(value & 0xffff), (short)((value >> 16) & 0xffff));
            if (client) { point.Offset(Border + MaximizedInset, Caption + MaximizedInset); return point; }
            GetWindowRect(Handle, out Rect bounds); point.Offset(-bounds.Left, -bounds.Top); return point;
        }

        private void PaintFrame()
        {
            if (Handle == IntPtr.Zero || form.IsDisposed || form.WindowState == FormWindowState.Minimized) return;
            IntPtr dc = GetWindowDC(Handle); if (dc == IntPtr.Zero) return;
            try { using (var graphics = Graphics.FromHdc(dc)) DrawFrame(graphics); }
            finally { ReleaseDC(Handle, dc); }
        }

        private void QueueStateUpdate()
        {
            if (stateUpdateQueued || form.IsDisposed || !form.IsHandleCreated) return;
            stateUpdateQueued = true;
            try { form.BeginInvoke(new Action(() => { stateUpdateQueued = false; if (!form.IsDisposed) { UpdateShape(); PaintFrame(); } })); }
            catch (InvalidOperationException) { stateUpdateQueued = false; }
        }

        private void DrawFrame(Graphics graphics)
        {
            var state = graphics.Save();
            try
            {
                var frame = FrameBounds;
                graphics.ExcludeClip(new Rectangle(frame.Left + Border, frame.Top + Caption, Math.Max(0, frame.Width - Border * 2), Math.Max(0, frame.Height - Caption - Border)));
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color surface = NeonButton.Mix(Color.FromArgb(19, 21, 26), ThemeEditor.FormsBackground, .20f);
                using (var background = new SolidBrush(surface)) graphics.FillRectangle(background, 0, 0, form.Width, form.Height);
                using (var outline = new Pen(Color.FromArgb(54, 59, 70), 1))
                using (var path = NeonButton.Round(new RectangleF(frame.Left + .5f, frame.Top + .5f, frame.Width - 1, frame.Height - 1), form.WindowState == FormWindowState.Maximized ? .5f : Scale(16)))
                    graphics.DrawPath(outline, path);
                int titleRight = frame.Right - Scale(22) - Scale(42) * (1 + (form.MaximizeBox ? 1 : 0) + (form.MinimizeBox ? 1 : 0));
                using (var font = new Font("Segoe UI Semibold", Scale(13), FontStyle.Regular, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(graphics, form.Text, font, new Rectangle(frame.Left + Scale(18), frame.Top, Math.Max(10, titleRight - frame.Left - Scale(18)), Caption),
                        Color.FromArgb(177, 187, 202), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (form.ControlBox)
                {
                    int count = 1 + (form.MaximizeBox ? 1 : 0) + (form.MinimizeBox ? 1 : 0);
                    var group = new Rectangle(frame.Right - Scale(10) - Scale(42) * count, frame.Top + Scale(6), Scale(42) * count - Scale(4), Scale(30));
                    using (var fill = new SolidBrush(NeonButton.Mix(surface, Color.White, .025f)))
                    using (var path = NeonButton.Round(group, Scale(9))) graphics.FillPath(fill, path);
                }
                foreach (int hit in new[] { 8, 9, 20 })
                {
                    Rectangle bounds = ButtonBounds(hit); if (bounds.IsEmpty) continue;
                    if (hover == hit || pressed == hit)
                        using (var brush = new SolidBrush(hit == 20 ? Color.FromArgb(147, 49, 70) : Color.FromArgb(43, 48, 59)))
                        using (var path = NeonButton.Round(bounds, Scale(8))) graphics.FillPath(brush, path);
                    float x = bounds.Left + bounds.Width / 2f, y = bounds.Top + bounds.Height / 2f, half = Scale(5);
                    using (var pen = new Pen(Color.FromArgb(188, 200, 215), Math.Max(1.4f, form.DeviceDpi / 75f)))
                    {
                        if (hit == 8) graphics.DrawLine(pen, x - half, y + 2, x + half, y + 2);
                        else if (hit == 20) { graphics.DrawLine(pen, x - half, y - half, x + half, y + half); graphics.DrawLine(pen, x + half, y - half, x - half, y + half); }
                        else if (form.WindowState == FormWindowState.Maximized)
                        {
                            graphics.DrawRectangle(pen, x - half + 3, y - half - 2, half * 2 - 1, half * 2 - 1);
                            using (var fill = new SolidBrush(surface)) graphics.FillRectangle(fill, x - half - 1, y - half + 2, half * 2, half * 2);
                            graphics.DrawRectangle(pen, x - half - 1, y - half + 2, half * 2 - 1, half * 2 - 1);
                        }
                        else graphics.DrawRectangle(pen, x - half, y - half, half * 2, half * 2);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        protected override void WndProc(ref Message message)
        {
            switch (message.Msg)
            {
                case 0x0083: // RECT is also the first field of NCCALCSIZE_PARAMS.
                    var rectangle = (Rect)Marshal.PtrToStructure(message.LParam, typeof(Rect));
                    int inset = MaximizedInset;
                    rectangle.Left += Border + inset; rectangle.Right -= Border + inset; rectangle.Top += Caption + inset; rectangle.Bottom -= Border + inset;
                    Marshal.StructureToPtr(rectangle, message.LParam, false); message.Result = IntPtr.Zero; return;
                case 0x0084: message.Result = (IntPtr)HitTest(WindowPoint(message.LParam)); return;
                case 0x0085: PaintFrame(); message.Result = IntPtr.Zero; return;
                case 0x0086: PaintFrame(); message.Result = (IntPtr)1; return;
                case 0x00AE: case 0x00AF: message.Result = IntPtr.Zero; return;
                case 0x00A0:
                    int next = ButtonAt(WindowPoint(message.LParam));
                    if (next != hover) { hover = next; PaintFrame(); }
                    var track = new TrackMouse { Size = Marshal.SizeOf(typeof(TrackMouse)), Flags = 0x12, Window = Handle };
                    TrackMouseEvent(ref track); break;
                case 0x02A2: hover = 0; PaintFrame(); break;
                case 0x00A1:
                    pressed = ButtonAt(WindowPoint(message.LParam));
                    if (pressed != 0) { SetCapture(Handle); PaintFrame(); message.Result = IntPtr.Zero; return; }
                    break;
                case 0x00A3:
                    if (ButtonAt(WindowPoint(message.LParam)) != 0) { message.Result = IntPtr.Zero; return; }
                    break;
                case 0x0202: case 0x00A2:
                    if (pressed != 0)
                    {
                        int action = pressed, released = ButtonAt(WindowPoint(message.LParam, message.Msg == 0x0202));
                        pressed = 0; ReleaseCapture(); PaintFrame();
                        if (action == released) PostMessage(Handle, 0x0112, (IntPtr)(action == 20 ? 0xF060 : action == 8 ? 0xF020 : form.WindowState == FormWindowState.Maximized ? 0xF120 : 0xF030), IntPtr.Zero);
                        message.Result = IntPtr.Zero; return;
                    }
                    break;
                case 0x0215: if (pressed != 0) { pressed = 0; PaintFrame(); } break;
            }
            base.WndProc(ref message);
            if (message.Msg == 0x0005) { UpdateShape(); QueueStateUpdate(); }
            if (message.Msg == 0x000C || message.Msg == 0x0005 || message.Msg == 0x007D) PaintFrame();
            if (message.Msg == 0x0317 && (message.LParam.ToInt64() & 2) != 0)
                using (var graphics = Graphics.FromHdc(message.WParam)) DrawFrame(graphics);
        }

        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct TrackMouse { public int Size, Flags; public IntPtr Window; public int HoverTime; }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect bounds);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern bool TrackMouseEvent(ref TrackMouse track);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
    }
}
