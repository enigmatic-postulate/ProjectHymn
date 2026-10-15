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
    /// What Saltmere gets back as the verses come home (VS-5 Part A), built into the village scene under
    /// <see cref="VerseGroup"/>s that <see cref="VerseRestoration"/> shows and celebrates at run time:
    /// <list type="bullet">
    /// <item>Always: iron lantern posts along the street, their glass dark.</item>
    /// <item>Verse 1 (the tide): the lanterns light, warm and pulsing with the waltz.</item>
    /// <item>Verse 2 (the bell): bunting strung post to post, with little lights along the string.</item>
    /// <item>Verse 3 (the summit): a golden star turning over the gate, and motes of light drifting up the street.</item>
    /// </list>
    /// Meshes come from <see cref="MeshKit"/>; the lit glass, beads and star use the Hymn glow shader.
    /// </summary>
    public static class VillageArt
    {
        const string Dir = "Assets/_Project/Art/Generated/Village";
        const string MatDir = Dir + "/Materials";
        const string MeshDir = Dir + "/Meshes";
        const string ShaderDir = "Assets/_Project/Shaders";

        /// <summary>Lantern posts along the street (x), clear of the villagers, doors and the gate.</summary>
        public static readonly float[] PostX = { -21f, -14f, -5f, 3f, 13f, 23f, 32f, 40f, 48f, 54f };
        const float PostZ = 2.1f, LampY = 2.67f;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        public static void Build(Transform level, float groundTop)
        {
            Mats.Clear();
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.Refresh();
            var root = new GameObject("Verses").transform;
            root.SetParent(level, false);

            var post = PostMesh();
            var cage = CageMesh();
            var glass = GlassMesh(1f, "LanternGlass");
            var flame = GlassMesh(1.1f, "LanternFlame");
            var iron = LitColor("VillageIron", 0x2a2630, 0.45f);
            var darkGlass = LitColor("LanternGlassDark", 0x3a2c22, 0.8f);
            var lit = Glow("LanternLit", Hdr(0xff9a3a, 2.2f), Hdr(0xfff0c0, 3.2f), -0.12f, 0.12f, Hdr(0xffd890, 1.5f), 2f, 0.15f, 0.35f);

            // Always: the posts with dark glass.
            var posts = new GameObject("Lantern Posts").transform;
            posts.SetParent(root, false);
            foreach (float x in PostX)
            {
                var p = new GameObject("Lantern Post").transform;
                p.SetParent(posts, false);
                p.position = new Vector3(x, groundTop, PostZ);
                Part(p, "Post", post, iron, Vector3.zero);
                Part(p, "Cage", cage, iron, Vector3.zero);
                Part(p, "Glass", glass, darkGlass, Vector3.zero);
            }

            // Verse 1: the flames.
            var v1 = Group(root, "Verse 1: Lanterns", 1);
            foreach (float x in PostX)
            {
                var f = new GameObject("Lantern Flame").transform;
                f.SetParent(v1, false);
                f.position = new Vector3(x, groundTop + LampY, PostZ);
                Part(f, "Flame", flame, lit, Vector3.zero).shadowCastingMode = ShadowCastingMode.Off;
                var lg = new GameObject("Light", typeof(Light), typeof(BeatLight));
                lg.transform.SetParent(f, false);
                lg.transform.localPosition = new Vector3(0f, 0f, -0.35f);
                var l = lg.GetComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.72f, 0.42f);
                l.range = 6.5f;
                l.intensity = 2.4f;
                l.shadows = LightShadows.None;
                var bl = lg.GetComponent<BeatLight>();
                bl.pulse = 0.3f; bl.flicker = 0.06f;
            }

            // Verse 2: bunting between neighbouring posts, with a bead of light every other flag.
            var v2 = Group(root, "Verse 2: Bunting", 2);
            var flagMats = new[]
            {
                LitColor("FlagRose", 0xe0607a, 0.2f, 0xe0607a, 0.25f), LitColor("FlagGold", 0xf0c050, 0.2f, 0xf0c050, 0.25f),
                LitColor("FlagSky", 0x60b0e8, 0.2f, 0x60b0e8, 0.25f), LitColor("FlagLilac", 0xa080e0, 0.2f, 0xa080e0, 0.25f),
            };
            var bead = Glow("BuntingBead", Hdr(0xffc070, 2.4f), Hdr(0xfff4d0, 3f), -0.05f, 0.05f, Hdr(0xffe0a0, 1.2f), 2f, 0f, 0.6f);
            var beadMesh = GlassMesh(0.28f, "BuntingBead");
            var stringMat = LitColor("BuntingString", 0x2a2024, 0.3f);
            for (int i = 0; i + 1 < PostX.Length; i++)
            {
                float a = PostX[i], b = PostX[i + 1];
                var span = new GameObject("Bunting").transform;
                span.SetParent(v2, false);
                span.position = new Vector3((a + b) * 0.5f, groundTop + 2.45f, PostZ - 0.02f);
                BuildSpan(span, a - span.position.x, b - span.position.x, 0.42f + (b - a) * 0.025f, i, flagMats, stringMat, bead, beadMesh);
            }

            // Verse 3: the star over the gate, and motes in the street.
            var v3 = Group(root, "Verse 3: Star", 3);
            var star = new GameObject("Gate Star").transform;
            star.SetParent(v3, false);
            star.position = new Vector3(VillageLayout.GateX, groundTop + 7.4f, 1.2f);
            var starMat = Glow("GateStar", Hdr(0xffb040, 2.6f), Hdr(0xfff6d8, 4f), -0.8f, 0.8f, Hdr(0xffffff, 2f), 2.5f, 0.1f, 0.5f, 0.35f);
            var starGo = Part(star, "Star", MeshKit.Star(0.95f, 0.4f, 0.32f).Save("GateStar", MeshDir), starMat, Vector3.zero).gameObject;
            starGo.AddComponent<Spin>().degreesPerSecond = new Vector3(0f, 40f, 0f);
            var bob = star.gameObject.AddComponent<FloatBob>();
            bob.amplitude = 0.25f; bob.period = 5f;
            var sl = new GameObject("Star Light", typeof(Light), typeof(BeatLight));
            sl.transform.SetParent(star, false);
            sl.transform.localPosition = new Vector3(0f, -0.6f, -1.2f);
            var starLight = sl.GetComponent<Light>();
            starLight.type = LightType.Point;
            starLight.color = new Color(1f, 0.85f, 0.6f);
            starLight.range = 16f;
            starLight.intensity = 3f;
            sl.GetComponent<BeatLight>().pulse = 0.25f;
            var spark = SparkMaterial();
            Emitter(star, "Star Glints", Vector3.zero, new Vector3(2.5f, 1.5f, 1f), spark, 8f, (1.5f, 3f), (0.2f, 0.6f), (0.06f, 0.16f),
                    new Color(1f, 0.9f, 0.6f), new Color(1f, 0.75f, 0.45f), -0.03f, 0.1f);
            var motes = new GameObject("Street Motes").transform;
            motes.SetParent(v3, false);
            motes.position = new Vector3((VillageLayout.XMin + VillageLayout.XMax) * 0.5f, groundTop + 0.5f, 2f);
            Emitter(motes, "Motes", Vector3.zero, new Vector3(VillageLayout.XMax - VillageLayout.XMin, 1f, 5f), spark, 26f, (5f, 9f), (0.15f, 0.45f),
                    (0.05f, 0.14f), new Color(1f, 0.85f, 0.55f), new Color(0.75f, 0.85f, 1f), -0.02f, 0.12f);
            AssetDatabase.SaveAssets();
        }

        static Transform Group(Transform root, string name, int verse)
        {
            var go = new GameObject(name, typeof(VerseGroup));
            go.transform.SetParent(root, false);
            go.GetComponent<VerseGroup>().verse = verse;
            return go.transform;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 local)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return r;
        }

        // ---------------- Meshes ----------------

        static Mesh PostMesh()
        {
            var k = MeshKit.Lathe(new[]
            {
                new Vector2(0f, 2.5f), new Vector2(0.055f, 2.5f), new Vector2(0.055f, 0.42f), new Vector2(0.09f, 0.36f),
                new Vector2(0.09f, 0.3f), new Vector2(0.15f, 0.22f), new Vector2(0.15f, 0f), new Vector2(0f, 0f),
            }, 14, Vector3.one);
            return k.Save("LanternPost", MeshDir);
        }

        static Mesh CageMesh()
        {
            var k = new MeshKit();
            // A pointed roof with a finial, a base plate, and four bars between.
            k.Append(MeshKit.Lathe(new[]
            {
                new Vector2(0f, 3.12f), new Vector2(0.03f, 3.08f), new Vector2(0.03f, 3.02f), new Vector2(0.21f, 2.86f),
                new Vector2(0.21f, 2.83f), new Vector2(0.12f, 2.83f), new Vector2(0f, 2.83f),
            }, 4, Vector3.one), Matrix4x4.Rotate(Quaternion.Euler(0f, 45f, 0f)));
            k.Append(MeshKit.Lathe(new[]
            {
                new Vector2(0f, 2.53f), new Vector2(0.16f, 2.53f), new Vector2(0.16f, 2.49f), new Vector2(0.06f, 2.47f), new Vector2(0f, 2.47f),
            }, 4, Vector3.one), Matrix4x4.Rotate(Quaternion.Euler(0f, 45f, 0f)));
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                k.Append(MeshKit.Drum(0.014f, 0.014f, 2.84f, 2.52f, 6, 1f), Matrix4x4.Translate(new Vector3(Mathf.Cos(a) * 0.15f, 0f, Mathf.Sin(a) * 0.15f)));
            }
            return k.Save("LanternCage", MeshDir);
        }

        /// <summary>A rounded glass (scaled for the lit flame and the bunting's beads), centred on its own origin.</summary>
        static Mesh GlassMesh(float scale, string name)
        {
            var prof = new[]
            {
                new Vector2(0f, 0.14f), new Vector2(0.07f, 0.12f), new Vector2(0.115f, 0.06f), new Vector2(0.125f, 0f),
                new Vector2(0.11f, -0.07f), new Vector2(0.06f, -0.12f), new Vector2(0f, -0.13f),
            };
            for (int i = 0; i < prof.Length; i++) prof[i] *= scale;
            var k = MeshKit.Lathe(prof, 16, Vector3.one);
            var m = new MeshKit();
            m.Append(k, name == "LanternGlass" ? Matrix4x4.Translate(new Vector3(0f, LampY, 0f)) : Matrix4x4.identity);
            return m.Save(name, MeshDir);
        }

        /// <summary>One span of bunting from x = a to b (local), sagging by <paramref name="sag"/>.</summary>
        static void BuildSpan(Transform span, float a, float b, float sag, int seed, Material[] flagMats, Material stringMat, Material beadMat, Mesh bead)
        {
            float len = b - a;
            int flags = Mathf.Max(3, Mathf.RoundToInt(len / 0.5f));
            var kits = new MeshKit[flagMats.Length];
            for (int i = 0; i < kits.Length; i++) kits[i] = new MeshKit();
            var line = new MeshKit();
            System.Func<float, Vector3> at = t => new Vector3(a + len * t, -sag * 4f * t * (1f - t), 0f);
            // The string: a thin strip, both sides.
            const int seg = 24;
            for (int s = 0; s < seg; s++)
            {
                Vector3 p0 = at(s / (float)seg), p1 = at((s + 1) / (float)seg);
                foreach (float side in new[] { -1f, 1f })
                {
                    var n = new Vector3(0f, 0f, side);
                    int v0 = line.Vert(p0 + Vector3.up * 0.012f, n, Vector2.zero), v1 = line.Vert(p1 + Vector3.up * 0.012f, n, Vector2.right);
                    int v2 = line.Vert(p1 - Vector3.up * 0.012f, n, Vector2.one), v3 = line.Vert(p0 - Vector3.up * 0.012f, n, Vector2.up);
                    line.Quad(v0, v1, v2, v3);
                }
            }
            var beads = new List<Vector3>();
            for (int f = 0; f < flags; f++)
            {
                float t = (f + 0.5f) / flags;
                var top = at(t);
                var kit = kits[(f + seed) % kits.Length];
                // A pennant: a triangle hanging from the string, both sides, with a slight random sway.
                float sway = Mathf.Sin(f * 2.3f + seed) * 0.05f;
                Vector3 l = top + new Vector3(-0.15f, 0f, 0f), r = top + new Vector3(0.15f, 0f, 0f), tip = top + new Vector3(sway, -0.34f, 0.03f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var n = new Vector3(0f, 0f, side);
                    kit.Tri(kit.Vert(l, n, new Vector2(0f, 1f)), kit.Vert(r, n, new Vector2(1f, 1f)), kit.Vert(tip, n, new Vector2(0.5f, 0f)));
                }
                if (f % 2 == 1) beads.Add(at((f + 1f) / flags) + new Vector3(0f, -0.03f, -0.02f));
            }
            Part(span, "String", line.Save($"BuntingString{seed}", MeshDir), stringMat, Vector3.zero).shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < kits.Length; i++)
                if (kits[i].Count > 0) Part(span, "Flags", kits[i].Save($"BuntingFlags{seed}_{i}", MeshDir), flagMats[i], Vector3.zero);
            foreach (var p in beads)
                Part(span, "Bead", bead, beadMat, p).shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------- Materials ----------------

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

        static Material Glow(string name, Color a, Color b, float gMin, float gMax, Color rim, float rimPower, float lit, float pulse, float sparkle = 0f)
        {
            var m = Mat(name, AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/HymnGlow.shader"));
            m.SetColor("_ColorA", a); m.SetColor("_ColorB", b);
            m.SetFloat("_GradientMin", gMin); m.SetFloat("_GradientMax", gMax);
            m.SetColor("_RimColor", rim); m.SetFloat("_RimPower", rimPower);
            m.SetFloat("_Lit", lit); m.SetFloat("_BeatPulse", pulse); m.SetFloat("_Sparkle", sparkle);
            m.SetFloat("_Cull", 2f);
            return m;
        }

        static Material LitColor(string name, uint rgb, float smoothness, uint emission = 0, float emissionIntensity = 0f)
        {
            var m = Mat(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Hdr(rgb));
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Cull", 0f);                      // flags and string are seen from both sides
            if (emission != 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Hdr(emission, emissionIntensity));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        static Material SparkMaterial()
        {
            var m = Mat("VillageSpark", Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Generated/Spring/SpringSpark.png"));
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
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(box.x, box.z, box.y);
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
    }
}
