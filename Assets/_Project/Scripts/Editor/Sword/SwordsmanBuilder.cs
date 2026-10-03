using System.Collections.Generic;
using System.IO;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools.Sword
{
    /// <summary>
    /// Generates the M2 prototype's 3D content: the skinned swordsman (bones from RigSpec, every body part rigidly
    /// skinned to its bone through a SkinnedMeshRenderer), the saber and broadsword with tracer trails on their tips,
    /// the straw training dummy, and the materials. Saved under Assets/_Project/Art/M2.
    /// </summary>
    public static class SwordsmanBuilder
    {
        public const string Dir = "Assets/_Project/Art/M2";
        const string MatDir = Dir + "/Materials";
        static readonly Dictionary<uint, Material> Mats = new Dictionary<uint, Material>();

        // ---------------- Mesh building ----------------

        sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int> Bone = new List<int>();
            public readonly List<uint> Colors = new List<uint>();
            public readonly List<List<int>> Tris = new List<List<int>>();

            List<int> Sub(uint color)
            {
                int i = Colors.IndexOf(color);
                if (i < 0) { Colors.Add(color); Tris.Add(new List<int>()); i = Colors.Count - 1; }
                return Tris[i];
            }

            /// <summary>Flat triangle, wound so it faces away from <paramref name="inside"/>.</summary>
            public void Tri(uint color, int bone, Vector3 inside, Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) { var t = b; b = c; c = t; n = -n; }
                n.Normalize();
                var list = Sub(color);
                foreach (var p in new[] { a, b, c })
                {
                    list.Add(V.Count);
                    V.Add(p); N.Add(n); Bone.Add(bone);
                }
            }

            public void Box(uint color, int bone, Vector3 c, Vector3 s)
            {
                var h = s * 0.5f;
                Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
                var f = new[]
                {
                    new[] { P(-1,-1,-1), P(1,-1,-1), P(1,1,-1), P(-1,1,-1) }, new[] { P(-1,-1,1), P(1,-1,1), P(1,1,1), P(-1,1,1) },
                    new[] { P(-1,-1,-1), P(-1,1,-1), P(-1,1,1), P(-1,-1,1) }, new[] { P(1,-1,-1), P(1,1,-1), P(1,1,1), P(1,-1,1) },
                    new[] { P(-1,-1,-1), P(1,-1,-1), P(1,-1,1), P(-1,-1,1) }, new[] { P(-1,1,-1), P(1,1,-1), P(1,1,1), P(-1,1,1) },
                };
                foreach (var q in f) { Tri(color, bone, c, q[0], q[1], q[2]); Tri(color, bone, c, q[0], q[2], q[3]); }
            }

            /// <summary>Prism along Y centred at c; taper = top radius / bottom radius (0 = cone).</summary>
            public void Cylinder(uint color, int bone, Vector3 c, float diameterX, float height, float diameterZ, float taper = 1f, int sides = 8)
            {
                float rx = diameterX * 0.5f, rz = diameterZ * 0.5f, y0 = c.y - height * 0.5f, y1 = c.y + height * 0.5f;
                var bot = new Vector3[sides]; var top = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = Mathf.PI * 2f * (i + 0.5f) / sides;
                    bot[i] = new Vector3(c.x + Mathf.Cos(a) * rx, y0, c.z + Mathf.Sin(a) * rz);
                    top[i] = new Vector3(c.x + Mathf.Cos(a) * rx * taper, y1, c.z + Mathf.Sin(a) * rz * taper);
                }
                var cb = new Vector3(c.x, y0, c.z); var ct = new Vector3(c.x, y1, c.z);
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Tri(color, bone, c, cb, bot[i], bot[j]);
                    if (taper > 0.001f) { Tri(color, bone, c, ct, top[i], top[j]); Tri(color, bone, c, bot[i], bot[j], top[j]); Tri(color, bone, c, bot[i], top[j], top[i]); }
                    else Tri(color, bone, c, bot[i], bot[j], ct);
                }
            }

            /// <summary>Faceted ellipsoid (once-subdivided icosahedron, 80 faces).</summary>
            public void Sphere(uint color, int bone, Vector3 c, Vector3 size)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var v = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
                var f = new List<int[]>
                {
                    new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                    new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1},
                };
                var nf = new List<int[]>();
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    return cache[key] = v.Count - 1;
                }
                foreach (var tri in f)
                {
                    int a = Mid(tri[0], tri[1]), b = Mid(tri[1], tri[2]), d = Mid(tri[2], tri[0]);
                    nf.Add(new[] { tri[0], a, d }); nf.Add(new[] { tri[1], b, a }); nf.Add(new[] { tri[2], d, b }); nf.Add(new[] { a, b, d });
                }
                var h = size * 0.5f;
                Vector3 P(int i) => c + Vector3.Scale(v[i], h);
                foreach (var tri in nf) Tri(color, bone, c, P(tri[0]), P(tri[1]), P(tri[2]));
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.subMeshCount = Tris.Count;
                for (int i = 0; i < Tris.Count; i++) m.SetTriangles(Tris[i], i);
                m.RecalculateBounds();
                return m;
            }

            public Material[] Materials()
            {
                var mats = new Material[Colors.Count];
                for (int i = 0; i < mats.Length; i++) mats[i] = Lit(Colors[i]);
                return mats;
            }
        }

        // ---------------- Materials ----------------

        static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        public static Material Lit(uint rgb, float smoothness = 0.25f, float metallic = 0f)
        {
            if (Mats.TryGetValue(rgb, out var cached) && cached != null) return cached;
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/Lit_{rgb:X6}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(LitShader()); AssetDatabase.CreateAsset(mat, path); }
            var c = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
            mat.SetColor("_BaseColor", c);
            mat.SetColor("_Color", c);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            Mats[rgb] = mat;
            return mat;
        }

        /// <summary>Additive unlit material for trails, rings and particles (uses vertex colour).</summary>
        public static Material Additive()
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/FX_Additive.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            if (shader.name.Contains("Particles"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f); // additive
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_BLENDMODE_ADD");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetColor("_BaseColor", Color.white);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------- Swordsman ----------------

        /// <summary>Build the swordsman prefab. Returns the prefab asset.</summary>
        public static GameObject BuildSwordsman()
        {
            Directory.CreateDirectory(Dir);
            var root = new GameObject("Swordsman");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            // Bones (bind pose: no rotations)
            var bones = new Transform[RigSpec.Bones.Length];
            var byName = new Dictionary<string, int>();
            for (int i = 0; i < RigSpec.Bones.Length; i++)
            {
                var b = RigSpec.Bones[i];
                var t = new GameObject(b.Name).transform;
                t.SetParent(b.Parent == null ? model : bones[byName[b.Parent]], false);
                t.localPosition = new Vector3(b.X, b.Y, b.Z);
                bones[i] = t;
                byName[b.Name] = i;
            }

            // Body parts, built in model space and skinned rigidly to their bones
            var mb = new Builder();
            foreach (var p in RigSpec.Parts)
            {
                int bi = byName[p.Bone];
                var c = model.InverseTransformPoint(bones[bi].TransformPoint(new Vector3(p.CX, p.CY, p.CZ)));
                switch (p.Shape)
                {
                    case RigSpec.Shape.Box: mb.Box(p.Color, bi, c, new Vector3(p.SX, p.SY, p.SZ)); break;
                    case RigSpec.Shape.Cylinder: mb.Cylinder(p.Color, bi, c, p.SX, p.SY, p.SZ, p.Taper); break;
                    default: mb.Sphere(p.Color, bi, c, new Vector3(p.SX, p.SY, p.SZ)); break;
                }
            }
            var mesh = mb.ToMesh("SwordsmanBody");
            var weights = new BoneWeight[mb.V.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = mb.Bone[i], weight0 = 1f };
            mesh.boneWeights = weights;
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * model.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh = SaveMesh(mesh, "SwordsmanBody");

            var body = new GameObject("Body", typeof(SkinnedMeshRenderer));
            body.transform.SetParent(model, false);
            var smr = body.GetComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterials = mb.Materials();
            smr.updateWhenOffscreen = true;

            // Grip in the right hand, both swords under it (the weapon set picks which one shows)
            var grip = new GameObject("Grip").transform;
            grip.SetParent(bones[byName["Hand.R"]], false);
            grip.localPosition = new Vector3(0f, RigSpec.GripY, 0f);
            grip.localRotation = Quaternion.Euler(RigSpec.GripEulerX, 0f, 0f);
            BuildSword(grip, "Saber", MoveSpecs.Saber().BladeLength, broad: false);
            BuildSword(grip, "Broadsword", MoveSpecs.Broadsword().BladeLength, broad: true);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/Swordsman.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildSword(Transform grip, string name, float length, bool broad)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(grip, false);
            var b = new Builder();
            const uint Steel = 0xd8e0f0, Handle = 0x4a2a18, Brass = 0xd8a840;
            float handle = broad ? 0.28f : 0.14f;
            b.Cylinder(Handle, 0, new Vector3(0, -handle * 0.5f, 0), 0.036f, handle, 0.036f);
            b.Sphere(Brass, 0, new Vector3(0, -handle - 0.02f, 0), Vector3.one * (broad ? 0.065f : 0.05f));
            b.Box(Brass, 0, new Vector3(0, 0.015f, 0), broad ? new Vector3(0.32f, 0.04f, 0.05f) : new Vector3(0.15f, 0.03f, 0.045f));
            if (!broad) b.Box(Brass, 0, new Vector3(0.06f, -0.06f, 0), new Vector3(0.015f, 0.13f, 0.02f)); // knuckle guard
            float width = broad ? 0.075f : 0.036f, thick = broad ? 0.016f : 0.01f, tipLen = broad ? 0.12f : 0.1f;
            float bladeLen = length - tipLen;
            b.Box(Steel, 0, new Vector3(0, 0.035f + bladeLen * 0.5f, 0), new Vector3(width, bladeLen, thick));
            b.Cylinder(Steel, 0, new Vector3(0, 0.035f + bladeLen + tipLen * 0.5f, 0), width, tipLen, thick, 0f, 4);
            if (broad) b.Box(0x9aa4c0, 0, new Vector3(0, 0.035f + bladeLen * 0.45f, thick * 0.5f), new Vector3(0.012f, bladeLen * 0.8f, 0.004f)); // fuller
            go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(b.ToMesh(name), name);
            var mats = b.Materials();
            for (int i = 0; i < b.Colors.Count; i++) if (b.Colors[i] == Steel) mats[i] = Lit(Steel, 0.85f, 0.9f);
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;

            var tip = new GameObject("Tip", typeof(TrailRenderer)).transform;
            tip.SetParent(go.transform, false);
            tip.localPosition = new Vector3(0f, length + 0.03f, 0f);
            var trail = tip.GetComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.02f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, broad ? 0.28f : 0.2f), new Keyframe(1f, 0f));
            trail.sharedMaterial = Additive();
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            if (broad)
            {
                var off = new GameObject("OffHandGrip").transform;
                off.SetParent(go.transform, false);
                off.localPosition = new Vector3(0f, -0.17f, 0f);
            }
        }

        // ---------------- Training dummy ----------------

        public static GameObject BuildDummy()
        {
            var root = new GameObject("TrainingDummy", typeof(CapsuleCollider), typeof(TrainingDummy));
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.32f; cap.height = 1.9f; cap.center = new Vector3(0f, 0.95f, 0f);
            var wobble = new GameObject("Wobble", typeof(MeshFilter), typeof(MeshRenderer));
            wobble.transform.SetParent(root.transform, false);
            const uint Wood = 0x7a5232, Straw = 0xd8b860, Rope = 0x9a7a48, Target = 0xc83040, White = 0xf0e8d8;
            var b = new Builder();
            b.Cylinder(Wood, 0, new Vector3(0, 0.04f, 0), 0.6f, 0.08f, 0.6f, 0.9f, 10);
            b.Cylinder(Wood, 0, new Vector3(0, 0.55f, 0), 0.08f, 1.0f, 0.08f);
            b.Cylinder(Straw, 0, new Vector3(0, 1.2f, 0), 0.44f, 0.72f, 0.4f, 0.9f, 10);
            b.Cylinder(Rope, 0, new Vector3(0, 0.9f, 0), 0.46f, 0.05f, 0.42f, 1f, 10);
            b.Cylinder(Rope, 0, new Vector3(0, 1.5f, 0), 0.42f, 0.05f, 0.38f, 1f, 10);
            b.Box(Wood, 0, new Vector3(0, 1.42f, 0), new Vector3(0.95f, 0.07f, 0.07f));
            b.Cylinder(Straw, 0, new Vector3(-0.5f, 1.42f, 0), 0.12f, 0.16f, 0.12f);
            b.Cylinder(Straw, 0, new Vector3(0.5f, 1.42f, 0), 0.12f, 0.16f, 0.12f);
            b.Sphere(Straw, 0, new Vector3(0, 1.75f, 0), new Vector3(0.3f, 0.32f, 0.3f));
            b.Box(White, 0, new Vector3(0, 1.22f, 0.205f), new Vector3(0.24f, 0.24f, 0.012f));
            b.Box(Target, 0, new Vector3(0, 1.22f, 0.212f), new Vector3(0.16f, 0.16f, 0.012f));
            b.Box(White, 0, new Vector3(0, 1.22f, 0.219f), new Vector3(0.07f, 0.07f, 0.012f));
            wobble.GetComponent<MeshFilter>().sharedMesh = SaveMesh(b.ToMesh("Dummy"), "Dummy");
            var mr = wobble.GetComponent<MeshRenderer>();
            mr.sharedMaterials = b.Materials();
            var td = root.GetComponent<TrainingDummy>();
            ProjectHymnSetup.SetRef(td, "wobbleRoot", wobble.transform);
            var so = new SerializedObject(td);
            var arr = so.FindProperty("flashRenderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = mr;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            Directory.CreateDirectory(Dir + "/Meshes");
            string path = $"{Dir}/Meshes/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
            EditorUtility.CopySerialized(m, existing);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
