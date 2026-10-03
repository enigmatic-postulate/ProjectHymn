using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Materials and the generated low-poly models saved as meshes and prefabs under Assets/_Project/Art.
    /// M6: every model gets a painted texture (PaintedAtlas) and one material of its own (plus untextured glow
    /// materials for glowing parts); the building-block materials (one per colour) share a painted stone texture
    /// laid on in world space. All use ProjectHymn/Toon, which draws both the Valor and the cel look.
    /// If the shader fails to compile, everything falls back to URP Simple Lit and the Console says so.
    /// </summary>
    public static class ModelGenerator
    {
        public const string ArtDir = "Assets/_Project/Art";
        public const string MaterialDir = ArtDir + "/Materials";
        public const string ModelDir = ArtDir + "/Models";
        public const string MeshDir = ModelDir + "/Meshes";
        public const string TextureDir = ModelDir + "/Textures";
        const string StonePath = TextureDir + "/PaintedStone.asset";
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

        /// <summary>
        /// Building-block material for a colour (0xRRGGBB): the painted stone texture in world space, tinted.
        /// Glow adds HDR emission so bloom picks it up.
        /// </summary>
        public static Material Toon(uint rgb, bool glow = false) => ColorMaterial($"Toon_{rgb:X6}{(glow ? "_Glow" : "")}", rgb, glow, worldTexture: true);

        /// <summary>Untextured glowing part of a model (visors, batons, notes).</summary>
        public static Material ModelGlow(uint rgb) => ColorMaterial($"Glow_{rgb:X6}", rgb, true, worldTexture: false);

        static Material ColorMaterial(string name, uint rgb, bool glow, bool worldTexture)
        {
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
            if (ToonShaderOk && worldTexture)
            {
                mat.SetTexture("_BaseMap", StoneTexture());
                mat.SetFloat("_WorldUV", 1f);
                mat.SetFloat("_WorldUVScale", 0.5f);
            }
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

        /// <summary>The painted-stone tile used by every building block (64 px, sharp pixels, tiles).</summary>
        public static Texture2D StoneTexture()
        {
            if (_stone != null) return _stone;
            _stone = AssetDatabase.LoadAssetAtPath<Texture2D>(StonePath);
            if (_stone == null) _stone = SaveTexture(PaintedAtlas.StoneTexture(64), 64, StonePath, TextureWrapMode.Repeat);
            return _stone;
        }
        static Texture2D _stone;

        /// <summary>Save RGBA32 pixels (row 0 = bottom) as a texture asset with sharp (point) filtering and no mipmaps.</summary>
        public static Texture2D SaveTexture(byte[] rgba, int size, string path, TextureWrapMode wrap)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool fresh = tex == null;
            if (fresh) tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            else tex.Reinitialize(size, size, TextureFormat.RGBA32, false);
            tex.name = Path.GetFileNameWithoutExtension(path);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = wrap;
            tex.anisoLevel = 0;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            if (fresh) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }

        /// <summary>A model's own material: its painted texture, white base colour.</summary>
        static Material ModelMaterial(string modelName, Texture2D tex)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/Model_{modelName}.mat";
            var shader = ToonShaderOk ? Shader.Find(ToonShaderName) : Shader.Find("Universal Render Pipeline/Simple Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Color ToColor(uint rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        // ---------------- Meshes and prefabs ----------------

        /// <summary>
        /// Unity mesh from a generated model. Painted (textured) models are drawn as one submesh for everything that
        /// isn't glowing (the texture carries the colours) plus one submesh per glowing colour; block meshes keep
        /// one submesh per colour.
        /// </summary>
        public static Mesh ToUnityMesh(LowPolyMesh m, string name, bool painted = false)
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
            mesh.SetUVs(3, smooth); // TEXCOORD3: smoothed normals (outline pass, Valor smooth lighting)
            if (m.UVs.Count == m.Positions.Count)
            {
                var uvs = new List<Vector2>(m.UVs.Count);
                foreach (var (u, v) in m.UVs) uvs.Add(new Vector2(u, v));
                mesh.SetUVs(0, uvs);
            }
            var groups = SubmeshGroups(m, painted);
            mesh.subMeshCount = groups.Count;
            for (int s = 0; s < groups.Count; s++) mesh.SetTriangles(groups[s].tris, s);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Submesh layout: (colour or null for the painted body, glow?, triangle indices).</summary>
        static List<(uint? color, bool glow, List<int> tris)> SubmeshGroups(LowPolyMesh m, bool painted)
        {
            var list = new List<(uint?, bool, List<int>)>();
            if (!painted)
            {
                for (int s = 0; s < m.SubmeshTriangles.Count; s++)
                    list.Add((m.SubmeshColors[s], m.Glowing.Contains(m.SubmeshColors[s]), m.SubmeshTriangles[s]));
                return list;
            }
            var body = new List<int>();
            for (int s = 0; s < m.SubmeshTriangles.Count; s++)
                if (!m.Glowing.Contains(m.SubmeshColors[s])) body.AddRange(m.SubmeshTriangles[s]);
            if (body.Count > 0) list.Add((null, false, body));
            for (int s = 0; s < m.SubmeshTriangles.Count; s++)
                if (m.Glowing.Contains(m.SubmeshColors[s])) list.Add((m.SubmeshColors[s], true, m.SubmeshTriangles[s]));
            return list;
        }

        public static Material[] MaterialsFor(LowPolyMesh m, bool painted = false, Material bodyMaterial = null)
        {
            var groups = SubmeshGroups(m, painted);
            var mats = new Material[groups.Count];
            for (int i = 0; i < mats.Length; i++)
            {
                var g = groups[i];
                mats[i] = g.color == null ? bodyMaterial : painted ? ModelGlow(g.color.Value) : Toon(g.color.Value, g.glow);
            }
            return mats;
        }

        /// <summary>Save a mesh asset (replacing the old one in place so references survive).</summary>
        public static Mesh SaveMesh(LowPolyMesh m, string name, bool painted = false)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var fresh = ToUnityMesh(m, name, painted);
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

        /// <summary>
        /// Save a model prefab: root (no renderer) with a "Mesh" child carrying the renderer. The model is painted
        /// (texture + UVs) and gets its own material.
        /// </summary>
        public static GameObject SavePrefab(LowPolyMesh m, string name)
        {
            Directory.CreateDirectory(ModelDir);
            Material body = null;
            bool hasBody = false;
            for (int s = 0; s < m.SubmeshColors.Count; s++) if (!m.Glowing.Contains(m.SubmeshColors[s])) hasBody = true;
            if (hasBody)
            {
                var atlas = PaintedAtlas.Paint(m, 32, 256);
                var tex = SaveTexture(atlas.Pixels, atlas.Size, $"{TextureDir}/{name}.asset", TextureWrapMode.Clamp);
                body = ModelMaterial(name, tex);
            }
            var mesh = SaveMesh(m, name, painted: true);
            var root = new GameObject(name);
            var child = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = child.GetComponent<MeshRenderer>();
            mr.sharedMaterials = MaterialsFor(m, painted: true, bodyMaterial: body);
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
            _stone = null;
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
            // M6: painted models have their own textures; regenerate when they're missing (first run after M5).
            if (LoadPrefab("Cantor_Robe") == null || LoadPrefab("FirstConductor") == null || LoadMesh("Block_Box") == null
                || AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Cantor_Robe.asset") == null) GenerateAll();
        }
    }
}
