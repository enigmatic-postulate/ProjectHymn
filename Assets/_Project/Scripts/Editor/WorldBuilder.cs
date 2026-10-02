using System.Collections.Generic;
using System.Linq;
using ProjectHymn.Combat;
using ProjectHymn.Gameplay;
using ProjectHymn.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using S = ProjectHymn.EditorTools.ProjectHymnSetup;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// M4 content and scenes: placeholder art, the shop catalogue (Phrases, Instruments, armor), the town of
    /// Cadence, the title screen, and a one-click "Build All Scenes" that sets the build order.
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
        const string SpriteMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        [MenuItem("Project Hymn/Build All Scenes", priority = -10)]
        public static void BuildAll()
        {
            S.BuildPrototypeScene();
            S.BuildLevel2Scene();
            S.BuildBossScene();
            BuildTownScene();
            BuildMainMenuScene();
            // Title first, then the town; arenas are loaded from town gates.
            EditorBuildSettings.scenes = new[] { MenuPath, TownPath, SceneDir + "/Prototype.unity", SceneDir + "/Level2.unity", SceneDir + "/Boss.unity" }
                .Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorSceneManager.OpenScene(MenuPath);
            Debug.Log("[Project Hymn] All scenes built. Press Play in MainMenu: New Game → town of Cadence → arena gates on the right.");
        }

        // ---------------- Content ----------------

        /// <summary>Art, Phrase prices, new Phrases, items, enemy sprites and the catalogue. Safe to re-run; tuned values are kept.</summary>
        public static void EnsureContent()
        {
            System.IO.Directory.CreateDirectory(ItemDir);
            ArtGenerator.EnsureGenerated();
            AssetDatabase.Refresh();

            // Prices for shop Phrases (starters stay free and unsold).
            SetPrice("Backbeat", 400);
            SetPrice("Waltz", 300);
            var oddTime = EnsurePhrase("OddTime", "Odd-Time", "Seven-beat combo, only in 7/8. Built for the Finale.", 7, 2.6f, "shockwave",
                BeatAction.Strike, BeatAction.Strike, BeatAction.Smash, BeatAction.Strike, BeatAction.Strike, BeatAction.Strike, BeatAction.Smash);
            SetPrice("OddTime", 600);

            // Pixel sprites for enemies and the boss (only if none assigned, so hand-picked art survives).
            AssignSprite("Enemies/MetronomeSlime", "slime", 1);
            AssignSprite("Enemies/SnareImp", "imp", 1);
            AssignSprite("Enemies/TubaKnight", "knight", 1);
            AssignSprite("Enemies/PiccoloBat", "bat", 1);
            AssignSprite("Bosses/FirstConductorBody", "conductor", 2);

            var items = new List<ItemDefinition>
            {
                Item("Blade", "Blade", "Balanced. The Cantor's first Instrument.", ItemKind.Weapon, 0, WeaponType.Blade, "blade"),
                Item("DrumHammer", "Drum Hammer", "Smash hits again half a beat later.", ItemKind.Weapon, 800, WeaponType.DrumHammer, "drum_hammer"),
                Item("LuteBow", "Lute Bow", "Strike fires a note. Reaches the Bat.", ItemKind.Weapon, 900, WeaponType.LuteBow, "lute_bow"),
                Armor("CantorRobe", "Cantor Robe", "Light and familiar.", 0, 0, 0, 1f, 1f, "player_robe"),
                Armor("BrassPlate", "Brass Breastplate", "+40 HP, a little slower.", 700, 40, 0, 1f, 0.92f, "player_brass"),
                Armor("MetronomeVest", "Metronome Vest", "+15 stamina, 25% faster regen.", 600, 0, 15, 1.25f, 1f, "player_vest"),
            };

            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.phrases = new[] { "TripletCut", "Syncopate", "OffStep", "Backbeat", "Waltz", "OddTime" }
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

        static PhraseData EnsurePhrase(string file, string display, string desc, int meter, float mult, string effect, params BeatAction[] pattern)
        {
            string path = $"{PhraseDir}/{file}.asset";
            var p = AssetDatabase.LoadAssetAtPath<PhraseData>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<PhraseData>();
            p.displayName = display; p.description = desc; p.requiredBeatsPerMeasure = meter;
            p.damageMultiplier = mult; p.effectId = effect; p.pattern = new List<BeatAction>(pattern);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static void AssignSprite(string assetPath, string sprite, int scale)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{DataDir}/{assetPath}.asset");
            var sp = ArtGenerator.Load(sprite);
            if (def == null || sp == null || def.sprite != null) return;
            def.sprite = sp;
            def.size = (Vector2)sp.bounds.size * scale;
            EditorUtility.SetDirty(def);
        }

        static ItemDefinition Item(string id, string name, string desc, ItemKind kind, int price, WeaponType wt, string sprite)
        {
            var it = LoadOrCreateItem(id);
            if (string.IsNullOrEmpty(it.description) || it.displayName == "Item")
            {
                it.displayName = name; it.description = desc; it.kind = kind; it.price = price; it.weaponType = wt;
            }
            if (it.sprite == null) it.sprite = ArtGenerator.Load(sprite);
            EditorUtility.SetDirty(it);
            return it;
        }

        static ItemDefinition Armor(string id, string name, string desc, int price, float hp, float stamina, float regen, float speed, string sprite)
        {
            var it = LoadOrCreateItem(id);
            if (string.IsNullOrEmpty(it.description) || it.displayName == "Item")
            {
                it.displayName = name; it.description = desc; it.kind = ItemKind.Armor; it.price = price;
                it.bonusHp = hp; it.bonusStamina = stamina; it.staminaRegenMultiplier = regen; it.moveSpeedMultiplier = speed;
            }
            if (it.sprite == null) it.sprite = ArtGenerator.Load(sprite);
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

        // ---------------- Shared scene pieces ----------------

        /// <summary>A pre-rendered backdrop sprite behind everything.</summary>
        public static GameObject Backdrop(string sprite, Vector2 position, Material mat, float dim = 1f)
        {
            var go = new GameObject("Backdrop", typeof(SpriteRenderer));
            go.transform.position = position;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = ArtGenerator.Load(sprite);
            sr.sortingOrder = -100;
            sr.color = new Color(dim, dim, dim, 1f);
            if (mat != null) sr.sharedMaterial = mat;
            return go;
        }

        /// <summary>Pixel-art Cantor with the weapon in hand and the loadout applier wired.</summary>
        public static SpriteRenderer DecoratePlayer(GameObject player, Material mat, GameCatalog catalog, out GameObject art)
        {
            art = new GameObject("Sprite", typeof(SpriteRenderer), typeof(BeatPulse));
            art.transform.SetParent(player.transform, false);
            var sr = art.GetComponent<SpriteRenderer>();
            sr.sprite = ArtGenerator.Load("player_robe");
            sr.sortingOrder = 10;
            if (mat != null) sr.sharedMaterial = mat;

            var weapon = new GameObject("Weapon", typeof(SpriteRenderer), typeof(WeaponVisual));
            weapon.transform.SetParent(player.transform, false);
            var wsr = weapon.GetComponent<SpriteRenderer>();
            wsr.sprite = ArtGenerator.Load("blade");
            wsr.sortingOrder = 11;
            if (mat != null) wsr.sharedMaterial = mat;
            S.SetRef(weapon.GetComponent<WeaponVisual>(), "sprite", wsr);

            var applier = player.GetComponent<LoadoutApplier>();
            if (applier != null)
            {
                S.SetRef(applier, "catalog", catalog);
                S.SetRef(applier, "body", sr);
                S.SetRef(applier, "weaponVisual", weapon.GetComponent<WeaponVisual>());
            }
            player.GetComponent<CircleCollider2D>().radius = 0.4f;
            return sr;
        }

        static Camera MakeCamera(Color bg)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            camGo.transform.position = new Vector3(0, 0, -10);
            return cam;
        }

        // ---------------- Town ----------------

        [MenuItem("Project Hymn/Build Town Scene", priority = 3)]
        public static void BuildTownScene()
        {
            EnsureContent();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = LoadCatalog();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteMatPath);
            var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var cam = MakeCamera(new Color(0.08f, 0.06f, 0.14f));
            Backdrop("bg_town", Vector2.zero, mat);

            // Walkable plaza (matches the painted backdrop): x -14.6..14.6, y -8.0..1.5
            var plaza = new Rect(-14.6f, -8.0f, 29.2f, 9.5f);
            S.BuildArenaWalls(plaza, square, mat);
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (r.gameObject.name.StartsWith("Wall")) r.enabled = false; // invisible: the art draws the edges

            var player = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(PlayerInputReader), typeof(PlayerMotor), typeof(LoadoutApplier));
            player.transform.position = new Vector3(0f, -1.2f, 0f);
            DecoratePlayer(player, mat, catalog, out _);

            var follow = cam.gameObject.AddComponent<CameraFollow>();
            S.SetRef(follow, "target", player.transform);

            var marker = ArtGenerator.Load("marker");
            Station("The Luthier (shop)", TownStation.Shop, new Vector2(-9.9f, 0.7f), marker, mat);
            Station("Wardrobe (loadout)", TownStation.Loadout, new Vector2(-5.2f, 0.7f), marker, mat);
            Gate("Practice Hall", "Prototype", "arena1", new string[0], new Vector2(7.2f, 0.7f), marker, mat);
            Gate("Moonlit Garden", "Level2", "arena2", new string[0], new Vector2(10f, 0.7f), marker, mat);
            Gate("The First Conductor", "Boss", "boss", new[] { "arena1", "arena2" }, new Vector2(13f, 0.7f), marker, mat);
            Lore("The First Verse", "verse_one", "Before the world there was a count-in. Four beats of silence, and on the fifth, everything began.", new Vector2(-12.9f, -6.3f), mat);
            Lore("A Margin Note", "verse_two", "The Conductors were never cruel. They only stopped listening to anyone but themselves.", new Vector2(12.9f, -6.3f), mat);

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

        static TownInteractable Station(string title, TownStation kind, Vector2 pos, Sprite marker, Material mat)
        {
            var go = new GameObject(title, typeof(TownInteractable), typeof(SpriteRenderer), typeof(BeatPulse));
            go.transform.position = pos;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = marker;
            sr.sortingOrder = 5;
            if (mat != null) sr.sharedMaterial = mat;
            var t = go.GetComponent<TownInteractable>();
            t.station = kind;
            t.title = title;
            return t;
        }

        static void Gate(string title, string scene, string arenaId, string[] requires, Vector2 pos, Sprite marker, Material mat)
        {
            var t = Station(title, TownStation.ArenaGate, pos, marker, mat);
            t.sceneName = scene;
            t.arenaId = arenaId;
            t.requiresCleared = requires;
        }

        static void Lore(string title, string id, string text, Vector2 pos, Material mat)
        {
            var t = Station(title, TownStation.Lore, pos, ArtGenerator.Load("lore_scroll"), mat);
            t.loreId = id;
            t.loreText = text;
            t.loreNotes = 100;
            t.radius = 1f;
        }

        // ---------------- Title screen ----------------

        [MenuItem("Project Hymn/Build Main Menu Scene", priority = 4)]
        public static void BuildMainMenuScene()
        {
            EnsureContent();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteMatPath);
            var cam = MakeCamera(new Color(0.05f, 0.04f, 0.1f));
            cam.orthographicSize = 8.44f; // the whole town painting
            Backdrop("bg_town", Vector2.zero, mat, 0.55f);
            new GameObject("MainMenu", typeof(MainMenu));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuPath);
            S.AddSceneToBuild(MenuPath);
        }
    }
}
