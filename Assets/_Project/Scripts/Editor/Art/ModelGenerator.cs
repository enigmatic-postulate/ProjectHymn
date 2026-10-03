using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Toon materials (one per colour, cached as assets) and the generated low-poly models saved as meshes and
    /// prefabs under Assets/_Project/Art. If the toon shader fails to compile, everything falls back to URP
    /// Simple Lit so the game stays playable, and the Console says so.
    /// </summary>
    public static class ModelGenerator
    {
        public const string ArtDir = "Assets/_Project/Art";
        public const string MaterialDir = ArtDir + "/Materials";
        public const string ModelDir = ArtDir + "/Models";
        public const string MeshDir = ModelDir + "/Meshes";
        const string ToonShaderName = "ProjectHymn/Toon";

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static bool? _toonOk;

        public static bool ToonShaderOk
        {
            get
            {
                if (_toonOk.HasValue) return _toonOk.Value;
                var sh = Shader.Find(ToonShaderName);
                _toonOk = sh != null && !ShaderUtil.ShaderHasError(sh);
                if (!_toonOk.Value)
                    Debug.LogError("[Project Hymn] The toon shader (Shaders/HymnToon.shader) is missing or failed to compile; " +
                                   "using URP Simple Lit instead. Select the shader to see its errors.");
                return _toonOk.Value;
            }
        }

        // ---------------- Materials ----------------

        /// <summary>Toon material for a colour (0xRRGGBB). Glow adds HDR emission so bloom picks it up.</summary>
        public static Material Toon(uint rgb, bool glow = false)
        {
            string name = $"Toon_{rgb:X6}{(glow ? "_Glow" : "")}";
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = ToonShaderOk ? Shader.Find(ToonShaderName) : Shader.Find("Universal Render Pipeline/Simple Lit");
            var color = ToColor(rgb);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            if (glow)
            {
                mat.SetColor("_EmissionColor", color * 1.6f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }

        /// <summary>Flat unlit colour (HP bars, UI-like world elements).</summary>
        public static Material Unlit(uint rgb)
        {
            string name = $"Unlit_{rgb:X6}";
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", ToColor(rgb));
            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }

        /// <summary>See-through dark material for blob shadows (URP Unlit, transparent).</summary>
        public static Material ShadowMaterial()
        {
            const string name = "BlobShadow";
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.06f, 0.03f, 0.1f, 0.5f));
            mat.SetFloat("_Surface", 1f);   // transparent
            mat.SetFloat("_Blend", 0f);     // alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }

        public static Color ToColor(uint rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        // ---------------- Meshes and prefabs ----------------

        public static Mesh ToUnityMesh(LowPolyMesh m, string name)
        {
            var mesh = new Mesh { name = name };
            if (m.Positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var verts = new List<Vector3>(m.Positions.Count);
            var norms = new List<Vector3>(m.Positions.Count);
            var smooth = new List<Vector4>(m.Positions.Count);
            for (int i = 0; i < m.Positions.Count; i++)
            {
                var p = m.Positions[i]; var n = m.Normals[i]; var s = m.SmoothNormals[i];
                verts.Add(new Vector3(p.X, p.Y, p.Z));
                norms.Add(new Vector3(n.X, n.Y, n.Z));
                smooth.Add(new Vector4(s.X, s.Y, s.Z, 0f));
            }
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(3, smooth); // TEXCOORD3: smoothed normals for the outline pass
            mesh.subMeshCount = m.SubmeshTriangles.Count;
            for (int s = 0; s < m.SubmeshTriangles.Count; s++) mesh.SetTriangles(m.SubmeshTriangles[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material[] MaterialsFor(LowPolyMesh m)
        {
            var mats = new Material[m.SubmeshColors.Count];
            for (int i = 0; i < mats.Length; i++) mats[i] = Toon(m.SubmeshColors[i], m.Glowing.Contains(m.SubmeshColors[i]));
            return mats;
        }

        /// <summary>Save a mesh asset (replacing the old one in place so references survive).</summary>
        public static Mesh SaveMesh(LowPolyMesh m, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var fresh = ToUnityMesh(m, name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Save a model prefab: root (no renderer) with a "Mesh" child carrying the renderer.</summary>
        public static GameObject SavePrefab(LowPolyMesh m, string name)
        {
            Directory.CreateDirectory(ModelDir);
            var mesh = SaveMesh(m, name);
            var root = new GameObject(name);
            var child = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = child.GetComponent<MeshRenderer>();
            mr.sharedMaterials = MaterialsFor(m);
            mr.shadowCastingMode = ShadowCastingMode.On;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ModelDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.prefab");
        public static Mesh LoadMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{name}.asset");

        [MenuItem("Project Hymn/Regenerate Placeholder Models", priority = 20)]
        public static void GenerateAll()
        {
            _toonOk = null;
            Cache.Clear();
            AssetDatabase.StartAssetEditing();
            try
            {
                SavePrefab(HymnModels.Cantor(HymnModels.Armor.Robe), "Cantor_Robe");
                SavePrefab(HymnModels.Cantor(HymnModels.Armor.BrassPlate), "Cantor_BrassPlate");
                SavePrefab(HymnModels.Cantor(HymnModels.Armor.MetronomeVest), "Cantor_MetronomeVest");
                SavePrefab(HymnModels.Slime(), "MetronomeSlime");
                SavePrefab(HymnModels.Imp(), "SnareImp");
                SavePrefab(HymnModels.Knight(), "TubaKnight");
                SavePrefab(HymnModels.Bat(), "PiccoloBat");
                SavePrefab(HymnModels.FirstConductor(), "FirstConductor");
                SavePrefab(HymnModels.Blade(), "Weapon_Blade");
                SavePrefab(HymnModels.DrumHammer(), "Weapon_DrumHammer");
                SavePrefab(HymnModels.LuteBow(), "Weapon_LuteBow");
                SavePrefab(HymnModels.NoteShot(), "NoteShot");
                SavePrefab(HymnModels.LoreScroll(), "LoreScroll");
                SavePrefab(HymnModels.Marker(), "Marker");
                SaveMesh(HymnModels.UnitBox(), "Block_Box");
                SaveMesh(HymnModels.UnitCylinder(12), "Block_Cylinder");
                SaveMesh(HymnModels.UnitCone(7), "Block_Cone");
                SaveMesh(HymnModels.UnitRoof(), "Block_Roof");
                SaveMesh(HymnModels.UnitGem(), "Block_Gem");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Project Hymn] Low-poly placeholder models written to {ModelDir} (toon shader {(ToonShaderOk ? "OK" : "FAILED — using Simple Lit")}).");
        }

        /// <summary>Generate models only if they aren't there yet.</summary>
        public static void EnsureGenerated()
        {
            if (LoadPrefab("Cantor_Robe") == null || LoadPrefab("FirstConductor") == null || LoadMesh("Block_Box") == null) GenerateAll();
        }
    }
}
