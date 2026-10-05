using System.Collections.Generic;
using System.IO;
using ProjectHymn.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// Art pass, environment: the Cliffside level dressed with the Quaternius Medieval Village MegaKit (CC0) and two
    /// Poly Haven boulders (CC0). Cliffs are generated meshes with world-space UVs (so the kit's stone texture tiles
    /// evenly on any size of cliff) and grass or sand tops; the kit adds stone borders along the lips, vines down the
    /// faces, fences and crates, a small two-storey house and a town gate at the summit, and the boulders stand on the
    /// beach and rise from the sea as stacks.
    /// </summary>
    public static class EnvironmentArt
    {
        static string T(string n) => $"{ArtLibrary.Village}/Textures/{n}.png";
        static string N(string n) => $"{ArtLibrary.Village}/Textures/Normals Godot-Unity/{n}.png";
        static string Model(string n) => $"{ArtLibrary.Village}/Models/{n}.fbx";

        // ---------------- Materials ----------------

        public static Material Stone => ArtLibrary.Textured("Cliff_Stone", T("T_UnevenBrick_BaseColor"), N("T_UnevenBrick_Normal"), new Color(0.93f, 0.87f, 0.8f), 0.08f);
        public static Material DarkStone => ArtLibrary.Textured("Cliff_DarkStone", T("T_UnevenBrick_BaseColor"), N("T_UnevenBrick_Normal"), new Color(0.62f, 0.58f, 0.6f), 0.08f);
        public static Material Pillar => ArtLibrary.Textured("Cliff_Pillar", T("T_Brick_BaseColor"), N("T_Brick_Normal"), new Color(0.98f, 0.9f, 0.8f), 0.1f);
        public static Material Grass => ArtLibrary.Textured("Cliff_Grass", T("T_Noise_Terrain"), null, new Color(0.5f, 0.72f, 0.33f), 0.02f);
        public static Material GrassDark => ArtLibrary.Textured("Cliff_GrassDark", T("T_Noise_Terrain"), null, new Color(0.33f, 0.52f, 0.25f), 0.02f);
        public static Material Sand => ArtLibrary.Textured("Cliff_Sand", T("T_Noise_Terrain"), null, new Color(1f, 0.9f, 0.68f), 0.05f);

        static readonly Dictionary<string, Material> Kit = new Dictionary<string, Material>();

        /// <summary>The kit's materials by name (MI_*), rebuilt from its textures (its FBX files point at the author's disk).</summary>
        static Material KitMaterial(string slot)
        {
            string key = slot.Replace(" (Instance)", "").Trim();
            if (Kit.TryGetValue(key, out var m) && m != null) return m;
            switch (key)
            {
                case "MI_Brick": m = ArtLibrary.Textured("Kit_Brick", T("T_Brick_BaseColor"), N("T_Brick_Normal")); break;
                case "MI_RedBrick": m = ArtLibrary.Textured("Kit_RedBrick", T("T_RedBrick_BaseColor"), N("T_Brick_Normal")); break;
                case "MI_Plaster": m = ArtLibrary.Textured("Kit_Plaster", T("T_Plaster_BaseColor"), N("T_Plaster_Normal")); break;
                case "MI_RockTrim": m = ArtLibrary.Textured("Kit_RockTrim", T("T_RockTrim_BaseColor"), N("T_RockTrim_Normal")); break;
                case "MI_RoundTiles": m = ArtLibrary.Textured("Kit_RoundTiles", T("T_RoundTiles_BaseColor"), N("T_RoundTiles_Normal"), null, 0.2f); break;
                case "MI_UnevenBrick": m = ArtLibrary.Textured("Kit_UnevenBrick", T("T_UnevenBrick_BaseColor"), N("T_UnevenBrick_Normal")); break;
                case "MI_WoodTrim":
                case "MI_WoodTrim_Wear": m = ArtLibrary.Textured("Kit_WoodTrim", T("T_WoodTrim_BaseColor"), N("T_WoodTrim_Normal")); break;
                case "MI_Vine": m = ArtLibrary.Textured("Kit_Vine", T("T_VineLeaf"), null, new Color(0.75f, 0.95f, 0.6f), 0.05f, 1f, cutout: true, doubleSided: true); break;
                case "MI_WindowGlass": m = ArtLibrary.Textured("Kit_WindowGlass", T("T_WindowGradient"), null, new Color(0.7f, 0.85f, 1f), 0.9f); break;
                case "MI_MetalOrnaments": m = ArtLibrary.Textured("Kit_Metal", null, null, new Color(0.25f, 0.25f, 0.28f), 0.6f); break;
                default: m = ArtLibrary.Textured("Kit_Plaster", T("T_Plaster_BaseColor"), N("T_Plaster_Normal")); break;
            }
            Kit[key] = m;
            return m;
        }

        // ---------------- Cliffs ----------------

        /// <summary>
        /// A box with world-space UVs (sides tile every <paramref name="tile"/> metres, the top every
        /// <paramref name="topTile"/>): submesh 0 the four sides, submesh 1 the top. With a box collider.
        /// </summary>
        public static GameObject Block(Transform parent, string name, Vector3 min, Vector3 max, Material sides, Material top, float tile, float topTile, bool collider, bool saveAsset = true)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var sideTris = new List<int>();
            var topTris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, List<int> tris, System.Func<Vector3, Vector2> uv)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { var t = b; b = d; d = t; }   // Unity: front faces wind clockwise
                int i = verts.Count;
                foreach (var v in new[] { a, b, c, d }) { verts.Add(v); norms.Add(n); uvs.Add(uv(v)); }
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            System.Func<Vector3, Vector2> xy = v => new Vector2(v.x / tile, v.y / tile);
            System.Func<Vector3, Vector2> zy = v => new Vector2(v.z / tile, v.y / tile);
            System.Func<Vector3, Vector2> xz = v => new Vector2(v.x / topTile, v.z / topTile);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, sideTris, xy);
            Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward, sideTris, xy);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left, sideTris, zy);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, sideTris, zy);
            Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up, topTris, xz);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sideTris, 0);
            mesh.SetTriangles(topTris, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (saveAsset) mesh = SaveMesh(mesh, name);   // otherwise the mesh is stored in the scene

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { sides, top };
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = (min + max) * 0.5f;
                bc.size = max - min;
            }
            go.isStatic = true;
            return go;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            Directory.CreateDirectory(ArtLibrary.MeshDir + "/Cliffs");
            string safe = name.Replace(' ', '_').Replace('/', '_');
            string path = $"{ArtLibrary.MeshDir}/Cliffs/{safe}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
            EditorUtility.CopySerialized(m, existing);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---------------- Distant cliffs ----------------

        static readonly Color Fog = new Color(0.62f, 0.69f, 0.84f);
        static readonly float[] Haze = { 0.3f, 0.52f, 0.72f };

        static Material FarStone(int layer) => ArtLibrary.Textured($"Far_Stone_{layer}", T("T_UnevenBrick_BaseColor"), N("T_UnevenBrick_Normal"),
            Color.Lerp(new Color(0.88f, 0.82f, 0.76f), Fog, Haze[layer]), 0.02f, 1f);
        static Material FarGrass(int layer) => ArtLibrary.Textured($"Far_Grass_{layer}", T("T_Noise_Terrain"), null,
            Color.Lerp(new Color(0.45f, 0.66f, 0.3f), Fog, Haze[layer]), 0.0f, 1f);

        /// <summary>
        /// A distant cliff in the backdrop: a stone mesa of two or three terraces (each narrower and grass-topped), a
        /// buttress at its foot, hazier with each layer; the nearest layer sometimes carries a boulder crag.
        /// </summary>
        public static void FarMesa(Transform parent, System.Random rng, int layer, float x, float width, float height, float z, float depth, float bottom)
        {
            var root = new GameObject("Far Cliff").transform;
            root.SetParent(parent, false);
            var stone = FarStone(layer);
            var grass = FarGrass(layer);
            float tile = 4f + layer * 3f;
            float R() => (float)rng.NextDouble();
            int tiers = height > 14f ? 3 : 2;
            float x0 = x, x1 = x + width, z0 = z, z1 = z + depth, y = bottom;
            float h0 = height * (tiers == 3 ? 0.5f + R() * 0.15f : 0.62f + R() * 0.15f);
            for (int t = 0; t < tiers; t++)
            {
                float top = t == tiers - 1 ? height : Mathf.Lerp(y, height, t == 0 ? h0 / height : 0.55f + R() * 0.2f);
                if (t == 0) top = h0;
                Block(root, "Tier", new Vector3(x0, y, z0), new Vector3(x1, top, z1), stone, grass, tile, tile * 1.5f, collider: false, saveAsset: false);
                y = top;
                float inset = (x1 - x0) * (0.12f + R() * 0.18f);
                float shift = (R() - 0.5f) * inset;
                x0 += inset + shift; x1 -= inset - shift;
                z0 += depth * 0.1f; z1 -= depth * 0.05f;
                if (x1 - x0 < 2f) break;
            }
            // A buttress breaking up the foot of the cliff.
            float bw = width * (0.15f + R() * 0.2f), bx = x + R() * (width - bw);
            Block(root, "Buttress", new Vector3(bx, bottom, z - depth * 0.25f), new Vector3(bx + bw, bottom + (h0 - bottom) * (0.35f + R() * 0.3f), z + depth * 0.3f),
                stone, grass, tile, tile * 1.5f, collider: false, saveAsset: false);
            if (layer == 0 && ArtLibrary.HasBoulders && R() < 0.45f)
            {
                var go = ArtLibrary.Instance($"{ArtLibrary.Boulders}/namaqualand_boulder_03.fbx", root);
                if (go != null)
                {
                    go.transform.SetPositionAndRotation(new Vector3((x0 + x1) * 0.5f, y - 0.3f, (z0 + z1) * 0.5f), Quaternion.Euler(0f, R() * 360f, 0f) * go.transform.localRotation);
                    go.transform.localScale = go.transform.localScale * (2f + R() * 2.5f);
                    ArtLibrary.Paint(go, ArtLibrary.Textured("Far_Boulder", $"{ArtLibrary.Boulders}/namaqualand_boulder_03_diff_1k.jpg", $"{ArtLibrary.Boulders}/namaqualand_boulder_03_nor_1k.png",
                        Color.Lerp(new Color(0.95f, 0.9f, 0.86f), Fog, Haze[0]), 0.05f));
                }
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = layer == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // ---------------- Kit pieces ----------------

        /// <summary>Place a kit piece (no colliders) with the kit's materials rebuilt from its textures.</summary>
        public static GameObject Piece(Transform parent, string model, Vector3 position, float yaw, float scale = 1f, Vector3 localOffset = default)
        {
            var go = ArtLibrary.Instance(Model(model), parent);
            if (go == null) return null;
            // Keep the model's own root rotation and scale (the kit's FBX files are Z-up: their root carries the
            // -90° turn that stands them upright) and turn it about the vertical on top.
            var rot = Quaternion.Euler(0f, yaw, 0f);
            go.transform.SetPositionAndRotation(position + rot * localOffset, rot * go.transform.localRotation);
            go.transform.localScale = go.transform.localScale * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = KitMaterial(mats[i] != null ? mats[i].name : "");
                r.sharedMaterials = mats;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            go.isStatic = true;
            return go;
        }

        /// <summary>Stone borders along cliff lips, vines down the faces, fences, crates, a house and props.</summary>
        public static void Dress(Transform level, float frontZ) => Dress(level, frontZ, CliffsideLayout.Route, true);

        /// <summary>Borders and vines along any stage's cliffs; the fences, crates, house and wagon only on Cliffside.</summary>
        public static void Dress(Transform level, float frontZ, CliffsideLayout.Block[] route, bool cliffsideProps)
        {
            var root = new GameObject("Village Dressing").transform;
            root.SetParent(level, false);
            var rng = new System.Random(5);
            foreach (var b in route)
            {
                if (b.Kind != CliffsideLayout.Kind.Cliff) continue;
                for (float x = b.XMin + 1f; x <= b.XMax - 1f + 1e-3f; x += 2f)
                    Piece(root, "Prop_ExteriorBorder_Straight1", new Vector3(x, b.Top, frontZ + 0.02f), 0f);
                int vines = Mathf.Max(1, Mathf.RoundToInt(b.Width / 4f));
                for (int i = 0; i < vines; i++)
                {
                    float x = b.XMin + 1f + (float)rng.NextDouble() * (b.Width - 2f);
                    Piece(root, rng.NextDouble() < 0.5 ? "Prop_Vine1" : "Prop_Vine4", new Vector3(x, b.Top - 0.05f, frontZ - 0.06f), 180f, 1f + (float)rng.NextDouble() * 0.4f);
                }
            }

            if (!cliffsideProps) return;
            var t1 = CliffsideLayout.Route[1];
            for (float x = t1.XMin + 1.5f; x < t1.XMax - 1f; x += 2.06f) Piece(root, "Prop_WoodenFence_Single", new Vector3(x, t1.Top, 4.6f), 0f);
            Piece(root, "Prop_Crate", new Vector3(23.4f, t1.Top, 2.3f), 10f);
            Piece(root, "Prop_Crate", new Vector3(24.6f, t1.Top, 2.6f), -15f);
            Piece(root, "Prop_Crate", new Vector3(24.0f, t1.Top + 1.06f, 2.45f), 30f, 0.9f);

            var summit = CliffsideLayout.Route[CliffsideLayout.Route.Length - 1];
            House(root, new Vector3(90f, summit.Top, 5f));
            Piece(root, "Prop_Wagon", new Vector3(94.6f, summit.Top, 5.2f), 0f);
            Piece(root, "Prop_Crate", new Vector3(86.8f, summit.Top, 2.6f), 5f);
            for (float x = 99.5f; x < 101.8f; x += 2.06f) Piece(root, "Prop_WoodenFence_Single", new Vector3(x, summit.Top, 4.6f), 0f);
        }

        /// <summary>A small 4 x 4 m two-storey house behind the lane (stone ground floor, timber-framed upper floor, tiled roof).</summary>
        public static void House(Transform parent, Vector3 c)
        {
            var root = new GameObject("House").transform;
            root.SetParent(parent, false);
            const float H = 3.12f;
            // Kit walls are 2 m wide with their outer face toward +Z: yaw 180 faces the camera (-Z).
            var front = new[] { ("Wall_UnevenBrick_Door_Round", "Wall_Plaster_Window_Wide_Round"), ("Wall_UnevenBrick_Window_Wide_Round", "Wall_Plaster_WoodGrid") };
            for (int i = 0; i < 2; i++)
            {
                float x = c.x - 1f + 2f * i;
                Piece(root, front[i].Item1, new Vector3(x, c.y, c.z - 2f), 180f);
                Piece(root, front[i].Item2, new Vector3(x, c.y + H, c.z - 2f), 180f);
                Piece(root, "Wall_UnevenBrick_Straight", new Vector3(x, c.y, c.z + 2f), 0f);
                Piece(root, "Wall_Plaster_Straight", new Vector3(x, c.y + H, c.z + 2f), 0f);
                float z = c.z - 1f + 2f * i;
                Piece(root, "Wall_UnevenBrick_Straight", new Vector3(c.x - 2f, c.y, z), -90f);
                Piece(root, "Wall_Plaster_Straight", new Vector3(c.x - 2f, c.y + H, z), -90f);
                Piece(root, "Wall_UnevenBrick_Straight", new Vector3(c.x + 2f, c.y, z), 90f);
                Piece(root, "Wall_Plaster_Straight", new Vector3(c.x + 2f, c.y + H, z), 90f);
            }
            Piece(root, "Door_1_Round", new Vector3(c.x - 1f, c.y, c.z - 2f), 180f, 1f, new Vector3(-0.53f, 0f, 0f));
            Piece(root, "Window_Wide_Round1", new Vector3(c.x + 1f, c.y, c.z - 2f), 180f);
            Piece(root, "Window_Wide_Round1", new Vector3(c.x - 1f, c.y + H, c.z - 2f), 180f);
            Piece(root, "WindowShutters_Wide_Round_Open", new Vector3(c.x - 1f, c.y + H, c.z - 2f), 180f);
            Piece(root, "Roof_RoundTiles_4x4", new Vector3(c.x, c.y + 2f * H, c.z), 0f);
            Piece(root, "Prop_Chimney", new Vector3(c.x + 1.2f, c.y + 2f * H + 0.6f, c.z + 1f), 0f, 0.8f);
        }

        /// <summary>The town gate at the goal: an arch flanked by stone walls, standing just behind the lane.</summary>
        public static void Gate(Transform parent, Vector3 at)
        {
            Piece(parent, "Wall_Arch", new Vector3(at.x, at.y, 1.6f), 180f);
            Piece(parent, "Wall_UnevenBrick_Straight", new Vector3(at.x - 2f, at.y, 1.6f), 180f);
            Piece(parent, "Wall_UnevenBrick_Straight", new Vector3(at.x + 2f, at.y, 1.6f), 180f);
            Piece(parent, "Prop_Vine9", new Vector3(at.x, at.y + 3.0f, 1.5f), 180f, 0.8f);
        }

        // ---------------- Boulders ----------------

        public static void Boulders(Transform level, float waterY)
        {
            if (!ArtLibrary.HasBoulders) return;
            var root = new GameObject("Boulders").transform;
            root.SetParent(level, false);
            string B(string f) => $"{ArtLibrary.Boulders}/{f}";
            var m2 = ArtLibrary.Textured("Boulder_02", B("namaqualand_boulder_02_diff_1k.jpg"), B("namaqualand_boulder_02_nor_1k.png"), new Color(0.95f, 0.9f, 0.86f), 0.1f);
            var m3 = ArtLibrary.Textured("Boulder_03", B("namaqualand_boulder_03_diff_1k.jpg"), B("namaqualand_boulder_03_nor_1k.png"), new Color(0.95f, 0.9f, 0.86f), 0.1f);
            void Rock(string file, Material m, Vector3 p, float yaw, float scale)
            {
                var go = ArtLibrary.Instance(B(file), root);
                if (go == null) return;
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation);
                go.transform.localScale = go.transform.localScale * scale;
                ArtLibrary.Paint(go, m);
                go.isStatic = true;
            }
            // Beach and ledges (behind the lane), then sea stacks out in the bay.
            Rock("namaqualand_boulder_03.fbx", m3, new Vector3(13.5f, -0.2f, 3.6f), 30f, 1.3f);
            Rock("namaqualand_boulder_02.fbx", m2, new Vector3(4.5f, -0.1f, 4.2f), -20f, 1.2f);
            Rock("namaqualand_boulder_02.fbx", m2, new Vector3(-8.5f, -0.1f, 3.2f), 70f, 1.5f);
            Rock("namaqualand_boulder_03.fbx", m3, new Vector3(-10.5f, -0.2f, 5.5f), 160f, 1.7f);
            Rock("namaqualand_boulder_02.fbx", m2, new Vector3(52f, 8.3f, 4.5f), 10f, 0.9f);
            Rock("namaqualand_boulder_03.fbx", m3, new Vector3(100.5f, 14.4f, 5.6f), -40f, 0.8f);
            Rock("namaqualand_boulder_03.fbx", m3, new Vector3(36f, waterY - 1.5f, 22f), 15f, 7f);
            Rock("namaqualand_boulder_03.fbx", m3, new Vector3(70f, waterY - 2f, 34f), 120f, 10f);
            Rock("namaqualand_boulder_02.fbx", m2, new Vector3(58f, waterY - 0.5f, 16f), 60f, 5f);
            Rock("namaqualand_boulder_03.fbx", m3, new Vector3(118f, waterY - 2f, 28f), 200f, 8f);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
