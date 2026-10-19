using System.Collections.Generic;
using System.IO;
using ProjectHymn.Gameplay;
using ProjectHymn.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// Dresses the Drowned Nave's stages (VS-6), a cathedral sunk in the bay, all generated: limestone masonry in sea-worn
    /// ashlar (textures by Tools/paint_drowned.py) with pilasters and a cornice along the walkable walls and fluted column
    /// drums for the pillars; behind the lane, two receding arcades of columns and arches, a back wall with lit clerestory
    /// windows and a great rose window; light falling from the surface in shafts (Project Hymn/Shafts), caustics rippling
    /// over every floor (Project Hymn/Caustics), kelp swaying in the current (Project Hymn/Kelp), drifting marine snow,
    /// bubble streams and schools of fish. Light from above, teal fog, the Deep look's post-processing.
    /// </summary>
    public static class DrownedArt
    {
        const string Dir = "Assets/_Project/Art/Generated/Drowned";
        const string MatDir = Dir + "/Materials";
        const string MeshDir = Dir + "/Meshes";
        const string ShaderDir = "Assets/_Project/Shaders";
        const float Bottom = -14f;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        // ---------------- Palettes (one look per stage) ----------------

        /// <summary>
        /// A drowned stage's palette. The Kelp Nave, the Choir Loft and the Sunken Apse sit at different heights in the
        /// same cathedral and read differently for it: the nave is green and choked with kelp on the silt; the loft is up
        /// in the galleries where the clerestory lets warm light in, clearer and golden; the apse is the deepest and
        /// coldest, violet and close. Materials are saved per palette (the key is their suffix), so the stages don't
        /// share one asset and overwrite each other's colours.
        /// </summary>
        public sealed class Palette
        {
            public string Key = "Nave";
            // Lighting
            public Color Sun = new Color(0.62f, 1f, 0.88f);
            public float SunIntensity = 1.05f;
            public Vector2 SunAngle = new Vector2(68f, 16f);
            public Color Sky = new Color(0.2f, 0.5f, 0.42f), Equator = new Color(0.08f, 0.26f, 0.24f), Ground = new Color(0.03f, 0.09f, 0.08f);
            public Color Fog = new Color(0.04f, 0.24f, 0.21f);
            public float FogStart = 12f, FogEnd = 76f;
            // Stone
            public Color Stone = new Color(0.78f, 1f, 0.88f), DarkStone = new Color(0.45f, 0.64f, 0.58f), Top = new Color(0.92f, 1f, 0.9f);
            // Water light
            public Color Shaft = new Color(0.5f, 1f, 0.82f);
            public float ShaftIntensity = 1f;
            public Color Caustic = new Color(0.45f, 1f, 0.85f);
            public float CausticIntensity = 0.55f;
            // Windows
            public Color WindowA = new Color(0.2f, 0.75f, 0.85f), WindowB = new Color(0.75f, 1f, 1f);
            public Color RoseA = new Color(0.25f, 0.5f, 1f), RoseB = new Color(0.9f, 0.6f, 1f), RoseLight = new Color(0.6f, 0.65f, 1f);
            // Kelp: two frond colours, how close together they grow (metres) and how tall
            public Color KelpRootA = new Color(0.02f, 0.1f, 0.07f), KelpTipA = new Color(0.32f, 0.62f, 0.3f);
            public Color KelpRootB = new Color(0.05f, 0.08f, 0.03f), KelpTipB = new Color(0.55f, 0.55f, 0.22f);
            public float KelpSpacing = 2.5f, KelpScale = 1f;
            public int KelpClump = 3;
            // Shape: the arcades' spacing and size, and where the windows sit (the loft is up among them)
            public float ArcadeSpacing = 1f, ArcadeScale = 1f;
            public float WindowY = 24f, WindowHeight = 8f, WindowStep = 14f, RoseY = 22f, RoseScale = 1f;
            // Particles
            public Color Snow = new Color(0.75f, 0.95f, 1f, 0.6f), Fish = new Color(0.2f, 0.35f, 0.38f, 0.9f);
            public float SnowRate = 60f, BubbleRate = 4f;
            public int FishBands = 3;
        }

        /// <summary>The Kelp Nave's palette: drowned green, thick kelp on the silt, light filtering a long way down.</summary>
        public static Palette Green => new Palette();

        /// <summary>
        /// The Choir Loft's palette: up in the galleries, close under the clerestory. Warm gold light, clearer water, warm stone,
        /// and only a little weed this high up.
        /// </summary>
        public static Palette Gold => new Palette
        {
            Key = "Loft",
            Sun = new Color(1f, 0.84f, 0.56f), SunIntensity = 1.3f, SunAngle = new Vector2(56f, -26f),
            Sky = new Color(0.44f, 0.38f, 0.3f), Equator = new Color(0.14f, 0.22f, 0.3f), Ground = new Color(0.05f, 0.08f, 0.12f),
            Fog = new Color(0.14f, 0.18f, 0.24f), FogStart = 20f, FogEnd = 100f,
            Stone = new Color(1f, 0.9f, 0.74f), DarkStone = new Color(0.62f, 0.54f, 0.46f), Top = new Color(1f, 0.94f, 0.8f),
            Shaft = new Color(1f, 0.82f, 0.5f), ShaftIntensity = 1.4f,
            Caustic = new Color(1f, 0.88f, 0.58f), CausticIntensity = 0.38f,
            WindowA = new Color(1f, 0.72f, 0.3f), WindowB = new Color(1f, 0.95f, 0.75f),
            RoseA = new Color(1f, 0.55f, 0.2f), RoseB = new Color(1f, 0.9f, 0.5f), RoseLight = new Color(1f, 0.8f, 0.5f),
            KelpRootA = new Color(0.08f, 0.07f, 0.03f), KelpTipA = new Color(0.6f, 0.5f, 0.2f),
            KelpRootB = new Color(0.1f, 0.06f, 0.03f), KelpTipB = new Color(0.72f, 0.46f, 0.22f),
            KelpSpacing = 7f, KelpScale = 0.7f, KelpClump = 2,
            ArcadeSpacing = 0.8f, ArcadeScale = 0.78f,
            WindowY = 13f, WindowHeight = 6f, WindowStep = 9f, RoseY = 12f, RoseScale = 0.8f,
            Snow = new Color(1f, 0.9f, 0.7f, 0.55f), Fish = new Color(0.42f, 0.32f, 0.2f, 0.9f),
            SnowRate = 42f, BubbleRate = 2.5f, FishBands = 2,
        };

        /// <summary>The Sunken Apse's palette: the deepest and coldest room, violet and close around the Precentor.</summary>
        public static Palette Violet => new Palette
        {
            Key = "Apse",
            Sun = new Color(0.68f, 0.68f, 1.1f), SunIntensity = 0.85f, SunAngle = new Vector2(74f, 8f),
            Sky = new Color(0.26f, 0.2f, 0.46f), Equator = new Color(0.12f, 0.08f, 0.26f), Ground = new Color(0.04f, 0.03f, 0.1f),
            Fog = new Color(0.09f, 0.06f, 0.22f), FogStart = 10f, FogEnd = 62f,
            Stone = new Color(0.82f, 0.8f, 1f), DarkStone = new Color(0.46f, 0.44f, 0.66f), Top = new Color(0.8f, 0.78f, 0.96f),
            Shaft = new Color(0.72f, 0.56f, 1.1f), ShaftIntensity = 0.85f,
            Caustic = new Color(0.68f, 0.58f, 1.1f), CausticIntensity = 0.5f,
            WindowA = new Color(0.5f, 0.3f, 1f), WindowB = new Color(0.9f, 0.75f, 1f),
            RoseA = new Color(0.6f, 0.2f, 1f), RoseB = new Color(1f, 0.5f, 0.85f), RoseLight = new Color(0.7f, 0.5f, 1f),
            KelpRootA = new Color(0.04f, 0.03f, 0.1f), KelpTipA = new Color(0.3f, 0.28f, 0.55f),
            KelpRootB = new Color(0.06f, 0.03f, 0.08f), KelpTipB = new Color(0.45f, 0.26f, 0.5f),
            KelpSpacing = 5.5f, KelpScale = 0.85f, KelpClump = 2,
            ArcadeSpacing = 1.35f, ArcadeScale = 1.18f,
            WindowY = 29f, WindowHeight = 10f, WindowStep = 22f, RoseY = 26f, RoseScale = 1.25f,
            Snow = new Color(0.8f, 0.78f, 1f, 0.55f), Fish = new Color(0.26f, 0.22f, 0.4f, 0.9f),
            SnowRate = 48f, FishBands = 2,
        };

        /// <summary>The Air Bell's masonry: warm, under the camp's lamps (its own lighting, not a stage's).</summary>
        public static Palette Lamplit => new Palette
        {
            Key = "Bell",
            Stone = new Color(1f, 0.88f, 0.72f), DarkStone = new Color(0.58f, 0.5f, 0.44f), Top = new Color(1f, 0.9f, 0.74f),
            Caustic = new Color(0.6f, 1f, 1f), CausticIntensity = 0.45f,
        };

        /// <summary>The palette for a stage id (the hub included); the Kelp Nave's green is the default.</summary>
        public static Palette For(string stageId) =>
            stageId == "Loft" ? Gold : stageId == "Apse" ? Violet : stageId == "AirBell" ? Lamplit : Green;

        static Palette P = Green;

        /// <summary>Choose the palette everything built after this call uses.</summary>
        public static void Use(string stageId) { P = For(stageId); Mats.Clear(); }

        // ---------------- Lighting ----------------

        /// <summary>Light from the surface far above, in the current palette's colour (<see cref="Use"/>), with its fog.</summary>
        public static void Lighting()
        {
            var sun = new GameObject("Surface Light", typeof(Light));
            sun.transform.rotation = Quaternion.Euler(P.SunAngle.x, P.SunAngle.y, 0f);
            var l = sun.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = P.SunIntensity; l.color = P.Sun;
            l.shadows = LightShadows.Soft; l.shadowStrength = 0.6f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = P.Sky;
            RenderSettings.ambientEquatorColor = P.Equator;
            RenderSettings.ambientGroundColor = P.Ground;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = P.Fog;
            RenderSettings.fogStartDistance = P.FogStart;
            RenderSettings.fogEndDistance = P.FogEnd;
        }

        // ---------------- Materials ----------------

        static void PrepareTextures()
        {
            Import("DrownedStone_Normal", normal: true, clamp: false);
            foreach (var n in new[] { "DrownedStone_Albedo", "DrownedTop_Albedo" }) Import(n, normal: false, clamp: false);
            foreach (var n in new[] { "Bubble", "Fish" }) Import(n, normal: false, clamp: true);
        }

        static void Import(string name, bool normal, bool clamp)
        {
            string path = $"{Dir}/{name}.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            var wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
            if (clamp && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }

        static string Tex(string name) => $"{Dir}/{name}.png";
        static Material StoneMat => ArtLibrary.Textured($"Drowned_Stone_{P.Key}", Tex("DrownedStone_Albedo"), Tex("DrownedStone_Normal"), P.Stone, 0.35f);
        static Material DarkStoneMat => ArtLibrary.Textured($"Drowned_StoneDark_{P.Key}", Tex("DrownedStone_Albedo"), Tex("DrownedStone_Normal"), P.DarkStone, 0.3f);
        static Material TopMat => ArtLibrary.Textured($"Drowned_Top_{P.Key}", Tex("DrownedTop_Albedo"), null, P.Top, 0.2f);

        static Shader HymnShader(string file) => AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/{file}.shader");

        static Material Mat(string name, Shader shader)
        {
            if (Mats.TryGetValue(name, out var m) && m != null) return m;
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            else if (shader != null && m.shader != shader) m.shader = shader;
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        static Material Shafts(string name, float intensity, float seed)
        {
            var m = Mat($"{name}_{P.Key}", HymnShader("HymnShafts"));
            m.SetColor("_Color", P.Shaft);
            m.SetFloat("_Intensity", intensity * P.ShaftIntensity); m.SetFloat("_Bands", 4f); m.SetFloat("_Speed", 0.12f);
            m.SetFloat("_Fade", 0.95f); m.SetFloat("_Seed", seed);
            return m;
        }

        static Material Caustics()
        {
            var m = Mat($"Drowned_Caustics_{P.Key}", HymnShader("HymnCaustics"));
            m.SetColor("_Color", P.Caustic);
            m.SetFloat("_Intensity", P.CausticIntensity); m.SetFloat("_Scale", 0.85f); m.SetFloat("_Speed", 0.35f); m.SetFloat("_Sharpness", 5f);
            return m;
        }

        static Material Kelp(string name, Color root, Color tip, float height)
        {
            var m = Mat(name, HymnShader("HymnKelp"));
            m.SetColor("_Root", root); m.SetColor("_Tip", tip); m.SetColor("_Rim", new Color(0.2f, 0.8f, 0.7f));
            m.SetFloat("_Height", height); m.SetFloat("_Sway", height * 0.12f); m.SetFloat("_Speed", 0.7f);
            return m;
        }

        static Material Glow(string name, Color a, Color b, float gMin, float gMax, Color rim, float pulse = 0.3f) =>
            DrownedEnemies.GlowMat(name, a, b, gMin, gMax, rim, false, pulse);

        static Material Particle(string name, string texture, bool additive)
        {
            var m = Mat(name, Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD"); else m.DisableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        // ---------------- The lane's blocks ----------------

        /// <summary>
        /// One route block as ruin: masonry with a silted top (walls, steps and floors), or a fluted column drum (pillars).
        /// The collider is the block's box either way, so the jump tests hold.
        /// </summary>
        public static void Block(Transform level, CliffsideLayout.Block b, float frontZ, float backZ)
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
            PrepareTextures();
            var root = new GameObject(b.Name).transform;
            root.SetParent(level, false);
            bool wall = b.Kind == CliffsideLayout.Kind.Wall, pillar = b.Kind == CliffsideLayout.Kind.Pillar, ground = b.Kind == CliffsideLayout.Kind.Ground;
            float front = pillar ? -1.2f : frontZ, back = pillar ? 1.2f : backZ;
            var box = EnvironmentArt.Block(root, $"Stone {b.Name}", new Vector3(b.XMin, Bottom, front), new Vector3(b.XMax, b.Top, back),
                                           wall ? DarkStoneMat : StoneMat, TopMat, 2.6f, 4f, collider: true);
            if (pillar)
            {
                // A fallen column drum standing upright: hide the box, show a fluted drum with a cushion capital.
                foreach (var r in box.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                float rad = b.Width * 0.5f + 0.05f;
                var drum = MeshKit.Lathe(new[]
                {
                    new Vector2(0f, 0f), new Vector2(rad * 0.98f, 0f), new Vector2(rad, -0.18f), new Vector2(rad * 1.1f, -0.32f),
                    new Vector2(rad * 0.92f, -0.5f), new Vector2(rad * 0.92f, -(b.Top - Bottom) + 0.6f), new Vector2(rad * 1.05f, -(b.Top - Bottom)),
                }, 20, Vector3.one, 3f).Save($"Drum_{b.Name.Replace(' ', '_')}_{(int)(b.Top * 10)}", MeshDir);
                Part(root, "Column Drum", drum, StoneMat, new Vector3(b.CenterX, b.Top, 0f));
                return;
            }
            if (wall || ground) return;
            // Pilasters down the camera face every three metres, a cornice under the lip.
            for (float x = b.XMin + 1.2f; x < b.XMax - 0.6f; x += 3f)
                EnvironmentArt.Block(root, "Pilaster", new Vector3(x - 0.28f, Bottom, front - 0.22f), new Vector3(x + 0.28f, b.Top - 0.5f, front + 0.01f),
                                     StoneMat, StoneMat, 2.6f, 2.6f, collider: false);
            EnvironmentArt.Block(root, "Cornice", new Vector3(b.XMin - 0.08f, b.Top - 0.55f, front - 0.32f), new Vector3(b.XMax + 0.08f, b.Top - 0.25f, front + 0.05f),
                                 DarkStoneMat, StoneMat, 2.6f, 2.6f, collider: false);
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 position, Quaternion? rotation = null, Vector3? scale = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            go.isStatic = true;
            return r;
        }

        // ---------------- The set ----------------

        public static void Build(Transform level, StageLayout layout)
        {
            Use(layout.Id);
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.Refresh();
            PrepareTextures();
            var root = new GameObject($"Drowned Art ({P.Key})").transform;
            root.SetParent(level, false);
            float x0 = layout.Route[0].XMin - 20f, x1 = layout.Route[layout.Route.Length - 1].XMax + 20f;
            Arcades(root, x0, x1);
            BackWall(root, x0, x1, (x0 + x1) * 0.5f);
            ShaftsAlong(root, x0, x1);
            foreach (var b in layout.Route) CausticsOn(root, b);
            KelpAlong(root, layout);
            Particles(root, x0, x1);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Two arcades of columns and arches receding behind the lane (the nave's aisles).</summary>
        static void Arcades(Transform root, float x0, float x1)
        {
            var parent = new GameObject("Arcades").transform;
            parent.SetParent(root, false);
            var col = MeshKit.Lathe(new[]
            {
                new Vector2(0f, 30f), new Vector2(1.25f, 30f), new Vector2(1.25f, 29.4f), new Vector2(0.95f, 28.8f),
                new Vector2(0.85f, 28.4f), new Vector2(0.85f, 1.2f), new Vector2(1.1f, 0.8f), new Vector2(1.2f, 0f), new Vector2(0f, 0f),
            }, 24, Vector3.one, 4f).Save("NaveColumn", MeshDir);
            var arch = ArchMesh(4.5f, 0.9f, 1.4f).Save("NaveArch", MeshDir);
            foreach (var (z, baseSpacing, baseScale, mat) in new[] { (11f, 10f, 1f, StoneMat), (24f, 12f, 1.25f, DarkStoneMat) })
            {
                float y0 = Bottom;
                float spacing = baseSpacing * P.ArcadeSpacing, scale = baseScale * P.ArcadeScale;
                for (float x = x0 + (z > 20f ? 6f : 0f); x < x1; x += spacing)
                {
                    Part(parent, "Column", col, mat, new Vector3(x, y0, z), null, new Vector3(scale, scale, scale));
                    // An arch springing from this column to the next, at the capital
                    float span = spacing * 0.5f;
                    Part(parent, "Arch", arch, mat, new Vector3(x + span, y0 + 29f * scale, z), null, new Vector3(span / 4.5f, span / 4.5f, scale));
                }
            }
        }

        /// <summary>A semicircular arch band (radius r to its outer edge, a band <paramref name="band"/> wide and <paramref name="depth"/> deep), standing in the XY plane.</summary>
        static MeshKit ArchMesh(float r, float band, float depth)
        {
            var k = new MeshKit();
            const int seg = 24;
            float ri = r - band, hz = depth * 0.5f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * i / seg, a1 = Mathf.PI * (i + 1) / seg;
                Vector3 O(float a, float z) => new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
                Vector3 I(float a, float z) => new Vector3(Mathf.Cos(a) * ri, Mathf.Sin(a) * ri, z);
                // front and back faces
                foreach (float z in new[] { -hz, hz })
                {
                    var n = new Vector3(0f, 0f, Mathf.Sign(z));
                    k.Quad(k.Vert(O(a0, z), n, Vector2.zero), k.Vert(O(a1, z), n, Vector2.right), k.Vert(I(a1, z), n, Vector2.one), k.Vert(I(a0, z), n, Vector2.up));
                }
                // outer and inner curves
                var no0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f); var no1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                k.Quad(k.Vert(O(a0, -hz), no0, Vector2.zero), k.Vert(O(a1, -hz), no1, Vector2.right), k.Vert(O(a1, hz), no1, Vector2.one), k.Vert(O(a0, hz), no0, Vector2.up));
                k.Quad(k.Vert(I(a0, -hz), -no0, Vector2.zero), k.Vert(I(a1, -hz), -no1, Vector2.right), k.Vert(I(a1, hz), -no1, Vector2.one), k.Vert(I(a0, hz), -no0, Vector2.up));
            }
            return k;
        }

        /// <summary>The far wall: masonry with lit clerestory windows and the great rose window glowing in the middle.</summary>
        static void BackWall(Transform root, float x0, float x1, float cx)
        {
            var parent = new GameObject("Back Wall").transform;
            parent.SetParent(root, false);
            EnvironmentArt.Block(parent, "Wall", new Vector3(x0 - 30f, Bottom, 36f), new Vector3(x1 + 30f, 48f, 40f), DarkStoneMat, DarkStoneMat, 3.2f, 3.2f, collider: false);
            var light = Glow($"Drowned_Window_{P.Key}", P.WindowA * 1.6f, P.WindowB * 2.6f, 0f, 1f, P.WindowB);
            var glass = Glow($"Drowned_RoseGlass_{P.Key}", P.RoseA * 2f, P.RoseB * 2.6f, -6f, 6f, P.RoseB * 1.5f, 0.5f);
            for (float x = x0; x < x1; x += P.WindowStep)
            {
                if (Mathf.Abs(x - cx) < 10f) continue;
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(w.GetComponent<Collider>());
                w.name = "Clerestory";
                w.transform.SetParent(parent, false);
                w.transform.position = new Vector3(x, P.WindowY, 35.9f);
                w.transform.localScale = new Vector3(2.2f, P.WindowHeight, 0.2f);
                w.GetComponent<MeshRenderer>().sharedMaterial = light;
            }
            // The rose: a glowing disc, a ring of tracery and a twelve-point star, with a light of its own.
            var rose = new GameObject("Rose Window").transform;
            rose.SetParent(parent, false);
            rose.position = new Vector3(cx, P.RoseY, 35.6f);
            float rs = P.RoseScale;
            Part(rose, "Glass", MeshKit.Disc(6f * rs, 6f * rs, 48, 0.2f).Save($"RoseDisc_{P.Key}", MeshDir), glass, rose.position, Quaternion.Euler(-90f, 0f, 0f));
            Part(rose, "Tracery", MeshKit.Torus(6.2f * rs, 0.45f * rs, 64, 10).Save($"RoseRing_{P.Key}", MeshDir), StoneMat, rose.position + Vector3.back * 0.2f, Quaternion.Euler(90f, 0f, 0f));
            Part(rose, "Star", MeshKit.Star(5.6f * rs, 2.2f * rs, 0.4f * rs, 12).Save($"RoseStar_{P.Key}", MeshDir), StoneMat, rose.position + Vector3.back * 0.25f);
            var lg = new GameObject("Rose Light", typeof(Light));
            lg.transform.SetParent(rose, false);
            lg.transform.position = rose.position + Vector3.back * 6f * rs;
            var l = lg.GetComponent<Light>();
            l.type = LightType.Point; l.range = 40f; l.intensity = 2f; l.color = P.RoseLight;
        }

        /// <summary>Light shafts slanting down from the surface, across the depth of the scene.</summary>
        static void ShaftsAlong(Transform root, float x0, float x1)
        {
            var parent = new GameObject("Light Shafts").transform;
            parent.SetParent(root, false);
            var quad = new MeshKit();
            int a = quad.Vert(new Vector3(-0.5f, 0f, 0f), Vector3.back, new Vector2(0f, 1f));
            int b = quad.Vert(new Vector3(0.5f, 0f, 0f), Vector3.back, new Vector2(1f, 1f));
            int c = quad.Vert(new Vector3(0.5f, 1f, 0f), Vector3.back, new Vector2(1f, 0f));
            int d = quad.Vert(new Vector3(-0.5f, 1f, 0f), Vector3.back, new Vector2(0f, 0f));
            quad.Quad(a, b, c, d);
            var mesh = quad.Save("ShaftQuad", MeshDir);
            var rng = new System.Random(41);
            int i = 0;
            for (float x = x0; x < x1; x += 9f + (float)rng.NextDouble() * 7f, i++)
            {
                float z = 3f + (float)rng.NextDouble() * 22f;
                float w = 4f + (float)rng.NextDouble() * 6f;
                var r = Part(parent, "Shaft", mesh, Shafts($"Drowned_Shaft{i % 4}", 0.35f + 0.2f * (i % 3), i * 1.37f),
                             new Vector3(x, -6f, z), Quaternion.Euler(0f, 0f, -12f + (float)rng.NextDouble() * 6f), new Vector3(w, 46f, 1f));
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.gameObject.isStatic = false;
            }
        }

        /// <summary>A sheet of caustics lying on a floor's top (just above it), from the camera face back.</summary>
        static void CausticsOn(Transform root, CliffsideLayout.Block b)
        {
            if (b.Kind == CliffsideLayout.Kind.Wall) return;
            var quad = new MeshKit();
            bool pillar = b.Kind == CliffsideLayout.Kind.Pillar;
            float zf = pillar ? -1.1f : -2.4f, zb = pillar ? 1.1f : 6.5f;
            int a = quad.Vert(new Vector3(b.XMin, 0f, zf), Vector3.up, new Vector2(0f, 0f));
            int c1 = quad.Vert(new Vector3(b.XMax, 0f, zf), Vector3.up, new Vector2(1f, 0f));
            int c2 = quad.Vert(new Vector3(b.XMax, 0f, zb), Vector3.up, new Vector2(1f, 1f));
            int d = quad.Vert(new Vector3(b.XMin, 0f, zb), Vector3.up, new Vector2(0f, 1f));
            quad.Quad(a, c1, c2, d);
            var r = Part(root, $"Caustics {b.Name}", quad.Save($"Caustics_{b.Name.Replace(' ', '_')}_{(int)b.XMin}", MeshDir), Caustics(), new Vector3(0f, b.Top + 0.03f, 0f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>A kelp frond: a ribbon that tapers and twists as it rises (swayed by its shader).</summary>
        static Mesh Frond(float height, float width, string name)
        {
            var k = new MeshKit();
            const int seg = 12;
            int prevL = -1, prevR = -1;
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg;
                float y = height * u;
                float w = width * (1f - 0.75f * u) * (0.8f + 0.2f * Mathf.Sin(u * 9f));
                float twist = u * 1.6f;
                var side = new Vector3(Mathf.Cos(twist), 0f, Mathf.Sin(twist)) * w * 0.5f;
                var n = Vector3.Cross(side.normalized, Vector3.up);
                int l = k.Vert(new Vector3(0f, y, 0f) - side, n, new Vector2(0f, u));
                int r = k.Vert(new Vector3(0f, y, 0f) + side, n, new Vector2(1f, u));
                if (prevL >= 0) k.Quad(prevL, prevR, r, l);
                prevL = l; prevR = r;
            }
            return k.Save(name, MeshDir);
        }

        static void KelpAlong(Transform root, StageLayout layout)
        {
            var parent = new GameObject("Kelp").transform;
            parent.SetParent(root, false);
            var fronds = new[] { Frond(2.4f, 0.45f, "Kelp_S"), Frond(3.6f, 0.55f, "Kelp_M"), Frond(5.2f, 0.6f, "Kelp_L") };
            var mats = new[]
            {
                Kelp($"Drowned_KelpA_{P.Key}", P.KelpRootA, P.KelpTipA, 5f),
                Kelp($"Drowned_KelpB_{P.Key}", P.KelpRootB, P.KelpTipB, 5f),
            };
            var rng = new System.Random(17);
            foreach (var b in layout.Route)
            {
                if (b.Kind == CliffsideLayout.Kind.Pillar || b.Width < 4f) continue;
                for (float x = b.XMin + 1f; x < b.XMax - 1f; x += P.KelpSpacing + (float)rng.NextDouble() * 4f)
                {
                    int clump = 1 + rng.Next(P.KelpClump);
                    for (int c = 0; c < clump; c++)
                    {
                        var f = fronds[rng.Next(fronds.Length)];
                        var pos = new Vector3(x + (float)rng.NextDouble() * 1.2f, b.Top, 2.8f + (float)rng.NextDouble() * 3.4f);
                        var r = Part(parent, "Frond", f, mats[rng.Next(mats.Length)], pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                                     Vector3.one * P.KelpScale * (0.8f + (float)rng.NextDouble() * 0.5f));
                        r.gameObject.isStatic = false;
                    }
                }
            }
            // Tall kelp rising out of the deep in front of the cliffs (well below the lane, for depth).
            for (float x = layout.Route[0].XMin; x < layout.Route[layout.Route.Length - 1].XMax; x += 7f + (float)rng.NextDouble() * 6f)
            {
                var r = Part(parent, "Deep Frond", fronds[2], mats[0], new Vector3(x, Bottom + 2f, -4.2f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                             new Vector3(1.2f, 1.6f, 1.2f));
                r.gameObject.isStatic = false;
            }
        }

        static void Particles(Transform root, float x0, float x1)
        {
            var snow = SparkMat();
            float cx = (x0 + x1) * 0.5f, w = x1 - x0;
            // Marine snow drifting down through the whole scene.
            Emitter(root, "Marine Snow", new Vector3(cx, 6f, 6f), new Vector3(w, 30f, 26f), snow, P.SnowRate, (8f, 14f), (0.05f, 0.2f), (0.04f, 0.1f),
                    P.Snow, P.Snow * new Color(0.85f, 0.9f, 0.9f, 0.7f), 0.01f, 0.15f, Vector3.down * 0.25f);
            // Bubble streams from vents in the floor, rising and wobbling.
            var bubble = Particle("Drowned_Bubble", Tex("Bubble"), false);
            var rng = new System.Random(5);
            for (float x = x0 + 8f; x < x1 - 8f; x += 14f + (float)rng.NextDouble() * 10f)
                Emitter(root, "Bubbles", new Vector3(x, -2f, 1.5f + (float)rng.NextDouble() * 6f), new Vector3(0.4f, 0.2f, 0.4f), bubble, P.BubbleRate, (4f, 6f),
                        (1.2f, 2.2f), (0.08f, 0.22f), new Color(0.85f, 1f, 1f, 0.9f), new Color(0.7f, 0.95f, 1f, 0.7f), -0.05f, 0.4f, Vector3.zero);
            // Schools of small fish drifting across the far aisle.
            var fish = Particle("Drowned_Fish", Tex("Fish"), false);
            var bands = new[] { (10f, 18f, 1f), (16f, 26f, -1f), (6f, 30f, 1f) };
            for (int bi = 0; bi < P.FishBands && bi < bands.Length; bi++)
            {
                var (y, z, dir) = bands[bi];
                var ps = Emitter(root, "Fish", new Vector3(dir > 0 ? x0 : x1, y, z), new Vector3(1f, 4f, 3f), fish, 6f, (14f, 20f), (2.5f, 3.5f),
                                 (0.25f, 0.45f), P.Fish, P.Fish * new Color(1.3f, 1.3f, 1.3f, 0.9f), 0f, 0.3f, Vector3.zero);
                ps.transform.rotation = Quaternion.LookRotation(Vector3.right * dir);     // swim along the aisle
                var shape = ps.shape;
                shape.scale = new Vector3(4f, 3f, 1f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.flip = new Vector3(dir > 0 ? 1f : 0f, 0f, 0f);
            }
        }

        static Material SparkMat()
        {
            var m = Particle("Drowned_Snow", "Assets/_Project/Art/Generated/Spring/SpringSpark.png", true);
            return m;
        }

        static ParticleSystem Emitter(Transform root, string name, Vector3 at, Vector3 box, Material mat, float rate, (float, float) life,
                                      (float, float) speed, (float, float) size, Color a, Color b, float gravity, float noise, Vector3 drift)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(root, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);       // emit upward
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 10f; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(gravity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(box.x, box.z, box.y);
            if (drift != Vector3.zero)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(drift.x);
                vel.y = new ParticleSystem.MinMaxCurve(drift.y);
                vel.z = new ParticleSystem.MinMaxCurve(drift.z);
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true; n.strength = new ParticleSystem.MinMaxCurve(noise); n.frequency = 0.4f;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // ---------------- The Air Bell (the region's hub) ----------------

        /// <summary>Inside the great bell: lamplight on bronze, a dim cool spill from the dive pool, a short warm fog.</summary>
        public static void AirBellLighting()
        {
            var key = new GameObject("Lamplight Key", typeof(Light));
            key.transform.rotation = Quaternion.Euler(40f, 24f, 0f);
            var l = key.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = 0.55f; l.color = new Color(1f, 0.82f, 0.6f);
            l.shadows = LightShadows.Soft; l.shadowStrength = 0.5f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.32f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.24f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.16f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.12f, 0.12f, 0.12f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 75f;
        }

        /// <summary>
        /// The Air Bell's set: the bell's bronze curving overhead and behind (seen from inside), the stone deck's caustics,
        /// the dive pool glowing behind the deck, the harbour chain you came down, lamps, crates and diving helmets, and
        /// three <see cref="VerseGroup"/>s for the deep's verses: sea-glass lamps (4), the choir's ribbons (5), the crown's
        /// ring of light (6).
        /// </summary>
        public static void AirBell(Transform level, float x0, float x1, float top, float bellX, float bellZ, float radius)
        {
            Mats.Clear();
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.Refresh();
            PrepareTextures();
            var root = new GameObject("Air Bell Art").transform;
            root.SetParent(level, false);
            var bronze = Bronze("Drowned_BellBronze", Tex("DrownedStone_Albedo"), Tex("DrownedStone_Normal"), new Color(1f, 0.74f, 0.44f), 0.3f);

            // The bell from inside: its profile walked from the lip up to the crown, so the faces (and normals) point inward.
            float h = radius * 1.05f;
            var profile = new[]
            {
                new Vector2(radius, -2f), new Vector2(radius * 0.93f, h * 0.12f), new Vector2(radius * 0.74f, h * 0.38f),
                new Vector2(radius * 0.62f, h * 0.66f), new Vector2(radius * 0.52f, h * 0.86f), new Vector2(radius * 0.32f, h * 0.97f), new Vector2(0f, h),
            };
            var inside = MeshKit.Lathe(profile, 64, Vector3.one, 10f).Save("AirBellInside", MeshDir);
            var bell = Part(root, "Bell Bronze", inside, bronze, new Vector3(bellX, top, bellZ));
            bell.shadowCastingMode = ShadowCastingMode.Off;
            // Bronze hoops round the inside (the bell's sound-bow and waist), slightly proud of the wall.
            foreach (var (y, k) in new[] { (h * 0.12f, 0.92f), (h * 0.38f, 0.735f), (h * 0.66f, 0.615f) })
            {
                var hoop = MeshKit.Lathe(new[] { new Vector2(radius * k - 0.5f, y + 0.4f), new Vector2(radius * k - 0.6f, y), new Vector2(radius * k - 0.5f, y - 0.4f) },
                                         64, Vector3.one, 12f).Save($"AirBellHoop_{(int)y}", MeshDir);
                var hr = Part(root, "Hoop", hoop, DarkStoneMat, new Vector3(bellX, top, bellZ));
                hr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Warm wash lights up the bronze walls (the camp's lamps alone don't reach a 30 m bell).
            foreach (var (wx, wy, wz) in new[] { (bellX - radius * 0.6f, top + 8f, bellZ + 10f), (bellX, top + 14f, bellZ + radius * 0.55f),
                                                 (bellX + radius * 0.6f, top + 8f, bellZ + 10f), (bellX, top + h * 0.7f, bellZ) })
            {
                var wash = new GameObject("Bronze Wash", typeof(Light));
                wash.transform.SetParent(root, false);
                wash.transform.position = new Vector3(wx, wy, wz);
                var wl = wash.GetComponent<Light>();
                wl.type = LightType.Point; wl.range = 26f; wl.intensity = 1.4f; wl.color = new Color(1f, 0.78f, 0.52f);
            }

            // Caustics on the deck, and the dive pool glowing behind it.
            CausticsOn(root, new CliffsideLayout.Block("Deck", x0, x1, top, CliffsideLayout.Kind.Ground));
            var pool = new MeshKit();
            int a = pool.Vert(new Vector3(bellX - radius, 0f, 6.6f), Vector3.up, new Vector2(0f, 0f));
            int b = pool.Vert(new Vector3(bellX + radius, 0f, 6.6f), Vector3.up, new Vector2(1f, 0f));
            int c = pool.Vert(new Vector3(bellX + radius, 0f, bellZ + radius), Vector3.up, new Vector2(1f, 1f));
            int d = pool.Vert(new Vector3(bellX - radius, 0f, bellZ + radius), Vector3.up, new Vector2(0f, 1f));
            pool.Quad(a, b, c, d);
            var poolR = Part(root, "Dive Pool", pool.Save("AirBellPool", MeshDir),
                             Glow("AirBell_Pool", new Color(0.02f, 0.2f, 0.26f), new Color(0.2f, 0.75f, 0.85f), -1f, 1f, new Color(0.3f, 0.9f, 1f), 0.15f),
                             new Vector3(0f, top - 0.6f, 0f));
            poolR.shadowCastingMode = ShadowCastingMode.Off;
            var poolCaustics = Part(root, "Pool Caustics", pool.Save("AirBellPoolCaustics", MeshDir), Caustics(), new Vector3(0f, top - 0.55f, 0f));
            poolCaustics.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var px in new[] { x0 + 6f, bellX, x1 - 4f })
            {
                var pl = new GameObject("Pool Light", typeof(Light));
                pl.transform.SetParent(root, false);
                pl.transform.position = new Vector3(px, top - 0.2f, 12f);
                var pll = pl.GetComponent<Light>();
                pll.type = LightType.Point; pll.range = 16f; pll.intensity = 1.2f; pll.color = new Color(0.35f, 0.85f, 0.95f);
            }

            // The harbour chain you came down, hanging from the crown to the west end of the deck.
            var chain = new GameObject("Harbour Chain").transform;
            chain.SetParent(root, false);
            var linkMesh = MeshKit.Lathe(new[] { new Vector2(0.28f, 0.12f), new Vector2(0.36f, 0f), new Vector2(0.28f, -0.12f), new Vector2(0.2f, 0f), new Vector2(0.28f, 0.12f) },
                                         14, new Vector3(1f, 1f, 1f)).Save("ChainLink", MeshDir);
            var iron = ArtLibrary.Textured("Drowned_Iron", null, null, new Color(0.22f, 0.24f, 0.24f), 0.5f);
            float chainX = x0 + 3f;
            for (float y = top + 0.3f, i = 0; y < top + h * 0.8f; y += 0.55f, i++)
            {
                var link = Part(chain, "Link", linkMesh, iron, new Vector3(chainX, y, 1.8f), Quaternion.Euler(90f, i % 2 == 0 ? 0f : 90f, 0f), new Vector3(1f, 1.6f, 1f));
                link.shadowCastingMode = ShadowCastingMode.Off;
            }

            // The camp: crates, diving helmets, hanging lamps.
            var camp = new GameObject("Camp").transform;
            camp.SetParent(root, false);
            var wood = ArtLibrary.Textured("Drowned_Wood", null, null, new Color(0.42f, 0.3f, 0.2f), 0.2f);
            var rng = new System.Random(81);
            foreach (var cx in new[] { x0 + 6f, x0 + 7.2f, 6f, 20f, 21.1f })
            {
                float sz = 0.8f + (float)rng.NextDouble() * 0.3f;
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(crate.GetComponent<Collider>());
                crate.name = "Crate";
                crate.transform.SetParent(camp, false);
                crate.transform.position = new Vector3(cx, top + sz * 0.5f, 3.2f + (float)rng.NextDouble());
                crate.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 40f - 20f, 0f);
                crate.transform.localScale = Vector3.one * sz;
                crate.GetComponent<MeshRenderer>().sharedMaterial = wood;
                if (rng.NextDouble() < 0.6)
                {
                    var helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(helmet.GetComponent<Collider>());
                    helmet.name = "Diving Helmet";
                    helmet.transform.SetParent(camp, false);
                    helmet.transform.position = crate.transform.position + Vector3.up * (sz * 0.5f + 0.3f);
                    helmet.transform.localScale = Vector3.one * 0.62f;
                    helmet.GetComponent<MeshRenderer>().sharedMaterial = bronze;
                    var port = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(port.GetComponent<Collider>());
                    port.name = "Port";
                    port.transform.SetParent(helmet.transform, false);
                    port.transform.localPosition = new Vector3(0f, 0.02f, -0.42f);
                    port.transform.localScale = new Vector3(0.5f, 0.5f, 0.2f);
                    port.GetComponent<MeshRenderer>().sharedMaterial = Glow("AirBell_Port", new Color(0.1f, 0.3f, 0.32f), new Color(0.5f, 0.9f, 0.9f), -0.5f, 0.5f, new Color(0.7f, 1f, 1f), 0f);
                }
            }
            var lampGlass = Glow("AirBell_Lamp", new Color(1f, 0.6f, 0.25f) * 1.8f, new Color(1f, 0.9f, 0.6f) * 2.4f, -0.3f, 0.3f, new Color(1f, 0.8f, 0.5f), 0.4f);
            foreach (var lx in new[] { x0 + 7f, 0f, 11f, 24f })
                HangingLamp(camp, new Vector3(lx, top + 4.4f, 1.6f), top + h * 0.7f, lampGlass, iron, new Color(1f, 0.72f, 0.42f), 2.2f);

            // The deep's verses.
            var glass = Glow("AirBell_SeaGlass", new Color(0.2f, 0.9f, 0.7f) * 1.6f, new Color(0.7f, 1f, 0.9f) * 2.4f, -0.3f, 0.3f, new Color(0.6f, 1f, 0.9f), 0.5f);
            var v4 = Verse(root, "Verse 4 Sea-glass Lamps", 4);
            for (float x = x0 + 2f; x < x1 - 2f; x += 6.5f)
                HangingLamp(v4, new Vector3(x + 1.5f, top + 2.6f, 4.6f), top + 7f, glass, iron, new Color(0.45f, 1f, 0.85f), 1.6f);
            var v5 = Verse(root, "Verse 5 Choir Ribbons", 5);
            var ribbon = Frond(7f, 0.7f, "ChoirRibbon");
            var ribbonMat = Kelp("AirBell_Ribbon", new Color(0.35f, 0.12f, 0.2f), new Color(0.95f, 0.75f, 0.45f), 7f);
            for (float x = x0 + 4f; x < x1; x += 4.2f)
            {
                var piece = new GameObject("Ribbon").transform;
                piece.SetParent(v5, false);
                piece.position = new Vector3(x, top + h * 0.55f, 6f + (float)rng.NextDouble() * 4f);
                var r = Part(piece, "Ribbon", ribbon, ribbonMat, piece.position, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 180f));
                r.gameObject.isStatic = false;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var v6 = Verse(root, "Verse 6 Crown Ring", 6);
            var crown = new GameObject("Crown").transform;
            crown.SetParent(v6, false);
            crown.position = new Vector3(bellX, top + h * 0.9f, bellZ);
            var ringMesh = MeshKit.Lathe(new[] { new Vector2(radius * 0.4f, 0.5f), new Vector2(radius * 0.4f + 0.6f, 0f), new Vector2(radius * 0.4f, -0.5f), new Vector2(radius * 0.4f - 0.6f, 0f), new Vector2(radius * 0.4f, 0.5f) },
                                         64, Vector3.one).Save("AirBellCrownRing", MeshDir);
            var ring = Part(crown, "Ring of Light", ringMesh, Glow("AirBell_Crown", new Color(1f, 0.8f, 0.4f) * 2f, new Color(0.6f, 1f, 1f) * 3f, -0.6f, 0.6f, Color.white * 2f, 0.6f), crown.position);
            ring.gameObject.isStatic = false;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            var crownLight = new GameObject("Crown Light", typeof(Light));
            crownLight.transform.SetParent(crown, false);
            crownLight.transform.localPosition = new Vector3(0f, -2f, -bellZ);
            var cl = crownLight.GetComponent<Light>();
            cl.type = LightType.Point; cl.range = 30f; cl.intensity = 1.6f; cl.color = new Color(1f, 0.88f, 0.65f);

            // Dust motes in the lamplight (the air pocket), and bubbles breaking from the pool.
            Emitter(root, "Motes", new Vector3((x0 + x1) * 0.5f, top + 6f, 4f), new Vector3(x1 - x0, 10f, 8f), SparkMat(), 18f, (8f, 14f), (0.02f, 0.08f),
                    (0.03f, 0.07f), new Color(1f, 0.85f, 0.6f, 0.5f), new Color(1f, 0.95f, 0.8f, 0.35f), 0f, 0.1f, Vector3.zero);
            var bub = Particle("Drowned_Bubble", Tex("Bubble"), false);
            foreach (var bx in new[] { x0 + 4f, bellX + 3f, x1 - 6f })
                Emitter(root, "Pool Bubbles", new Vector3(bx, top - 0.6f, 12f), new Vector3(6f, 0.1f, 6f), bub, 3f, (0.6f, 1.2f), (0.4f, 0.8f),
                        (0.08f, 0.2f), new Color(0.85f, 1f, 1f, 0.9f), new Color(0.7f, 0.95f, 1f, 0.6f), -0.02f, 0.2f, Vector3.zero);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Bronze that reads under water: mostly diffuse (a fully metallic surface reflects the environment, and down here
        /// there is no sky to reflect, so it renders near-black), with a faint warm glow of its own so the big shapes hold.
        /// </summary>
        static Material Bronze(string name, string albedo, string normal, Color tint, float glow)
        {
            var m = ArtLibrary.Textured(name, albedo, normal, tint, 0.45f);
            m.SetFloat("_Metallic", 0.2f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(tint.r * glow, tint.g * glow * 0.85f, tint.b * glow * 0.6f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        static Transform Verse(Transform root, string name, int verse)
        {
            var go = new GameObject(name, typeof(VerseGroup));
            go.transform.SetParent(root, false);
            go.GetComponent<VerseGroup>().verse = verse;
            return go.transform;
        }

        /// <summary>A lamp on a rod hung from <paramref name="ceiling"/>: glass, a cap, and a point light (one piece under its parent).</summary>
        static void HangingLamp(Transform parent, Vector3 at, float ceiling, Material glass, Material iron, Color light, float intensity)
        {
            var piece = new GameObject("Lamp").transform;
            piece.SetParent(parent, false);
            piece.position = at;
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(rod.GetComponent<Collider>());
            rod.name = "Rod";
            rod.transform.SetParent(piece, false);
            float len = Mathf.Max(0.5f, ceiling - at.y);
            rod.transform.position = at + Vector3.up * (len * 0.5f + 0.3f);
            rod.transform.localScale = new Vector3(0.05f, len * 0.5f, 0.05f);
            rod.GetComponent<MeshRenderer>().sharedMaterial = iron;
            rod.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.name = "Glass";
            bulb.transform.SetParent(piece, false);
            bulb.transform.position = at;
            bulb.transform.localScale = new Vector3(0.42f, 0.56f, 0.42f);
            bulb.GetComponent<MeshRenderer>().sharedMaterial = glass;
            bulb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lg = new GameObject("Lamp Light", typeof(Light));
            lg.transform.SetParent(piece, false);
            lg.transform.position = at + new Vector3(0f, -0.2f, -0.8f);
            var l = lg.GetComponent<Light>();
            l.type = LightType.Point; l.range = 7f; l.intensity = intensity; l.color = light;
        }

        // ---------------- The apse (the boss stage) ----------------

        /// <summary>
        /// The Sunken Apse behind the boss's floor: a great bronze bell hung from a beam, swinging once across every bar
        /// (it tops its swing on each downbeat, whatever the meter), the drowned lectern, and a ring of glowing anemones.
        /// </summary>
        public static void Apse(Transform level, float x0, float x1, float top)
        {
            var root = new GameObject("Apse").transform;
            root.SetParent(level, false);
            float cx = (x0 + x1) * 0.5f;
            var bronze = Bronze("Drowned_Bronze", null, null, new Color(0.72f, 0.62f, 0.4f), 0.22f);
            // The beam and its two posts
            var post = MeshKit.Lathe(new[]
            {
                new Vector2(0f, 14f), new Vector2(0.55f, 14f), new Vector2(0.45f, 13.4f), new Vector2(0.4f, 0.6f), new Vector2(0.6f, 0f), new Vector2(0f, 0f),
            }, 16, Vector3.one, 3f).Save("ApsePost", MeshDir);
            Part(root, "Post W", post, StoneMat, new Vector3(cx - 7f, top, 6.5f));
            Part(root, "Post E", post, StoneMat, new Vector3(cx + 7f, top, 6.5f));
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.name = "Beam";
            beam.transform.SetParent(root, false);
            beam.transform.position = new Vector3(cx, top + 14.2f, 6.5f);
            beam.transform.localScale = new Vector3(15.5f, 0.9f, 1.1f);
            beam.GetComponent<MeshRenderer>().sharedMaterial = DarkStoneMat;
            // The bell on a swinging pivot under the beam
            var pivot = new GameObject("Great Bell", typeof(BellSwing)).transform;
            pivot.SetParent(root, false);
            pivot.position = new Vector3(cx, top + 13.7f, 6.5f);
            var so = new SerializedObject(pivot.GetComponent<BellSwing>());
            so.FindProperty("amplitude").floatValue = 14f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var bellMesh = MeshKit.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.9f, 0f), new Vector2(1.5f, -0.6f), new Vector2(1.75f, -2.2f), new Vector2(2.1f, -4.1f),
                new Vector2(2.75f, -5.1f), new Vector2(2.85f, -5.4f), new Vector2(2.55f, -5.35f), new Vector2(1.9f, -4.4f),
                new Vector2(1.55f, -2.4f), new Vector2(1.3f, -0.8f), new Vector2(0.75f, -0.35f), new Vector2(0f, -0.35f),
            }, 40, Vector3.one).Save("GreatBell", MeshDir);
            var bell = new GameObject("Bell", typeof(MeshFilter), typeof(MeshRenderer));
            bell.transform.SetParent(pivot, false);
            bell.GetComponent<MeshFilter>().sharedMesh = bellMesh;
            bell.GetComponent<MeshRenderer>().sharedMaterial = bronze;
            var clapper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(clapper.GetComponent<Collider>());
            clapper.name = "Clapper";
            clapper.transform.SetParent(pivot, false);
            clapper.transform.localPosition = new Vector3(0f, -4.8f, 0f);
            clapper.transform.localScale = Vector3.one * 0.6f;
            clapper.GetComponent<MeshRenderer>().sharedMaterial = Glow("Apse_Clapper", new Color(0.3f, 0.9f, 1f) * 2f,
                                                                       new Color(0.8f, 1f, 1f) * 2.5f, -0.3f, 0.3f, Color.white * 1.5f, 0.6f);
            var lg = new GameObject("Bell Light", typeof(Light));
            lg.transform.SetParent(pivot, false);
            lg.transform.localPosition = new Vector3(0f, -5.2f, -2.5f);
            var l = lg.GetComponent<Light>();
            l.type = LightType.Point; l.range = 12f; l.intensity = 2f; l.color = new Color(0.45f, 0.95f, 1f);
            var key = new GameObject("Bell Key", typeof(Light));
            key.transform.SetParent(root, false);
            key.transform.position = new Vector3(cx, top + 11f, 1.2f);
            var kl = key.GetComponent<Light>();
            kl.type = LightType.Point; kl.range = 14f; kl.intensity = 1.6f; kl.color = new Color(1f, 0.82f, 0.58f);
            // The drowned lectern at the back of the floor
            var lectern = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lectern.GetComponent<Collider>());
            lectern.name = "Lectern";
            lectern.transform.SetParent(root, false);
            lectern.transform.position = new Vector3(cx + 9f, top + 0.8f, 3.2f);
            lectern.transform.localScale = new Vector3(0.9f, 1.6f, 0.7f);
            lectern.GetComponent<MeshRenderer>().sharedMaterial = bronze;
            // Anemones: small glowing domes along the back of the floor, pulsing with the beat
            var dome = MeshKit.Lathe(new[] { new Vector2(0f, 0.5f), new Vector2(0.3f, 0.42f), new Vector2(0.45f, 0.15f), new Vector2(0.4f, 0f), new Vector2(0f, 0f) },
                                     16, Vector3.one).Save("Anemone", MeshDir);
            var rng = new System.Random(66);
            var anemone = Glow("Apse_Anemone", new Color(0.9f, 0.3f, 0.7f) * 1.6f, new Color(1f, 0.85f, 0.6f) * 2f, 0f, 0.5f, new Color(1f, 0.6f, 0.9f), 0.5f);
            for (float x = x0 + 1.5f; x < x1 - 1f; x += 2.2f + (float)rng.NextDouble() * 2f)
            {
                float z = 2.2f + (float)rng.NextDouble() * 2.5f;
                float s = 0.7f + (float)rng.NextDouble() * 0.7f;
                var r = Part(root, "Anemone", dome, anemone, new Vector3(x, top, z), null, Vector3.one * s);
                r.gameObject.isStatic = false;
                r.gameObject.AddComponent<BeatPulse>();
            }
        }

        // ---------------- The way out ----------------

        /// <summary>The stage's way out: a ruined arch with a hymnal of light hovering in it.</summary>
        public static void GoalArch(Transform goal, Vector3 at)
        {
            var col = MeshKit.Lathe(new[]
            {
                new Vector2(0f, 4.6f), new Vector2(0.42f, 4.6f), new Vector2(0.36f, 4.3f), new Vector2(0.3f, 0.4f), new Vector2(0.42f, 0f), new Vector2(0f, 0f),
            }, 16, Vector3.one, 2f).Save("GoalColumn", MeshDir);
            Part(goal, "Column L", col, StoneMat, at + new Vector3(-1.7f, 0f, 1.4f));
            Part(goal, "Column R", col, StoneMat, at + new Vector3(1.7f, 0f, 1.4f));
            Part(goal, "Arch", ArchMesh(2.1f, 0.5f, 0.7f).Save("GoalArch", MeshDir), StoneMat, at + new Vector3(0f, 4.6f, 1.4f));
            var lg = new GameObject("Hymnal Light", typeof(Light));
            lg.transform.SetParent(goal, false);
            lg.transform.position = at + new Vector3(0f, 2.2f, 0.4f);
            var l = lg.GetComponent<Light>();
            l.type = LightType.Point; l.range = 7f; l.intensity = 2.2f; l.color = new Color(0.65f, 1f, 1f);
        }
    }
}
