using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.EditorTools.Art;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using static ProjectHymn.EditorTools.ProjectHymnSetup;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Builds VS-6's enemies for the Drowned Nave (6/8): the Bell Jelly (generated: a glowing lathed bell, a core and
    /// swaying tentacles), and, through the KayKit builder, the Barnacled Knight and the Drowned Chorister (with primitive
    /// fallbacks when the pack isn't imported).
    /// </summary>
    public static class DrownedEnemies
    {
        const string Dir = "Assets/_Project/Art/Generated/Drowned";

        /// <summary>Settings every 6/8 enemy shares: its patterns, health, a dotted-quarter aura and pose pulse.</summary>
        public static void Compound(BeatEnemy e, string name, List<EnemyAttackPattern> patterns, float maxHp)
        {
            var so = new SerializedObject(e);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("maxHp").floatValue = maxHp;
            so.FindProperty("galvanizeBeats").floatValue = 3f;
            so.FindProperty("poseBeatScale").floatValue = 3f;
            var p = so.FindProperty("patterns");
            p.arraySize = patterns.Count;
            for (int i = 0; i < patterns.Count; i++)
            {
                var src = patterns[i];
                var el = p.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("Name").stringValue = src.Name;
                var hits = el.FindPropertyRelative("Hits");
                hits.arraySize = src.Hits.Length;
                for (int h = 0; h < src.Hits.Length; h++) hits.GetArrayElementAtIndex(h).doubleValue = src.Hits[h];
                el.FindPropertyRelative("LeadBeats").intValue = src.LeadBeats;
                el.FindPropertyRelative("RecoverBeats").intValue = src.RecoverBeats;
                el.FindPropertyRelative("Damage").intValue = src.Damage;
                el.FindPropertyRelative("OnDownbeat").boolValue = src.OnDownbeat;
                el.FindPropertyRelative("Unblockable").boolValue = src.Unblockable;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static ShieldKnight Knight(Transform parent, Vector3 at, Vector2 bounds, Conductor conductor)
        {
            if (ArtLibrary.HasSkeleton) return KayKitBuilder.BuildBarnacledKnight(parent, at, bounds, conductor);
            var k = EnemyBuilder.BuildKnight(parent, at, bounds, conductor);
            k.gameObject.name = "Barnacled Knight";
            Compound(k, "Barnacled Knight", DrownedPatterns.Knight(), 200f);
            return k;
        }

        public static DrownedChorister Chorister(Transform parent, Vector3 at, Vector2 bounds, Conductor conductor, Material additive)
        {
            if (ArtLibrary.HasSkeleton) return KayKitBuilder.BuildSkeletonChorister(parent, at, bounds, conductor, additive);
            var root = new GameObject("Drowned Chorister", typeof(CapsuleCollider), typeof(DrownedChorister));
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.42f; cap.height = 2.0f; cap.center = new Vector3(0f, 1.0f, 0f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var fig = new GameObject("Figure").transform;
            fig.SetParent(body, false);
            var robe = Prim(PrimitiveType.Capsule, fig, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.6f), new Color(0.2f, 0.32f, 0.36f));
            var skull = Prim(PrimitiveType.Sphere, fig, new Vector3(0f, 1.75f, 0f), Vector3.one * 0.38f, new Color(0.75f, 0.9f, 0.84f));
            var c = root.GetComponent<DrownedChorister>();
            SetRef(c, "conductor", conductor);
            SetRef(c, "body", body);
            SetRef(c, "boltMaterial", additive);
            SetArray(c, "flashRenderers", new Renderer[] { robe, skull });
            var so = new SerializedObject(c);
            so.FindProperty("bounds").vector2Value = bounds;
            so.FindProperty("aggroRange").floatValue = 12f;
            so.FindProperty("barHeight").floatValue = 2.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Compound(c, "Drowned Chorister", DrownedPatterns.Chorister(), 110f);
            return c;
        }

        /// <summary>The Bell Jelly: a lathed, glowing bell with a bright core and seven swaying tentacles.</summary>
        public static BellJelly Jelly(Transform parent, Vector3 at, Vector2 bounds, Conductor conductor, Material additive)
        {
            System.IO.Directory.CreateDirectory(Dir + "/Meshes");
            var root = new GameObject("Bell Jelly", typeof(SphereCollider), typeof(BellJelly));
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            var sc = root.GetComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 0.75f;

            var bell = new GameObject("Bell").transform;
            bell.SetParent(root.transform, false);
            var domeMesh = MeshKit.Lathe(new[]
            {
                new Vector2(0f, 0.62f), new Vector2(0.22f, 0.6f), new Vector2(0.42f, 0.5f), new Vector2(0.58f, 0.32f),
                new Vector2(0.68f, 0.1f), new Vector2(0.7f, -0.04f), new Vector2(0.62f, -0.1f), new Vector2(0.5f, -0.06f),
            }, 32, Vector3.one).Save("JellyBell", Dir + "/Meshes");
            var dome = MeshPart(bell, "Dome", domeMesh, GlowMat("JellyDome", new Color(0.45f, 0.25f, 0.85f) * 1.4f,
                                                                new Color(0.7f, 0.95f, 1f) * 2.2f, -0.1f, 0.6f, new Color(1f, 0.6f, 0.95f) * 1.6f, true));
            var core = MeshPart(bell, "Core", MeshKit.Lathe(new[]
            {
                new Vector2(0f, 0.35f), new Vector2(0.2f, 0.28f), new Vector2(0.26f, 0.12f), new Vector2(0.18f, -0.02f), new Vector2(0f, -0.05f),
            }, 20, Vector3.one).Save("JellyCore", Dir + "/Meshes"), GlowMat("JellyCore", new Color(1f, 0.45f, 0.8f) * 2.5f,
                                                                           new Color(1f, 0.85f, 0.95f) * 3f, -0.05f, 0.35f, Color.white * 2f, false));
            core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dome.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var tRoot = new GameObject("Tentacles").transform;
            tRoot.SetParent(root.transform, false);
            tRoot.localPosition = new Vector3(0f, -0.06f, 0f);
            var lines = new List<LineRenderer>();
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject("Tentacle", typeof(LineRenderer));
                go.transform.SetParent(tRoot, false);
                var lr = go.GetComponent<LineRenderer>();
                lr.positionCount = 8;
                lr.useWorldSpace = true;
                lr.startWidth = 0.07f; lr.endWidth = 0.01f;
                lr.sharedMaterial = additive;
                lr.startColor = new Color(0.8f, 0.6f, 1f, 0.9f);
                lr.endColor = new Color(0.4f, 0.8f, 1f, 0f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lines.Add(lr);
            }
            var lightGo = new GameObject("Glow", typeof(Light));
            lightGo.transform.SetParent(root.transform, false);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Point; l.range = 4.5f; l.intensity = 1.8f; l.color = new Color(0.85f, 0.6f, 1f);

            var j = root.GetComponent<BellJelly>();
            SetRef(j, "conductor", conductor);
            SetRef(j, "bell", bell);
            SetRef(j, "tentacleRoot", tRoot);
            SetArray(j, "tentacles", lines.ToArray());
            SetArray(j, "flashRenderers", new Renderer[] { dome, core });
            var so = new SerializedObject(j);
            so.FindProperty("bounds").vector2Value = bounds;
            so.FindProperty("aggroRange").floatValue = 9f;
            so.FindProperty("aggroHeight").floatValue = 5f;
            so.FindProperty("barHeight").floatValue = 1.1f;
            so.FindProperty("warnColor").colorValue = new Color(1f, 0.3f, 0.55f);
            so.ApplyModifiedPropertiesWithoutUndo();
            Compound(j, "Bell Jelly", DrownedPatterns.Jelly(), 80f);
            return j;
        }

        static MeshRenderer MeshPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return r;
        }

        static Renderer Prim(PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ArtLibrary.Textured("Drowned_" + type, null, null, color, 0.5f);
            return r;
        }

        /// <summary>A Hymn/Glow material (pulses with the music) saved under the Drowned art folder.</summary>
        public static Material GlowMat(string name, Color a, Color b, float gMin, float gMax, Color rim, bool twoSided, float pulse = 0.35f)
        {
            System.IO.Directory.CreateDirectory(Dir + "/Materials");
            string path = $"{Dir}/Materials/{name}.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/HymnGlow.shader");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_ColorA", a); m.SetColor("_ColorB", b);
            m.SetFloat("_GradientMin", gMin); m.SetFloat("_GradientMax", gMax);
            m.SetColor("_RimColor", rim); m.SetFloat("_RimPower", 2.5f);
            m.SetFloat("_Lit", 0.2f); m.SetFloat("_BeatPulse", pulse); m.SetFloat("_Sparkle", 0f);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
