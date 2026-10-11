using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// Small procedural mesh kit for the Starlit Spring art (Slice 11): lathes (towers, bowls, the island's underside),
    /// elliptical drums and discs, rings, tori, faceted crystals, five-point stars and curved petals. Every triangle is
    /// wound from the normals it is given, so faces always point the right way. Meshes are saved as assets.
    /// </summary>
    public sealed class MeshKit
    {
        public const string MeshDir = "Assets/_Project/Art/Generated/Spring/Meshes";

        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int> _t = new List<int>();

        public int Count => _v.Count;

        public int Vert(Vector3 p, Vector3 normal, Vector2 uv)
        {
            _v.Add(p); _n.Add(normal.normalized); _uv.Add(uv);
            return _v.Count - 1;
        }

        /// <summary>A triangle facing along its vertices' normals (Unity's front faces: cross(b-a, c-a) along the normal).</summary>
        public void Tri(int a, int b, int c)
        {
            var face = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            var want = _n[a] + _n[b] + _n[c];
            if (Vector3.Dot(face, want) < 0f) { _t.Add(a); _t.Add(c); _t.Add(b); }
            else { _t.Add(a); _t.Add(b); _t.Add(c); }
        }

        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        /// <summary>Copy another kit in, moved by <paramref name="m"/>.</summary>
        public void Append(MeshKit k, Matrix4x4 m)
        {
            int o = _v.Count;
            for (int i = 0; i < k._v.Count; i++)
            {
                _v.Add(m.MultiplyPoint3x4(k._v[i]));
                _n.Add(m.MultiplyVector(k._n[i]).normalized);
                _uv.Add(k._uv[i]);
            }
            foreach (int i in k._t) _t.Add(o + i);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Save (or replace) the mesh as an asset so scenes and builds keep it.</summary>
        public Mesh Save(string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var fresh = ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
            EditorUtility.CopySerialized(fresh, existing);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---------------- Shapes ----------------

        /// <summary>
        /// Revolve a profile of (radius, height) points about Y. List the profile from the top going outward and down
        /// (clockwise in the r-y plane) so normals face out. <paramref name="scale"/> stretches X and Z (ellipses).
        /// UV: u around (times <paramref name="uRepeat"/>), v along the profile from 0 at its first point.
        /// </summary>
        public static MeshKit Lathe(Vector2[] profile, int segments, Vector3 scale, float uRepeat = 1f)
        {
            var k = new MeshKit();
            int n = profile.Length;
            var len = new float[n];
            for (int i = 1; i < n; i++) len[i] = len[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            float total = Mathf.Max(0.0001f, len[n - 1]);
            var rows = new int[n, segments + 1];
            for (int i = 0; i < n; i++)
            {
                var t = (i < n - 1 ? profile[i + 1] - profile[i] : Vector2.zero) + (i > 0 ? profile[i] - profile[i - 1] : Vector2.zero);
                var n2 = new Vector2(-t.y, t.x).normalized;                     // outward in the r-y plane
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var p = new Vector3(profile[i].x * ca * scale.x, profile[i].y * scale.y, profile[i].x * sa * scale.z);
                    var nn = new Vector3(n2.x * ca / Mathf.Max(0.0001f, scale.x), n2.y / Mathf.Max(0.0001f, scale.y), n2.x * sa / Mathf.Max(0.0001f, scale.z));
                    if (nn.sqrMagnitude < 1e-8f) nn = new Vector3(0f, Mathf.Sign(n2.y + 1e-6f), 0f);
                    rows[i, s] = k.Vert(p, nn, new Vector2(s / (float)segments * uRepeat, len[i] / total));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < segments; s++)
                {
                    int a = rows[i, s], b = rows[i, s + 1], c = rows[i + 1, s + 1], d = rows[i + 1, s];
                    if (profile[i].x <= 0.0001f) k.Tri(a, c, d);                    // at the axis: a fan
                    else if (profile[i + 1].x <= 0.0001f) k.Tri(a, b, c);
                    else k.Quad(a, b, c, d);
                }
            return k;
        }

        /// <summary>The side of an elliptical drum, open at both ends; v runs 0 at the top to 1 at the bottom.</summary>
        public static MeshKit Drum(float rx, float rz, float yTop, float yBottom, int segments, float uRepeat) =>
            Lathe(new[] { new Vector2(1f, yTop), new Vector2(1f, yBottom) }, segments, new Vector3(rx, 1f, rz), uRepeat);

        /// <summary>A flat elliptical disc facing up, with planar UVs (world metres times <paramref name="uvScale"/>).</summary>
        public static MeshKit Disc(float rx, float rz, int segments, float uvScale)
        {
            var k = new MeshKit();
            int c = k.Vert(Vector3.zero, Vector3.up, Vector2.zero);
            var ring = new int[segments + 1];
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                ring[s] = k.Vert(p, Vector3.up, new Vector2(p.x, p.z) * uvScale);
            }
            for (int s = 0; s < segments; s++) k.Tri(c, ring[s], ring[s + 1]);
            return k;
        }

        /// <summary>A flat elliptical ring facing up (a pool's lip, the grass edge).</summary>
        public static MeshKit Ring(float rxOut, float rzOut, float rxIn, float rzIn, int segments, float uvScale)
        {
            var k = new MeshKit();
            for (int s = 0; s < segments; s++)
            {
                float a0 = s / (float)segments * Mathf.PI * 2f, a1 = (s + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 O(float a) => new Vector3(Mathf.Cos(a) * rxOut, 0f, Mathf.Sin(a) * rzOut);
                Vector3 I(float a) => new Vector3(Mathf.Cos(a) * rxIn, 0f, Mathf.Sin(a) * rzIn);
                int v0 = k.Vert(O(a0), Vector3.up, new Vector2(O(a0).x, O(a0).z) * uvScale);
                int v1 = k.Vert(O(a1), Vector3.up, new Vector2(O(a1).x, O(a1).z) * uvScale);
                int v2 = k.Vert(I(a1), Vector3.up, new Vector2(I(a1).x, I(a1).z) * uvScale);
                int v3 = k.Vert(I(a0), Vector3.up, new Vector2(I(a0).x, I(a0).z) * uvScale);
                k.Quad(v0, v1, v2, v3);
            }
            return k;
        }

        public static MeshKit Torus(float radius, float tube, int segments, int sides)
        {
            var k = new MeshKit();
            var idx = new int[segments + 1, sides + 1];
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                var outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    var n = outDir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    idx[s, j] = k.Vert(center + n * tube, n, new Vector2(s / (float)segments, j / (float)sides));
                }
            }
            for (int s = 0; s < segments; s++)
                for (int j = 0; j < sides; j++)
                    k.Quad(idx[s, j], idx[s + 1, j], idx[s + 1, j + 1], idx[s, j + 1]);
            return k;
        }

        /// <summary>A faceted crystal standing on Y: a short point below, a long prism, a tall point above (flat shaded).</summary>
        public static MeshKit Crystal(float height, float radius, int sides, float seed)
        {
            var k = new MeshKit();
            var rnd = new System.Random((int)(seed * 1000f) + 7);
            float Jit() => 0.85f + (float)rnd.NextDouble() * 0.3f;
            var ring0 = new Vector3[sides];
            var ring1 = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float r = radius * Jit();
                ring0[i] = new Vector3(Mathf.Cos(a) * r, height * 0.12f, Mathf.Sin(a) * r);
                ring1[i] = new Vector3(Mathf.Cos(a) * r * 0.92f, height * 0.72f, Mathf.Sin(a) * r * 0.92f);
            }
            var bottom = new Vector3(0f, 0f, 0f);
            var top = new Vector3(radius * 0.15f, height, 0f);
            void Face(params Vector3[] p)
            {
                var c = Vector3.zero;
                foreach (var q in p) c += q;
                c /= p.Length;
                var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                var outward = c - new Vector3(0f, c.y, 0f);
                if (outward.sqrMagnitude < 1e-6f) outward = c.y > height * 0.5f ? Vector3.up : Vector3.down;
                if (Vector3.Dot(n, outward) < 0f) n = -n;
                var ids = new int[p.Length];
                for (int i = 0; i < p.Length; i++) ids[i] = k.Vert(p[i], n, new Vector2(i * 0.5f, p[i].y / height));
                for (int i = 1; i < p.Length - 1; i++) k.Tri(ids[0], ids[i], ids[i + 1]);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Face(bottom, ring0[i], ring0[j]);
                Face(ring0[i], ring1[i], ring1[j], ring0[j]);
                Face(ring1[i], top, ring1[j]);
            }
            return k;
        }

        /// <summary>A five-point star prism facing -Z (toward the camera), <paramref name="depth"/> thick.</summary>
        public static MeshKit Star(float outer, float inner, float depth, int points = 5)
        {
            var k = new MeshKit();
            int n = points * 2;
            var rim = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 0.5f + i / (float)n * Mathf.PI * 2f;
                float r = i % 2 == 0 ? outer : inner;
                rim[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var nrm = new Vector3(0f, 0f, side);
                int c = k.Vert(new Vector3(0f, 0f, side * depth * 0.5f), nrm, new Vector2(0.5f, 0.5f));
                var ids = new int[n];
                for (int i = 0; i < n; i++) ids[i] = k.Vert(rim[i] + nrm * depth * 0.5f * 0.35f, nrm, new Vector2(rim[i].x, rim[i].y));
                for (int i = 0; i < n; i++) k.Tri(c, ids[i], ids[(i + 1) % n]);
            }
            for (int i = 0; i < n; i++)
            {
                var a = rim[i]; var b = rim[(i + 1) % n];
                var nrm = Vector3.Cross(b - a, Vector3.forward).normalized;
                if (Vector3.Dot(nrm, (a + b) * 0.5f) < 0f) nrm = -nrm;
                float h = depth * 0.5f * 0.35f;
                int v0 = k.Vert(a + Vector3.back * h, nrm, Vector2.zero), v1 = k.Vert(b + Vector3.back * h, nrm, Vector2.right);
                int v2 = k.Vert(b + Vector3.forward * h, nrm, Vector2.one), v3 = k.Vert(a + Vector3.forward * h, nrm, Vector2.up);
                k.Quad(v0, v1, v2, v3);
            }
            return k;
        }

        /// <summary>A curved petal (or leaf) growing up Y, cupped toward +Z; both sides drawn.</summary>
        public static MeshKit Petal(float length, float width, float cup, float curl)
        {
            var k = new MeshKit();
            const int rows = 7, cols = 4;
            foreach (float side in new[] { 1f, -1f })
            {
                var idx = new int[rows + 1, cols + 1];
                for (int r = 0; r <= rows; r++)
                {
                    float t = r / (float)rows;
                    float w = Mathf.Sin(Mathf.Pow(t, 0.8f) * Mathf.PI) * width * 0.5f;
                    for (int c = 0; c <= cols; c++)
                    {
                        float u = c / (float)cols * 2f - 1f;
                        float x = u * w;
                        float z = cup * (1f - u * u) * w / Mathf.Max(0.001f, width) + curl * t * t;
                        var p = new Vector3(x, t * length, z);
                        var n = new Vector3(-2f * cup * u * w / Mathf.Max(0.001f, width) * 0.5f, -curl * 2f * t * 0.3f, 1f).normalized * side;
                        idx[r, c] = k.Vert(p, n, new Vector2((u + 1f) * 0.5f, t));
                    }
                }
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        k.Quad(idx[r, c], idx[r, c + 1], idx[r + 1, c + 1], idx[r + 1, c]);
            }
            return k;
        }
    }
}
