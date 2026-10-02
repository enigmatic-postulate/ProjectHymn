using System;
using System.Collections.Generic;

namespace ProjectHymn.EditorTools
{
    /// <summary>RGBA pixel buffer with painting helpers. (0,0) is the TOP-left; ToBottomUp() gives Unity's order.</summary>
    public sealed class PixelCanvas
    {
        public readonly int Width, Height;
        readonly byte[] _px; // RGBA, top-down rows

        public PixelCanvas(int w, int h) { Width = w; Height = h; _px = new byte[w * h * 4]; }

        public struct Rgba
        {
            public byte R, G, B, A;
            public Rgba(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }
            public static Rgba Hex(uint rgb) => new Rgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            public static Rgba Lerp(Rgba a, Rgba b, float t) => new Rgba(
                (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
            public Rgba Scale(float k) => new Rgba(Clamp(R * k), Clamp(G * k), Clamp(B * k), A);
            static byte Clamp(float v) => (byte)Math.Max(0, Math.Min(255, v));
        }

        static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

        public void Set(int x, int y, Rgba c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = (y * Width + x) * 4;
            if (c.A == 255) { _px[i] = c.R; _px[i + 1] = c.G; _px[i + 2] = c.B; _px[i + 3] = 255; return; }
            float a = c.A / 255f;
            _px[i] = (byte)(_px[i] + (c.R - _px[i]) * a);
            _px[i + 1] = (byte)(_px[i + 1] + (c.G - _px[i + 1]) * a);
            _px[i + 2] = (byte)(_px[i + 2] + (c.B - _px[i + 2]) * a);
            _px[i + 3] = (byte)Math.Max(_px[i + 3], c.A);
        }

        public Rgba Get(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return new Rgba(_px[i], _px[i + 1], _px[i + 2], _px[i + 3]);
        }

        public void Rect(int x, int y, int w, int h, Rgba c)
        {
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) Set(xx, yy, c);
        }

        /// <summary>16-bit style ordered dither between two colours (t = 0..1 toward b).</summary>
        public Rgba Dither(int x, int y, Rgba a, Rgba b, float t)
        {
            // 5 flat levels with Bayer between them keeps the SNES look.
            float level = t * 4f;
            int lo = (int)Math.Floor(level);
            float frac = level - lo;
            var c0 = Rgba.Lerp(a, b, Math.Min(1f, lo / 4f));
            var c1 = Rgba.Lerp(a, b, Math.Min(1f, (lo + 1) / 4f));
            return frac * 16f > Bayer[y & 3, x & 3] ? c1 : c0;
        }

        public void VGradient(int x, int y, int w, int h, Rgba top, Rgba bottom)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    Set(x + xx, y + yy, Dither(x + xx, y + yy, top, bottom, h <= 1 ? 0 : yy / (float)(h - 1)));
        }

        public void Disc(int cx, int cy, int r, Rgba c)
        {
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) if (x * x + y * y <= r * r) Set(cx + x, cy + y, c);
        }

        public void Ring(int cx, int cy, int r, int thickness, Rgba c)
        {
            int r2 = r * r, ri = Math.Max(0, r - thickness), ri2 = ri * ri;
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) { int d = x * x + y * y; if (d <= r2 && d > ri2) Set(cx + x, cy + y, c); }
        }

        /// <summary>Filled triangle roof: apex at (cx, top), base from x0..x1 at y = baseY.</summary>
        public void Roof(int x0, int x1, int top, int baseY, Rgba c, Rgba edge)
        {
            int cx = (x0 + x1) / 2;
            for (int y = top; y <= baseY; y++)
            {
                float t = (y - top) / (float)Math.Max(1, baseY - top);
                int half = (int)((x1 - x0) / 2f * t);
                for (int x = cx - half; x <= cx + half; x++) Set(x, y, (x == cx - half || x == cx + half) ? edge : c);
            }
        }

        public void Line(int x0, int y0, int x1, int y1, Rgba c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Stamp ASCII pixel art; '.' or ' ' is transparent, other chars index the palette.</summary>
        public void Stamp(string[] rows, IDictionary<char, Rgba> palette, int ox = 0, int oy = 0, bool flipX = false)
        {
            for (int y = 0; y < rows.Length; y++)
            {
                var row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char ch = row[x];
                    if (ch == '.' || ch == ' ') continue;
                    if (!palette.TryGetValue(ch, out var c)) continue;
                    int px = flipX ? row.Length - 1 - x : x;
                    Set(ox + px, oy + y, c);
                }
            }
        }

        public static PixelCanvas FromArt(string[] rows, IDictionary<char, Rgba> palette)
        {
            int w = 0;
            foreach (var r in rows) w = Math.Max(w, r.Length);
            var c = new PixelCanvas(w, rows.Length);
            c.Stamp(Center(rows, w), palette);
            return c;
        }

        /// <summary>Centre rows of uneven length so hand-drawn art stays symmetric.</summary>
        static string[] Center(string[] rows, int w)
        {
            var o = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                int pad = (w - rows[i].Length) / 2;
                o[i] = new string('.', pad) + rows[i];
            }
            return o;
        }

        /// <summary>Pixels in Unity's bottom-up row order (for Texture2D.SetPixels32 / LoadRawTextureData).</summary>
        public byte[] ToBottomUp()
        {
            var o = new byte[_px.Length];
            int stride = Width * 4;
            for (int y = 0; y < Height; y++) Buffer.BlockCopy(_px, y * stride, o, (Height - 1 - y) * stride, stride);
            return o;
        }

        public byte[] TopDown => _px;
    }
}
