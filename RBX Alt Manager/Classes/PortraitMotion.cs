using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    // Continuous bilinear deformation: the face and background stay anchored.
    internal static class PortraitMotion
    {
        private sealed class Frame : IDisposable
        {
            public Bitmap Image, Source, Spare, PendingImage;
            public Task Pending;
            public long Tick = -1;
            public int Strength = -1;
            public float[] Edge;
            public void Dispose()
            {
                Image.Dispose(); Spare?.Dispose();
                // The worker owns its source and destination until it has finished with LockBits.
                if (Pending != null) { var source = Source; var pendingImage = PendingImage; _ = Pending.ContinueWith(task => { var ignored = task.Exception; source.Dispose(); pendingImage.Dispose(); }, TaskScheduler.Default); }
                else Source.Dispose();
            }
        }
        private static readonly Dictionary<string, Frame> Frames = new Dictionary<string, Frame>();
        public static void Clear() { foreach (var frame in Frames.Values) frame.Dispose(); Frames.Clear(); }
        public static Bitmap Render(Bitmap source, string key, double time, bool enabled)
        {
            int strength = Forms.ThemeEditor.CharacterMotion;
            if (!enabled || strength == 0) return source;
            if (!Frames.TryGetValue(key, out Frame frame))
            {
                // The dimmed illustration does not need a full-window per-pixel deformation.
                // Keep UI controls at native resolution and bound only the animation work surface.
                float scale = Math.Min(1f, Math.Min(720f / source.Width, 360f / source.Height));
                var size = new Size(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
                var working = size == source.Size ? source.Clone(new Rectangle(Point.Empty, size), PixelFormat.Format32bppPArgb)
                    : new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                if (size != source.Size)
                    using (var canvas = Graphics.FromImage(working))
                    { canvas.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy; canvas.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear; canvas.DrawImage(source, new Rectangle(Point.Empty, size)); }
                frame = new Frame { Source = working, Image = working.Clone(new Rectangle(Point.Empty, size), PixelFormat.Format32bppPArgb),
                    Spare = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb), Edge = new float[size.Width] };
                for (int x = 0; x < size.Width; x++) frame.Edge[x] = Smooth(.55f, .78f, (float)x / size.Width);
                Frames[key] = frame;
            }
            if (frame.Pending?.IsCompleted == true)
            {
                if (frame.Pending.Status == TaskStatus.RanToCompletion)
                { frame.Spare = frame.Image; frame.Image = frame.PendingImage; }
                else { var ignored = frame.Pending.Exception; frame.Spare = frame.PendingImage; frame.Tick = -1; }
                frame.Pending = null; frame.PendingImage = null;
            }
            long tick = (long)(time * 30);
            if (frame.Pending == null && (frame.Tick != tick || frame.Strength != strength))
            {
                var input = frame.Source; var output = frame.Spare; var edge = frame.Edge;
                frame.PendingImage = output; frame.Spare = null; frame.Tick = tick; frame.Strength = strength;
                frame.Pending = Task.Run(() => Warp(input, output, edge, time, strength));
            }
            return frame.Image;
        }
        // Export and validation can await completion; the UI always draws the last completed bitmap.
        internal static Task PendingFrame(string key) => Frames.TryGetValue(key, out var frame) ? frame.Pending ?? Task.CompletedTask : Task.CompletedTask;
        private static float Smooth(float a, float b, float x)
        { float t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
        private static uint Blend(uint a, uint b, int fraction)
        {
            uint f = (uint)fraction, inverse = 256 - f;
            uint rb = ((((a & 0x00ff00ff) * inverse + (b & 0x00ff00ff) * f) >> 8) & 0x00ff00ff);
            uint ag = (((((a >> 8) & 0x00ff00ff) * inverse + ((b >> 8) & 0x00ff00ff) * f) >> 8) & 0x00ff00ff);
            return rb | (ag << 8);
        }
        private static unsafe void Warp(Bitmap source, Bitmap output, float[] edges, double time, int strength)
        {
            int width = source.Width, height = source.Height;
            var bounds = new Rectangle(0, 0, width, height);
            var src = source.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var dst = output.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                byte* input = (byte*)src.Scan0, target = (byte*)dst.Scan0;
                for (int y = 0; y < height; y++) Buffer.MemoryCopy(input + y * src.Stride, target + y * dst.Stride, dst.Stride, width * 4);
                float breath = (float)Math.Sin(time * Math.PI * 2 / 6.5);
                float sway = (float)Math.Sin(time * Math.PI * 2 / 9);
                float amplitude = (.25f + strength / 100f * 1.75f) * height / 640f;
                for (int y = 1; y < height - 1; y++)
                {
                    float yn = (float)y / height;
                    float body = Smooth(.56f, .93f, yn);
                    float hair = 1 - Smooth(.20f, .40f, yn);
                    float head = (1 - Smooth(.48f, .64f, yn)) * Smooth(-.05f, .08f, yn);
                    float wind = (float)(Math.Sin(time * .85 + yn * 5) + .2 * Math.Sin(time * 1.35 + yn * 9));
                    float horizontal = hair * wind * 2.5f + head * sway * .9f;
                    float bodyScale = body * breath * 6f;
                    float vertical = body * breath * 4.5f + hair * wind * .45f + head * breath * .9f;
                    uint* row = (uint*)(target + y * dst.Stride);
                    for (int x = (int)(width * .55f); x < width - 1; x++)
                    {
                        float xn = (float)x / width;
                        float edge = edges[x];
                        float dx = amplitude * edge * (horizontal + bodyScale * (xn - .83f));
                        float dy = amplitude * edge * vertical;
                        float sx = Math.Max(0, Math.Min(width - 2, x - dx));
                        float sy = Math.Max(0, Math.Min(height - 2, y - dy));
                        int ix = (int)sx, iy = (int)sy;
                        int fx = (int)((sx - ix) * 256), fy = (int)((sy - iy) * 256);
                        uint* top = (uint*)(input + iy * src.Stride);
                        uint* bottom = (uint*)(input + (iy + 1) * src.Stride);
                        row[x] = Blend(Blend(top[ix], top[ix + 1], fx), Blend(bottom[ix], bottom[ix + 1], fx), fy);
                    }
                }
            }
            finally { source.UnlockBits(src); output.UnlockBits(dst); }
        }
    }
}
