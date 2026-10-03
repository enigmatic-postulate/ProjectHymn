using System.Collections.Generic;
using System.Linq;
using ProjectHymn.Combat;
using ProjectHymn.Gameplay;
using ProjectHymn.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using K = ProjectHymn.EditorTools.LookBuilder;
using S = ProjectHymn.EditorTools.ProjectHymnSetup;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// M5 content and scenes in 3D: low-poly models and toon materials, the shop catalogue, the player rig,
    /// three styled arena sets, the town of Cadence, the title screen, and "Build All Scenes" with build order.
    /// </summary>
    public static class WorldBuilder
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string ItemDir = DataDir + "/Items";
        const string PhraseDir = DataDir + "/Phrases";
        const string SceneDir = Root + "/Scenes";
        const string CatalogPath = DataDir + "/GameCatalog.asset";
        const string TownPath = SceneDir + "/Town.unity";
        const string MenuPath = SceneDir + "/MainMenu.unity";

        [MenuItem("Project Hymn/Build All Scenes", priority = -10)]
        public static void BuildAll()
        {
            S.BuildPrototypeScene();
            S.BuildLevel2Scene();
            S.BuildBossScene();
            BuildTownScene();
            BuildMainMenuScene();
            EditorBuildSettings.scenes = new[] { MenuPath, TownPath, SceneDir + "/Prototype.unity", SceneDir + "/Level2.unity", SceneDir + "/Boss.unity" }
                .Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorSceneManager.OpenScene(MenuPath);
            Debug.Log("[Project Hymn] All scenes built (3D). Press Play in MainMenu: New Game → Cadence → arena gates on the right.");
        }

        // ---------------- Content ----------------

        /// <summary>Models, Phrase prices, items, enemy models and the catalogue. Safe to re-run; tuned values are kept.</summary>
        public static void EnsureContent()
        {
            System.IO.Directory.CreateDirectory(ItemDir);
            ModelGenerator.EnsureGenerated();
            AssetDatabase.Refresh();

            SetPrice("Backbeat", 400);
            SetPrice("Waltz", 300);
            EnsurePhrase("OddTime", "Odd-Time", "Seven-beat combo, only in 7/8. Built for the Finale.", 7, 2.6f, "shockwave",
                BeatAction.Strike, BeatAction.Strike, BeatAction.Smash, BeatAction.Strike, BeatAction.Strike, BeatAction.Strike, BeatAction.Smash);
            SetPrice("OddTime", 600);
            SetPrice("Lunge", 350);

            // Enemy models: the first time a definition gets a model its 2D sizes are replaced by 3D ones.
            AssignModel("Enemies/MetronomeSlime", "MetronomeSlime", new Vector2(0.9f, 0.8f), 0f);
            AssignModel("Enemies/SnareImp", "SnareImp", new Vector2(0.7f, 1.1f), 0f);
            AssignModel("Enemies/TubaKnight", "TubaKnight", new Vector2(1.0f, 1.8f), 0f);
            AssignModel("Enemies/PiccoloBat", "PiccoloBat", new Vector2(0.7f, 0.6f), 0.9f);
            AssignModel("Bosses/FirstConductorBody", "FirstConductor", new Vector2(1.2f, 2.5f), 0f);

            var items = new List<ItemDefinition>
            {
                Item("Blade", "Blade", "Balanced. The Cantor's first Instrument.", ItemKind.Weapon, 0, WeaponType.Blade, "Weapon_Blade"),
                Item("DrumHammer", "Drum Hammer", "Smash hits again half a beat later.", ItemKind.Weapon, 800, WeaponType.DrumHammer, "Weapon_DrumHammer"),
                Item("LuteBow", "Lute Bow", "Strike fires a note. Reaches the Bat.", ItemKind.Weapon, 900, WeaponType.LuteBow, "Weapon_LuteBow"),
                Armor("CantorRobe", "Cantor Robe", "Light and familiar.", 0, 0, 0, 1f, 1f, "Cantor_Robe"),
                Armor("BrassPlate", "Brass Breastplate", "+40 HP, a little slower.", 700, 40, 0, 1f, 0.92f, "Cantor_BrassPlate"),
                Armor("MetronomeVest", "Metronome Vest", "+15 stamina, 25% faster regen.", 600, 0, 15, 1.25f, 1f, "Cantor_MetronomeVest"),
            };

            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.phrases = new[] { "TripletCut", "Syncopate", "OffStep", "RisingSlash", "Stomp", "Lunge", "Backbeat", "Waltz", "OddTime" }
                .Select(n => AssetDatabase.LoadAssetAtPath<PhraseData>($"{PhraseDir}/{n}.asset")).Where(p => p != null).ToList();
            catalog.items = items;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static GameCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);

        static void SetPrice(string phrase, int price)
        {
            var p = AssetDatabase.LoadAssetAtPath<PhraseData>($"{PhraseDir}/{phrase}.asset");
            if (p == null || p.price != 0) return;
            p.price = price;
            EditorUtility.SetDirty(p);
        }

        static void EnsurePhrase(string file, string display, string desc, int meter, float mult, string effect, params BeatAction[] pattern)
        {
            string path = $"{PhraseDir}/{file}.asset";
            if (AssetDatabase.LoadAssetAtPath<PhraseData>(path) != null) return;
            var p = ScriptableObject.CreateInstance<PhraseData>();
            p.displayName = display; p.description = desc; p.requiredBeatsPerMeasure = meter;
            p.damageMultiplier = mult; p.effectId = effect; p.pattern = new List<BeatAction>(pattern);
            AssetDatabase.CreateAsset(p, path);
        }

        static void AssignModel(string assetPath, string prefab, Vector2 size, float hover)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{DataDir}/{assetPath}.asset");
            var model = ModelGenerator.LoadPrefab(prefab);
            if (def == null || model == null || def.model != null) return;
            def.model = model;
            def.modelScale = 1f;
            def.size = size;
            def.hoverHeight = hover;
            EditorUtility.SetDirty(def);
        }

        static ItemDefinition Item(string id, string name, string desc, ItemKind kind, int price, WeaponType wt, string model)
        {
            var it = LoadOrCreateItem(id);
            if (string.IsNullOrEmpty(it.description) || it.displayName == "Item")
            {
                it.displayName = name; it.description = desc; it.kind = kind; it.price = price; it.weaponType = wt;
            }
            if (it.model == null) it.model = ModelGenerator.LoadPrefab(model);
            EditorUtility.SetDirty(it);
            return it;
        }

        static ItemDefinition Armor(string id, string name, string desc, int price, float hp, float stamina, float regen, float speed, string model)
        {
            var it = LoadOrCreateItem(id);
            if (string.IsNullOrEmpty(it.description) || it.displayName == "Item")
            {
                it.displayName = name; it.description = desc; it.kind = ItemKind.Armor; it.price = price;
                it.bonusHp = hp; it.bonusStamina = stamina; it.staminaRegenMultiplier = regen; it.moveSpeedMultiplier = speed;
            }
            if (it.model == null) it.model = ModelGenerator.LoadPrefab(model);
            EditorUtility.SetDirty(it);
            return it;
        }

        static ItemDefinition LoadOrCreateItem(string id)
        {
            string path = $"{ItemDir}/{id}.asset";
            var it = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (it != null) return it;
            it = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(it, path);
            return it;
        }

        // ---------------- Player ----------------

        /// <summary>
        /// The 3D player: Rigidbody + capsule on the ground plane, a turning "Visual" root holding the Cantor body,
        /// and the weapon in the right hand. Arena players also get combat, health and loadout.
        /// </summary>
        public static GameObject MakePlayer(GameCatalog catalog, bool combatant, Vector2 position)
        {
            var types = new List<System.Type> { typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerInputReader), typeof(PlayerMotor), typeof(ToonTint), typeof(LoadoutApplier) };
            if (combatant) { types.Add(typeof(BeatCombatController)); types.Add(typeof(PlayerHealth)); }
            var player = new GameObject("Player", types.ToArray());
            player.transform.position = Ground.ToWorld(position);
            var cap = player.GetComponent<CapsuleCollider>();
            cap.radius = 0.35f; cap.height = 1.5f; cap.center = new Vector3(0f, 0.75f, 0f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f); // start facing the camera
            var body = K.Model(visual, "Cantor_Robe", Vector3.zero);
            if (body != null) body.name = "Body";
            var beat = visual.gameObject.AddComponent<BeatPulse>();

            var hand = new GameObject("Weapon", typeof(WeaponVisual)).transform;
            hand.SetParent(visual, false);
            hand.localPosition = new Vector3(0.36f, 0.72f, 0.1f);
            hand.localRotation = Quaternion.Euler(55f, 20f, 0f);
            var blade = K.Model(hand, "Weapon_Blade", Vector3.zero);
            K.Shadow(player.transform, visual, 0.95f); // M6: shows where a jump will land

            S.SetRef(player.GetComponent<PlayerMotor>(), "visual", visual);
            var applier = player.GetComponent<LoadoutApplier>();
            S.SetRef(applier, "catalog", catalog);
            S.SetRef(applier, "visualRoot", visual);
            S.SetRef(applier, "weaponVisual", hand.GetComponent<WeaponVisual>());
            SetFloat(beat, "beatPunch", 0.03f);
            SetFloat(beat, "downbeatPunch", 0.07f);
            return player;
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Arena sets ----------------

        public enum ArenaStyle { Practice, Garden, Boss }

        public static Color Background(ArenaStyle style) => style switch
        {
            ArenaStyle.Garden => new Color(0.05f, 0.08f, 0.2f),
            ArenaStyle.Boss => new Color(0.12f, 0.03f, 0.08f),
            _ => new Color(0.1f, 0.06f, 0.18f),
        };

        /// <summary>
        /// Builds the floor, border walls (with colliders), a glowing musical staff across the floor and
        /// themed dressing. Returns the glowing props for the beat-pulsed stage lighting.
        /// </summary>
        public static List<Renderer> BuildArenaSet(ArenaStyle style, Rect r)
        {
            var root = new GameObject("Arena Set").transform;
            var glow = new List<Renderer>();
            (uint floorA, uint floorB, uint wall, uint trim, uint staff) = style switch
            {
                ArenaStyle.Garden => (0x2e8a4au, 0x38a058u, 0x1f6a3au, 0xff7ac8u, 0x9cffb8u),
                ArenaStyle.Boss => (0x8a4a2au, 0x9c5632u, 0x5a1a2au, 0xffc21au, 0xffd85au),
                _ => (0x4a3a8au, 0x5a46a0u, 0x7a3a8cu, 0xffc21au, 0x7af0ffu),
            };

            // Floor: striped boards (no overlapping faces), slightly larger than the playable area.
            int rows = Mathf.CeilToInt(r.height) + 2;
            for (int i = 0; i < rows; i++)
            {
                float z = r.yMin - 1f + i + 0.5f;
                K.Block(root, "Block_Box", i % 2 == 0 ? floorA : floorB, new Vector3(r.center.x, -0.3f, z), new Vector3(r.width + 2f, 0.3f, 1f), name: "Floor");
            }

            // Musical staff: five glowing lines across the floor, pulsed on the beat.
            for (int i = 0; i < 5; i++)
            {
                var line = K.Block(root, "Block_Box", staff, new Vector3(r.center.x, 0f, -1.2f + i * 0.6f), new Vector3(r.width - 1f, 0.02f, 0.06f), glow: true, name: "Staff Line");
                line.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glow.Add(line.GetComponent<Renderer>());
            }

            // Border walls: low at the front (camera side), taller at the back.
            K.Block(root, "Block_Box", wall, new Vector3(r.center.x, 0f, r.yMax + 0.5f), new Vector3(r.width + 2f, 1.2f, 1f), collider: true, name: "Back Wall");
            K.Block(root, "Block_Box", wall, new Vector3(r.center.x, 0f, r.yMin - 0.5f), new Vector3(r.width + 2f, 0.35f, 1f), collider: true, name: "Front Wall");
            K.Block(root, "Block_Box", wall, new Vector3(r.xMin - 0.5f, 0f, r.center.y), new Vector3(1f, 0.7f, r.height), collider: true, name: "Left Wall");
            K.Block(root, "Block_Box", wall, new Vector3(r.xMax + 0.5f, 0f, r.center.y), new Vector3(1f, 0.7f, r.height), collider: true, name: "Right Wall");
            K.Block(root, "Block_Box", trim, new Vector3(r.center.x, 1.2f, r.yMax + 0.5f), new Vector3(r.width + 2f, 0.12f, 1.05f), name: "Back Trim");
            K.Bounds(root, r); // invisible backstop, in case anything slips past the walls

            // Corner pillars with lamps
            foreach (var (x, z) in new[] { (r.xMin - 0.5f, r.yMax + 0.5f), (r.xMax + 0.5f, r.yMax + 0.5f), (r.xMin - 0.5f, r.yMin - 0.5f), (r.xMax + 0.5f, r.yMin - 0.5f) })
            {
                K.Block(root, "Block_Cylinder", wall, new Vector3(x, 0f, z), new Vector3(1.1f, 1.8f, 1.1f), collider: true, name: "Pillar");
                var lamp = K.Block(root, "Block_Gem", trim, new Vector3(x, 1.8f, z), new Vector3(0.6f, 0.6f, 0.6f), glow: true, name: "Lamp");
                glow.Add(lamp.GetComponent<Renderer>());
            }

            switch (style)
            {
                case ArenaStyle.Practice:
                    // Organ pipes behind the back wall
                    for (int i = 0; i < 11; i++)
                    {
                        float x = r.center.x - 5f + i;
                        float h = 2.2f + (5 - Mathf.Abs(i - 5)) * 0.45f;
                        var pipe = K.Block(root, "Block_Cylinder", 0xffc21a, new Vector3(x, 0f, r.yMax + 1.6f), new Vector3(0.6f, h, 0.6f), name: "Organ Pipe");
                        K.Block(root, "Block_Cone", 0xd89a10, new Vector3(x, h, r.yMax + 1.6f), new Vector3(0.6f, 0.3f, 0.6f), name: "Pipe Cap");
                    }
                    K.Block(root, "Block_Box", 0x3a2a5a, new Vector3(r.center.x, 0f, r.yMax + 2.4f), new Vector3(r.width + 4f, 3.5f, 0.6f), name: "Back Panel");
                    break;

                case ArenaStyle.Garden:
                    // Hedge crowns, trees outside the walls, flower dots
                    K.Block(root, "Block_Box", 0x2fb85a, new Vector3(r.center.x, 1.2f, r.yMax + 0.5f), new Vector3(r.width + 2.2f, 0.5f, 1.2f), name: "Hedge");
                    var rng = new System.Random(7);
                    foreach (var (x, z) in new[] { (-9.5f, 5.5f), (-6f, 6.2f), (-2f, 5.8f), (2.5f, 6.4f), (6.5f, 5.6f), (9.5f, 6f), (-10f, 0f), (10f, 1f) })
                    {
                        K.Block(root, "Block_Cylinder", 0x6a3a1a, new Vector3(x, 0f, z), new Vector3(0.4f, 1f, 0.4f), name: "Trunk");
                        K.Block(root, "Block_Cone", 0x1f9a4a, new Vector3(x, 0.8f, z), new Vector3(2.2f, 1.8f, 2.2f), name: "Tree");
                        K.Block(root, "Block_Cone", 0x3fd06a, new Vector3(x, 1.9f, z), new Vector3(1.6f, 1.5f, 1.6f), name: "Tree Top");
                    }
                    for (int i = 0; i < 26; i++)
                    {
                        float x = r.xMin + (float)rng.NextDouble() * r.width, z = r.yMin + (float)rng.NextDouble() * r.height;
                        if (Mathf.Abs(z) < 1.6f) continue; // keep the staff clear
                        uint c = (i % 3) switch { 0 => 0xff7ac8u, 1 => 0xfff05au, _ => 0xb88cffu };
                        K.Block(root, "Block_Gem", c, new Vector3(x, 0f, z), new Vector3(0.18f, 0.18f, 0.18f), name: "Flower");
                    }
                    var moon = K.Block(root, "Block_Gem", 0xfff2c0, new Vector3(7f, 7f, 14f), new Vector3(2.5f, 2.5f, 2.5f), glow: true, name: "Moon");
                    moon.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    break;

                case ArenaStyle.Boss:
                    // Curtained stage: red drapes, gold valance, footlights along the front edge
                    for (int i = 0; i < 9; i++)
                    {
                        float x = r.center.x - 8.5f + i * 2.125f;
                        K.Block(root, "Block_Cylinder", i % 2 == 0 ? 0xc81a3cu : 0xa8122eu, new Vector3(x, 0f, r.yMax + 1.8f), new Vector3(1.4f, 5f, 0.9f), name: "Curtain");
                    }
                    K.Block(root, "Block_Box", 0xffc21a, new Vector3(r.center.x, 4.8f, r.yMax + 1.4f), new Vector3(r.width + 4f, 0.5f, 0.4f), name: "Valance");
                    for (int i = 0; i < 9; i++)
                    {
                        var fl = K.Block(root, "Block_Gem", 0xffe08a, new Vector3(r.center.x - 6f + i * 1.5f, 0.35f, r.yMin - 0.45f), new Vector3(0.35f, 0.25f, 0.35f), glow: true, name: "Footlight");
                        glow.Add(fl.GetComponent<Renderer>());
                    }
                    break;
            }
            return glow;
        }

        // ---------------- Town of Cadence ----------------

        static readonly Rect Plaza = new Rect(-14.6f, -8f, 29.2f, 9.5f);

        [MenuItem("Project Hymn/Build Town Scene", priority = 3)]
        public static void BuildTownScene()
        {
            EnsureContent();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = LoadCatalog();

            var key = K.KeyLight(new Color(1f, 0.82f, 0.62f), 1.1f);
            key.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            K.Ambient(new Color(0.46f, 0.36f, 0.62f));
            K.GlobalVolume(new Color(0.2f, 0.1f, 0.28f));
            var lamps = BuildTownSet();

            var player = MakePlayer(catalog, combatant: false, new Vector2(0f, -1.2f));
            var rig = K.CameraRig(player.transform, new Vector2(0f, -1.2f), 15f, 52f, new Rect(-9f, -6.5f, 18f, 6f), new Color(0.2f, 0.1f, 0.28f));
            S.SetRef(rig, "target", player.transform);

            var pulse = new GameObject("Lamp Glow", typeof(BeatLightPulse)).GetComponent<BeatLightPulse>();
            S.SetList(pulse, "glowRenderers", lamps);
            SetFloat(pulse, "lightBoost", 0f);

            Station("The Luthier (shop)", TownStation.Shop, new Vector2(-9.9f, 0.9f));
            Station("Wardrobe (loadout)", TownStation.Loadout, new Vector2(-5.2f, 0.9f));
            Gate("Practice Hall", "Prototype", "arena1", new string[0], new Vector2(7.2f, 0.9f));
            Gate("Moonlit Garden", "Level2", "arena2", new string[0], new Vector2(10f, 0.9f));
            Gate("The First Conductor", "Boss", "boss", new[] { "arena1", "arena2" }, new Vector2(13f, 0.9f));
            Lore("The First Verse", "verse_one", "Before the world there was a count-in. Four beats of silence, and on the fifth, everything began.", new Vector2(-13.4f, -7f));
            Lore("A Margin Note", "verse_two", "The Conductors were never cruel. They only stopped listening to anyone but themselves.", new Vector2(13.4f, -7f));

            // Town HUD: prompt, Notes, controls hint
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("TownHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var prompt = S.NewText("Prompt", canvasGo.transform, font, 40, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1200, 60));
            prompt.text = "";
            var notes = S.NewText("Notes", canvasGo.transform, font, 36, new Vector2(1f, 1f), new Vector2(-220, -50), new Vector2(400, 50));
            notes.alignment = TextAnchor.MiddleRight;
            var title = S.NewText("Title", canvasGo.transform, font, 34, new Vector2(0f, 1f), new Vector2(240, -50), new Vector2(440, 50));
            title.alignment = TextAnchor.MiddleLeft;
            title.text = "CADENCE";
            var hint = S.NewText("Hint", canvasGo.transform, font, 22, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1400, 36));
            hint.text = "WASD move  ·  E interact  ·  Esc menu";

            var game = new GameObject("Town", typeof(TownController), typeof(PauseMenu), typeof(SettingsApplier));
            var town = game.GetComponent<TownController>();
            S.SetRef(town, "catalog", catalog);
            S.SetRef(town, "input", player.GetComponent<PlayerInputReader>());
            S.SetRef(town, "motor", player.GetComponent<PlayerMotor>());
            S.SetRef(town, "promptText", prompt);
            S.SetRef(town, "notesText", notes);
            var pause = game.GetComponent<PauseMenu>();
            S.SetRef(pause, "input", player.GetComponent<PlayerInputReader>());
            var so = new SerializedObject(pause);
            so.FindProperty("isTown").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownPath);
            S.AddSceneToBuild(TownPath);
        }

        /// <summary>
        /// Cadence at dusk: a row of shops and the Hall of Measures along the north edge, the arena wall with three
        /// gates on the east, a cobbled plaza with a round stage, lamps, trees and flower beds. Returns lamp glows.
        /// </summary>
        static List<Renderer> BuildTownSet()
        {
            var root = new GameObject("Cadence").transform;
            var lamps = new List<Renderer>();
            const float front = 1.8f; // building fronts (z)

            // Plaza floor: alternating cobble bands, lighter path strips in front of each door
            for (int i = 0; i < 12; i++)
                K.Block(root, "Block_Box", i % 2 == 0 ? 0x7a6e96u : 0x6e6288u, new Vector3(0f, -0.3f, -9.5f + i * 1f + 0.5f), new Vector3(32f, 0.3f, 1f), name: "Cobbles");
            K.Block(root, "Block_Box", 0x5a4e78, new Vector3(0f, -0.3f, 5f), new Vector3(32f, 0.3f, 6f), name: "Back Lot");
            foreach (float x in new[] { -9.9f, -5.2f, 0.5f, 7.2f, 10f, 13f })
                K.Block(root, "Block_Box", 0x9a8eb6, new Vector3(x, 0f, front - 1.2f), new Vector3(1.6f, 0.02f, 2.4f), name: "Path");

            // North row
            House(root, -13.7f, 2.6f, 0x8a6aa0, 0x4a2a5a, front);
            House(root, -9.9f, 4f, 0x9a5a32, 0xd8323c, front, door: true);       // The Luthier
            var sign = K.Block(root, "Block_Box", 0xffe0a0, new Vector3(-9.9f, 2.6f, front + 0.05f), new Vector3(1.8f, 0.7f, 0.15f), name: "Shop Sign");
            var note = K.Model(root, "NoteShot", new Vector3(-10.05f, 2.85f, front - 0.08f), 180f, 1.2f);
            House(root, -5.2f, 3.8f, 0x2aa8b0, 0x1c349c, front, door: true);       // Wardrobe
            // Hall of Measures: stone body, columns, pediment, golden pipes
            K.Block(root, "Block_Box", 0xd8d0ea, new Vector3(0.5f, 0f, front + 2.2f), new Vector3(6f, 4.2f, 4.4f), collider: true, name: "Hall");
            for (int i = 0; i < 6; i++)
                K.Block(root, "Block_Cylinder", 0xf4f0ff, new Vector3(-1.9f + i * 0.96f, 0f, front - 0.2f), new Vector3(0.5f, 3.8f, 0.5f), name: "Column");
            K.Block(root, "Block_Box", 0xb8aed4, new Vector3(0.5f, 3.8f, front + 0.9f), new Vector3(6.4f, 0.35f, 2.6f), name: "Entablature");
            K.Block(root, "Block_Roof", 0xa89cc8, new Vector3(0.5f, 4.15f, front + 2.2f), new Vector3(6.6f, 1.4f, 4.6f), name: "Pediment");
            for (int i = 0; i < 9; i++)
            {
                float h = 1f + (4 - Mathf.Abs(i - 4)) * 0.35f;
                var pipe = K.Block(root, "Block_Cylinder", 0xffc21a, new Vector3(-1.1f + i * 0.45f, 4.6f, front + 3.4f), new Vector3(0.3f, h, 0.3f), glow: i == 4, name: "Pipe");
                if (i == 4) lamps.Add(pipe.GetComponent<Renderer>());
            }
            K.Block(root, "Block_Box", 0x2a1e48, new Vector3(0.5f, 0f, front - 0.01f), new Vector3(1.4f, 2.2f, 0.05f), name: "Hall Door");
            House(root, 4.4f, 2.4f, 0x7a64a8, 0x3a2a6a, front);

            // East: arena wall with three gates (the last is the boss gate)
            K.Block(root, "Block_Box", 0x8a80a8, new Vector3(10.4f, 0f, front + 0.6f), new Vector3(9.4f, 3.2f, 1.2f), collider: true, name: "Arena Wall");
            K.Block(root, "Block_Box", 0xffc21a, new Vector3(10.4f, 3.2f, front + 0.6f), new Vector3(9.5f, 0.2f, 1.25f), name: "Wall Trim");
            foreach (var (x, boss) in new[] { (7.2f, false), (10f, false), (13f, true) })
            {
                uint trim = boss ? 0xff3a4au : 0xffc21au;
                K.Block(root, "Block_Box", boss ? 0x3a0e1au : 0x1c1430u, new Vector3(x, 0f, front - 0.02f), new Vector3(1.6f, 2.3f, 0.05f), name: "Gate");
                K.Block(root, "Block_Box", trim, new Vector3(x - 0.9f, 0f, front - 0.1f), new Vector3(0.25f, 2.5f, 0.25f), name: "Gate Post");
                K.Block(root, "Block_Box", trim, new Vector3(x + 0.9f, 0f, front - 0.1f), new Vector3(0.25f, 2.5f, 0.25f), name: "Gate Post");
                var arch = K.Block(root, "Block_Box", trim, new Vector3(x, 2.5f, front - 0.1f), new Vector3(2.1f, 0.3f, 0.3f), glow: boss, name: "Gate Arch");
                if (boss) lamps.Add(arch.GetComponent<Renderer>());
            }

            // Round stage in the plaza
            K.Block(root, "Block_Cylinder", 0x9a6038, new Vector3(0f, 0f, -4.2f), new Vector3(5.6f, 0.25f, 5.6f), collider: false, name: "Stage");
            K.Block(root, "Block_Cylinder", 0xffc21a, new Vector3(0f, 0f, -4.2f), new Vector3(5.9f, 0.18f, 5.9f), name: "Stage Rim");

            // Lamps, trees, flower beds, benches
            foreach (var x in new[] { -3.6f, 3.6f, -11.5f, 11.5f })
            {
                K.Block(root, "Block_Cylinder", 0x24163a, new Vector3(x, 0f, -0.6f), new Vector3(0.18f, 2.4f, 0.18f), collider: true, name: "Lamp Post");
                var bulb = K.Block(root, "Block_Gem", 0xffd27a, new Vector3(x, 2.4f, -0.6f), new Vector3(0.45f, 0.45f, 0.45f), glow: true, name: "Lamp");
                lamps.Add(bulb.GetComponent<Renderer>());
            }
            foreach (var (x, z) in new[] { (-14f, -2.5f), (14f, -2.5f), (-7.5f, -7.4f), (7.5f, -7.4f) })
            {
                K.Block(root, "Block_Cylinder", 0x6a3a1a, new Vector3(x, 0f, z), new Vector3(0.4f, 1f, 0.4f), collider: true, name: "Trunk");
                K.Block(root, "Block_Cone", 0x2a9a4a, new Vector3(x, 0.8f, z), new Vector3(2f, 1.7f, 2f), name: "Tree");
                K.Block(root, "Block_Cone", 0x4fd06a, new Vector3(x, 1.8f, z), new Vector3(1.4f, 1.4f, 1.4f), name: "Tree Top");
            }
            var rng = new System.Random(11);
            foreach (var (x, z) in new[] { (-4.2f, -7.4f), (4.2f, -7.4f), (-12f, -5f), (12f, -5f) })
            {
                K.Block(root, "Block_Box", 0x2e7a3a, new Vector3(x, 0f, z), new Vector3(2.4f, 0.4f, 1f), collider: true, name: "Flower Bed");
                for (int i = 0; i < 7; i++)
                {
                    uint c = (i % 3) switch { 0 => 0xff7ac8u, 1 => 0xfff05au, _ => 0xb88cffu };
                    K.Block(root, "Block_Gem", c, new Vector3(x - 1f + (float)rng.NextDouble() * 2f, 0.4f, z - 0.3f + (float)rng.NextDouble() * 0.6f), new Vector3(0.2f, 0.2f, 0.2f), name: "Flower");
                }
            }

            K.Bounds(root, Plaza);
            return lamps;
        }

        static void House(Transform root, float x, float width, uint wall, uint roof, float front, bool door = false)
        {
            float depth = 3.6f, height = 2.8f;
            K.Block(root, "Block_Box", wall, new Vector3(x, 0f, front + depth / 2f), new Vector3(width, height, depth), collider: true, name: "House");
            K.Block(root, "Block_Roof", roof, new Vector3(x, height, front + depth / 2f), new Vector3(width + 0.4f, 1.3f, depth + 0.4f), name: "Roof");
            for (int i = 0; i < Mathf.Max(1, Mathf.FloorToInt(width / 1.3f)); i++)
            {
                float wx = x - width / 2f + 0.65f + i * 1.3f;
                if (door && Mathf.Abs(wx - x) < 0.6f) continue;
                K.Block(root, "Block_Box", 0xffd27a, new Vector3(wx, 1.5f, front - 0.02f), new Vector3(0.5f, 0.55f, 0.06f), glow: true, name: "Window");
            }
            if (door) K.Block(root, "Block_Box", 0x5a3a24, new Vector3(x, 0f, front - 0.02f), new Vector3(0.9f, 1.6f, 0.06f), name: "Door");
        }

        static TownInteractable Station(string title, TownStation kind, Vector2 pos)
        {
            var go = new GameObject(title, typeof(TownInteractable));
            go.transform.position = Ground.ToWorld(pos);
            var marker = K.Model(go.transform, "Marker", new Vector3(0f, 2.3f, 0f));
            if (marker != null) marker.AddComponent<BeatPulse>();
            var t = go.GetComponent<TownInteractable>();
            t.station = kind;
            t.title = title;
            t.radius = 1.5f;
            return t;
        }

        static void Gate(string title, string scene, string arenaId, string[] requires, Vector2 pos)
        {
            var t = Station(title, TownStation.ArenaGate, pos);
            t.sceneName = scene;
            t.arenaId = arenaId;
            t.requiresCleared = requires;
        }

        static void Lore(string title, string id, string text, Vector2 pos)
        {
            var go = new GameObject(title, typeof(TownInteractable));
            go.transform.position = Ground.ToWorld(pos);
            var scroll = K.Model(go.transform, "LoreScroll", new Vector3(0f, 0.15f, 0f), 30f, 1.2f);
            var t = go.GetComponent<TownInteractable>();
            t.station = TownStation.Lore;
            t.title = title;
            t.loreId = id;
            t.loreText = text;
            t.loreNotes = 100;
            t.radius = 1.2f;
        }

        // ---------------- Title screen ----------------

        [MenuItem("Project Hymn/Build Main Menu Scene", priority = 4)]
        public static void BuildMainMenuScene()
        {
            EnsureContent();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var key = K.KeyLight(new Color(1f, 0.78f, 0.6f), 1.05f);
            key.transform.rotation = Quaternion.Euler(30f, -50f, 0f);
            K.Ambient(new Color(0.42f, 0.32f, 0.6f));
            K.GlobalVolume(new Color(0.22f, 0.1f, 0.3f));
            BuildTownSet();
            // Wide establishing shot of Cadence, with the Cantor on the stage
            K.CameraRig(null, new Vector2(0f, -1.5f), 24f, 32f, default, new Color(0.22f, 0.1f, 0.3f));
            var hero = K.Model(null, "Cantor_Robe", Ground.ToWorld(new Vector2(0f, -4.2f), 0.25f), 180f, 1.4f);
            if (hero != null) hero.AddComponent<BeatPulse>();
            new GameObject("MainMenu", typeof(MainMenu));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuPath);
            S.AddSceneToBuild(MenuPath);
        }
    }
}
