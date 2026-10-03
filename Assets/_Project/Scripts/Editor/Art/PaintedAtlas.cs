using System;
using System.Collections.Generic;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Paints a small texture for a generated model (GDD M6 Valor look) and gives every vertex a UV into it.
    /// Faces are grouped by colour region and by the direction they face (±X, ±Y, ±Z); each group gets a cell of the
    /// atlas and is projected flat onto it. The painter fills each cell from the model-space position of its texels:
    /// light painted in from above, a per-style pattern (cloth folds, metal sheen, wood grain, hair strands), soft
    /// hand-painted mottling, a painted palette shift (warm lights, cool shadows, less saturation than the M5 colours)
    /// and face details (eyes, brows, mouth, blush) on front-facing skin. Pure C#; Unity is only needed to save it.
    /// </summary>
    public static class PaintedAtlas
    {
        public sealed class Result
        {
            public int Size;
            /// <summary>RGBA32, row 0 = bottom (Unity texture order).</summary>
            public byte[] Pixels;
            public int Cells;
        }

        struct Group
        {
            public int Region;      // submesh index
            public int Dir;         // 0 +X, 1 -X, 2 +Y, 3 -Y, 4 +Z, 5 -Z
            public float AMin, AMax, BMin, BMax;
            public int CellX, CellY;
        }

        static int DirOf(V3 n)
        {
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            if (ax >= ay && ax >= az) return n.X >= 0 ? 0 : 1;
            if (ay >= az) return n.Y >= 0 ? 2 : 3;
            return n.Z >= 0 ? 4 : 5;
        }

        /// <summary>Flat projection used for a direction: (a, b). Side and front views keep b = height.</summary>
        static (float a, float b) Project(int dir, V3 p) => dir switch
        {
            0 or 1 => (p.Z, p.Y),
            2 or 3 => (p.X, p.Z),
            _ => (p.X, p.Y),
        };

        public static Result Paint(LowPolyMesh m, int cellPx = 32, int maxSize = 256)
        {
            // ---- group triangles ----
            var groups = new List<Group>();
            var index = new Dictionary<(int, int), int>();
            var triGroup = new Dictionary<int, int>(); // first vertex of a triangle -> group
            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var p in m.Positions) { yMin = Math.Min(yMin, p.Y); yMax = Math.Max(yMax, p.Y); }
            if (yMax - yMin < 1e-3f) yMax = yMin + 1f;

            for (int s = 0; s < m.SubmeshTriangles.Count; s++)
            {
                var tris = m.SubmeshTriangles[s];
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int i0 = tris[t];
                    int dir = DirOf(m.Normals[i0]);
                    if (!index.TryGetValue((s, dir), out int g))
                    {
                        g = groups.Count;
                        index[(s, dir)] = g;
                        groups.Add(new Group { Region = s, Dir = dir, AMin = float.MaxValue, AMax = float.MinValue, BMin = float.MaxValue, BMax = float.MinValue });
                    }
                    var gr = groups[g];
                    for (int k = 0; k < 3; k++)
                    {
                        var (a, b) = Project(dir, m.Positions[tris[t + k]]);
                        gr.AMin = Math.Min(gr.AMin, a); gr.AMax = Math.Max(gr.AMax, a);
                        gr.BMin = Math.Min(gr.BMin, b); gr.BMax = Math.Max(gr.BMax, b);
                    }
                    groups[g] = gr;
                    triGroup[i0] = g;
                }
            }

            // ---- lay out cells ----
            int n = Math.Max(1, groups.Count);
            int grid = (int)Math.Ceiling(Math.Sqrt(n));
            int size = 16;
            while (size < grid * cellPx && size < maxSize) size *= 2;
            int cell = Math.Max(4, size / grid);
            for (int g = 0; g < groups.Count; g++)
            {
                var gr = groups[g];
                gr.CellX = g % grid; gr.CellY = g / grid;
                if (gr.AMax - gr.AMin < 0.01f) { gr.AMin -= 0.005f; gr.AMax += 0.005f; }
                if (gr.BMax - gr.BMin < 0.01f) { gr.BMin -= 0.005f; gr.BMax += 0.005f; }
                groups[g] = gr;
            }

            // ---- UVs ----
            m.UVs.Clear();
            for (int i = 0; i < m.Positions.Count; i++) m.UVs.Add((0f, 0f));
            foreach (var kv in triGroup)
            {
                var gr = groups[kv.Value];
                for (int k = 0; k < 3; k++)
                {
                    int vi = kv.Key + k; // Tri() adds a triangle's three vertices consecutively
                    var (a, b) = Project(gr.Dir, m.Positions[vi]);
                    float ta = (a - gr.AMin) / (gr.AMax - gr.AMin), tb = (b - gr.BMin) / (gr.BMax - gr.BMin);
                    float px = gr.CellX * cell + 1f + ta * (cell - 2f);
                    float py = gr.CellY * cell + 1f + tb * (cell - 2f);
                    m.UVs[vi] = (px / size, py / size);
                }
            }

            // ---- paint ----
            var pixels = new byte[size * size * 4];
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = pixels[i + 1] = pixels[i + 2] = 255; pixels[i + 3] = 255; }
            foreach (var gr in groups)
            {
                uint color = m.SubmeshColors[gr.Region];
                var style = m.StyleOf(color);
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    // Texel centre -> model-space position on the projection plane (border texels clamp).
                    float ta = Clamp01((x + 0.5f - 1f) / (cell - 2f)), tb = Clamp01((y + 0.5f - 1f) / (cell - 2f));
                    float a = gr.AMin + ta * (gr.AMax - gr.AMin), b = gr.BMin + tb * (gr.BMax - gr.BMin);
                    float height = gr.Dir <= 1 || gr.Dir >= 4 ? (b - yMin) / (yMax - yMin) : (gr.Dir == 2 ? 1.05f : -0.2f);
                    var c = Shade(color, style, gr.Dir, a, b, ta, tb, height);
                    if (gr.Dir == 4) c = ApplyDecals(m, color, a, b, c);
                    int px = gr.CellX * cell + x, py = gr.CellY * cell + y;
                    if (px >= size || py >= size) continue;
                    int o = (py * size + px) * 4;
                    pixels[o] = Q(c.r); pixels[o + 1] = Q(c.g); pixels[o + 2] = Q(c.b); pixels[o + 3] = 255;
                }
            }
            return new Result { Size = size, Pixels = pixels, Cells = groups.Count };
        }

        // ---------------- painting ----------------

        struct C3 { public float r, g, b; public C3(float r, float g, float b) { this.r = r; this.g = g; this.b = b; } }

        static C3 Rgb(uint c) => new C3(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);

        /// <summary>15-bit colour (5 bits per channel), as on PS1. At modern resolution this only shows as gentle steps.</summary>
        static byte Q(float v)
        {
            int i = (int)Math.Round(Clamp01(v) * 255f);
            i = (i >> 3) << 3 | (i >> 5); // keep 5 bits, refill the low bits so white stays white
            return (byte)Math.Min(255, i);
        }

        static C3 Shade(uint color, PaintStyle style, int dir, float a, float b, float ta, float tb, float height)
        {
            var c = Rgb(color);
            // Painted palette: pull the bright M5 colours toward a painted, slightly earthier range.
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            const float desat = 0.8f;
            c = new C3(lum + (c.r - lum) * desat, lum + (c.g - lum) * desat, lum + (c.b - lum) * desat);

            // Light painted in from above.
            float k = 0.78f + 0.34f * Smooth(0f, 1f, height);
            float noise = Noise(a * 14f, b * 14f) * 0.5f + Noise(a * 37f + 11f, b * 37f - 5f) * 0.25f; // -0.75..0.75
            k *= 1f + noise * 0.08f;

            switch (style)
            {
                case PaintStyle.Cloth:
                    k *= 0.9f + 0.1f * (float)Math.Cos(a * 2 * Math.PI * 5.5f + Noise(a * 3f, b * 2f) * 2f);
                    if (dir != 2 && dir != 3) k *= 0.82f + 0.18f * Smooth(0f, 0.18f, tb);   // shaded hem
                    break;
                case PaintStyle.Metal:
                    k *= 0.86f + 0.42f * (float)Math.Exp(-Math.Pow((tb - 0.66f) / 0.13f, 2));  // sheen band
                    k *= 1f + 0.12f * (float)Math.Exp(-Math.Pow((ta - 0.3f) / 0.05f, 2));     // glint
                    break;
                case PaintStyle.Skin:
                    k *= 1.02f;
                    break;
                case PaintStyle.Wood:
                    k *= 0.88f + 0.12f * (float)Math.Sin(b * 48f + Noise(a * 6f, b * 2f) * 5f);
                    break;
                case PaintStyle.Hair:
                    k *= 0.84f + 0.2f * Math.Abs((float)Math.Sin(a * 55f + b * 9f + Noise(a * 4f, b * 4f) * 3f));
                    break;
                case PaintStyle.Gem:
                    float d = (float)Math.Sqrt((ta - 0.4f) * (ta - 0.4f) + (tb - 0.65f) * (tb - 0.65f));
                    k *= 1.25f - 0.5f * Clamp01(d * 1.6f);
                    break;
            }

            c = new C3(c.r * k, c.g * k, c.b * k);
            // Warm lights, cool shadows.
            float w = Clamp01((k - 0.92f) * 3f), s = Clamp01((0.92f - k) * 3f);
            c.r *= 1f + 0.05f * w - 0.06f * s;
            c.g *= 1f + 0.01f * w - 0.03f * s;
            c.b *= 1f - 0.06f * w + 0.08f * s;
            return c;
        }

        static C3 ApplyDecals(LowPolyMesh m, uint region, float x, float y, C3 c)
        {
            foreach (var d in m.Decals)
            {
                if (d.Region != region) continue;
                // Local coordinates, rotated by the decal angle.
                double ang = d.Angle * Math.PI / 180.0;
                float dx = x - d.X, dy = y - d.Y;
                float lx = (float)(dx * Math.Cos(ang) + dy * Math.Sin(ang));
                float ly = (float)(-dx * Math.Sin(ang) + dy * Math.Cos(ang));
                float ex = lx / (d.W * 0.5f), ey = ly / (d.H * 0.5f);
                float r2 = ex * ex + ey * ey;
                var ink = Rgb(d.Color);
                switch (d.Kind)
                {
                    case DecalKind.Eye:
                        if (r2 > 1f) break;
                        c = ink;
                        // Coloured lower half (iris) and a white highlight top-right, anime style.
                        if (ey < -0.1f && r2 < 0.7f) c = new C3(ink.r * 0.4f + 0.25f, ink.g * 0.4f + 0.2f, ink.b * 0.4f + 0.45f);
                        float hx = ex - 0.35f, hy = ey - 0.4f;
                        if (hx * hx + hy * hy < 0.12f) c = new C3(1f, 1f, 1f);
                        break;
                    case DecalKind.Brow:
                    case DecalKind.Mouth:
                        if (Math.Abs(ex) <= 1f && Math.Abs(ey) <= 1f) c = ink;
                        break;
                    case DecalKind.Blush:
                        if (r2 > 1f) break;
                        float t = 0.55f * (1f - r2);
                        c = new C3(c.r + (ink.r - c.r) * t, c.g + (ink.g - c.g) * t, c.b + (ink.b - c.b) * t);
                        break;
                }
            }
            return c;
        }

        // ---------------- environment texture ----------------

        /// <summary>
        /// A tileable grey "painted stone" texture for the building blocks (tinted by each block's colour in the shader):
        /// broad soft mottling and a few brush streaks. RGBA32, row 0 = bottom.
        /// </summary>
        public static byte[] StoneTexture(int size)
        {
            var px = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float n = TileNoise(u, v, 4) * 0.55f + TileNoise(u, v, 9) * 0.3f + TileNoise(u, v, 23) * 0.15f; // -1..1
                float streak = (float)Math.Sin((u * 3f + v) * Math.PI * 2 * 2 + TileNoise(u, v, 5) * 2f) * 0.5f + 0.5f;
                float k = 0.93f + n * 0.09f + (streak - 0.5f) * 0.04f;
                byte g = Q(k * 0.98f);
                int o = (y * size + x) * 4;
                px[o] = Q(k); px[o + 1] = g; px[o + 2] = Q(k * 0.96f); px[o + 3] = 255;
            }
            return px;
        }

        // ---------------- helpers ----------------

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Smooth(float e0, float e1, float x) { float t = Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 32767.5f - 1f; // -1..1
            }
        }

        /// <summary>Smooth value noise, -1..1.</summary>
        static float Noise(float x, float y)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        /// <summary>Value noise that tiles over the unit square with <paramref name="period"/> cells.</summary>
        static float TileNoise(float u, float v, int period)
        {
            float x = u * period, y = v * period;
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = ((xi % period) + period) % period, y0 = ((yi % period) + period) % period;
            int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            float a = Hash(x0, y0 + period * 31), b = Hash(x1, y0 + period * 31), c = Hash(x0, y1 + period * 31), d = Hash(x1, y1 + period * 31);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
    }
}
