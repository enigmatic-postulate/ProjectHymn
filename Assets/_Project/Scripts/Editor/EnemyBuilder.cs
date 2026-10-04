using System.Collections.Generic;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using static ProjectHymn.EditorTools.ProjectHymnSetup;
using Mats = ProjectHymn.EditorTools.Sword.SwordsmanBuilder;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Builds the slice 2 enemies from primitives (placeholder art; pass 3 replaces it): a shield knight whose body,
    /// torso, sword arm and shield arm are pivots the <see cref="ShieldKnight"/> poses in code, and a glowing wisp.
    /// </summary>
    public static class EnemyBuilder
    {
        const uint Steel = 0x8a92a4, SteelDark = 0x4c5262, Tabard = 0xa02a30, Gold = 0xd8a840, Visor = 0x14101c,
                   ShieldFace = 0x3a5a9a, Leather = 0x5a3a24, WispCore = 0xc8f4ff, WispMote = 0x8ad8ff;

        public static ShieldKnight BuildKnight(Transform parent, Vector3 position, Vector2 bounds, Conductor conductor)
        {
            var root = new GameObject("Shield Knight", typeof(CapsuleCollider), typeof(ShieldKnight));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.42f; cap.height = 2.1f; cap.center = new Vector3(0f, 1.05f, 0f);

            var renderers = new List<Renderer>();
            var body = Pivot("Body", root.transform, Vector3.zero);
            // Legs
            foreach (var x in new[] { -0.15f, 0.15f })
            {
                renderers.Add(Part(PrimitiveType.Cylinder, "Leg", body, new Vector3(x, 0.47f, 0f), new Vector3(0.21f, 0.45f, 0.21f), SteelDark));
                renderers.Add(Part(PrimitiveType.Cube, "Boot", body, new Vector3(x, 0.07f, 0.06f), new Vector3(0.2f, 0.14f, 0.32f), Leather));
            }
            var torso = Pivot("Torso", body, new Vector3(0f, 0.95f, 0f));
            renderers.Add(Part(PrimitiveType.Cube, "Chest", torso, new Vector3(0f, 0.36f, 0f), new Vector3(0.58f, 0.62f, 0.38f), Steel));
            renderers.Add(Part(PrimitiveType.Cube, "Tabard", torso, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.36f, 0.78f, 0.03f), Tabard));
            renderers.Add(Part(PrimitiveType.Cube, "Belt", torso, new Vector3(0f, 0.06f, 0f), new Vector3(0.6f, 0.08f, 0.4f), Leather));
            renderers.Add(Part(PrimitiveType.Sphere, "Helmet", torso, new Vector3(0f, 0.88f, 0f), new Vector3(0.36f, 0.4f, 0.36f), Steel));
            renderers.Add(Part(PrimitiveType.Cube, "Visor", torso, new Vector3(0f, 0.88f, 0.17f), new Vector3(0.24f, 0.05f, 0.04f), Visor));
            renderers.Add(Part(PrimitiveType.Cube, "Crest", torso, new Vector3(0f, 1.1f, -0.02f), new Vector3(0.05f, 0.14f, 0.34f), Tabard));
            foreach (var x in new[] { -0.33f, 0.33f })
                renderers.Add(Part(PrimitiveType.Sphere, "Pauldron", torso, new Vector3(x, 0.62f, 0f), new Vector3(0.26f, 0.18f, 0.28f), Gold));

            // Sword arm (right, +X): hangs down from the shoulder; the blade continues the arm's line.
            var swordArm = Pivot("Sword Arm", torso, new Vector3(0.36f, 0.6f, 0f));
            renderers.Add(Part(PrimitiveType.Cylinder, "Arm", swordArm, new Vector3(0f, -0.27f, 0f), new Vector3(0.14f, 0.27f, 0.14f), SteelDark));
            renderers.Add(Part(PrimitiveType.Cube, "Guard", swordArm, new Vector3(0f, -0.58f, 0f), new Vector3(0.28f, 0.05f, 0.08f), Gold));
            renderers.Add(Part(PrimitiveType.Cube, "Blade", swordArm, new Vector3(0f, -1.15f, 0f), new Vector3(0.08f, 1.1f, 0.03f), 0xe4e8f0, 0.8f));

            // Shield arm (left): the shield plate faces along the arm, so it faces forward when the arm points forward.
            var shieldArm = Pivot("Shield Arm", torso, new Vector3(-0.36f, 0.6f, 0f));
            renderers.Add(Part(PrimitiveType.Cylinder, "Arm", shieldArm, new Vector3(0f, -0.22f, 0f), new Vector3(0.14f, 0.22f, 0.14f), SteelDark));
            renderers.Add(Part(PrimitiveType.Cube, "Shield", shieldArm, new Vector3(0.04f, -0.46f, 0f), new Vector3(0.62f, 0.07f, 0.82f), ShieldFace, 0.4f));
            renderers.Add(Part(PrimitiveType.Cube, "Rim", shieldArm, new Vector3(0.04f, -0.5f, 0f), new Vector3(0.7f, 0.03f, 0.9f), Gold, 0.5f));
            renderers.Add(Part(PrimitiveType.Cube, "Boss", shieldArm, new Vector3(0.04f, -0.51f, 0f), new Vector3(0.16f, 0.05f, 0.16f), Gold, 0.6f));

            var k = root.GetComponent<ShieldKnight>();
            SetRef(k, "conductor", conductor);
            SetRef(k, "body", body);
            SetRef(k, "torso", torso);
            SetRef(k, "swordArm", swordArm);
            SetRef(k, "shieldArm", shieldArm);
            SetArray(k, "flashRenderers", renderers.ToArray());
            var so = new SerializedObject(k);
            so.FindProperty("displayName").stringValue = "Shield Knight";
            so.FindProperty("maxHp").floatValue = 180f;
            so.FindProperty("aggroRange").floatValue = 7f;
            so.FindProperty("barHeight").floatValue = 2.55f;
            so.FindProperty("bounds").vector2Value = bounds;
            so.ApplyModifiedPropertiesWithoutUndo();
            swordArm.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            shieldArm.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            body.rotation = Quaternion.Euler(0f, -90f + 14f, 0f) ;
            return k;
        }

        public static Wisp BuildWisp(Transform parent, Vector3 position, Conductor conductor, Material additive)
        {
            var root = new GameObject("Wisp", typeof(SphereCollider), typeof(Wisp));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var sc = root.GetComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 0.6f;

            var renderers = new List<Renderer>();
            var core = Pivot("Core", root.transform, Vector3.zero);
            var coreMat = Glow(WispCore, new Color(0.5f, 0.9f, 1f) * 1.5f);
            var c = Part(PrimitiveType.Sphere, "Heart", core, Vector3.zero, Vector3.one * 0.5f, WispCore);
            c.sharedMaterial = coreMat;
            renderers.Add(c);
            var halo = Part(PrimitiveType.Sphere, "Halo", core, Vector3.zero, Vector3.one * 1.05f, WispCore);
            halo.sharedMaterial = additive;
            halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var orbit = Pivot("Orbit", root.transform, Vector3.zero);
            orbit.localRotation = Quaternion.Euler(20f, 0f, 15f);
            var moteMat = Glow(WispMote, new Color(0.3f, 0.7f, 1f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var m = Part(PrimitiveType.Sphere, "Mote", orbit, new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f), Vector3.one * 0.14f, WispMote);
                m.sharedMaterial = moteMat;
                m.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.Add(m);
            }
            var lightGo = new GameObject("Glow", typeof(Light));
            lightGo.transform.SetParent(root.transform, false);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Point; l.range = 5f; l.intensity = 1.6f; l.color = new Color(0.55f, 0.9f, 1f);

            var w = root.GetComponent<Wisp>();
            SetRef(w, "conductor", conductor);
            SetRef(w, "core", core);
            SetRef(w, "orbit", orbit);
            SetArray(w, "flashRenderers", renderers.ToArray());
            var so = new SerializedObject(w);
            so.FindProperty("displayName").stringValue = "Gap Wisp";
            so.FindProperty("maxHp").floatValue = 60f;
            so.FindProperty("aggroRange").floatValue = 9f;
            so.FindProperty("aggroHeight").floatValue = 4f;
            so.FindProperty("barHeight").floatValue = 0.95f;
            so.FindProperty("warnColor").colorValue = new Color(1f, 0.35f, 0.6f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return w;
        }

        static Transform Pivot(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static Renderer Part(PrimitiveType type, string name, Transform parent, Vector3 local, Vector3 scale, uint color, float smoothness = 0.3f)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Lit(color, smoothness, color == Steel || color == SteelDark ? 0.5f : 0f);
            return r;
        }

        /// <summary>A lit material with emission switched on (so the telegraph glow shows).</summary>
        static Material Glow(uint rgb, Color emission)
        {
            var m = Mats.Lit(rgb, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
