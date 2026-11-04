using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace RBX_Alt_Manager.Classes
{
    // Continuous bilinear deformation: the face and background stay anchored.
    internal static class PortraitMotion
    {
        private sealed class Frame
        {
            public Bitmap Image;
            public long Tick = -1;
            public int Strength = -1;
            public float[] Edge;
        }
        private static readonly Dictionary<string, Frame> Frames = new Dictionary<string, Frame>();
        public static void Clear() { foreach (var frame in Frames.Values) frame.Image.Dispose(); Frames.Clear(); }
        public static Bitmap Render(Bitmap source, string key, double time, bool enabled)
        {
            int strength = Forms.ThemeEditor.CharacterMotion;
            if (!enabled || strength == 0) return source;
            if (!Frames.TryGetValue(key, out Frame frame))
            {
                frame = new Frame { Image = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb), Edge = new float[source.Width] };
                for (int x = 0; x < source.Width; x++) frame.Edge[x] = Smooth(.55f, .78f, (float)x / source.Width);
                Frames[key] = frame;
            }
            long tick = (long)(time * 30);
            if (frame.Tick != tick || frame.Strength != strength) { Warp(source, frame.Image, frame.Edge, time, strength); frame.Tick = tick; frame.Strength = strength; }
            return frame.Image;
        }
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
                    uint* row = (uint*)(target + y * dst.Stride);
                    for (int x = (int)(width * .55f); x < width - 1; x++)
                    {
                        float xn = (float)x / width;
                        float edge = edges[x];
                        float dx = amplitude * edge * (hair * wind * 2.5f + body * breath * (xn - .83f) * 6f + head * sway * .9f);
                        float dy = amplitude * edge * (body * breath * 4.5f + hair * wind * .45f + head * breath * .9f);
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
