using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RBX_Alt_Manager.Classes;

namespace RBX_Alt_Manager.Forms
{
    public sealed class RoflForm : Form
    {
        private readonly Timer animation;
        private readonly Timer evasion;
        private readonly Label status;
        private readonly Label tag, title;
        private readonly NeonButton unlock;
        private int tick;
        private Point? escapeTarget;
        public RoflForm()
        {
            Text = "ROFL MODE • Шуточный экран";
            ClientSize = new Size(760, 440);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(15, 5, 11);
            ForeColor = Color.FromArgb(255, 219, 230);
            Font = new Font("Segoe UI Semibold", 11);
            tag = new Label { Text = "ROFL MODE", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(24, 30, 712, 28), ForeColor = Color.FromArgb(255, 100, 132) };
            title = new Label { Text = "ДОСТУП К ЧИЛЛУ\nЗАБЛОКИРОВАН", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(24, 82, 712, 120), Font = new Font("Segoe UI Black", 48) };
            status = new Label { Text = "Обнаружен подозрительно высокий уровень скилла…", AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(24, 225, 712, 32), Font = new Font("Consolas", 11) };
            unlock = new NeonButton { Text = "Разблокировать за 0 Robux", Bounds = new Rectangle(205, 294, 350, 52), Font = Font,
                AccessibleName = "Убегающая кнопка разблокировки", TabStop = false, DialogResult = DialogResult.None };
            unlock.MouseEnter += (sender, e) => EvadeAt(PointToClient(Cursor.Position), true);
            unlock.MouseDown += (sender, e) => EvadeAt(PointToClient(Cursor.Position), true);
            Controls.Add(tag); Controls.Add(title); Controls.Add(status); Controls.Add(unlock);
            Shown += (sender, e) => { Bounds = Screen.FromControl(Owner ?? this).Bounds; LayoutScreen(); };
            Resize += (sender, e) => LayoutScreen();
            KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); } };
            animation = new Timer { Interval = 160 };
            animation.Tick += (sender, e) =>
            {
                tick++;
                string[] messages = { "Обнаружен подозрительно высокий уровень скилла…", "Поиск потерянных Robux: найдено 0 из 0", "Запущена экстренная загрузка мемов…", "Анти-кринж протокол активирован. Можно выдохнуть." };
                status.Text = messages[tick / 12 % messages.Length];
                Invalidate();
            };
            animation.Start();
            evasion = new Timer { Interval = 20 };
            evasion.Tick += (sender, e) =>
            {
                if (!Visible || !ContainsFocus) return;
                EvadeAt(PointToClient(Cursor.Position), false);
                if (escapeTarget.HasValue)
                {
                    Point target = escapeTarget.Value;
                    int dx = target.X - unlock.Left, dy = target.Y - unlock.Top;
                    if (Math.Abs(dx) + Math.Abs(dy) < 3) { unlock.Location = target; escapeTarget = null; }
                    else unlock.Location = new Point(unlock.Left + (int)(dx * .42), unlock.Top + (int)(dy * .42));
                }
            };
            evasion.Start();
        }
        private void EvadeAt(Point pointer, bool immediate)
        {
            var near = unlock.Bounds;
            near.Inflate(100, 85);
            if (!near.Contains(pointer)) return;
            int left = 36, right = Math.Max(left, ClientSize.Width - unlock.Width - 36);
            int top = 36, bottom = Math.Max(top, ClientSize.Height - unlock.Height - 36);
            var candidates = new[] { new Point(left, top), new Point(right, top), new Point(left, bottom), new Point(right, bottom),
                new Point(left, (top + bottom) / 2), new Point(right, (top + bottom) / 2) };
            double best = -1;
            Point destination = unlock.Location;
            foreach (var candidate in candidates)
            {
                int nearestX = Math.Max(candidate.X, Math.Min(candidate.X + unlock.Width, pointer.X));
                int nearestY = Math.Max(candidate.Y, Math.Min(candidate.Y + unlock.Height, pointer.Y));
                double distance = (double)(nearestX - pointer.X) * (nearestX - pointer.X) + (double)(nearestY - pointer.Y) * (nearestY - pointer.Y);
                if (distance > best) { best = distance; destination = candidate; }
            }
            escapeTarget = destination;
            if (immediate) { unlock.Location = destination; escapeTarget = null; }
        }
        private void LayoutScreen()
        {
            if (title == null || tag == null || status == null || unlock == null) return;
            int width = Math.Max(300, Math.Min(1100, ClientSize.Width - 60));
            int left = (ClientSize.Width - width) / 2;
            int top = Math.Max(35, (ClientSize.Height - 420) / 2);
            tag.SetBounds(left, top, width, 34);
            title.SetBounds(left, top + 65, width, 160);
            status.SetBounds(left, top + 252, width, 42);
            unlock.SetBounds((ClientSize.Width - 380) / 2, top + 332, 380, 58);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(50, 255, 50, 100)))
                for (int y = tick % 8; y < Height; y += 8) e.Graphics.DrawLine(pen, 0, y, Width, y);
            using (var border = new Pen(Color.FromArgb(190, 224, 48, 95), 2)) e.Graphics.DrawRectangle(border, 10, 10, ClientSize.Width - 21, ClientSize.Height - 21);
        }
        protected override void Dispose(bool disposing) { if (disposing) { animation?.Dispose(); evasion?.Dispose(); } base.Dispose(disposing); }
    }
}
