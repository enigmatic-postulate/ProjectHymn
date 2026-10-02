using System.Collections.Generic;
using System.IO;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
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
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Prototype.unity";

        [MenuItem("Project Hymn/Build Prototype Scene", priority = 0)]
        public static void BuildPrototypeScene()
        {
            foreach (var dir in new[] { PhraseDir, SongDir, SceneDir, Root + "/Audio/Music", Root + "/Art", Root + "/Prefabs" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var phrases = CreatePhrases();
            var theme1 = CreateSong("BattleTheme1", "https://www.youtube.com/watch?v=Z3l0ehUm4lM",
                new TempoSection(112, 130f, 4));
            var theme2 = CreateSong("BattleTheme2", "https://www.youtube.com/watch?v=fGCFGbvfNCQ",
                new TempoSection(4, 150f, 4),
                new TempoSection(4, 150f, 3),
                new TempoSection(4, 165f, 7, 8));
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Re-load the data assets after the scene switch: references held across NewScene can
            // come back empty on newer Unity versions, which leaves the scene with no song or phrases.
            AssetDatabase.SaveAssets();
            theme1 = AssetDatabase.LoadAssetAtPath<SongData>($"{SongDir}/BattleTheme1.asset");
            theme2 = AssetDatabase.LoadAssetAtPath<SongData>($"{SongDir}/BattleTheme2.asset");
            for (int i = 0; i < phrases.Count; i++)
                phrases[i] = AssetDatabase.LoadAssetAtPath<PhraseData>(AssetDatabase.GetAssetPath(phrases[i]));

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
            SetRef(conductor, "song", theme1);

            var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // URP projects: use the unlit sprite material so sprites render without 2D lights.
            var spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

            // Player
            var player = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(PlayerInputReader), typeof(PlayerMotor), typeof(BeatCombatController));
            var playerArt = new GameObject("Sprite", typeof(SpriteRenderer), typeof(BeatPulse));
            playerArt.transform.SetParent(player.transform, false);
            var psr = playerArt.GetComponent<SpriteRenderer>();
            psr.sprite = circle;
            psr.color = new Color(0.35f, 0.75f, 1f);
            if (spriteMat != null) psr.sharedMaterial = spriteMat;
            FitToSize(playerArt.transform, psr.sprite, new Vector2(1f, 1f));
            var combat = player.GetComponent<BeatCombatController>();
            SetRef(combat, "conductor", conductor);
            SetList(combat, "equippedPhrases", phrases);

            // Training dummy
            var dummy = new GameObject("TrainingDummy", typeof(BoxCollider2D), typeof(TrainingDummy));
            dummy.transform.position = new Vector3(2.2f, 0, 0);
            var dsr = dummy.AddComponent<SpriteRenderer>();
            dsr.sprite = square;
            dsr.color = new Color(0.9f, 0.4f, 0.5f);
            if (spriteMat != null) dsr.sharedMaterial = spriteMat;
            FitToSize(dummy.transform, dsr.sprite, new Vector2(1.2f, 1.6f));
            dummy.GetComponent<BoxCollider2D>().size = dsr.sprite.bounds.size;

            SetRef(pulse, "conductor", conductor);

            // Feedback: hit-stop, key-matched quantized hit sounds, finisher shake.
            var feedbackGo = new GameObject("CombatFeedback", typeof(CombatFeedback));
            var feedback = feedbackGo.GetComponent<CombatFeedback>();
            SetRef(feedback, "combat", combat);
            SetRef(feedback, "conductor", conductor);
            SetRef(feedback, "cameraRig", camGo.transform);

            BuildHud(conductor, combat, circle, square);

            if (theme1 == null) Debug.LogError("[Project Hymn] BattleTheme1 asset could not be loaded; assign a Song on the Conductor by hand.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[Project Hymn] Prototype scene built. Press Play: J/K/Space/L (or X/Y/A/B) on the beat, WASD to move, C to calibrate. Off-beat actions cost stamina.");
        }

        static void BuildHud(Conductor conductor, BeatCombatController combat, Sprite circle, Sprite square)
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

        static SongData CreateSong(string file, string url, params TempoSection[] sections)
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
        static void FitToSize(Transform t, Sprite sprite, Vector2 worldSize)
        {
            var b = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            t.localScale = new Vector3(worldSize.x / Mathf.Max(0.001f, b.x), worldSize.y / Mathf.Max(0.001f, b.y), 1f);
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, anchor, pos, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            return img;
        }

        static Text NewText(string name, Transform parent, Font font, int size, Vector2 anchor, Vector2 pos, Vector2 box)
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

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"Field '{field}' not found on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetList<T>(Object target, string field, List<T> values) where T : Object
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
