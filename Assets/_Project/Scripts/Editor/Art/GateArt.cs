using System.Collections.Generic;
using System.IO;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// The way out of a stage, and the village's own gate: a gatehouse with real depth instead of a flat wall.
    ///
    /// The kit's Wall_Arch is a plane 2 m wide, 3 m tall and 64 mm thick with a hole in it, so the doorway showed the
    /// ground behind it and the whole thing read as a billboard as the camera scrolled past. This builds the parts the
    /// plane has not got: a reveal lining the opening (its outline taken from the kit mesh, so it fits the hole exactly),
    /// a dark tunnel running back from it with warm light spilling in from somewhere beyond, voussoirs standing proud
    /// around the arch, and a solid core behind the whole gate so nothing shows through.
    /// </summary>
    public static class GateArt
    {
        const string MeshDir = "Assets/_Project/Art/Generated/Gate/Meshes";
        const string GenDir = "Assets/_Project/Art/Generated/Village";

        /// <summary>How deep the gatehouse runs back from the face of the wall.</summary>
        public const float Depth = 1.1f;
        /// <summary>The front face of the kit's arch, relative to where the gate is placed (the plane is 64 mm thick).</summary>
        public const float FaceZ = 0.032f;

        /// <summary>
        /// Half of the opening in Wall_Arch, read from the mesh: straight jambs to a springline at 2.037 m, then the
        /// arch to its apex at 2.744 m. Mirrored for the other side.
        /// </summary>
        static readonly Vector2[] HalfOutline =
        {
            new Vector2(0.833f, 0f), new Vector2(0.833f, 1.836f), new Vector2(0.833f, 2.037f),
            new Vector2(0.799f, 2.162f), new Vector2(0.750f, 2.284f), new Vector2(0.667f, 2.413f),
            new Vector2(0.597f, 2.495f), new Vector2(0.500f, 2.576f), new Vector2(0.417f, 2.630f),
            new Vector2(0.333f, 2.677f), new Vector2(0.167f, 2.731f), new Vector2(0f, 2.744f),
        };

        /// <summary>The whole opening: left jamb foot, up and over the arch, down to the right jamb foot.</summary>
        static List<Vector2> Outline()
        {
            var pts = new List<Vector2>();
            foreach (var p in HalfOutline) pts.Add(new Vector2(-p.x, p.y));
            for (int i = HalfOutline.Length - 2; i >= 0; i--) pts.Add(HalfOutline[i]);
            return pts;
        }

        // ---------------- Materials ----------------

        static string T(string n) => $"{ArtLibrary.Village}/Textures/{n}.png";
        static string N(string n) => $"{ArtLibrary.Village}/Textures/Normals Godot-Unity/{n}.png";
        static string H(string n) => $"{GenDir}/{n}_Height.png";

        /// <summary>
        /// Put the generated height map on a material made from one of the kit's stone textures, so URP's parallax sinks
        /// the joints and stands the stones proud. Safe to call on the kit's own materials; does nothing if the map
        /// hasn't been painted yet (Tools/paint_gate.py).
        /// </summary>
        public static void Height(Material m, string texture, float parallax = 0.03f, float bump = 1.6f)
        {
            if (m == null) return;
            EnsureHeightImport(texture);
            var height = AssetDatabase.LoadAssetAtPath<Texture2D>(H(texture));
            if (height != null)
            {
                m.SetTexture("_ParallaxMap", height);
                m.SetFloat("_Parallax", Mathf.Clamp(parallax, 0.005f, 0.08f));
                m.EnableKeyword("_PARALLAXMAP");
            }
            m.SetFloat("_BumpScale", bump);         // the kit's normals are gentle; lean on them
            EditorUtility.SetDirty(m);
        }

        /// <summary>A height map is data, not colour: it has to come in linear, or the relief reads wrong.</summary>
        static void EnsureHeightImport(string texture)
        {
            if (!(AssetImporter.GetAtPath(H(texture)) is TextureImporter ti) || !ti.sRGBTexture) return;
            ti.sRGBTexture = false;
            ti.SaveAndReimport();
        }

        /// <summary>The kit's stone with the height map on it.</summary>
        public static Material Stone(string name, string texture, Color tint, float parallax = 0.03f)
        {
            var m = ArtLibrary.Textured(name, T(texture + "_BaseColor"), N(texture + "_Normal"), tint, 0.08f);
            Height(m, texture, parallax);
            return m;
        }

        static Material Jamb => Stone("Gate_Jamb", "T_UnevenBrick", new Color(0.52f, 0.5f, 0.46f), 0.04f);
        static Material Core => Stone("Gate_Core", "T_UnevenBrick", new Color(0.34f, 0.33f, 0.32f), 0.02f);
        static Material Voussoir => Stone("Gate_Voussoir", "T_RockTrim", new Color(1.02f, 0.98f, 0.9f), 0.03f);

        /// <summary>The dark at the end of the passage, with the faintest warmth in it.</summary>
        static Material Gloom
        {
            get
            {
                var m = ArtLibrary.Textured("Gate_Gloom", null, null, new Color(0.035f, 0.03f, 0.028f), 0f);
                m.SetFloat("_Smoothness", 0f);
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        // ---------------- Build ----------------

        /// <summary>
        /// The kit gate with its depth: the three wall pieces at the front, a lined passage through the arch, and the
        /// core behind. <paramref name="at"/> is the foot of the gate on the lane; the wall stands at <paramref name="wallZ"/>.
        /// </summary>
        public static void Build(Transform parent, Vector3 at, float wallZ = 1.6f)
        {
            Directory.CreateDirectory(MeshDir);
            // A grouping node only: everything below is placed in world coordinates, so this stays at the origin.
            var root = new GameObject("Gatehouse").transform;
            root.SetParent(parent, false);

            // The kit's face: the arch and the two straight walls, as before.
            EnvironmentArt.Piece(root, "Wall_Arch", new Vector3(at.x, at.y, wallZ), 180f);
            EnvironmentArt.Piece(root, "Wall_UnevenBrick_Straight", new Vector3(at.x - 2f, at.y, wallZ), 180f);
            EnvironmentArt.Piece(root, "Wall_UnevenBrick_Straight", new Vector3(at.x + 2f, at.y, wallZ), 180f);

            float front = wallZ + FaceZ, back = front + Depth;
            Passage(root, at, front, back);
            Core3(root, at, front, back);
            Voussoirs(root, at, front);
            EnvironmentArt.Piece(root, "Prop_Vine9", new Vector3(at.x, at.y + 3.0f, wallZ - 0.1f), 180f, 0.8f);
        }

        /// <summary>The reveal: the opening's outline swept back into the wall, floored, and closed at the far end.</summary>
        static void Passage(Transform root, Vector3 at, float front, float back)
        {
            var pts = Outline();
            var k = new MeshKit();
            // The lining. Normals point into the passage, since that is the only side ever seen.
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                var along = (b - a).normalized;
                var inward = new Vector3(-along.y, along.x, 0f);
                if (Vector3.Dot(inward, new Vector3(-a.x, 2.0f - a.y, 0f)) < 0f) inward = -inward;
                float run = (b - a).magnitude;
                float u0 = Run(pts, i), u1 = u0 + run;
                int fa = k.Vert(new Vector3(a.x, a.y, front), inward, new Vector2(u0, 0f));
                int fb = k.Vert(new Vector3(b.x, b.y, front), inward, new Vector2(u1, 0f));
                int bb = k.Vert(new Vector3(b.x, b.y, back), inward, new Vector2(u1, Depth));
                int ba = k.Vert(new Vector3(a.x, a.y, back), inward, new Vector2(u0, Depth));
                k.Quad(fa, fb, bb, ba);
            }
            // The floor of the passage, a shade darker than the lane.
            float w = HalfOutline[0].x;
            int f0 = k.Vert(new Vector3(-w, 0.01f, front), Vector3.up, new Vector2(0f, 0f));
            int f1 = k.Vert(new Vector3(w, 0.01f, front), Vector3.up, new Vector2(2f * w, 0f));
            int f2 = k.Vert(new Vector3(w, 0.01f, back), Vector3.up, new Vector2(2f * w, Depth));
            int f3 = k.Vert(new Vector3(-w, 0.01f, back), Vector3.up, new Vector2(0f, Depth));
            k.Quad(f0, f3, f2, f1);
            var lining = Part(root, "Passage", k.Save("GatePassage", MeshDir), Jamb, new Vector3(at.x, at.y, 0f));
            lining.shadowCastingMode = ShadowCastingMode.On;

            // The far end: dark, and set a little further back so the light has somewhere to come from.
            var cap = new MeshKit();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];       // wraps across the threshold, so the foot closes too
                int c0 = cap.Vert(new Vector3(a.x, a.y, back), Vector3.back, new Vector2(a.x, a.y));
                int c1 = cap.Vert(new Vector3(b.x, b.y, back), Vector3.back, new Vector2(b.x, b.y));
                int c2 = cap.Vert(new Vector3(0f, 1.2f, back), Vector3.back, new Vector2(0f, 1.2f));
                cap.Tri(c0, c1, c2);
            }
            Part(root, "Gloom", cap.Save("GateGloom", MeshDir), Gloom, new Vector3(at.x, at.y, 0f));

            // Light from beyond: a lamp low and deep in the passage, warm, reaching the lining but not the lane.
            var lamp = new GameObject("Light Beyond", typeof(Light));
            lamp.transform.SetParent(root, false);
            lamp.transform.position = new Vector3(at.x, at.y + 0.9f, back - 0.25f);
            var l = lamp.GetComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.84f, 0.6f); l.intensity = 2.6f; l.range = 4.2f;
            l.shadows = LightShadows.None;
            lamp.AddComponent<BeatLight>();

            Motes(root, new Vector3(at.x, at.y + 1.2f, (front + back) * 0.5f));
        }

        /// <summary>Distance along the outline to point <paramref name="i"/>, so the lining's texture runs with it.</summary>
        static float Run(List<Vector2> pts, int i)
        {
            float d = 0f;
            for (int j = 0; j < i; j++) d += (pts[j + 1] - pts[j]).magnitude;
            return d;
        }

        /// <summary>
        /// The body of the gate behind the face: a block either side of the passage and a lintel over it, so the wall has
        /// thickness at its top and ends, and nothing can be seen through it.
        /// </summary>
        static void Core3(Transform root, Vector3 at, float front, float back)
        {
            const float Half = 3f, Top = 3f;
            float w = HalfOutline[0].x, apex = HalfOutline[HalfOutline.Length - 1].y;
            var side = Core;
            EnvironmentArt.Block(root, "Core West", new Vector3(at.x - Half, at.y, front), new Vector3(at.x - w, at.y + Top, back),
                                 side, side, 1.2f, 1.2f, collider: false);
            EnvironmentArt.Block(root, "Core East", new Vector3(at.x + w, at.y, front), new Vector3(at.x + Half, at.y + Top, back),
                                 side, side, 1.2f, 1.2f, collider: false);
            EnvironmentArt.Block(root, "Core Lintel", new Vector3(at.x - w, at.y + apex, front), new Vector3(at.x + w, at.y + Top, back),
                                 side, side, 1.2f, 1.2f, collider: false);
            // A parapet along the top, which gives the silhouette its depth where the camera sees the wall's edge.
            EnvironmentArt.Block(root, "Parapet", new Vector3(at.x - Half - 0.1f, at.y + Top, front - 0.12f),
                                 new Vector3(at.x + Half + 0.1f, at.y + Top + 0.34f, back + 0.12f), Jamb, Jamb, 1.2f, 1.2f, collider: false);
        }

        /// <summary>Wedge stones around the arch, standing proud of the face: the one place the eye lands.</summary>
        static void Voussoirs(Transform root, Vector3 at, float front)
        {
            var parent = new GameObject("Voussoirs").transform;
            parent.SetParent(root, false);
            var mat = Voussoir;
            var ring = new MeshKit();
            // Each stone spans one step of the arch, from the springline over the crown and down the other side.
            const int first = 2;                             // the springline, where the curve starts
            var steps = new List<Vector2>();
            for (int i = first; i < HalfOutline.Length; i++) steps.Add(new Vector2(-HalfOutline[i].x, HalfOutline[i].y));
            for (int i = HalfOutline.Length - 2; i >= first; i--) steps.Add(HalfOutline[i]);
            for (int i = 0; i + 1 < steps.Count; i++)
            {
                Vector2 a = steps[i], b = steps[i + 1];
                var along = (b - a).normalized;
                var outward = new Vector3(along.y, -along.x, 0f);
                if (Vector3.Dot(outward, new Vector3(a.x, a.y - 2.0f, 0f)) < 0f) outward = -outward;
                // The stones narrow toward the crown, so the ring stays under the top of the 3 m wall.
                float up = Mathf.InverseLerp(HalfOutline[first].y, HalfOutline[HalfOutline.Length - 1].y, (a.y + b.y) * 0.5f);
                float thick = Mathf.Lerp(0.34f, 0.21f, up);
                Vector3 a0 = new Vector3(a.x, a.y, 0f), b0 = new Vector3(b.x, b.y, 0f);
                Vector3 a1 = a0 + outward * thick, b1 = b0 + outward * thick;
                float z0 = front - 0.11f, z1 = front + 0.02f;  // proud of the wall's face
                Wedge(ring, a0, b0, a1, b1, z0, z1, i);
            }
            Part(parent, "Arch Stones", ring.Save("GateVoussoirs", MeshDir), mat, new Vector3(at.x, at.y, 0f));

            // Impost blocks where the arch springs, and a keystone at the crown.
            float spring = HalfOutline[2].y, w = HalfOutline[2].x;
            foreach (float s in new[] { -1f, 1f })
                EnvironmentArt.Block(parent, "Impost", new Vector3(at.x + s * (w - 0.04f) - (s > 0 ? 0f : 0.42f), at.y + spring - 0.17f, front - 0.13f),
                                     new Vector3(at.x + s * (w - 0.04f) + (s > 0 ? 0.42f : 0f), at.y + spring + 0.05f, front + 0.04f),
                                     mat, mat, 1f, 1f, collider: false);
            float apex = HalfOutline[HalfOutline.Length - 1].y;
            EnvironmentArt.Block(parent, "Keystone", new Vector3(at.x - 0.17f, at.y + apex - 0.12f, front - 0.16f),
                                 new Vector3(at.x + 0.17f, at.y + 3f, front + 0.05f),     // rises into the parapet
                                 mat, mat, 1f, 1f, collider: false);
        }

        /// <summary>One voussoir: a four-sided wedge between two steps of the arch, given its own depth.</summary>
        static void Wedge(MeshKit k, Vector3 a0, Vector3 b0, Vector3 a1, Vector3 b1, float z0, float z1, int index)
        {
            // Alternate stones sit a few millimetres further out, so the ring is not a smooth band.
            float bump = index % 2 == 0 ? 0.02f : 0f;
            z0 -= bump;
            Vector3[] f = { a0, b0, b1, a1 };
            int[] front = new int[4], back = new int[4];
            for (int i = 0; i < 4; i++)
            {
                front[i] = k.Vert(new Vector3(f[i].x, f[i].y, z0), Vector3.back, new Vector2(f[i].x, f[i].y));
                back[i] = k.Vert(new Vector3(f[i].x, f[i].y, z1), Vector3.forward, new Vector2(f[i].x, f[i].y));
            }
            k.Quad(front[0], front[1], front[2], front[3]);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                var n = Vector3.Cross(f[j] - f[i], Vector3.forward).normalized;
                int s0 = k.Vert(new Vector3(f[i].x, f[i].y, z0), n, new Vector2(0f, 0f));
                int s1 = k.Vert(new Vector3(f[j].x, f[j].y, z0), n, new Vector2(1f, 0f));
                int s2 = k.Vert(new Vector3(f[j].x, f[j].y, z1), n, new Vector2(1f, 1f));
                int s3 = k.Vert(new Vector3(f[i].x, f[i].y, z1), n, new Vector2(0f, 1f));
                k.Quad(s0, s1, s2, s3);
            }
        }

        /// <summary>Dust turning in the light of the passage.</summary>
        static void Motes(Transform root, Vector3 at)
        {
            var go = new GameObject("Dust", typeof(ParticleSystem));
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f; main.startSpeed = 0.08f; main.startSize = 0.035f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.76f, 0.55f), new Color(1f, 0.86f, 0.62f, 0.3f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f, 2.2f, Depth);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ArtLibrary.Textured("Gate_Dust", null, null, new Color(1f, 0.92f, 0.75f), 0f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 at)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            go.isStatic = true;
            return r;
        }
    }
}
