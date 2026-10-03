using System;
using System.Collections.Generic;

namespace ProjectHymn.EditorTools
{
    /// <summary>Minimal 3D vector for building meshes outside Unity (so models can be previewed and tested anywhere).</summary>
    public readonly struct V3
    {
        public readonly float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, float k) => new V3(a.X * k, a.Y * k, a.Z * k);
        public static V3 Scale(V3 a, V3 b) => new V3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized { get { float l = Length; return l > 1e-8f ? this * (1f / l) : new V3(0, 1, 0); } }
        public static readonly V3 Zero = new V3(0, 0, 0), One = new V3(1, 1, 1);
    }

    /// <summary>How a colour region is painted in the model's texture (M6 Valor look).</summary>
    public enum PaintStyle { Plain, Cloth, Metal, Skin, Wood, Hair, Gem }

    /// <summary>Face details painted into the texture instead of modelled (M6): eyes, brows, mouths, blush.</summary>
    public enum DecalKind { Eye, Brow, Mouth, Blush }

    public readonly struct Decal
    {
        public readonly DecalKind Kind;
        public readonly uint Region;      // colour region it is painted on (front-facing faces)
        public readonly float X, Y;       // centre in model space (front view)
        public readonly float W, H;       // size in metres
        public readonly float Angle;      // degrees, for brows and mouths
        public readonly uint Color;
        public Decal(DecalKind kind, uint region, float x, float y, float w, float h, float angle, uint color)
        {
            Kind = kind; Region = region; X = x; Y = y; W = w; H = h; Angle = angle; Color = color;
        }
    }

    /// <summary>
    /// Builds flat-shaded low-poly meshes from simple parts (boxes, prisms, cones, gem spheres). Triangles are grouped
    /// by colour into submeshes (one material each). Each vertex also gets a smoothed normal (averaged across faces
    /// sharing that position) for the toon outline pass. Unity-style axes: +Y up, +Z forward, left-handed winding.
    /// </summary>
    public sealed class LowPolyMesh
    {
        public readonly List<V3> Positions = new List<V3>();
        public readonly List<V3> Normals = new List<V3>();
        public readonly List<V3> SmoothNormals = new List<V3>();
        /// <summary>Colour (0xRRGGBB) of each submesh, and its triangle index list.</summary>
        public readonly List<uint> SubmeshColors = new List<uint>();
        public readonly List<List<int>> SubmeshTriangles = new List<List<int>>();
        /// <summary>Colours that should glow (emission), e.g. visors, lamps, the note shot.</summary>
        public readonly HashSet<uint> Glowing = new HashSet<uint>();
        /// <summary>Paint style per colour region (default Plain).</summary>
        public readonly Dictionary<uint, PaintStyle> Styles = new Dictionary<uint, PaintStyle>();
        public readonly List<Decal> Decals = new List<Decal>();
        /// <summary>Texture coordinates into the painted atlas (filled by <see cref="PaintedAtlas.Paint"/>).</summary>
        public readonly List<(float U, float V)> UVs = new List<(float, float)>();

        // ---------- transforms ----------

        /// <summary>Rotation by Euler degrees, Unity order (Z, then X, then Y).</summary>
        static V3 Rotate(V3 v, V3 eulerDeg)
        {
            if (eulerDeg.X == 0 && eulerDeg.Y == 0 && eulerDeg.Z == 0) return v;
            double rx = eulerDeg.X * Math.PI / 180, ry = eulerDeg.Y * Math.PI / 180, rz = eulerDeg.Z * Math.PI / 180;
            // Z
            double x = v.X * Math.Cos(rz) - v.Y * Math.Sin(rz), y = v.X * Math.Sin(rz) + v.Y * Math.Cos(rz), z = v.Z;
            // X
            double y2 = y * Math.Cos(rx) - z * Math.Sin(rx), z2 = y * Math.Sin(rx) + z * Math.Cos(rx);
            // Y
            double x3 = x * Math.Cos(ry) + z2 * Math.Sin(ry), z3 = -x * Math.Sin(ry) + z2 * Math.Cos(ry);
            return new V3((float)x3, (float)y2, (float)z3);
        }

        int Submesh(uint color)
        {
            int i = SubmeshColors.IndexOf(color);
            if (i >= 0) return i;
            SubmeshColors.Add(color);
            SubmeshTriangles.Add(new List<int>());
            return SubmeshColors.Count - 1;
        }

        /// <summary>Add one flat triangle (a, b, c clockwise when seen from outside, as Unity expects).</summary>
        public void Tri(uint color, V3 a, V3 b, V3 c)
        {
            var n = V3.Cross(b - a, c - a).Normalized;
            int sm = Submesh(color);
            int i0 = Positions.Count;
            Positions.Add(a); Positions.Add(b); Positions.Add(c);
            Normals.Add(n); Normals.Add(n); Normals.Add(n);
            SubmeshTriangles[sm].Add(i0); SubmeshTriangles[sm].Add(i0 + 1); SubmeshTriangles[sm].Add(i0 + 2);
        }

        public void Quad(uint color, V3 a, V3 b, V3 c, V3 d) { Tri(color, a, b, c); Tri(color, a, c, d); }

        /// <summary>Add a closed convex point set as a hull-less solid: faces are given explicitly by the shape helpers.</summary>
        void Faces(uint color, List<V3> pts, int[][] faces, V3 center, V3 euler)
        {
            var world = new List<V3>(pts.Count);
            foreach (var p in pts) world.Add(Rotate(p, euler) + center);
            foreach (var f in faces)
                for (int k = 1; k + 1 < f.Length; k++) Tri(color, world[f[0]], world[f[k]], world[f[k + 1]]);
        }

        // ---------- parts ----------

        /// <summary>Box centred at <paramref name="center"/>.</summary>
        public LowPolyMesh Box(uint color, V3 center, V3 size, V3 euler = default)
        {
            float x = size.X / 2, y = size.Y / 2, z = size.Z / 2;
            var p = new List<V3>
            {
                new V3(-x, -y, -z), new V3(x, -y, -z), new V3(x, y, -z), new V3(-x, y, -z),
                new V3(-x, -y, z), new V3(x, -y, z), new V3(x, y, z), new V3(-x, y, z),
            };
            int[][] f =
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, // -z, +z
                new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, // -x, +x
                new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 }, // +y, -y
            };
            Faces(color, p, f, center, euler);
            return this;
        }

        /// <summary>Prism / frustum / cone along local +Y, base centred at <paramref name="baseCenter"/>.</summary>
        public LowPolyMesh Cylinder(uint color, V3 baseCenter, float radius, float height, int sides, float topRadius = -1f, V3 euler = default, V3 scale = default)
        {
            if (topRadius < 0) topRadius = radius;
            if (scale.X == 0 && scale.Y == 0 && scale.Z == 0) scale = V3.One;
            var p = new List<V3>();
            for (int i = 0; i < sides; i++)
            {
                double a = Math.PI * 2 * i / sides + Math.PI / sides;
                p.Add(V3.Scale(new V3((float)Math.Sin(a) * radius, 0, (float)Math.Cos(a) * radius), scale));
            }
            bool cone = topRadius <= 1e-4f;
            if (cone) p.Add(V3.Scale(new V3(0, height, 0), scale));
            else for (int i = 0; i < sides; i++)
            {
                double a = Math.PI * 2 * i / sides + Math.PI / sides;
                p.Add(V3.Scale(new V3((float)Math.Sin(a) * topRadius, height, (float)Math.Cos(a) * topRadius), scale));
            }
            var faces = new List<int[]>();
            var bottom = new int[sides];
            for (int i = 0; i < sides; i++) bottom[i] = i; // seen from below: clockwise
            faces.Add(bottom);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (cone) faces.Add(new[] { i, sides, j });
                else faces.Add(new[] { i, sides + i, sides + j, j });
            }
            if (!cone)
            {
                var top = new int[sides];
                for (int i = 0; i < sides; i++) top[i] = sides + (sides - 1 - i);
                faces.Add(top);
            }
            // Winding above runs counter-clockwise from outside; Unity wants clockwise.
            var flippedFaces = new List<int[]>();
            foreach (var face in faces) { var r = (int[])face.Clone(); Array.Reverse(r); flippedFaces.Add(r); }
            Faces(color, p, flippedFaces.ToArray(), baseCenter, euler);
            return this;
        }

        /// <summary>Faceted "gem" sphere: an icosahedron (detail 0, 20 faces) or once-subdivided (detail 1, 80 faces).</summary>
        public LowPolyMesh Sphere(uint color, V3 center, V3 radii, int detail = 1, V3 euler = default)
        {
            float t = (1f + (float)Math.Sqrt(5)) / 2f;
            var v = new List<V3>
            {
                new V3(-1, t, 0), new V3(1, t, 0), new V3(-1, -t, 0), new V3(1, -t, 0),
                new V3(0, -1, t), new V3(0, 1, t), new V3(0, -1, -t), new V3(0, 1, -t),
                new V3(t, 0, -1), new V3(t, 0, 1), new V3(-t, 0, -1), new V3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].Normalized;
            var f = new List<int[]>
            {
                new[] {0,11,5}, new[] {0,5,1}, new[] {0,1,7}, new[] {0,7,10}, new[] {0,10,11},
                new[] {1,5,9}, new[] {5,11,4}, new[] {11,10,2}, new[] {10,7,6}, new[] {7,1,8},
                new[] {3,9,4}, new[] {3,4,2}, new[] {3,2,6}, new[] {3,6,8}, new[] {3,8,9},
                new[] {4,9,5}, new[] {2,4,11}, new[] {6,2,10}, new[] {8,6,7}, new[] {9,8,1},
            };
            for (int d = 0; d < detail; d++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).Normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                }
                var nf = new List<int[]>();
                foreach (var tri in f)
                {
                    int a = Mid(tri[0], tri[1]), b = Mid(tri[1], tri[2]), c = Mid(tri[2], tri[0]);
                    nf.Add(new[] { tri[0], a, c }); nf.Add(new[] { tri[1], b, a }); nf.Add(new[] { tri[2], c, b }); nf.Add(new[] { a, b, c });
                }
                f = nf;
            }
            var pts = new List<V3>();
            foreach (var p in v) pts.Add(V3.Scale(p, radii));
            // This icosahedron face list already winds clockwise from outside (Unity's front faces).
            Faces(color, pts, f.ToArray(), center, euler);
            return this;
        }

        /// <summary>Triangular prism (a gable roof): ridge along X, peak up.</summary>
        public LowPolyMesh Roof(uint color, V3 baseCenter, float width, float height, float depth, V3 euler = default)
        {
            float x = width / 2, z = depth / 2;
            var p = new List<V3>
            {
                new V3(-x, 0, -z), new V3(x, 0, -z), new V3(x, height, 0), new V3(-x, height, 0),
                new V3(-x, 0, z), new V3(x, 0, z),
            };
            int[][] f =
            {
                new[] { 0, 3, 2, 1 },   // front slope (-z)
                new[] { 4, 5, 2, 3 },   // back slope (+z)
                new[] { 0, 4, 3 },      // left gable
                new[] { 1, 2, 5 },      // right gable
                new[] { 0, 1, 5, 4 },   // bottom
            };
            Faces(color, p, f, baseCenter, euler);
            return this;
        }

        public LowPolyMesh Glow(uint color) { Glowing.Add(color); return this; }

        public LowPolyMesh Style(PaintStyle style, params uint[] colors)
        {
            foreach (var c in colors) Styles[c] = style;
            return this;
        }

        public PaintStyle StyleOf(uint color) => Styles.TryGetValue(color, out var s) ? s : PaintStyle.Plain;

        /// <summary>Paint a face detail on the front of a colour region (front view coordinates, metres).</summary>
        public LowPolyMesh Paint(DecalKind kind, uint region, float x, float y, float w, float h, float angle = 0f, uint color = 0x1c1426)
        {
            Decals.Add(new Decal(kind, region, x, y, w, h, angle, color));
            return this;
        }

        /// <summary>Average face normals per position (welded within 1 mm) for the outline pass. Call once when done.</summary>
        public LowPolyMesh Finish()
        {
            var sums = new Dictionary<(int, int, int), V3>();
            (int, int, int) Key(V3 p) => ((int)Math.Round(p.X * 1000), (int)Math.Round(p.Y * 1000), (int)Math.Round(p.Z * 1000));
            for (int i = 0; i < Positions.Count; i++)
            {
                var k = Key(Positions[i]);
                sums[k] = (sums.TryGetValue(k, out var s) ? s : V3.Zero) + Normals[i];
            }
            SmoothNormals.Clear();
            for (int i = 0; i < Positions.Count; i++) SmoothNormals.Add(sums[Key(Positions[i])].Normalized);
            return this;
        }

        /// <summary>Scale every vertex about the origin (oversized Dragon Valor-style weapons). Call after Finish.</summary>
        public LowPolyMesh Scaled(float k)
        {
            for (int i = 0; i < Positions.Count; i++) Positions[i] = Positions[i] * k;
            return this;
        }

        public int TriangleCount { get { int n = 0; foreach (var t in SubmeshTriangles) n += t.Count / 3; return n; } }
    }
}
