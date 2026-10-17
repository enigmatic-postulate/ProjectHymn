using System.Collections.Generic;
using System.IO;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// The look of a stage (VS-4, Look &amp; Sound): Starlit Spring's finish carried across the run. Each preset sets a
    /// sky (the Hymn sky shader on a quad that rides with the camera, so it reads as infinitely far), the fog to match
    /// its horizon, a post-processing volume (bloom, ACES tonemapping, grading, vignette), optionally the Hymn water for
    /// a sea, and an AmbientBeat so glowing things and BeatLights pulse with the music.
    /// </summary>
    public static class StageLook
    {
        const string Dir = "Assets/_Project/Art/Generated/Looks";
        const string ShaderDir = "Assets/_Project/Shaders";

        public sealed class Preset
        {
            public string Name;
            // Sky
            public Color Top, Mid, Horizon;
            public float Stars, Aurora, AuroraHeight = 0.66f, Nebula;
            public Color AuroraLow = new Color(0.25f, 1.6f, 0.9f), AuroraHigh = new Color(1.2f, 0.35f, 1.1f), NebulaColor = new Color(0.55f, 0.18f, 0.75f);
            public float Clouds, CloudCover = 0.5f, CloudHeight = 0.5f;
            public Color CloudLit = new Color(1.1f, 1f, 0.92f), CloudShade = new Color(0.42f, 0.5f, 0.72f);
            public Vector2 Sun = new Vector2(0.8f, 0.4f);
            public float SunGlow, SunSize = 0.03f;
            public Color SunColor = new Color(2.2f, 1.6f, 1f);
            // Post
            public float BloomThreshold = 1f, BloomIntensity = 0.6f, BloomScatter = 0.65f;
            public float Exposure = 0.3f, Contrast = 10f, Saturation = 10f, Vignette = 0.2f;
            public Color VignetteColor = new Color(0.03f, 0.03f, 0.1f), Filter = Color.white;
            // Water (a sea)
            public Color WaterDeep = new Color(0.05f, 0.18f, 0.32f), WaterShallow = new Color(0.15f, 0.42f, 0.55f), WaterSky = new Color(0.55f, 0.7f, 0.95f), WaterAurora = Color.black;
            public float WaterGlints = 0.6f;
        }

        // ---------------- Presets ----------------

        /// <summary>Cliffside and the Arena: a bright afternoon with a sea of cloud.</summary>
        public static Preset Day => new Preset
        {
            Name = "Day",
            Top = new Color(0.05f, 0.16f, 0.42f), Mid = new Color(0.16f, 0.36f, 0.7f), Horizon = new Color(0.62f, 0.62f, 0.66f),
            Clouds = 1f, CloudCover = 0.55f, CloudHeight = 0.55f,
            Sun = new Vector2(0.78f, 0.42f), SunGlow = 0.45f,
            BloomThreshold = 1.05f, BloomIntensity = 0.55f, Exposure = 0.35f, Contrast = 10f, Saturation = 12f, Vignette = 0.18f,
        };

        /// <summary>The Practice Yard: early morning, warm and soft.</summary>
        public static Preset Morning => new Preset
        {
            Name = "Morning",
            Top = new Color(0.1f, 0.2f, 0.45f), Mid = new Color(0.36f, 0.48f, 0.72f), Horizon = new Color(0.95f, 0.72f, 0.55f),
            Clouds = 0.8f, CloudCover = 0.45f, CloudHeight = 0.48f, CloudLit = new Color(1.25f, 0.95f, 0.8f), CloudShade = new Color(0.5f, 0.48f, 0.66f),
            Sun = new Vector2(0.2f, 0.28f), SunGlow = 0.7f, SunColor = new Color(2.4f, 1.6f, 0.9f),
            BloomThreshold = 1f, BloomIntensity = 0.6f, Exposure = 0.35f, Saturation = 10f, Vignette = 0.2f,
        };

        /// <summary>Belfry Ridge: dusk, the sun going down behind violet cloud, the first stars.</summary>
        public static Preset Dusk => new Preset
        {
            Name = "Dusk",
            Top = new Color(0.05f, 0.03f, 0.14f), Mid = new Color(0.22f, 0.1f, 0.32f), Horizon = new Color(0.85f, 0.36f, 0.24f),
            Stars = 0.6f, Clouds = 1f, CloudCover = 0.5f, CloudHeight = 0.32f, CloudLit = new Color(1.3f, 0.6f, 0.45f), CloudShade = new Color(0.22f, 0.12f, 0.28f),
            Sun = new Vector2(0.22f, 0.16f), SunGlow = 0.8f, SunSize = 0.05f, SunColor = new Color(2.6f, 1.2f, 0.6f),
            BloomThreshold = 0.95f, BloomIntensity = 0.85f, Exposure = 0.3f, Contrast = 12f, Saturation = 14f, Vignette = 0.24f,
            VignetteColor = new Color(0.1f, 0.03f, 0.12f),
            WaterDeep = new Color(0.07f, 0.04f, 0.14f), WaterShallow = new Color(0.25f, 0.14f, 0.3f), WaterSky = new Color(0.85f, 0.42f, 0.42f), WaterGlints = 0.9f,
        };

        /// <summary>The Summit: night on the headland, a faint aurora over the stones.</summary>
        public static Preset Night => new Preset
        {
            Name = "Night",
            Top = new Color(0.006f, 0.008f, 0.03f), Mid = new Color(0.025f, 0.02f, 0.08f), Horizon = new Color(0.14f, 0.07f, 0.2f),
            Stars = 1.3f, Aurora = 0.55f, AuroraHeight = 0.7f, Nebula = 0.3f, Clouds = 0.35f, CloudCover = 0.35f, CloudHeight = 0.25f,
            CloudLit = new Color(0.35f, 0.3f, 0.55f), CloudShade = new Color(0.05f, 0.04f, 0.1f), SunGlow = 0f,
            BloomThreshold = 0.9f, BloomIntensity = 1.1f, BloomScatter = 0.72f, Exposure = 0.25f, Contrast = 12f, Saturation = 14f, Vignette = 0.28f,
            VignetteColor = new Color(0.05f, 0.02f, 0.1f),
            WaterDeep = new Color(0.01f, 0.015f, 0.05f), WaterShallow = new Color(0.04f, 0.06f, 0.14f), WaterSky = new Color(0.12f, 0.08f, 0.26f),
            WaterAurora = new Color(0.12f, 0.5f, 0.32f), WaterGlints = 1.3f,
        };

        /// <summary>The village and the title: golden evening, lanterns lit.</summary>
        public static Preset Evening => new Preset
        {
            Name = "Evening",
            Top = new Color(0.08f, 0.12f, 0.3f), Mid = new Color(0.35f, 0.35f, 0.6f), Horizon = new Color(0.95f, 0.55f, 0.32f),
            Stars = 0.2f, Clouds = 0.9f, CloudCover = 0.45f, CloudHeight = 0.4f, CloudLit = new Color(1.3f, 0.8f, 0.55f), CloudShade = new Color(0.3f, 0.25f, 0.42f),
            Sun = new Vector2(0.15f, 0.25f), SunGlow = 0.9f, SunSize = 0.04f, SunColor = new Color(2.6f, 1.5f, 0.8f),
            BloomThreshold = 0.95f, BloomIntensity = 0.8f, Exposure = 0.3f, Contrast = 10f, Saturation = 12f, Vignette = 0.22f,
            VignetteColor = new Color(0.1f, 0.05f, 0.08f),
            WaterDeep = new Color(0.05f, 0.08f, 0.2f), WaterShallow = new Color(0.2f, 0.3f, 0.45f), WaterSky = new Color(0.95f, 0.6f, 0.45f), WaterGlints = 0.9f,
        };

        /// <summary>
        /// The Drowned Nave (VS-6): deep water. A dark teal column brightening toward the surface far above (a hazy glow
        /// where the light comes down, a ripple of "cloud" up there), no sun disc, cool bloom and a deep blue vignette.
        /// </summary>
        public static Preset Deep => new Preset
        {
            Name = "Deep",
            Top = new Color(0.12f, 0.42f, 0.5f), Mid = new Color(0.03f, 0.2f, 0.26f), Horizon = new Color(0.02f, 0.1f, 0.14f),
            Stars = 0.12f, Clouds = 0.45f, CloudCover = 0.55f, CloudHeight = 0.9f,
            CloudLit = new Color(0.55f, 0.95f, 1f), CloudShade = new Color(0.05f, 0.25f, 0.3f),
            Sun = new Vector2(0.55f, 0.98f), SunGlow = 0.55f, SunSize = 0.0f, SunColor = new Color(0.9f, 1.6f, 1.7f),
            BloomThreshold = 0.9f, BloomIntensity = 0.9f, BloomScatter = 0.75f, Exposure = 0.2f, Contrast = 14f, Saturation = 8f, Vignette = 0.32f,
            VignetteColor = new Color(0f, 0.04f, 0.08f), Filter = new Color(0.86f, 1f, 1f),
        };

        // ---------------- Apply ----------------

        /// <summary>Sky, fog, post-processing and the beat pulse for a scene, under <paramref name="root"/>.</summary>
        public static void Apply(Transform root, Camera cam, Preset p, string sceneId)
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
            if (cam != null) Sky(cam, p, sceneId);
            RenderSettings.fogColor = Color.Lerp(p.Horizon, p.Mid, 0.35f);
            Post(root, cam, sceneId, p);
            PulseLights();
        }

        static Shader HymnShader(string file) => AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/{file}.shader");

        static Material Mat(string name, Shader shader)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The sky on a quad parented to the camera, far out and big enough to fill any wide view.</summary>
        static void Sky(Camera cam, Preset p, string sceneId)
        {
            var m = Mat($"Sky_{sceneId}", HymnShader("HymnSky"));
            const float d = 300f;
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f, w = h * 2.6f;
            m.SetFloat("_Aspect", w / h);
            m.SetColor("_TopColor", p.Top); m.SetColor("_MidColor", p.Mid); m.SetColor("_HorizonColor", p.Horizon);
            m.SetFloat("_StarStrength", p.Stars);
            m.SetFloat("_AuroraStrength", p.Aurora); m.SetFloat("_AuroraHeight", p.AuroraHeight);
            m.SetColor("_AuroraLow", p.AuroraLow); m.SetColor("_AuroraHigh", p.AuroraHigh);
            m.SetFloat("_NebulaStrength", p.Nebula); m.SetColor("_NebulaColor", p.NebulaColor);
            m.SetFloat("_CloudStrength", p.Clouds); m.SetFloat("_CloudCover", p.CloudCover); m.SetFloat("_CloudHeight", p.CloudHeight);
            m.SetColor("_CloudColor", p.CloudLit); m.SetColor("_CloudShade", p.CloudShade);
            m.SetVector("_SunPos", new Vector4(p.Sun.x, p.Sun.y, 0f, 0f));
            m.SetFloat("_SunGlow", p.SunGlow); m.SetFloat("_SunSize", p.SunSize); m.SetColor("_SunColor", p.SunColor);

            var quad = new MeshKit();
            int a = quad.Vert(new Vector3(-0.5f, -0.5f, 0f), Vector3.back, new Vector2(0f, 0f));
            int b = quad.Vert(new Vector3(0.5f, -0.5f, 0f), Vector3.back, new Vector2(1f, 0f));
            int c = quad.Vert(new Vector3(0.5f, 0.5f, 0f), Vector3.back, new Vector2(1f, 1f));
            int e = quad.Vert(new Vector3(-0.5f, 0.5f, 0f), Vector3.back, new Vector2(0f, 1f));
            quad.Quad(a, b, c, e);
            var go = new GameObject("Sky", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, d);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(w, h, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = quad.Save("StageSkyQuad");
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, d + 50f);
            cam.backgroundColor = p.Mid;
        }

        /// <summary>The Hymn water for a sea or lake in this look (broad, slow waves; no fountain rings).</summary>
        public static Material Water(Preset p, string sceneId, float waveScale = 2.5f)
        {
            Directory.CreateDirectory(Dir);
            var m = Mat($"Water_{sceneId}", HymnShader("HymnWater"));
            m.SetColor("_DeepColor", p.WaterDeep); m.SetColor("_ShallowColor", p.WaterShallow);
            m.SetColor("_SkyColor", p.WaterSky); m.SetColor("_AuroraColor", p.WaterAurora);
            m.SetFloat("_Reflect", 0.85f); m.SetFloat("_Ripple", 0.35f); m.SetFloat("_Glints", p.WaterGlints);
            m.SetFloat("_RingStrength", 0f); m.SetFloat("_WaveScale", waveScale); m.SetFloat("_WaveSpeed", 0.7f);
            return m;
        }

        /// <summary>A post-processing volume for the scene (its own profile asset), and the camera set to render it.</summary>
        public static void Post(Transform root, Camera cam, string sceneId, Preset p)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{sceneId}Volume.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(p.BloomThreshold);
            bloom.intensity.Override(p.BloomIntensity);
            bloom.scatter.Override(p.BloomScatter);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(p.Exposure);
            grade.contrast.Override(p.Contrast);
            grade.saturation.Override(p.Saturation);
            grade.colorFilter.Override(p.Filter);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(p.Vignette);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(p.VignetteColor);
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
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            if (Object.FindAnyObjectByType<AmbientBeat>() == null)
                new GameObject("Ambient Beat", typeof(AmbientBeat)).transform.SetParent(root, false);
        }

        /// <summary>Every point light in the scene (fires, lanterns, the bell) swells on the beat.</summary>
        public static void PulseLights()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Point && l.GetComponent<BeatLight>() == null)
                {
                    var b = l.gameObject.AddComponent<BeatLight>();
                    bool fire = l.color.r > 0.9f && l.color.b < 0.5f;
                    b.flicker = fire ? 0.12f : 0.03f;
                    b.pulse = fire ? 0.5f : 0.3f;
                }
        }
    }
}
