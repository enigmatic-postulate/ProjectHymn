using System.Collections.Generic;
using System.IO;
using ProjectHymn.Gameplay;
using ProjectHymn.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// Dresses Starlit Spring (Slice 11), an original floating-island arena under a night sky. Everything is generated:
    /// the hand-written Hymn shaders (sky and aurora, water, waterfalls, glow), textures painted by
    /// Tools/paint_spring.py, and meshes from <see cref="MeshKit"/>.
    /// <list type="bullet">
    /// <item>Sky: a starfield with an aurora and a nebula, a ringed moon, distant islets bobbing in the dark.</item>
    /// <item>The island: an elliptical drum of lilac masonry with arched, latticed windows; a pearl-tiled terrace;
    /// a pool with a three-tier fountain crowned by a turning star; water spilling over the whole rim; a violet rock
    /// underside hung with crystals.</item>
    /// <item>Around the pool: two domed spires with orbiting gold rings, luminous trees of petal rosettes, crystal
    /// clusters and gold stars.</item>
    /// <item>The platforms: floating tiled discs with gold rims; the side daises pour water into the pool.</item>
    /// <item>Light and finish: moonlight with soft shadows, coloured point lights, sparkles and mist, bloom, ACES
    /// tonemapping and a vignette. Glowing things pulse on the beat.</item>
    /// </list>
    /// </summary>
    public static class SpringArt
    {
        const string Dir = "Assets/_Project/Art/Generated/Spring";
        const string MatDir = Dir + "/Materials";
        const string ShaderDir = "Assets/_Project/Shaders";

        // The island: an ellipse in X and Z, centred behind the lane so the pool and fountain sit behind the fighting.
        public const float RX = 14.45f, RZ = 7.4f, CZ = 3.0f, WallDepth = 4.4f;   // at the lane (z = 0) the edge is at x = ±13.2
        const float PoolZ = 5.4f, PoolRX = 9.6f, PoolRZ = 2.9f;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        // ---------------- Entry points ----------------

        /// <summary>Build the whole set under <paramref name="level"/> for the layout's platforms.</summary>
        public static void Build(Transform level, StageLayout layout, Camera cam)
        {
            Mats.Clear();
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
            PrepareTextures();
            var root = new GameObject("Starlit Spring Art").transform;
            root.SetParent(level, false);

            Lighting(root);
            Sky(root);
            Island(root);
            Fountain(root, new Vector3(0f, 0f, PoolZ));
            Spire(root, new Vector3(-11.0f, 0f, 5.6f), 0f);
            Spire(root, new Vector3(11.1f, 0f, 5.3f), 1.7f);
            Tree(root, new Vector3(-6.4f, 0f, 8.9f), 4.8f, 0);
            Tree(root, new Vector3(6.2f, 0f, 9.3f), 5.3f, 1);
            Tree(root, new Vector3(-12.2f, 0f, 3.2f), 3.2f, 2);
            Crystals(root);
            foreach (var p in layout.Platforms) Platform(root, p);
            Islets(root);
            Particles(root);
            Finish(root, cam);
            AssetDatabase.SaveAssets();
        }

        // ---------------- Textures and materials ----------------

        static void PrepareTextures()
        {
            foreach (var name in new[] { "SpringWall_Normal", "SpringTiles_Normal" }) Import(name, normal: true);
            foreach (var name in new[] { "SpringWall_Albedo", "SpringWall_Emission", "SpringTiles_Albedo", "SpringRock_Albedo", "SpringSpark" })
                Import(name, normal: false);
        }

        static void Import(string name, bool normal)
        {
            string path = $"{Dir}/{name}.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            var wrap = name == "SpringSpark" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
            if (name == "SpringSpark" && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{name}.png");
        static Shader HymnShader(string file) => AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/{file}.shader");

        static Material Mat(string name, Shader shader)
        {
            if (Mats.TryGetValue(name, out var m) && m != null) return m;
            string path = $"{MatDir}/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (shader != null && m.shader != shader) m.shader = shader;
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        static Color Hdr(uint rgb, float intensity = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f * intensity, ((rgb >> 8) & 255) / 255f * intensity, (rgb & 255) / 255f * intensity, 1f);

        /// <summary>A Hymn/Glow material: a gradient from <paramref name="a"/> (bottom) to <paramref name="b"/> (top) with a rim.</summary>
        static Material Glow(string name, Color a, Color b, float gMin, float gMax, Color rim, float rimPower = 3f, float lit = 0.35f,
                             float pulse = 0.25f, float sparkle = 0f, bool twoSided = false)
        {
            var m = Mat(name, HymnShader("HymnGlow"));
            m.SetColor("_ColorA", a); m.SetColor("_ColorB", b);
            m.SetFloat("_GradientMin", gMin); m.SetFloat("_GradientMax", gMax);
            m.SetColor("_RimColor", rim); m.SetFloat("_RimPower", rimPower);
            m.SetFloat("_Lit", lit); m.SetFloat("_BeatPulse", pulse); m.SetFloat("_Sparkle", sparkle);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            return m;
        }

        static Material Waterfall(string name, float columns, float gaps, float speed, float stretch, float alpha, float streakAlpha, float fadeBottom)
        {
            var m = Mat(name, HymnShader("HymnWaterfall"));
            m.SetColor("_Color", Hdr(0x8cc8ff, 1.1f));
            m.SetColor("_Highlight", Hdr(0xe0eaff, 1.6f));
            m.SetFloat("_Columns", columns); m.SetFloat("_Gaps", gaps); m.SetFloat("_Speed", speed); m.SetFloat("_Stretch", stretch);
            m.SetFloat("_Alpha", alpha); m.SetFloat("_StreakAlpha", streakAlpha); m.SetFloat("_FadeBottom", fadeBottom);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>URP Lit with the generated maps (albedo, optional normal and emission), tiled.</summary>
        static Material LitTex(string name, string albedo, string normal, string emission, Color emissionColor, Vector2 tiling, float smoothness)
        {
            var m = Mat(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", Tex(albedo));
            m.SetTextureScale("_BaseMap", tiling);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", Tex(normal));
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (emission != null)
            {
                m.SetTexture("_EmissionMap", Tex(emission));
                m.SetColor("_EmissionColor", emissionColor);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        static Material LitColor(string name, uint rgb, float smoothness, uint emission = 0, float emissionIntensity = 0f)
        {
            var m = Mat(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Hdr(rgb));
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (emission != 0)
            {
                m.SetColor("_EmissionColor", Hdr(emission, emissionIntensity));
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        // ---------------- Objects ----------------

        static GameObject Obj(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static GameObject Obj(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, bool shadows = true) =>
            Obj(parent, name, mesh, mat, pos, Quaternion.identity, Vector3.one, shadows);

        static Transform Group(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static Light PointLight(Transform parent, string name, Vector3 pos, uint rgb, float intensity, float range)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = Hdr(rgb);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ---------------- Lighting ----------------

        static void Lighting(Transform root)
        {
            var moon = new GameObject("Moonlight", typeof(Light));
            moon.transform.SetParent(root, false);
            moon.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            var l = moon.GetComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(0.78f, 0.74f, 1f); l.intensity = 1.05f;
            l.shadows = LightShadows.Soft; l.shadowStrength = 0.7f;
            RenderSettings.sun = l;

            var fill = new GameObject("Front Fill", typeof(Light));
            fill.transform.SetParent(root, false);
            fill.transform.rotation = Quaternion.Euler(12f, 8f, 0f);
            var f = fill.GetComponent<Light>();
            f.type = LightType.Directional; f.color = new Color(0.62f, 0.56f, 0.95f); f.intensity = 0.32f; f.shadows = LightShadows.None;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.27f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.18f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.07f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.07f, 0.05f, 0.15f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 210f;
        }

        // ---------------- Sky, moon ----------------

        static void Sky(Transform root)
        {
            var sky = Mat("SpringSky", HymnShader("HymnSky"));
            const float w = 280f, h = 150f;
            sky.SetFloat("_Aspect", w / h);
            var quad = new MeshKit();
            int a = quad.Vert(new Vector3(-0.5f, -0.5f, 0f), Vector3.back, new Vector2(0f, 0f));
            int b = quad.Vert(new Vector3(0.5f, -0.5f, 0f), Vector3.back, new Vector2(1f, 0f));
            int c = quad.Vert(new Vector3(0.5f, 0.5f, 0f), Vector3.back, new Vector2(1f, 1f));
            int d = quad.Vert(new Vector3(-0.5f, 0.5f, 0f), Vector3.back, new Vector2(0f, 1f));
            quad.Quad(a, b, c, d);
            Obj(root, "Sky", quad.Save("SpringSkyQuad"), sky, new Vector3(0f, -6f, 115f), Quaternion.identity, new Vector3(w, h, 1f), false);

            // A ringed moon low on the left.
            var moon = Group(root, "Ringed Moon", new Vector3(-58f, 36f, 100f));
            var sphere = MeshKit.Lathe(Arc(1f, 24), 48, Vector3.one).Save("SpringSphere");
            Obj(moon, "Moon", sphere, Glow("Moon", Hdr(0x8e7ad8, 0.9f), Hdr(0xf2e8ff, 1.6f), -1f, 1f, Hdr(0xc8b8ff, 1.2f), 2.2f, 0.6f, 0f),
                Vector3.zero, Quaternion.identity, Vector3.one * 7.5f, false);
            var ring = Obj(moon, "Moon Ring", MeshKit.Torus(1f, 0.035f, 96, 8).Save("SpringMoonRing"),
                Glow("MoonRing", Hdr(0xd8b0ff, 1.4f), Hdr(0xffe6b0, 1.8f), -0.05f, 0.05f, Hdr(0xffffff, 0.6f), 2f, 0f, 0.1f),
                Vector3.zero, Quaternion.Euler(-14f, 0f, 22f), new Vector3(13f, 13f, 13f), false);
            ring.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, 3f, 0f);
        }

        /// <summary>A half-circle profile (top to bottom, outward) for a lathed sphere of radius r.</summary>
        static Vector2[] Arc(float r, int steps)
        {
            var p = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI * 0.5f - i / (float)steps * Mathf.PI;
                p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            p[0].x = 0f; p[steps].x = 0f;
            return p;
        }

        // ---------------- The island ----------------

        static void Island(Transform root)
        {
            var island = Group(root, "Island", new Vector3(0f, 0f, CZ));

            // The drum wall: 32 arches around (the texture holds eight; four repeats keep them in proportion).
            var wall = LitTex("SpringWall", "SpringWall_Albedo", "SpringWall_Normal", "SpringWall_Emission", Hdr(0xffe0a0, 1.6f), Vector2.one, 0.35f);
            Obj(island, "Wall", MeshKit.Drum(RX, RZ, 0f, -WallDepth, 192, 4f).Save("SpringWall"), wall, Vector3.zero);

            // The terrace: pearl tiles over the whole top.
            var tiles = LitTex("SpringTiles", "SpringTiles_Albedo", "SpringTiles_Normal", null, Color.black, new Vector2(0.5f, 0.5f), 0.55f);
            Obj(island, "Terrace", MeshKit.Disc(RX, RZ, 128, 0.5f).Save("SpringTerrace"), tiles, new Vector3(0f, -0.002f, 0f));

            // A moss rim glowing faintly, and a gold cornice ring along the top edge.
            var moss = Glow("Moss", Hdr(0x2f7a5a, 0.8f), Hdr(0x6fd8a0, 1.1f), -0.1f, 0.15f, Hdr(0x9affd8, 0.4f), 2.5f, 0.6f, 0.15f);
            Obj(island, "Moss Rim", MeshKit.Lathe(new[] { new Vector2(0.92f, 0.0f), new Vector2(0.95f, 0.11f), new Vector2(0.985f, 0.16f), new Vector2(1.012f, 0.06f), new Vector2(1.012f, -0.05f) },
                160, new Vector3(RX, 1f, RZ)).Save("SpringMoss"), moss, Vector3.zero);
            var gold = LitColor("SpringGold", 0xe8c068, 0.75f, 0xffc860, 0.5f);
            Obj(island, "Cornice", MeshKit.Torus(1f, 0.012f, 160, 8).Save("SpringCornice"), gold,
                new Vector3(0f, -0.22f, 0f), Quaternion.identity, new Vector3(RX + 0.06f, 8f, RZ + 0.06f));

            // The underside: violet rock tapering to a point, hung with crystals.
            var rock = LitTex("SpringRock", "SpringRock_Albedo", null, null, Color.black, new Vector2(3f, 2f), 0.2f);
            var under = new[] { new Vector2(0f, -WallDepth), new Vector2(1.0f, -WallDepth), new Vector2(0.97f, -5.4f), new Vector2(0.86f, -6.6f),
                                new Vector2(0.66f, -8.2f), new Vector2(0.42f, -10.2f), new Vector2(0.2f, -12.4f), new Vector2(0.05f, -14.2f), new Vector2(0f, -14.6f) };
            Obj(island, "Underside", MeshKit.Lathe(under, 96, new Vector3(RX, 1f, RZ)).Save("SpringUnderside"), rock, Vector3.zero);
            var crystal = Glow("UnderCrystal", Hdr(0x7a4ad8, 1.2f), Hdr(0xff9ad8, 2.2f), 0f, 1f, Hdr(0xffd8ff, 1.4f), 2.2f, 0.2f, 0.4f);
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float k = 0.35f + (float)rnd.NextDouble() * 0.55f;
                float y = Mathf.Lerp(-6f, -11.5f, (k - 0.35f) / 0.55f);
                float r = Mathf.Lerp(0.86f, 0.38f, (k - 0.35f) / 0.55f);
                var pos = new Vector3(Mathf.Cos(a) * RX * r, y, Mathf.Sin(a) * RZ * r);
                float len = 1.2f + (float)rnd.NextDouble() * 2.2f;
                Obj(island, "Hanging Crystal", MeshKit.Crystal(len, len * 0.22f, 6, i).Save($"SpringHang{i}"), crystal, pos,
                    Quaternion.Euler(180f + (float)rnd.NextDouble() * 30f - 15f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 30f - 15f), Vector3.one, false);
            }

            // Water over the rim: a sheet all round the wall, falling past the underside into the dark.
            var rim = Waterfall("RimFall", 150f, 0.42f, 1.4f, 2.2f, 0.12f, 0.55f, 0.55f);
            Obj(island, "Rim Waterfall", MeshKit.Drum(RX + 0.12f, RZ + 0.12f, 0.03f, -12f, 160, 1f).Save("SpringRimFall"), rim, Vector3.zero, false);
            var lip = Waterfall("RimLip", 220f, 0.2f, 0.9f, 1.2f, 0.28f, 0.5f, 0.8f);
            Obj(island, "Rim Lip", MeshKit.Lathe(new[] { new Vector2(0.985f, 0.08f), new Vector2(1.006f, 0.05f), new Vector2(1.009f, -0.3f) }, 160, new Vector3(RX, 1f, RZ)).Save("SpringRimLip"), lip, Vector3.zero, false);

            // The pool: still water behind the lane, a marble lip around it.
            var water = Mat("SpringWater", HymnShader("HymnWater"));
            water.SetVector("_RingCenter", new Vector4(0f, PoolZ, 0f, 0f));
            Obj(root, "Pool", MeshKit.Disc(PoolRX, PoolRZ, 96, 0.25f).Save("SpringPool"), water, new Vector3(0f, 0.035f, PoolZ), false);
            var marble = LitColor("SpringMarble", 0xe6e0f4, 0.8f, 0x8070c0, 0.08f);
            Obj(root, "Pool Lip", MeshKit.Lathe(new[] { new Vector2(0.975f, 0.06f), new Vector2(1.0f, 0.18f), new Vector2(1.035f, 0.2f), new Vector2(1.05f, 0.0f) },
                128, new Vector3(PoolRX, 1f, PoolRZ)).Save("SpringPoolLip"), marble, new Vector3(0f, 0f, PoolZ));
        }

        // ---------------- The fountain ----------------

        static void Fountain(Transform root, Vector3 at)
        {
            var f = Group(root, "Fountain", at);
            f.localScale = new Vector3(1.15f, 1.5f, 1.15f);      // tall enough that its star rises above the crown
            var marble = LitColor("SpringMarble", 0xe6e0f4, 0.8f, 0x8070c0, 0.08f);
            var stem = new[]
            {
                new Vector2(0f, 4.6f), new Vector2(0.25f, 4.6f), new Vector2(0.32f, 4.4f), new Vector2(0.2f, 4.1f), new Vector2(0.24f, 3.3f),
                new Vector2(0.38f, 3.1f), new Vector2(0.28f, 2.8f), new Vector2(0.3f, 1.9f), new Vector2(0.5f, 1.6f), new Vector2(0.42f, 1.3f),
                new Vector2(0.45f, 0.5f), new Vector2(0.9f, 0.25f), new Vector2(1.0f, 0f),
            };
            Obj(f, "Stem", MeshKit.Lathe(stem, 48, Vector3.one).Save("SpringFountainStem"), marble, Vector3.zero);
            Bowl(f, marble, 1.6f, 2.3f, 0.5f, "Low");
            Bowl(f, marble, 3.0f, 1.4f, 0.35f, "Mid");
            Bowl(f, marble, 4.05f, 0.75f, 0.22f, "High");

            // Sheets of water from each bowl's lip down to the next (and the pool).
            var sheet = Waterfall("FountainSheet", 60f, 0.15f, 1.8f, 2.6f, 0.22f, 0.6f, 0.75f);
            Obj(f, "Sheet Low", MeshKit.Lathe(new[] { new Vector2(2.3f, 1.62f), new Vector2(2.42f, 1.2f), new Vector2(2.55f, 0.04f) }, 64, Vector3.one).Save("SpringSheetLow"), sheet, Vector3.zero, false);
            Obj(f, "Sheet Mid", MeshKit.Lathe(new[] { new Vector2(1.4f, 3.02f), new Vector2(1.5f, 2.5f), new Vector2(1.62f, 1.72f) }, 48, Vector3.one).Save("SpringSheetMid"), sheet, Vector3.zero, false);
            Obj(f, "Sheet High", MeshKit.Lathe(new[] { new Vector2(0.75f, 4.07f), new Vector2(0.82f, 3.6f), new Vector2(0.9f, 3.1f) }, 32, Vector3.one).Save("SpringSheetHigh"), sheet, Vector3.zero, false);

            // The crowning star, turning and bright, with a light.
            var starMat = Glow("FountainStar", Hdr(0xff8ad0, 2.6f), Hdr(0xffe6a0, 3.2f), -0.7f, 0.7f, Hdr(0xffffff, 1.6f), 2.5f, 0.1f, 0.6f, 0.4f);
            var star = Obj(f, "Star", MeshKit.Star(0.7f, 0.3f, 0.35f).Save("SpringStar"), starMat, new Vector3(0f, 5.45f, 0f), false);
            star.transform.localScale = new Vector3(1.3f / 1.15f, 1.3f / 1.5f, 1.3f / 1.15f);   // undo the fountain's stretch
            var bob = star.AddComponent<FloatBob>();
            bob.amplitude = 0.12f; bob.period = 3.2f; bob.spin = new Vector3(0f, 40f, 0f);
            PointLight(f, "Star Light", new Vector3(0f, 5.4f, -0.6f), 0xffb0e0, 3.2f, 14f);
            PointLight(f, "Pool Glow", new Vector3(0f, 0.8f, -1.6f), 0x80c0ff, 1.6f, 10f);
        }

        static void Bowl(Transform parent, Material mat, float y, float r, float depth, string name)
        {
            var p = new[]
            {
                new Vector2(0f, y - depth * 0.3f), new Vector2(r * 0.85f, y - depth * 0.15f), new Vector2(r * 0.95f, y), new Vector2(r, y + 0.04f),
                new Vector2(r * 1.04f, y - 0.06f), new Vector2(r * 0.8f, y - depth * 0.7f), new Vector2(r * 0.35f, y - depth), new Vector2(0f, y - depth * 1.05f),
            };
            Obj(parent, $"Bowl {name}", MeshKit.Lathe(p, 48, Vector3.one).Save($"SpringBowl{name}"), mat, Vector3.zero);
            var water = Mat("SpringWater", HymnShader("HymnWater"));
            Obj(parent, $"Bowl Water {name}", MeshKit.Disc(r * 0.9f, r * 0.9f, 40, 0.5f).Save($"SpringBowlWater{name}"), water, new Vector3(0f, y - 0.02f, 0f), false);
        }

        // ---------------- Spires ----------------

        static void Spire(Transform root, Vector3 at, float phase)
        {
            var s = Group(root, "Spire", at);
            var body = Glow("SpireBody", Hdr(0x6a5ab8, 0.85f), Hdr(0xc8b0f0, 1.05f), 0f, 9f, Hdr(0xe0d0ff, 0.5f), 3.5f, 0.75f, 0.1f);
            var profile = new[]
            {
                new Vector2(0f, 10.6f), new Vector2(0.12f, 10.4f), new Vector2(0.45f, 9.6f), new Vector2(0.95f, 8.7f), new Vector2(1.25f, 7.8f),
                new Vector2(1.3f, 7.1f), new Vector2(1.1f, 6.6f), new Vector2(0.78f, 6.3f), new Vector2(0.72f, 3.4f), new Vector2(0.95f, 3.1f),
                new Vector2(0.85f, 2.8f), new Vector2(0.9f, 0.6f), new Vector2(1.45f, 0.3f), new Vector2(1.5f, 0f),
            };
            Obj(s, "Tower", MeshKit.Lathe(profile, 48, Vector3.one).Save("SpringSpire"), body, Vector3.zero);
            var gold = Glow("SpireGold", Hdr(0xd8a040, 1.6f), Hdr(0xffe0a0, 2.4f), -0.1f, 0.1f, Hdr(0xffffff, 0.8f), 2f, 0.3f, 0.5f);
            var band = MeshKit.Torus(1f, 0.08f, 48, 8).Save("SpringBand");
            Obj(s, "Band Low", band, gold, new Vector3(0f, 3.2f, 0f), Quaternion.identity, Vector3.one * 0.92f);
            Obj(s, "Band High", band, gold, new Vector3(0f, 6.5f, 0f), Quaternion.identity, Vector3.one * 0.82f);
            // A ring of lit windows under the dome.
            var window = Glow("SpireWindow", Hdr(0xffd070, 2.5f), Hdr(0xfff0c0, 3f), -0.2f, 0.2f, Hdr(0xffffff, 0.5f), 2f, 0f, 0.6f);
            var dot = MeshKit.Lathe(Arc(0.13f, 8), 12, Vector3.one).Save("SpringWindowDot");
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + 0.2f;
                Obj(s, "Window", dot, window, new Vector3(Mathf.Cos(a) * 1.27f, 7.45f, Mathf.Sin(a) * 1.27f), false);
            }
            // Orbiting gold rings (bloom gives them their halo).
            var ringMesh = MeshKit.Torus(1f, 0.03f, 96, 6).Save("SpringOrbit");
            for (int i = 0; i < 2; i++)
            {
                var pivot = Group(s, "Orbit", new Vector3(0f, 7.6f - i * 4.2f, 0f));
                pivot.localRotation = Quaternion.Euler(18f - i * 30f, phase * 40f, 10f + i * 12f);
                Obj(pivot, "Ring", ringMesh, gold, Vector3.zero, Quaternion.identity, Vector3.one * (2.1f - i * 0.4f), false);
                pivot.gameObject.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, 22f + i * 14f, 0f);
            }
            PointLight(s, "Spire Light", new Vector3(0f, 7.4f, -1.4f), 0xffd090, 2.4f, 9f);
        }

        // ---------------- Luminous trees ----------------

        static readonly (uint a, uint b)[] Blooms =
        {
            (0x6a4ae0, 0xc8a8ff), (0xe05aa0, 0xffc0a0), (0x3aa8a0, 0xa8ffd8), (0x4a78e8, 0x9ae8ff), (0xb04ae0, 0xffa8f0),
        };

        static void Tree(Transform root, Vector3 at, float height, int seed)
        {
            var t = Group(root, "Luminous Tree", at);
            var rnd = new System.Random(31 + seed * 17);
            var bark = Glow("Bark", Hdr(0x6a5a90, 0.7f), Hdr(0xd0c0f0, 0.95f), 0f, 5f, Hdr(0xe8d8ff, 0.35f), 3f, 0.8f, 0f);
            var trunk = new[]
            {
                new Vector2(0f, height), new Vector2(0.18f, height), new Vector2(0.24f, height * 0.7f), new Vector2(0.3f, height * 0.35f),
                new Vector2(0.42f, 0.4f), new Vector2(0.7f, 0f),
            };
            Obj(t, "Trunk", MeshKit.Lathe(trunk, 20, Vector3.one).Save($"SpringTrunk{seed}"), bark, Vector3.zero);
            var petal = MeshKit.Petal(0.95f, 0.42f, 0.25f, 0.22f).Save("SpringPetal");
            int branches = 4 + seed % 2;
            for (int i = 0; i < branches; i++)
            {
                float a = i / (float)branches * 360f + (float)rnd.NextDouble() * 40f;
                float y = height * (0.55f + 0.4f * (float)rnd.NextDouble());
                float len = height * (0.35f + 0.25f * (float)rnd.NextDouble());
                var dir = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(0f, 0f, -48f - (float)rnd.NextDouble() * 18f) * Vector3.up;
                var start = new Vector3(0f, y, 0f);
                var end = start + dir * len;
                var branch = Obj(t, "Branch", MeshKit.Lathe(new[] { new Vector2(0f, 1f), new Vector2(0.08f, 1f), new Vector2(0.16f, 0f), new Vector2(0f, 0f) }, 10, Vector3.one).Save("SpringBranch"),
                    bark, start, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(1f, len, 1f));
                branch.name = "Branch";
                Rosette(t, end, petal, Blooms[(seed * 2 + i) % Blooms.Length], 1.0f + (float)rnd.NextDouble() * 0.5f, rnd);
            }
            Rosette(t, new Vector3(0f, height + 0.1f, 0f), petal, Blooms[(seed + 3) % Blooms.Length], 1.5f, rnd);
            var c = Blooms[seed % Blooms.Length].b;
            PointLight(t, "Bloom Light", new Vector3(0f, height * 0.8f, -1.2f), c, 1.4f, 7f);
        }

        /// <summary>A rosette of petals around a point, opening up and out like the trees' glowing blooms.</summary>
        static void Rosette(Transform parent, Vector3 at, Mesh petal, (uint a, uint b) colors, float size, System.Random rnd)
        {
            var mat = Glow($"Bloom{colors.a:X6}", Hdr(colors.a, 1.25f), Hdr(colors.b, 2.1f), 0f, 0.95f, Hdr(0xffffff, 0.55f), 2.5f, 0.25f, 0.45f, 0f, twoSided: true);
            var g = Group(parent, "Rosette", at);
            g.localScale = Vector3.one * size;
            int rings = 2, per = 9;
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < per; i++)
                {
                    float yaw = i / (float)per * 360f + r * 20f + (float)rnd.NextDouble() * 8f;
                    float tilt = 35f + r * 28f + (float)rnd.NextDouble() * 10f;
                    var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                    Obj(g, "Petal", petal, mat, Vector3.zero, rot, Vector3.one * (1f - r * 0.2f), false);
                }
        }

        // ---------------- Crystals and stars ----------------

        static void Crystals(Transform root)
        {
            var g = Group(root, "Crystals", Vector3.zero);
            var mats = new[]
            {
                Glow("CrystalPink", Hdr(0xa04ad0, 1.3f), Hdr(0xffa0e0, 2.4f), 0f, 1.6f, Hdr(0xffe0ff, 1.4f), 2.2f, 0.25f, 0.4f, 0.6f),
                Glow("CrystalTeal", Hdr(0x2a7ad0, 1.2f), Hdr(0x9affe8, 2.2f), 0f, 1.6f, Hdr(0xe0ffff, 1.2f), 2.2f, 0.25f, 0.4f, 0.6f),
                Glow("CrystalViolet", Hdr(0x4a30a0, 1.2f), Hdr(0xc8a0ff, 2.3f), 0f, 1.6f, Hdr(0xf0e0ff, 1.3f), 2.2f, 0.25f, 0.4f, 0.6f),
            };
            var rnd = new System.Random(77);
            // Clusters around the back half of the rim and beside the pool (never on the lane).
            var spots = new[]
            {
                new Vector3(-12.6f, 0f, 3.4f), new Vector3(-9.8f, 0f, 7.6f), new Vector3(-3.2f, 0f, 9.8f), new Vector3(2.8f, 0f, 10.0f),
                new Vector3(9.0f, 0f, 7.9f), new Vector3(12.9f, 0f, 2.6f), new Vector3(-8.2f, 0f, 3.0f), new Vector3(8.6f, 0f, 3.2f),
                new Vector3(-13.0f, 0f, 1.5f), new Vector3(13.1f, 0f, 1.8f),
            };
            for (int s = 0; s < spots.Length; s++)
            {
                int n = 3 + rnd.Next(3);
                var mat = mats[s % mats.Length];
                for (int i = 0; i < n; i++)
                {
                    float h = 0.6f + (float)rnd.NextDouble() * (i == 0 ? 1.6f : 0.9f);
                    var off = new Vector3((float)rnd.NextDouble() * 0.9f - 0.45f, -0.05f, (float)rnd.NextDouble() * 0.7f - 0.35f);
                    var rot = Quaternion.Euler((float)rnd.NextDouble() * 34f - 17f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 34f - 17f);
                    Obj(g, "Crystal", MeshKit.Crystal(h, h * 0.2f, 6, s * 10 + i).Save($"SpringCrystal{s}_{i}"), mat, spots[s] + off, rot, Vector3.one, false);
                }
            }
            // Gold stars half sunk along the back.
            var starMat = Glow("GoldStar", Hdr(0xd89a30, 1.5f), Hdr(0xffe6a0, 2.2f), -0.5f, 0.5f, Hdr(0xffffff, 0.6f), 2f, 0.4f, 0.5f);
            var star = MeshKit.Star(0.5f, 0.22f, 0.25f).Save("SpringSmallStar");
            foreach (var p in new[] { new Vector3(-5.2f, 0.25f, 9.5f), new Vector3(4.4f, 0.3f, 9.6f), new Vector3(-10.9f, 0.3f, 6.9f), new Vector3(11f, 0.25f, 6.4f) })
                Obj(g, "Gold Star", star, starMat, p, Quaternion.Euler(0f, (float)rnd.NextDouble() * 50f - 25f, (float)rnd.NextDouble() * 40f - 20f), Vector3.one * 0.9f, false);
        }

        // ---------------- Platforms ----------------

        static void Platform(Transform root, CliffsideLayout.Block b)
        {
            var g = Group(root, b.Name, new Vector3(b.CenterX, b.Top, 0f));
            float rx = b.Width * 0.5f + 0.15f, rz = 1.35f;
            var tiles = LitTex("SpringTiles", "SpringTiles_Albedo", "SpringTiles_Normal", null, Color.black, new Vector2(0.5f, 0.5f), 0.55f);
            Obj(g, "Top", MeshKit.Disc(rx, rz, 64, 0.5f).Save($"SpringPlatTop{b.Name.Replace(" ", "")}"), tiles, new Vector3(0f, 0.002f, 0f));
            var under = Glow("PlatformUnder", Hdr(0x4a3aa8, 0.9f), Hdr(0xb8a8f0, 1.1f), -1.6f, 0f, Hdr(0x9ae8ff, 1.0f), 2.6f, 0.6f, 0.2f);
            var profile = new[] { new Vector2(1f, 0f), new Vector2(1f, -0.2f), new Vector2(0.84f, -0.36f), new Vector2(0.5f, -0.6f), new Vector2(0.2f, -0.85f), new Vector2(0f, -1.0f) };
            Obj(g, "Underside", MeshKit.Lathe(profile, 64, new Vector3(rx, 1f, rz)).Save($"SpringPlatUnder{b.Name.Replace(" ", "")}"), under, Vector3.zero);
            var gold = LitColor("SpringGold", 0xe8c068, 0.75f, 0xffc860, 0.5f);
            Obj(g, "Gold Rim", MeshKit.Torus(1f, 0.05f, 96, 8).Save("SpringPlatRim"), gold, new Vector3(0f, -0.04f, 0f), Quaternion.identity, new Vector3(rx, 1.4f, rz));
            var gem = Glow("PlatformGem", Hdr(0x40a0ff, 2.2f), Hdr(0xb0f0ff, 3f), -0.8f, 0f, Hdr(0xffffff, 1.2f), 2f, 0.1f, 0.6f);
            Obj(g, "Gem", MeshKit.Crystal(0.9f, 0.2f, 6, b.CenterX).Save($"SpringPlatGem{b.Name.Replace(" ", "")}"), gem, new Vector3(0f, -0.85f, 0f), Quaternion.Euler(180f, 0f, 0f), Vector3.one, false);
            PointLight(g, "Underglow", new Vector3(0f, -0.9f, -0.6f), 0x80c8ff, 1.2f, 5f);

            // The side daises pour a spout of water from their back edge into the pool behind the lane.
            if (b.Top < 4f)
            {
                var spout = Waterfall("PlatformSpout", 10f, 0.05f, 2.0f, 2.2f, 0.3f, 0.65f, 0.8f);
                var curtain = new MeshKit();
                float w = Mathf.Min(1.4f, b.Width * 0.3f);
                int a0 = curtain.Vert(new Vector3(-w, 0f, 0f), Vector3.back, new Vector2(0f, 0f));
                int a1 = curtain.Vert(new Vector3(w, 0f, 0f), Vector3.back, new Vector2(1f, 0f));
                int a2 = curtain.Vert(new Vector3(w * 1.15f, -b.Top, 0.9f), Vector3.back, new Vector2(1f, 1f));
                int a3 = curtain.Vert(new Vector3(-w * 1.15f, -b.Top, 0.9f), Vector3.back, new Vector2(0f, 1f));
                curtain.Quad(a0, a1, a2, a3);
                Obj(g, "Spout", curtain.Save($"SpringSpout{b.Name.Replace(" ", "")}"), spout, new Vector3(0f, -0.05f, rz - 0.15f), false);
            }
        }

        // ---------------- The way out ----------------

        /// <summary>The goal as a standing ring of light with a star at its heart (it opens once the waves are done).</summary>
        public static void GoalPortal(Transform goal)
        {
            var g = Group(goal, "Star Portal", new Vector3(0f, 1.7f, 0.6f));
            var gold = Glow("SpireGold", Hdr(0xd8a040, 1.6f), Hdr(0xffe0a0, 2.4f), -0.1f, 0.1f, Hdr(0xffffff, 0.8f), 2f, 0.3f, 0.5f);
            var ring = Obj(g, "Ring", MeshKit.Torus(1f, 0.07f, 64, 8).Save("SpringPortal"), gold, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.5f, false);
            ring.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, 25f, 0f);
            var inner = Obj(g, "Inner Ring", MeshKit.Torus(1f, 0.04f, 64, 6).Save("SpringPortalInner"), gold, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.15f, false);
            inner.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, -40f, 0f);
            PointLight(g, "Portal Light", new Vector3(0f, 0f, -0.8f), 0xffd8a0, 1.8f, 6f);
        }

        // ---------------- Distant islets ----------------

        static void Islets(Transform root)
        {
            var rock = LitTex("SpringRock", "SpringRock_Albedo", null, null, Color.black, new Vector2(3f, 2f), 0.2f);
            var wall = LitTex("SpringWall", "SpringWall_Albedo", "SpringWall_Normal", "SpringWall_Emission", Hdr(0xffe0a0, 1.6f), Vector2.one, 0.35f);
            var moss = Glow("Moss", Hdr(0x2f7a5a, 0.8f), Hdr(0x6fd8a0, 1.1f), -0.1f, 0.15f, Hdr(0x9affd8, 0.4f), 2.5f, 0.6f, 0.15f);
            var fall = Waterfall("IsletFall", 40f, 0.4f, 1.0f, 1.6f, 0.1f, 0.5f, 0.5f);
            var spots = new[]
            {
                (new Vector3(-44f, 1f, 55f), 2.4f), (new Vector3(42f, 15f, 66f), 2.8f), (new Vector3(-24f, 21f, 80f), 1.9f),
                (new Vector3(57f, -5f, 72f), 2.2f), (new Vector3(16f, 27f, 94f), 1.6f),
            };
            var under = new[] { new Vector2(0f, -1f), new Vector2(1f, -1f), new Vector2(0.8f, -2.6f), new Vector2(0.4f, -4.2f), new Vector2(0f, -5f) };
            var underMesh = MeshKit.Lathe(under, 32, Vector3.one).Save("SpringIsletUnder");
            var drum = MeshKit.Drum(1f, 1f, 0f, -1f, 48, 1f).Save("SpringIsletWall");
            var top = MeshKit.Disc(1f, 1f, 32, 1f).Save("SpringIsletTop");
            var curtain = MeshKit.Drum(1.02f, 1.02f, 0f, -5f, 48, 1f).Save("SpringIsletFall");
            for (int i = 0; i < spots.Length; i++)
            {
                var (pos, size) = spots[i];
                var g = Group(root, "Distant Islet", pos);
                g.localScale = Vector3.one * size;
                Obj(g, "Wall", drum, wall, Vector3.zero, false);
                Obj(g, "Top", top, moss, Vector3.zero, false);
                Obj(g, "Underside", underMesh, rock, Vector3.zero, false);
                Obj(g, "Fall", curtain, fall, Vector3.zero, false);
                var bloom = Blooms[i % Blooms.Length];
                var crystal = Glow($"Bloom{bloom.a:X6}", Hdr(bloom.a, 1.25f), Hdr(bloom.b, 2.1f), 0f, 0.95f, Hdr(0xffffff, 0.55f), 2.5f, 0.25f, 0.45f, 0f, twoSided: true);
                Obj(g, "Glow Crystal", MeshKit.Crystal(1.4f, 0.28f, 6, 90 + i).Save($"SpringIsletCrystal{i}"), crystal, new Vector3(0.2f, 0f, 0.1f), false);
                var bob = g.gameObject.AddComponent<FloatBob>();
                bob.amplitude = 0.35f + i * 0.08f; bob.period = 7f + i * 1.3f; bob.phase = i * 1.7f;
            }
        }

        // ---------------- Particles ----------------

        static Material SparkMaterial()
        {
            var m = Mat("SpringSpark", Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            m.SetTexture("_BaseMap", Tex("SpringSpark"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static void Particles(Transform root)
        {
            var spark = SparkMaterial();
            // Motes drifting up around the island.
            Emitter(root, "Motes", new Vector3(0f, -1f, CZ), new Vector3(30f, 10f, 16f), spark, 22f, (6f, 10f), (0.15f, 0.45f), (0.08f, 0.2f),
                    new Color(0.75f, 0.85f, 1f), new Color(1f, 0.7f, 0.95f), 0f, 0.08f);
            // Mist and spray where the rim water breaks up below the wall.
            Emitter(root, "Rim Mist", new Vector3(0f, -6.5f, CZ), new Vector3(28f, 1f, 15f), spark, 30f, (2.5f, 4f), (0.2f, 0.6f), (0.5f, 1.4f),
                    new Color(0.55f, 0.75f, 1f, 0.35f), new Color(0.8f, 0.7f, 1f, 0.3f), 0.05f, 0f);
            // Glints rising off the fountain.
            Emitter(root, "Fountain Glints", new Vector3(0f, 2f, PoolZ), new Vector3(4.5f, 2.5f, 1.5f), spark, 10f, (1.5f, 3f), (0.3f, 0.8f), (0.06f, 0.14f),
                    new Color(1f, 0.85f, 0.6f), new Color(0.7f, 0.9f, 1f), -0.05f, 0f);
        }

        static void Emitter(Transform root, string name, Vector3 at, Vector3 box, Material mat, float rate, (float, float) life, (float, float) speed,
                            (float, float) size, Color a, Color b, float gravity, float noise)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);       // emit upward
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 10f; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(gravity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(box.x, box.z, box.y);                      // rotated: local Z is world up
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true; n.strength = new ParticleSystem.MinMaxCurve(noise); n.frequency = 0.3f;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------- Post-processing and camera ----------------

        static void Finish(Transform root, Camera cam)
        {
            string path = $"{Dir}/SpringVolume.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.25f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.92f, 1f));
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.25f);
            grade.contrast.Override(12f);
            grade.saturation.Override(14f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.06f, 0.02f, 0.12f));
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Post Processing", typeof(Volume));
            go.transform.SetParent(root, false);
            var vol = go.GetComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = profile;

            if (cam != null)
            {
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
                cam.farClipPlane = 400f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            new GameObject("Ambient Beat", typeof(AmbientBeat)).transform.SetParent(root, false);
        }
    }
}
