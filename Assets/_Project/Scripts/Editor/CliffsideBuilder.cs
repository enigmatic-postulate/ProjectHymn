using System.Collections.Generic;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Movement;
using ProjectHymn.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ProjectHymn.EditorTools.ProjectHymnSetup;
using ProjectHymn.EditorTools.Art;
using Lit = ProjectHymn.EditorTools.Sword.SwordsmanBuilder;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Vertical slice 1 ("Movement"): builds Scenes/Cliffside.unity from <see cref="CliffsideLayout"/>. A greybox
    /// side-scrolling climb from a beach up three cliff tiers, over a gap and up stone pillars to a summit, with
    /// checkpoints, two enemies (a shield knight on the first tier, a wisp over the gap) and camera zones. The player is
    /// the M2 swordsman on the platformer controller: run, jump, double jump, grab ledges, and play sword rhythms
    /// anywhere, and guards (parries) enemy hits on the beat. Menu: Project Hymn ▸ Build Cliffside Slice. Safe to re-run.
    /// </summary>
    public static class CliffsideBuilder
    {
        static StageLayout L = StageLayout.Cliffside;
        static string ScenePath => $"{SceneDir}/{L.Scene}.unity";
        static bool Waltz => L.Meter == 3;
        const float Bottom = -14f;      // cliffs reach down into the water
        const float FrontZ = -2.6f;     // cliff faces toward the camera; the lane is z = 0
        const float BackZ = 7f;

        const uint Stone = 0x8c7c6c, StoneDark = 0x6a5c52, StoneWarm = 0x9c8670, Grass = 0x6c9a48, GrassDark = 0x4e7a3a,
                   Sand = 0xd8c49a, Water = 0x3c6c98, Far1 = 0x7c8aa8, Far2 = 0x95a3c0, Far3 = 0xb0bcd4, Crystal = 0x9ab8c8,
                   Bush = 0x3e6a34, Rope = 0x9a7a48;

        [MenuItem("Project Hymn/Build Cliffside Slice", priority = 1)]
        public static void Build() => BuildStage(StageLayout.Cliffside);

        [MenuItem("Project Hymn/Build Belfry Ridge Slice", priority = 2)]
        public static void BuildBelfry() => BuildStage(StageLayout.Belfry);

        /// <summary>
        /// Build a stage scene from its layout: Cliffside (4/4, the battle theme) or Belfry Ridge (VS-2: 3/4, the waltz,
        /// bone dancers, dusk light and the belfry). Safe to re-run.
        /// </summary>
        public static void BuildStage(StageLayout layout)
        {
            L = layout;
            PrepareContent();
            ArtLibrary.EnsureImportSettings();
            AssetDatabase.Refresh();
            _art = ArtLibrary.HasKayKit;
            _village = ArtLibrary.HasVillage;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var theme = Waltz ? LoadWaltz() : LoadTheme();
            var weapons = _art ? KayKitBuilder.EnsureWeapons() : LoadWeapons();
            var additive = LoadAdditive();
            var locomotion = _art ? KayKitBuilder.EnsureLocomotion() : Sword.SwordContent.EnsureLocomotion();

            if (Waltz) BuildDuskLighting(); else BuildLighting();
            var level = new GameObject("Level").transform;
            foreach (var b in L.Route) BuildBlock(level, b);
            foreach (var b in L.Props) BuildBlock(level, b);
            BuildWaterAndBackdrop(level);
            BuildDressing(level);
            if (_village) EnvironmentArt.Dress(level, FrontZ, L.Route, cliffsideProps: L.Id == "Cliffside");
            if (L.Id == "Cliffside") EnvironmentArt.Boulders(level, CliffsideLayout.WaterY);
            if (Waltz) BuildBelfry(level);

            foreach (var b in L.Secrets) BuildBlock(level, b);
            var conductor = BuildConductor(theme);

            // Players: one per class on the platformer controller (the session's class is kept at run time).
            var db = HubBuilder.EnsureDatabase(weapons);
            var spawn = L.First(CliffsideLayout.MarkerKind.Spawn);
            var spawns = new GameObject("Spawns").transform;
            HubBuilder.Spawn(spawns, "Start", new Vector3(spawn.X, spawn.Y, 0f));
            var player = HubBuilder.BuildPlayers(new Vector3(spawn.X, spawn.Y, 0f), conductor, db, locomotion, additive, _art);

            // Checkpoints, targets, pickups, goal, camera zones
            Checkpoint first = null;
            foreach (var m in L.Markers)
            {
                switch (m.Kind)
                {
                    case CliffsideLayout.MarkerKind.Checkpoint:
                        var cp = BuildCheckpoint(level, m);
                        if (first == null) { first = cp; SetBool(cp, "startsLit", true); }
                        break;
                    case CliffsideLayout.MarkerKind.Target: BuildKnight(level, m, conductor); break;
                    case CliffsideLayout.MarkerKind.Dancer: BuildDancer(level, m, conductor); break;
                    case CliffsideLayout.MarkerKind.FloatingTarget: EnemyBuilder.BuildWisp(level, new Vector3(m.X, m.Y + 1.0f, 0f), conductor, additive); break;
                    case CliffsideLayout.MarkerKind.Goal: BuildGoal(level, m); break;
                    case CliffsideLayout.MarkerKind.CameraZone: BuildCameraZone(level, m); break;
                    case CliffsideLayout.MarkerKind.Pickup:
                        HubBuilder.BuildPickup(level, m.Name, new Vector3(m.X, m.Y, 0f), L.Id, m.X < 20f ? 40 : m.X < 40f ? 60 : 80);
                        break;
                }
            }

            // The path home, at the west end of the beach.
            var homeGo = new GameObject("Path to the Village", typeof(SceneDoor));
            homeGo.transform.SetParent(level, false);
            homeGo.transform.position = new Vector3(-8.6f, 0f, 0f);
            var home = homeGo.GetComponent<SceneDoor>();
            home.scene = "Village"; home.spawn = "FromCliffside"; home.saveOnArrival = true; home.prompt = "Back to the village"; home.range = 1.2f;
            if (_village) EnvironmentArt.Piece(level, "Prop_WoodenFence_Single", new Vector3(-8.6f, 0f, 1.6f), 0f);

            // Camera, effects, HUD (the minimal HUD builds itself; the old one is the debug HUD)
            var cam = HubBuilder.BuildCamera(player.transform.position);
            if (Waltz) cam.GetComponent<Camera>().backgroundColor = new Color(0.5f, 0.42f, 0.62f);
            var fx = BuildFx(conductor, null, null, cam, additive);
            fx.AddComponent<DefenseFeedback>();
            var hud = BuildHud(conductor, null);
            BuildRunStatus(hud);
            var debugParts = new List<GameObject>();
            foreach (Transform t in hud.transform)
                if (t.name != "Calibration" && t.name != "Judgement" && t.name != "Chain") debugParts.Add(t.gameObject);
            var dbg = hud.gameObject.AddComponent<DebugHud>();
            SetList(dbg, "parts", debugParts);
            HubBuilder.BuildGameUi(hub: false);
            var director = new GameObject("Stage Director", typeof(StageDirector)).GetComponent<StageDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("stageId").stringValue = L.Id;
            dso.FindProperty("stageName").stringValue = L.Name;
            dso.FindProperty("parSeconds").floatValue = L.ParSeconds;
            dso.FindProperty("perfectAttacks").intValue = L.PerfectAttacks;
            dso.ApplyModifiedPropertiesWithoutUndo();

            if (theme == null) Debug.LogError($"[Project Hymn] The {(Waltz ? "BelfryWaltz" : "BattleTheme1")} song asset could not be loaded; assign a Song on the Conductor by hand.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath, first: false);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[Project Hymn] {L.Name} built ({L.Meter}/4). A/D run, Space jump (again in the air to double jump; hold for height), " +
                      "jump at a ledge to grab it, Up/Space to climb, Down to let go. J / K play notes anywhere, on the ground or in the air. " +
                      "L guards: press it on an enemy's hit beat (count 3-2-1 over its head) to parry. " +
                      "W / Up uses doors, E drinks the quick item, Q changes it, Esc pauses. Shift dodge, R restart, F1-F4 tempos, F9 debug HUD.");
        }

        static bool _art, _village;



        // ---------------- Lighting ----------------

        internal static void BuildLighting()
        {
            // Late-afternoon sun from over the camera's shoulder so the cliff faces read; soft sky fill; distance haze.
            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.rotation = Quaternion.Euler(32f, 24f, 0f);
            var l = sun.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.35f; l.color = new Color(1f, 0.88f, 0.72f);
            l.shadows = LightShadows.Soft; l.shadowStrength = 0.75f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.62f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.48f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.22f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.69f, 0.84f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 170f;
        }

        /// <summary>Belfry Ridge: dusk. A low warm sun from the side, violet sky fill and haze.</summary>
        internal static void BuildDuskLighting()
        {
            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.rotation = Quaternion.Euler(14f, -38f, 0f);
            var l = sun.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.15f; l.color = new Color(1f, 0.66f, 0.42f);
            l.shadows = LightShadows.Soft; l.shadowStrength = 0.8f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.4f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.42f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.16f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.58f, 0.48f, 0.66f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 150f;
        }

        /// <summary>The cantors' belfry behind the goal: a stone tower with an open bell chamber; the bell swings once a bar.</summary>
        static void BuildBelfry(Transform level)
        {
            var goal = L.First(CliffsideLayout.MarkerKind.Goal);
            var root = new GameObject("Belfry").transform;
            root.SetParent(level, false);
            float x = goal.X, y = goal.Y, z0 = 5f, z1 = 10f;
            Box(root, "Tower", new Vector3(x - 2.6f, y, z0), new Vector3(x + 2.6f, y + 11f, z1), StoneWarm, false);
            foreach (var px in new[] { x - 2.6f, x + 2.0f })
                Box(root, "Post", new Vector3(px, y + 11f, z0), new Vector3(px + 0.6f, y + 15f, z0 + 0.6f), StoneDark, false);
            Box(root, "Back", new Vector3(x - 2.6f, y + 11f, z1 - 0.6f), new Vector3(x + 2.6f, y + 15f, z1), StoneDark, false);
            Box(root, "Cornice", new Vector3(x - 3.0f, y + 15f, z0 - 0.4f), new Vector3(x + 3.0f, y + 15.6f, z1 + 0.4f), Stone, false);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.name = "Roof";
            roof.transform.SetParent(root, false);
            roof.transform.position = new Vector3(x, y + 16.9f, (z0 + z1) * 0.5f);
            roof.transform.rotation = Quaternion.Euler(0f, 45f, 0f) * Quaternion.Euler(0f, 0f, 0f);
            roof.transform.localScale = new Vector3(4.6f, 2.6f, 4.6f);
            roof.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(0x5a3a4a, 0.1f);

            // The bell hangs from a pivot at the top of the chamber and swings across one bar.
            var pivot = new GameObject("Bell", typeof(BellSwing)).transform;
            pivot.SetParent(root, false);
            pivot.position = new Vector3(x, y + 14.6f, z0 + 0.9f);
            var bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(bell.GetComponent<Collider>());
            bell.name = "Bell Body";
            bell.transform.SetParent(pivot, false);
            bell.transform.localPosition = new Vector3(0f, -1.3f, 0f);
            bell.transform.localScale = new Vector3(1.7f, 0.9f, 1.7f);
            bell.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(0xc89a40, 0.75f, 0.6f);
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            crown.name = "Crown";
            crown.transform.SetParent(pivot, false);
            crown.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            crown.transform.localScale = new Vector3(1.3f, 1.0f, 1.3f);
            crown.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(0xc89a40, 0.75f, 0.6f);
            var lampGo = new GameObject("Bell Light", typeof(Light));
            lampGo.transform.SetParent(root, false);
            lampGo.transform.position = new Vector3(x, y + 13f, z0 - 1.5f);
            var lamp = lampGo.GetComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 9f; lamp.intensity = 1.4f; lamp.color = new Color(1f, 0.8f, 0.5f);

            // Lanterns on posts along the route, for the dusk.
            foreach (var b in L.Route)
            {
                if (b.Kind != CliffsideLayout.Kind.Cliff) continue;
                var post = new GameObject("Lantern").transform;
                post.SetParent(root, false);
                Box(post, "Pole", new Vector3(b.XMax - 1.4f, b.Top, 2.0f), new Vector3(b.XMax - 1.2f, b.Top + 2.4f, 2.2f), StoneDark, false);
                var glow = Box(post, "Glass", new Vector3(b.XMax - 1.5f, b.Top + 2.4f, 1.9f), new Vector3(b.XMax - 1.1f, b.Top + 2.85f, 2.3f), 0xffd890, false, 0.9f);
                var gm = glow.GetComponent<MeshRenderer>().sharedMaterial;
                gm.EnableKeyword("_EMISSION");
                gm.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.4f) * 1.5f);
                var lg = new GameObject("Lantern Light", typeof(Light));
                lg.transform.SetParent(post, false);
                lg.transform.position = new Vector3(b.XMax - 1.3f, b.Top + 2.6f, 1.6f);
                var ll = lg.GetComponent<Light>();
                ll.type = LightType.Point; ll.range = 5f; ll.intensity = 0.9f; ll.color = new Color(1f, 0.75f, 0.45f);
            }
        }

        /// <summary>A bone dancer waltzes along the ledge it stands on; the lead dancer is tougher.</summary>
        static void BuildDancer(Transform level, CliffsideLayout.Marker m, Conductor conductor)
        {
            var ledge = L.BlockAt(m.X);
            var bounds = new Vector2(ledge.XMin + 0.8f, ledge.XMax - 0.8f);
            float hp = m.Name.StartsWith("Lead") ? 230f : 150f;
            var pos = new Vector3(m.X, m.Y, 0f);
            if (_art && ArtLibrary.HasSkeleton) KayKitBuilder.BuildSkeletonDancer(level, m.Name, pos, bounds, conductor, hp);
            else EnemyBuilder.BuildDancer(level, m.Name, pos, bounds, conductor, hp);
        }

        // ---------------- Geometry ----------------

        internal static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, uint color, bool collider, float smoothness = 0.1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            go.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(color, smoothness);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        static void BuildBlock(Transform level, CliffsideLayout.Block b)
        {
            var root = new GameObject(b.Name).transform;
            root.SetParent(level, false);
            bool ground = b.Kind == CliffsideLayout.Kind.Ground;
            bool wall = b.Kind == CliffsideLayout.Kind.Wall;
            bool pillar = b.Kind == CliffsideLayout.Kind.Pillar;
            bool ledge = b.Kind == CliffsideLayout.Kind.Ledge;
            float front = pillar || ledge ? -1.4f : FrontZ, back = pillar || ledge ? 1.4f : BackZ;
            float bottom = ledge ? b.Top - CliffsideLayout.LedgeThickness : Bottom;
            uint body = wall ? StoneDark : pillar ? StoneWarm : Stone;

            if (_village)
            {
                // Art pass: kit stone with world-space UVs, grass (sand on the beach) on top.
                var sides = wall ? EnvironmentArt.DarkStone : pillar ? EnvironmentArt.Pillar : EnvironmentArt.Stone;
                var top = ground ? EnvironmentArt.Sand : EnvironmentArt.Grass;
                EnvironmentArt.Block(root, $"Rock {b.Name}", new Vector3(b.XMin, bottom, front), new Vector3(b.XMax, b.Top, back), sides, top, 3.2f, 5f, collider: true);
                if (!ground && !wall && !ledge)
                {
                    float o = 0.12f;
                    EnvironmentArt.Block(root, $"Lip {b.Name}", new Vector3(b.XMin - o, b.Top - 0.4f, front - o), new Vector3(b.XMax + o, b.Top + 0.02f, front + 0.6f),
                        EnvironmentArt.GrassDark, EnvironmentArt.Grass, 1.5f, 5f, collider: false);
                }
                return;
            }

            // The solid block the player walks on and climbs.
            Box(root, "Rock", new Vector3(b.XMin, bottom, front), new Vector3(b.XMax, b.Top, back), body, collider: true);
            if (wall || ledge) return;

            // Cap: grass (sand on the beach) overhanging the lip a little; visual only, flush with the walkable top.
            float lip = 0.12f;
            Box(root, ground ? "Sand" : "Grass", new Vector3(b.XMin - lip, b.Top - 0.28f, front - lip), new Vector3(b.XMax + lip, b.Top + 0.01f, back),
                ground ? Sand : Grass, collider: false, 0.05f);
            if (!ground)
            {
                // Hanging grass lip on the camera face, and dark strata bands down the cliff.
                Box(root, "Lip", new Vector3(b.XMin - lip, b.Top - 0.55f, front - lip - 0.02f), new Vector3(b.XMax + lip, b.Top - 0.25f, front - lip + 0.1f), GrassDark, false, 0.05f);
                for (float y = b.Top - 1.6f; y > Bottom + 4f; y -= 2.3f)
                    Box(root, "Strata", new Vector3(b.XMin + 0.05f, y - 0.22f, front - 0.06f), new Vector3(b.XMax - 0.05f, y + 0.22f, front + 0.2f), StoneDark, false);
            }
        }

        static void BuildWaterAndBackdrop(Transform level)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(level, false);
            Box(root, "Sea", new Vector3(-120f, CliffsideLayout.WaterY - 0.1f, -60f), new Vector3(240f, CliffsideLayout.WaterY, 220f), Water, false, 0.85f);

            // Three bands of distant cliffs, paler with distance (the fog finishes the job).
            var rng = new System.Random(7);
            var layers = new[] { (z: 26f, color: Far1, min: 6f, max: 22f, w: 9f), (z: 55f, color: Far2, min: 10f, max: 34f, w: 16f), (z: 110f, color: Far3, min: 18f, max: 55f, w: 30f) };
            foreach (var layer in layers)
            {
                for (float x = -60f; x < 180f;)
                {
                    float w = layer.w * (0.6f + (float)rng.NextDouble() * 0.9f);
                    float h = layer.min + (float)rng.NextDouble() * (layer.max - layer.min);
                    if (_village) EnvironmentArt.FarMesa(root, rng, System.Array.IndexOf(layers, layer), x, w, h, layer.z, layer.w, Bottom);
                    else Box(root, "Far Cliff", new Vector3(x, Bottom, layer.z), new Vector3(x + w, h, layer.z + layer.w), layer.color, false, 0f);
                    x += w * (0.7f + (float)rng.NextDouble() * 0.6f);
                }
            }
        }

        /// <summary>Bushes and boulders off the lane (behind the player), for scale. No colliders.</summary>
        static void BuildDressing(Transform level)
        {
            var root = new GameObject("Dressing").transform;
            root.SetParent(level, false);
            var rng = new System.Random(11);
            foreach (var b in L.Route)
            {
                if (b.Width < 4f) continue;
                for (float x = b.XMin + 1.5f; x < b.XMax - 1.5f; x += 3f + (float)rng.NextDouble() * 4f)
                {
                    bool rock = b.Kind == CliffsideLayout.Kind.Ground || rng.NextDouble() < 0.35;
                    if (rock && _village) continue;              // the kit and the scanned boulders dress the ground instead
                    var go = GameObject.CreatePrimitive(rock ? PrimitiveType.Cube : PrimitiveType.Sphere);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.name = rock ? "Boulder" : "Bush";
                    go.transform.SetParent(root, false);
                    float s = rock ? 0.6f + (float)rng.NextDouble() * 1.1f : 0.9f + (float)rng.NextDouble() * 1.0f;
                    go.transform.position = new Vector3(x, b.Top + s * (rock ? 0.3f : 0.35f), 2.2f + (float)rng.NextDouble() * 3.5f);
                    go.transform.localScale = rock ? new Vector3(s, s * 0.7f, s * 0.9f) : new Vector3(s * 1.3f, s, s);
                    go.transform.rotation = Quaternion.Euler(rock ? (float)rng.NextDouble() * 20f : 0f, (float)rng.NextDouble() * 90f, rock ? (float)rng.NextDouble() * 15f : 0f);
                    go.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(rock ? StoneWarm : Bush, 0.05f);
                    go.isStatic = true;
                }
            }
        }

        // ---------------- Markers ----------------

        static Checkpoint BuildCheckpoint(Transform level, CliffsideLayout.Marker m)
        {
            var go = new GameObject(m.Name, typeof(BoxCollider), typeof(Checkpoint));
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(m.X, m.Y, 0f);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(2f, 4f, 4f); box.center = new Vector3(0f, 2f, 0f);

            // A waist-high stone post behind the lane with a crystal that lights up.
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.name = "Post";
            post.transform.SetParent(go.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.55f, 1.3f);
            post.transform.localScale = new Vector3(0.32f, 0.55f, 0.32f);
            post.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(StoneDark);

            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.name = "Crystal";
            crystal.transform.SetParent(go.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 1.42f, 1.3f);
            crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            crystal.transform.localScale = Vector3.one * 0.32f;
            var mat = Lit.Lit(Crystal, 0.8f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", new Color(0.05f, 0.08f, 0.1f));
            crystal.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var lampGo = new GameObject("Lamp", typeof(Light));
            lampGo.transform.SetParent(go.transform, false);
            lampGo.transform.localPosition = new Vector3(0f, 1.6f, 0.8f);
            var lamp = lampGo.GetComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 1.2f; lamp.color = new Color(0.5f, 0.9f, 1f);

            var cp = go.GetComponent<Checkpoint>();
            SetArray(cp, "glow", new Renderer[] { crystal.GetComponent<MeshRenderer>() });
            SetRef(cp, "lamp", lamp);
            return cp;
        }

        /// <summary>The shield knight guards the ledge it stands on (it never walks off it).</summary>
        static void BuildKnight(Transform level, CliffsideLayout.Marker m, Conductor conductor)
        {
            var ledge = default(CliffsideLayout.Block);
            foreach (var b in L.Route) if (m.X > b.XMin && m.X < b.XMax) ledge = b;
            var bounds = new Vector2(ledge.XMin + 0.8f, ledge.XMax - 0.8f);
            if (_art && ArtLibrary.HasSkeleton) KayKitBuilder.BuildSkeletonKnight(level, new Vector3(m.X, m.Y, 0f), bounds, conductor);
            else EnemyBuilder.BuildKnight(level, new Vector3(m.X, m.Y, 0f), bounds, conductor);
        }

        static void BuildGoal(Transform level, CliffsideLayout.Marker m)
        {
            var go = new GameObject(m.Name, typeof(BoxCollider), typeof(SliceGoal));
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(m.X, m.Y, 0f);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(2.5f, 4f, 4f); box.center = new Vector3(0f, 2f, 0f);

            // A stone arch standing behind the lane (the kit's town gate when the art is in), with a turning crystal.
            var arch = go.transform;
            if (_village) EnvironmentArt.Gate(arch, new Vector3(m.X, m.Y, 0f));
            else
            {
            Box(arch, "Pillar L", new Vector3(m.X - 1.8f, m.Y, 1.0f), new Vector3(m.X - 1.2f, m.Y + 4.2f, 1.8f), StoneWarm, false);
            Box(arch, "Pillar R", new Vector3(m.X + 1.2f, m.Y, 1.0f), new Vector3(m.X + 1.8f, m.Y + 4.2f, 1.8f), StoneWarm, false);
            Box(arch, "Lintel", new Vector3(m.X - 2.1f, m.Y + 4.2f, 0.9f), new Vector3(m.X + 2.1f, m.Y + 4.8f, 1.9f), StoneDark, false);
            Box(arch, "Banner", new Vector3(m.X - 0.9f, m.Y + 2.6f, 1.38f), new Vector3(m.X + 0.9f, m.Y + 4.2f, 1.42f), Rope, false);
            }
            var spin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(spin.GetComponent<Collider>());
            spin.name = "Goal Crystal";
            spin.transform.SetParent(arch, false);
            spin.transform.localPosition = new Vector3(0f, 1.6f, 1.0f);
            spin.transform.localRotation = Quaternion.Euler(35f, 0f, 45f);
            spin.transform.localScale = Vector3.one * 0.55f;
            spin.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(0xf0d070, 0.9f, 0.3f);
            SetRef(go.GetComponent<SliceGoal>(), "spin", spin.transform);
        }

        static void BuildCameraZone(Transform level, CliffsideLayout.Marker m)
        {
            var go = new GameObject(m.Name, typeof(CameraZone));
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(m.X, 6f, 0f);
            var z = go.GetComponent<CameraZone>();
            z.size = new Vector2(m.Width, 60f);
            z.priority = 1;
            switch (m.Name)
            {
                case "Climb View": z.distance = 13.5f; z.height = 3.0f; z.lookHeight = 2.4f; z.lookAhead = 2.0f; z.verticalBias = 1.2f; break;
                case "Gap View": z.distance = 12.5f; z.height = 2.4f; z.lookHeight = 1.6f; z.lookAhead = 4.0f; z.verticalBias = 0.3f; break;
                default: z.distance = 15f; z.height = 3.5f; z.lookHeight = 1.8f; z.lookAhead = 1.5f; z.verticalBias = 0.5f; break;
            }
        }

        static void BuildRunStatus(Canvas hud)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var status = NewText("Run Status", hud.transform, font, 26, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(600, 36));
            status.color = new Color(0.92f, 0.94f, 1f);
            var run = hud.gameObject.AddComponent<SliceRun>();
            SetRef(run, "status", status);          // toasts go to the game HUD; the player is found at run time
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
