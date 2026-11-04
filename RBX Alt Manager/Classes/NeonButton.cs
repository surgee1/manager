using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    public class NeonButton : Button
    {
        private bool hovered;
        private bool pressed;
        public bool Accent { get; set; }

        public NeonButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
        }

        internal static GraphicsPath Round(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static Color Mix(Color a, Color b, float ratio)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * ratio),
                (int)(a.G + (b.G - a.G) * ratio), (int)(a.B + (b.B - a.B) * ratio));
        }

        internal static float Phase => Forms.ThemeEditor.AnimationsEnabled ? (float)(Math.Sin(VisualBackground.Time * 0.55) * 0.5 + 0.5) : 0.5f;

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnClick(EventArgs e)
        {
            if (!Program.PreviewMode) base.OnClick(e);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width < 8 || Height < 8) return;
            using (var path = Round(new RectangleF(0, 0, Width, Height), Math.Min(13, Height / 3f)))
            {
                var old = Region;
                Region = new Region(path);
                old?.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? BackColor);
            if (Width < 8 || Height < 8) return;
            float phase = Phase;
            Color violet = Mix(Forms.ThemeEditor.AccentPrimary, Forms.ThemeEditor.AccentSecondary, phase * 0.25f);
            Color cyan = Mix(Forms.ThemeEditor.AccentSecondary, Forms.ThemeEditor.AccentPrimary, phase * 0.2f);
            var rect = new RectangleF(2, 2, Width - 5, Height - 5);
            using (var path = Round(rect, Math.Min(13, Height / 3f)))
            {
                Color left = Accent ? violet : Mix(Forms.ThemeEditor.ButtonsBackground, violet, hovered ? 0.30f : 0.13f);
                Color right = Accent ? cyan : Mix(Forms.ThemeEditor.ButtonsBackground, cyan, hovered ? 0.30f : 0.10f);
                if (pressed) { left = Mix(left, Color.Black, 0.22f); right = Mix(right, Color.Black, 0.22f); }
                if (!Enabled) { left = Color.FromArgb(28, 30, 43); right = left; }
                using (var fill = new LinearGradientBrush(rect, left, right, 12f)) g.FillPath(fill, path);
                using (var border = new LinearGradientBrush(rect, Color.FromArgb(hovered || Focused ? 240 : 120, violet),
                    Color.FromArgb(hovered || Focused ? 240 : 120, cyan), 0f))
                using (var pen = new Pen(border, hovered || Focused ? 1.7f : 1f)) g.DrawPath(pen, path);
                if (Accent || hovered)
                    using (var sheen = new Pen(Color.FromArgb(55, Color.White)))
                        g.DrawLine(sheen, rect.Left + 14, rect.Top + 1, rect.Right - 14, rect.Top + 1);
            }
            var textRect = ClientRectangle;
            if (this is MenuButton) textRect.Width -= 18;
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? (Accent ? Color.White : Forms.ThemeEditor.ButtonsForeground) : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -7, -7), ForeColor, BackColor);
        }
    }
}
