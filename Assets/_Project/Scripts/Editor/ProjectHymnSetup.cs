using System.Collections.Generic;
using System.IO;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// One-click setup: creates the starter Phrase and Song assets and a playable prototype scene.
    /// Menu: Project Hymn ▸ Build Prototype Scene. Safe to re-run; existing assets are kept.
    /// </summary>
    public static class ProjectHymnSetup
    {
        const string Root = "Assets/_Project";
        const string PhraseDir = Root + "/Data/Phrases";
        const string SongDir = Root + "/Data/Songs";
        const string EnemyDir = Root + "/Data/Enemies";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Prototype.unity";
        const string BossScenePath = SceneDir + "/Boss.unity";
        const string BossDir = Root + "/Data/Bosses";

        [MenuItem("Project Hymn/Build Prototype Scene", priority = 0)]
        public static void BuildPrototypeScene() => BuildArenaScene(false);

        const string Level2ScenePath = SceneDir + "/Level2.unity";
        const string Level2Clip = Root + "/Audio/Music/Donkey_Kong_Country_A_Different_Kind_of_Peace_OC_ReMix.mp3";

        /// <summary>The wave arena on the Level 2 track (OC ReMix OCR02936), looping its main body.</summary>
        [MenuItem("Project Hymn/Build Level 2 Scene", priority = 1)]
        public static void BuildLevel2Scene() => BuildArenaScene(false, level2: true);

        /// <summary>The First Conductor on Battle Theme 2 (M3). Its music loops each phase until the boss moves on.</summary>
        [MenuItem("Project Hymn/Build Boss Scene", priority = 2)]
        public static void BuildBossScene() => BuildArenaScene(true);

        static void BuildArenaScene(bool bossFight, bool level2 = false)
        {
            string scenePath = bossFight ? BossScenePath : level2 ? Level2ScenePath : ScenePath;
            foreach (var dir in new[] { PhraseDir, SongDir, EnemyDir, BossDir, SceneDir, Root + "/Audio/Music", Root + "/Art", Root + "/Prefabs" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var phrases = CreatePhrases();
            var theme1 = CreateSong("BattleTheme1", "https://www.youtube.com/watch?v=Z3l0ehUm4lM",
                new TempoSection(112, 130f, 4));
            var theme2 = CreateSong("BattleTheme2", "https://www.youtube.com/watch?v=fGCFGbvfNCQ",
                new TempoSection(4, 150f, 4),
                new TempoSection(4, 150f, 3),
                new TempoSection(4, 165f, 7, 8));
            CreateEnemies();
            AssetDatabase.SaveAssets();
            CreateBoss();
            AssetDatabase.SaveAssets();
            WorldBuilder.EnsureContent();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Re-load the data assets after the scene switch: references held across NewScene can
            // come back empty on newer Unity versions, which leaves the scene with no song or phrases.
            AssetDatabase.SaveAssets();
            theme1 = AssetDatabase.LoadAssetAtPath<SongData>($"{SongDir}/BattleTheme1.asset");
            theme2 = AssetDatabase.LoadAssetAtPath<SongData>($"{SongDir}/BattleTheme2.asset");
            for (int i = 0; i < phrases.Count; i++)
                phrases[i] = AssetDatabase.LoadAssetAtPath<PhraseData>(AssetDatabase.GetAssetPath(phrases[i]));
            var slime = LoadEnemy("MetronomeSlime");
            var imp = LoadEnemy("SnareImp");
            var knight = LoadEnemy("TubaKnight");
            var bat = LoadEnemy("PiccoloBat");
            var firstConductor = AssetDatabase.LoadAssetAtPath<BossDefinition>($"{BossDir}/FirstConductor.asset");
            EnsureBossThemePhases(theme2);
            var level2Theme = level2 ? CreateLevel2Theme() : null;
            var catalog = WorldBuilder.LoadCatalog();

            // Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.14f);
            camGo.transform.position = new Vector3(0, 0, -10);
            // Environment pulse replaces the beat ring (GDD).
            var pulse = camGo.AddComponent<BackgroundPulse>();

            // Conductor
            var condGo = new GameObject("Conductor", typeof(AudioSource), typeof(Conductor));
            var conductor = condGo.GetComponent<Conductor>();
            SetRef(conductor, "song", bossFight ? theme2 : level2 ? level2Theme : theme1);

            var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // URP projects: use the unlit sprite material so sprites render without 2D lights.
            var spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

            // Backdrop (pre-rendered arena art), behind everything
            WorldBuilder.Backdrop(bossFight ? "bg_arena_boss" : level2 ? "bg_arena_garden" : "bg_arena_practice", Vector2.zero, spriteMat);

            // Player: pixel-art Cantor, weapon in hand, loadout from the save
            var player = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(PlayerInputReader), typeof(PlayerMotor), typeof(BeatCombatController), typeof(PlayerHealth), typeof(LoadoutApplier));
            var psr = WorldBuilder.DecoratePlayer(player, spriteMat, catalog, out var playerArt);
            var combat = player.GetComponent<BeatCombatController>();
            SetRef(combat, "projectileSprite", ArtGenerator.Load("note_shot"));
            if (spriteMat != null) SetRef(combat, "projectileMaterial", spriteMat);
            var health = player.GetComponent<PlayerHealth>();
            SetRef(health, "sprite", psr);
            SetRef(combat, "conductor", conductor);
            SetList(combat, "equippedPhrases", phrases);

            // Arena: walls around a 15 x 8 floor (the training dummy is retired; TrainingDummy.cs stays for tests).
            var arenaRect = new Rect(-7.5f, -4f, 15f, 8f);
            BuildArenaWalls(arenaRect, square, spriteMat);

            SetRef(pulse, "conductor", conductor);

            // Feedback: hit-stop, key-matched quantized hit sounds, finisher shake.
            var feedbackGo = new GameObject("CombatFeedback", typeof(CombatFeedback));
            var feedback = feedbackGo.GetComponent<CombatFeedback>();
            SetRef(feedback, "combat", combat);
            SetRef(feedback, "conductor", conductor);
            SetRef(feedback, "cameraRig", camGo.transform);

            // Arena director: the waves of the 2-minute fight.
            var arenaGo = new GameObject("ArenaDirector", typeof(ArenaDirector), typeof(PlaystyleTracker));
            var arena = arenaGo.GetComponent<ArenaDirector>();
            SetRef(arena, "conductor", conductor);
            SetRef(arena, "combat", combat);
            SetRef(arena, "playerHealth", health);
            SetRef(arena, "feedback", feedback);
            SetRef(arena, "circleSprite", circle);
            SetRef(arena, "squareSprite", square);
            if (spriteMat != null) SetRef(arena, "spriteMaterial", spriteMat);
            if (bossFight) SetRef(arena, "boss", firstConductor);
            else SetWaves(arena, new (string, EnemyDefinition[])[]
            {
                ("Slimes", new[] { slime, slime }),
                ("Backbeat", new[] { slime, imp }),
                ("Snare Line", new[] { imp, imp }),
                ("Wings", new[] { bat, slime }),
                ("Brass", new[] { knight }),
                ("Ensemble", new[] { knight, imp, bat }),
                ("Full Band", new[] { slime, imp, bat, knight }),
            });
            var tracker = arenaGo.GetComponent<PlaystyleTracker>();
            SetRef(tracker, "conductor", conductor);
            SetRef(tracker, "combat", combat);
            SetRef(tracker, "arena", arena);

            BuildHud(conductor, combat, health, arena, circle, square);

            // Game systems: Groove-driven music layers, pause menu, results/progress, settings
            var game = new GameObject("Game", typeof(MusicMixer), typeof(PauseMenu), typeof(ResultsScreen), typeof(SettingsApplier));
            SetRef(game.GetComponent<MusicMixer>(), "conductor", conductor);
            SetRef(game.GetComponent<MusicMixer>(), "combat", combat);
            SetRef(game.GetComponent<PauseMenu>(), "input", player.GetComponent<PlayerInputReader>());
            var results = game.GetComponent<ResultsScreen>();
            SetRef(results, "arena", arena);
            SetRef(results, "combat", combat);
            var (arenaId, arenaName, reward) = bossFight ? ("boss", "The First Conductor", "")
                                             : level2 ? ("arena2", "Moonlit Garden", "OddTime")
                                             : ("arena1", "Practice Hall", "Backbeat");
            SetString(results, "arenaId", arenaId);
            SetString(results, "arenaName", arenaName);
            SetString(results, "firstClearPhrase", reward);

            if (theme1 == null) Debug.LogError("[Project Hymn] BattleTheme1 asset could not be loaded; assign a Song on the Conductor by hand.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuild(scenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (bossFight) Debug.Log("[Project Hymn] Boss scene built: The First Conductor on Battle Theme 2. Phase 2 (3/4): Waltz (Strike x3) breaks its guard. Phase 3 (7/8): only a Bar Finisher ends it.");
            Debug.Log("[Project Hymn] Prototype scene built. Press Play: J/K/Space/L (or X/Y/A/B) on the beat, WASD to move, C to calibrate. Off-beat actions cost stamina; L parries enemy hits, on-beat Space dodges them.");
        }

        static void BuildHud(Conductor conductor, BeatCombatController combat, PlayerHealth health, ArenaDirector arena, Sprite circle, Sprite square)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HymnHud), typeof(CalibrationPanel));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif

            var ring = NewImage("BeatRing", canvasGo.transform, circle, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(180, 180));
            ring.raycastTarget = false;
            ring.gameObject.SetActive(false); // optional accessibility cue, off by default (GDD)

            var pipRow = new GameObject("MeasurePips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var pipRt = (RectTransform)pipRow.transform;
            pipRt.SetParent(canvasGo.transform, false);
            Anchor(pipRt, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(600, 30));
            var layout = pipRow.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14;
            layout.childControlWidth = layout.childControlHeight = false;

            var sig = NewText("Signature", canvasGo.transform, font, 34, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(800, 50));
            var judge = NewText("Judgement", canvasGo.transform, font, 48, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(800, 70));
            var phrase = NewText("Phrase", canvasGo.transform, font, 40, new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(800, 60));
            var banner = NewText("Banner", canvasGo.transform, font, 56, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1200, 80));
            var groove = NewText("Groove", canvasGo.transform, font, 30, new Vector2(0f, 1f), new Vector2(230, -50), new Vector2(400, 40));
            groove.alignment = TextAnchor.MiddleLeft;
            var calib = NewText("Calibration", canvasGo.transform, font, 24, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1400, 36));
            calib.text = "Press C to calibrate audio latency";

            var grooveBg = NewImage("GrooveBack", canvasGo.transform, square, new Vector2(0f, 1f), new Vector2(230, -90), new Vector2(400, 18));
            grooveBg.color = new Color(1, 1, 1, 0.15f);
            var grooveFill = NewImage("GrooveFill", canvasGo.transform, square, new Vector2(0f, 1f), new Vector2(230, -90), new Vector2(400, 18));
            grooveFill.color = new Color(1f, 0.6f, 0.2f);
            grooveFill.type = Image.Type.Filled;
            grooveFill.fillMethod = Image.FillMethod.Horizontal;
            grooveFill.fillAmount = 0f;

            var staminaLabel = NewText("StaminaLabel", canvasGo.transform, font, 22, new Vector2(0f, 1f), new Vector2(230, -122), new Vector2(400, 28));
            staminaLabel.alignment = TextAnchor.MiddleLeft;
            staminaLabel.text = "STAMINA";
            var staminaBg = NewImage("StaminaBack", canvasGo.transform, square, new Vector2(0f, 1f), new Vector2(230, -150), new Vector2(400, 14));
            staminaBg.color = new Color(1, 1, 1, 0.15f);
            var staminaFill = NewImage("StaminaFill", canvasGo.transform, square, new Vector2(0f, 1f), new Vector2(230, -150), new Vector2(400, 14));
            staminaFill.color = new Color(0.35f, 0.9f, 0.55f);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Horizontal;
            staminaFill.fillAmount = 1f;

            var hpLabel = NewText("HpLabel", canvasGo.transform, font, 22, new Vector2(0f, 1f), new Vector2(230, -178), new Vector2(400, 28));
            hpLabel.alignment = TextAnchor.MiddleLeft;
            hpLabel.text = "HP";
            var hpBg = NewImage("HpBack", canvasGo.transform, square, new Vector2(0f, 1f), new Vector2(230, -206), new Vector2(400, 14));
            hpBg.color = new Color(1, 1, 1, 0.15f);
            var hpFill = NewImage("HpFill", canvasGo.transform, square, new Vector2(0f, 1f), new Vector2(230, -206), new Vector2(400, 14));
            hpFill.color = new Color(1f, 0.35f, 0.4f);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillAmount = 1f;

            // Phrase trail: last four inputs, bottom centre above the measure pips
            var trailRow = new GameObject("PhraseTrail", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(PhraseTrail));
            var trailRt = (RectTransform)trailRow.transform;
            trailRt.SetParent(canvasGo.transform, false);
            Anchor(trailRt, new Vector2(0.5f, 0f), new Vector2(0, 200), new Vector2(360, 56));
            var trailLayout = trailRow.GetComponent<HorizontalLayoutGroup>();
            trailLayout.childAlignment = TextAnchor.MiddleCenter;
            trailLayout.spacing = 18;
            trailLayout.childControlWidth = trailLayout.childControlHeight = false;
            var trailSlots = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                trailSlots[i] = NewText($"Slot{i}", trailRow.transform, font, 46, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 56));
                trailSlots[i].text = "";
            }
            var trail = trailRow.GetComponent<PhraseTrail>();
            SetRef(trail, "combat", combat);
            SetList(trail, "slots", new List<Text>(trailSlots));

            var wave = NewText("Wave", canvasGo.transform, font, 34, new Vector2(0.5f, 1f), new Vector2(0, -45), new Vector2(800, 50));
            wave.text = "GET READY";

            var score = NewText("Score", canvasGo.transform, font, 40, new Vector2(1f, 1f), new Vector2(-230, -50), new Vector2(400, 50));
            score.alignment = TextAnchor.MiddleRight;
            score.text = "0";

            var hud = canvasGo.GetComponent<HymnHud>();
            SetRef(hud, "conductor", conductor);
            SetRef(hud, "combat", combat);
            SetRef(hud, "beatRing", ring.rectTransform);
            SetRef(hud, "beatRingImage", ring);
            SetRef(hud, "pipRow", pipRt);
            SetRef(hud, "signatureText", sig);
            SetRef(hud, "judgementText", judge);
            SetRef(hud, "phraseText", phrase);
            SetRef(hud, "grooveText", groove);
            SetRef(hud, "grooveFill", grooveFill);
            SetRef(hud, "bannerText", banner);
            SetRef(hud, "staminaFill", staminaFill);
            SetRef(hud, "scoreText", score);
            SetRef(hud, "health", health);
            SetRef(hud, "arena", arena);
            SetRef(hud, "hpFill", hpFill);
            SetRef(hud, "waveText", wave);

            var cal = canvasGo.GetComponent<CalibrationPanel>();
            SetRef(cal, "conductor", conductor);
            SetRef(cal, "label", calib);
        }

        static List<PhraseData> CreatePhrases()
        {
            const BeatAction S = BeatAction.Strike, H = BeatAction.Smash, R = BeatAction.Rest, D = BeatAction.Step;
            return new List<PhraseData>
            {
                CreatePhrase("TripletCut", "Triplet Cut", "Three-hit chain, last hit staggers.", 0, 1.6f, "stagger", S, S, H),
                CreatePhrase("Syncopate", "Syncopate", "Delayed heavy that breaks guard.", 0, 1.8f, "guard_break", S, R, H),
                CreatePhrase("Backbeat", "Backbeat", "Two shockwaves, area damage.", 0, 2.0f, "shockwave", R, H, R, H),
                CreatePhrase("OffStep", "Off-step", "Dash-through slash.", 0, 1.4f, "dash_slash", D, S),
                CreatePhrase("Waltz", "Waltz", "Spinning AoE. Only in 3/4.", 3, 2.2f, "spin", S, S, S),
            };
        }

        static void CreateEnemies()
        {
            CreateEnemy("MetronomeSlime", d =>
            {
                d.displayName = "Metronome Slime"; d.teaches = "Downbeat accent and basic timing. Hops on every beat, strikes on beat 1.";
                d.maxHp = 40; d.damage = 8; d.scoreValue = 200;
                d.pattern = new EnemyPattern(1, 0, 0f);
                d.movement = EnemyMovement.Hopper; d.moveSpeed = 2.2f; d.preferredDistance = 0.9f; d.attackRange = 1.3f;
                d.shape = EnemyShape.Circle; d.color = new Color(0.4f, 0.9f, 0.45f); d.size = new Vector2(0.9f, 0.75f);
            });
            CreateEnemy("SnareImp", d =>
            {
                d.displayName = "Snare Imp"; d.teaches = "Parrying the backbeat. Keeps its distance, darts in on beats 2 and 4.";
                d.maxHp = 55; d.damage = 10; d.scoreValue = 300;
                d.pattern = new EnemyPattern(1, 0, 1f, 3f);
                d.movement = EnemyMovement.Skirmisher; d.moveSpeed = 3f; d.preferredDistance = 2.4f; d.attackRange = 1.3f;
                d.shape = EnemyShape.Square; d.color = new Color(1f, 0.6f, 0.2f); d.size = new Vector2(0.7f, 0.9f);
            });
            CreateEnemy("TubaKnight", d =>
            {
                d.displayName = "Tuba Knight"; d.teaches = "Guard-break with Syncopate (Strike, rest, Smash). Heavy hit every other measure.";
                d.maxHp = 160; d.damage = 25; d.heavy = true; d.guarded = true; d.scoreValue = 800;
                d.pattern = new EnemyPattern(2, 1, 0f);
                d.movement = EnemyMovement.Juggernaut; d.moveSpeed = 1.1f; d.preferredDistance = 1.2f; d.attackRange = 1.7f;
                d.shape = EnemyShape.Square; d.color = new Color(0.55f, 0.62f, 0.9f); d.size = new Vector2(1.3f, 1.6f);
            });
            CreateEnemy("PiccoloBat", d =>
            {
                d.displayName = "Piccolo Bat"; d.teaches = "Off-step. Circles out of reach, swoops in for eighth-note pecks.";
                d.maxHp = 30; d.damage = 5; d.scoreValue = 350;
                d.pattern = new EnemyPattern(2, 0, 2f, 2.5f, 3f);
                d.movement = EnemyMovement.Flyer; d.moveSpeed = 3.5f; d.preferredDistance = 3.2f; d.attackRange = 1.1f;
                d.shape = EnemyShape.Circle; d.color = new Color(0.75f, 0.45f, 1f); d.size = new Vector2(0.6f, 0.45f);
            });
        }

        static void CreateBoss()
        {
            string bodyPath = $"{BossDir}/FirstConductorBody.asset";
            var body = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(bodyPath);
            if (body == null)
            {
                body = ScriptableObject.CreateInstance<EnemyDefinition>();
                body.displayName = "The First Conductor";
                body.teaches = "Boss of the vertical slice. Its phases follow Battle Theme 2: 4/4, then 3/4, then 7/8 10% faster.";
                body.maxHp = 600; body.scoreValue = 5000;
                body.movement = EnemyMovement.Juggernaut; body.moveSpeed = 0.9f; body.preferredDistance = 1.8f; body.attackRange = 2.3f;
                body.shape = EnemyShape.Square; body.color = new Color(0.85f, 0.75f, 0.35f); body.size = new Vector2(1.6f, 2.2f);
                AssetDatabase.CreateAsset(body, bodyPath);
            }

            string bossPath = $"{BossDir}/FirstConductor.asset";
            if (AssetDatabase.LoadAssetAtPath<BossDefinition>(bossPath) != null) return; // keep tuned values
            var slime = LoadEnemy("MetronomeSlime");
            var imp = LoadEnemy("SnareImp");
            var bat = LoadEnemy("PiccoloBat");
            var boss = ScriptableObject.CreateInstance<BossDefinition>();
            boss.displayName = "The First Conductor";
            boss.body = body;
            boss.phases = new List<BossPhase>
            {
                new BossPhase
                {
                    name = "Overture", enterAtHpFraction = 1f,
                    pattern = new EnemyPattern(1, 0, 0f, 2f), damage = 15f, attackRange = 2.3f,
                    color = new Color(0.85f, 0.75f, 0.35f), summons = new[] { slime, slime },
                    hint = "4/4: baton strikes on beats 1 and 3. Parry them with Guard.",
                },
                new BossPhase
                {
                    name = "Waltz", enterAtHpFraction = 0.66f,
                    pattern = new EnemyPattern(1, 0, 0f), damage = 22f, attackRange = 2.6f, heavy = true,
                    guarded = true, guardBreakEffect = "spin",
                    color = new Color(0.7f, 0.45f, 0.85f), summons = new[] { imp },
                    hint = "3/4: guarded. Waltz (Strike, Strike, Strike) breaks the guard.",
                },
                new BossPhase
                {
                    name = "Finale", enterAtHpFraction = 0.33f,
                    pattern = new EnemyPattern(1, 0, 0f, 2f, 4f), damage = 12f, attackRange = 2.3f,
                    finisherOnly = true,
                    color = new Color(0.95f, 0.35f, 0.3f), summons = new[] { bat },
                    hint = "7/8, faster: hits on 1, 3 and 5. Fill a whole bar on the beat to finish it.",
                },
            };
            AssetDatabase.CreateAsset(boss, bossPath);
        }

        /// <summary>Battle Theme 2's sections become the three boss phases (placeholder map: 4/4, 3/4, 7/8).</summary>
        static void EnsureBossThemePhases(SongData theme2)
        {
            if (theme2 == null || theme2.IsAdaptive) return;
            if (theme2.sections == null || theme2.sections.Count < 3)
            {
                Debug.LogWarning("[Project Hymn] BattleTheme2 needs at least 3 sections (one per boss phase); phases not set.");
                return;
            }
            theme2.phases = new List<SongPhase>
            {
                new SongPhase("Overture", 0, 1),
                new SongPhase("Waltz", 1, 1),
                new SongPhase("Finale", 2, 1),
            };
            EditorUtility.SetDirty(theme2);
            AssetDatabase.SaveAssets();
        }

        static void CreateEnemy(string file, System.Action<EnemyDefinition> setup)
        {
            string path = $"{EnemyDir}/{file}.asset";
            if (AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path) != null) return; // keep tuned values
            var d = ScriptableObject.CreateInstance<EnemyDefinition>();
            setup(d);
            AssetDatabase.CreateAsset(d, path);
        }

        internal static EnemyDefinition LoadEnemy(string file) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EnemyDir}/{file}.asset");

        static void SetWaves(ArenaDirector arena, (string name, EnemyDefinition[] enemies)[] waves)
        {
            var so = new SerializedObject(arena);
            var list = so.FindProperty("waves");
            list.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                var w = list.GetArrayElementAtIndex(i);
                w.FindPropertyRelative("name").stringValue = waves[i].name;
                var arr = w.FindPropertyRelative("enemies");
                arr.arraySize = waves[i].enemies.Length;
                for (int j = 0; j < waves[i].enemies.Length; j++)
                    arr.GetArrayElementAtIndex(j).objectReferenceValue = waves[i].enemies[j];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BuildArenaWalls(Rect r, Sprite square, Material mat)
        {
            var root = new GameObject("Arena");
            const float t = 0.3f;
            MakeWall(root.transform, "WallTop", new Vector2(r.center.x, r.yMax + t / 2), new Vector2(r.width + 2 * t, t), square, mat);
            MakeWall(root.transform, "WallBottom", new Vector2(r.center.x, r.yMin - t / 2), new Vector2(r.width + 2 * t, t), square, mat);
            MakeWall(root.transform, "WallLeft", new Vector2(r.xMin - t / 2, r.center.y), new Vector2(t, r.height), square, mat);
            MakeWall(root.transform, "WallRight", new Vector2(r.xMax + t / 2, r.center.y), new Vector2(t, r.height), square, mat);
        }

        internal static void MakeWall(Transform parent, string name, Vector2 pos, Vector2 size, Sprite square, Material mat)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.35f, 0.3f, 0.5f);
            if (mat != null) sr.sharedMaterial = mat;
            FitToSize(go.transform, square, size);
            go.GetComponent<BoxCollider2D>().size = square.bounds.size;
        }

        static PhraseData CreatePhrase(string file, string display, string desc, int meter, float mult, string effect, params BeatAction[] pattern)
        {
            string path = $"{PhraseDir}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PhraseData>(path);
            if (existing != null) return existing;
            var p = ScriptableObject.CreateInstance<PhraseData>();
            p.displayName = display;
            p.description = desc;
            p.requiredBeatsPerMeasure = meter;
            p.damageMultiplier = mult;
            p.effectId = effect;
            p.pattern = new List<BeatAction>(pattern);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        /// <summary>
        /// Measured from the track: 150 BPM, 4/4, first downbeat 0.03 s, C minor, 96 bars in 8-bar
        /// phrases (quiet 8-bar intro, 80-bar body, 8-bar outro). The arena loops the body.
        /// </summary>
        static SongData CreateLevel2Theme()
        {
            string path = $"{SongDir}/Level2Theme.asset";
            var s = AssetDatabase.LoadAssetAtPath<SongData>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<SongData>();
                s.referenceUrl = "https://ocremix.org/remix/OCR02936";
                s.firstBeatOffset = 0.03;
                s.loop = false;
                s.keyRoot = 0;       // C
                s.minorKey = true;
                s.sections = new List<TempoSection>
                {
                    new TempoSection(8, 150f, 4),   // intro (quiet)
                    new TempoSection(80, 150f, 4),  // body
                    new TempoSection(8, 150f, 4),   // outro
                };
                s.phases = new List<SongPhase> { new SongPhase("Fight", 1, 1) }; // loop bars 9-88
                AssetDatabase.CreateAsset(s, path);
            }
            if (s.clip == null)
            {
                s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Level2Clip);
                if (s.clip == null) Debug.LogWarning($"[Project Hymn] Level 2 track not found at {Level2Clip}; the arena will run on the metronome.");
                EditorUtility.SetDirty(s);
            }
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<SongData>(path);
        }

        internal static SongData CreateSong(string file, string url, params TempoSection[] sections)
        {
            string path = $"{SongDir}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SongData>(path);
            if (existing != null) return existing;
            var s = ScriptableObject.CreateInstance<SongData>();
            s.referenceUrl = url;
            s.loop = true;
            s.sections = new List<TempoSection>(sections);
            if (file == "BattleTheme1")
            {
                // Measured from the OC ReMix track: 130 BPM, 4/4, first downbeat 0.244 s, key E minor.
                s.firstBeatOffset = 0.244;
                s.loop = false;
                s.keyRoot = 4;
                s.minorKey = true;
                s.referenceUrl = "https://ocremix.org/remix/OCR04994";
            }
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        /// <summary>Scales a transform so its sprite covers the given world size.</summary>
        internal static void FitToSize(Transform t, Sprite sprite, Vector2 worldSize)
        {
            var b = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            t.localScale = new Vector3(worldSize.x / Mathf.Max(0.001f, b.x), worldSize.y / Mathf.Max(0.001f, b.y), 1f);
        }

        internal static Image NewImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, anchor, pos, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            return img;
        }

        internal static Text NewText(string name, Transform parent, Font font, int size, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, anchor, pos, box);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        internal static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"Field '{field}' not found on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"Field '{field}' not found on {target.GetType().Name}"); return; }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetList<T>(Object target, string field, List<T> values) where T : Object
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
