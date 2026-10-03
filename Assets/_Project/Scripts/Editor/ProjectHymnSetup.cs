using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// One-click setup for the M2 sword prototype: generates the skinned swordsman, swords, dummy and the editable
    /// move / sequence / weapon assets, then builds Scenes/Prototype.unity: a lit 3D training yard, the player,
    /// a straw dummy, side-on follow camera, Conductor, effects, sounds and HUD.
    /// Menu: Project Hymn ▸ Build Prototype Scene. Safe to re-run; tuned assets are kept.
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
            foreach (var dir in new[] { SongDir, SceneDir, Root + "/Audio/Music", Root + "/Art" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            CreateSong("BattleTheme1", "https://www.youtube.com/watch?v=Z3l0ehUm4lM", new TempoSection(112, 130f, 4));
            CreateSong("BattleTheme2", "https://www.youtube.com/watch?v=fGCFGbvfNCQ",
                new TempoSection(4, 150f, 4), new TempoSection(4, 150f, 3), new TempoSection(4, 165f, 7, 8));
            Sword.SwordContent.Ensure();
            Sword.SwordsmanBuilder.BuildSwordsman();
            var additive = Sword.SwordsmanBuilder.Additive();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Re-load assets after the scene switch (references held across NewScene can come back empty).
            var theme1 = AssetDatabase.LoadAssetAtPath<SongData>($"{SongDir}/BattleTheme1.asset");
            var weapons = new List<WeaponSet>
            {
                AssetDatabase.LoadAssetAtPath<WeaponSet>($"{Root}/Data/Sword/Saber.asset"),
                AssetDatabase.LoadAssetAtPath<WeaponSet>($"{Root}/Data/Sword/Broadsword.asset"),
            };
            var swordsman = AssetDatabase.LoadAssetAtPath<GameObject>($"{Sword.SwordsmanBuilder.Dir}/Swordsman.prefab");
            additive = AssetDatabase.LoadAssetAtPath<Material>($"{Sword.SwordsmanBuilder.Dir}/Materials/FX_Additive.mat");

            // Light and yard
            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var l = sun.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.2f; l.color = new Color(1f, 0.95f, 0.86f); l.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.34f, 0.46f);
            RenderSettings.skybox = null;
            BuildYard();

            // Conductor
            var condGo = new GameObject("Conductor", typeof(AudioSource), typeof(Conductor));
            var conductor = condGo.GetComponent<Conductor>();
            SetRef(conductor, "song", theme1);

            // Player: skinned swordsman under a physics root
            var player = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerInputReader),
                typeof(PlayerMotor), typeof(RhythmAnimator), typeof(SwordCombatController));
            var cap = player.GetComponent<CapsuleCollider>();
            cap.radius = 0.3f; cap.height = 1.75f; cap.center = new Vector3(0f, 0.88f, 0f);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(swordsman, visual);
            var model = body.transform.Find("Model");
            var smr = body.GetComponentInChildren<SkinnedMeshRenderer>();
            var grip = body.GetComponentsInChildren<Transform>(true).First(t => t.name == "Grip");
            SetRef(player.GetComponent<PlayerMotor>(), "visual", visual);
            var anim = player.GetComponent<RhythmAnimator>();
            SetRef(anim, "conductor", conductor);
            SetRef(anim, "modelRoot", model);
            SetRef(anim, "grip", grip);
            SetRef(anim, "weapon", weapons[0]);
            SetArray(anim, "bones", smr.bones);
            var combat = player.GetComponent<SwordCombatController>();
            SetRef(combat, "conductor", conductor);
            SetRef(combat, "animator", anim);
            SetList(combat, "weapons", weapons);

            // Dummy, facing the player
            var dummy = Sword.SwordsmanBuilder.BuildDummy();
            dummy.transform.position = new Vector3(0f, 0f, 1.75f);
            dummy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Camera: side-on, following the player
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FollowCamera), typeof(BackgroundPulse));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.16f);
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(4.3f, 2.0f, 0.3f);
            camGo.transform.LookAt(new Vector3(0f, 1.05f, 0.8f));
            SetRef(camGo.GetComponent<FollowCamera>(), "target", player.transform);
            SetRef(camGo.GetComponent<BackgroundPulse>(), "conductor", conductor);

            // Effects, sounds, popups, tempo tester
            var fxGo = new GameObject("Sword FX", typeof(SwordFx), typeof(CombatFeedback), typeof(DamagePopups), typeof(TempoTester));
            SetRef(fxGo.GetComponent<SwordFx>(), "combat", combat);
            SetRef(fxGo.GetComponent<SwordFx>(), "additive", additive);
            SetRef(fxGo.GetComponent<SwordFx>(), "player", player.transform);
            SetRef(fxGo.GetComponent<SwordFx>(), "cameraRig", camGo.GetComponent<FollowCamera>());
            SetRef(fxGo.GetComponent<CombatFeedback>(), "combat", combat);
            SetRef(fxGo.GetComponent<CombatFeedback>(), "conductor", conductor);
            SetRef(fxGo.GetComponent<CombatFeedback>(), "cameraRig", camGo.GetComponent<FollowCamera>());
            SetRef(fxGo.GetComponent<DamagePopups>(), "font", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            SetRef(fxGo.GetComponent<TempoTester>(), "conductor", conductor);
            SetRef(fxGo.GetComponent<TempoTester>(), "input", player.GetComponent<PlayerInputReader>());

            BuildHud(conductor, combat);

            if (theme1 == null) Debug.LogError("[Project Hymn] BattleTheme1 asset could not be loaded; assign a Song on the Conductor by hand.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[Project Hymn] M2 sword prototype built. Press Play: J = note 1, K = note 2 on the beat (pad X / Y). Try 1-2-1, 2-2 and 1-1-1-2-1-1-1. " +
                      "WASD move, Space dash, Tab saber/broadsword, [ ] animation offset, F1-F4 tempos, C calibrate.");
        }

        /// <summary>A small stone yard: floor tiles, a low wall and a few posts for depth.</summary>
        static void BuildYard()
        {
            var root = new GameObject("Yard").transform;
            for (int x = -5; x <= 5; x++)
            for (int z = -4; z <= 6; z++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(t.GetComponent<Collider>());
                t.name = "Tile";
                t.transform.SetParent(root, false);
                t.transform.localPosition = new Vector3(x, -0.05f, z);
                t.transform.localScale = new Vector3(0.98f, 0.1f, 0.98f);
                t.GetComponent<MeshRenderer>().sharedMaterial = Sword.SwordsmanBuilder.Lit(((x + z) & 1) == 0 ? 0x4a4458u : 0x544e64u, 0.15f);
                t.isStatic = true;
            }
            foreach (var (pos, scale) in new[] { (new Vector3(-6f, 0.5f, 1f), new Vector3(0.6f, 1f, 11f)), (new Vector3(0f, 0.5f, 7f), new Vector3(12.6f, 1f, 0.6f)) })
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = "Wall";
                w.transform.SetParent(root, false);
                w.transform.localPosition = pos;
                w.transform.localScale = scale;
                w.GetComponent<MeshRenderer>().sharedMaterial = Sword.SwordsmanBuilder.Lit(0x6a5a50);
            }
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                p.name = "Post";
                p.transform.SetParent(root, false);
                p.transform.localPosition = new Vector3(-4.5f + i * 3f, 1.2f, 6.2f);
                p.transform.localScale = new Vector3(0.35f, 1.2f, 0.35f);
                p.GetComponent<MeshRenderer>().sharedMaterial = Sword.SwordsmanBuilder.Lit(0x7a6a5a);
            }
        }

        static void BuildHud(Conductor conductor, SwordCombatController combat)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SwordHud), typeof(CalibrationPanel));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
            var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var t = canvasGo.transform;
            var beatTrack = NewText("BeatTrack", t, font, 44, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1100, 80));
            var chain = NewText("Chain", t, font, 30, new Vector2(0.5f, 0f), new Vector2(0, 95), new Vector2(1200, 40));
            var judge = NewText("Judgement", t, font, 46, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(900, 64));
            var banner = NewText("Banner", t, font, 56, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1600, 80));
            var seq = NewText("Sequences", t, font, 30, new Vector2(0f, 0.5f), new Vector2(330, 120), new Vector2(620, 200));
            seq.alignment = TextAnchor.UpperLeft;
            var info = NewText("Info", t, font, 22, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1800, 32));
            info.color = new Color(0.75f, 0.72f, 0.85f);
            var score = NewText("Score", t, font, 40, new Vector2(1f, 1f), new Vector2(-230, -50), new Vector2(400, 50));
            score.alignment = TextAnchor.MiddleRight;
            var groove = NewText("Groove", t, font, 28, new Vector2(0f, 1f), new Vector2(230, -50), new Vector2(400, 40));
            groove.alignment = TextAnchor.MiddleLeft;
            var grooveBg = NewImage("GrooveBack", t, square, new Vector2(0f, 1f), new Vector2(230, -88), new Vector2(400, 16));
            grooveBg.color = new Color(1, 1, 1, 0.15f);
            var grooveFill = NewImage("GrooveFill", t, square, new Vector2(0f, 1f), new Vector2(230, -88), new Vector2(400, 16));
            grooveFill.color = new Color(1f, 0.6f, 0.2f);
            grooveFill.type = Image.Type.Filled;
            grooveFill.fillMethod = Image.FillMethod.Horizontal;
            grooveFill.fillAmount = 0f;
            var calib = NewText("Calibration", t, font, 22, new Vector2(1f, 0f), new Vector2(-260, 70), new Vector2(480, 30));
            calib.text = "Press C to calibrate audio latency";
            calib.color = new Color(0.75f, 0.72f, 0.85f);

            var hud = canvasGo.GetComponent<SwordHud>();
            SetRef(hud, "conductor", conductor);
            SetRef(hud, "combat", combat);
            SetRef(hud, "judgementText", judge);
            SetRef(hud, "chainText", chain);
            SetRef(hud, "bannerText", banner);
            SetRef(hud, "sequenceText", seq);
            SetRef(hud, "beatTrackText", beatTrack);
            SetRef(hud, "infoText", info);
            SetRef(hud, "scoreText", score);
            SetRef(hud, "grooveText", groove);
            SetRef(hud, "grooveFill", grooveFill);
            var cal = canvasGo.GetComponent<CalibrationPanel>();
            SetRef(cal, "conductor", conductor);
            SetRef(cal, "label", calib);
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

        internal static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"Field '{field}' not found on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray<T>(Object target, string field, T[] values) where T : Object => SetList(target, field, new List<T>(values));

        internal static void SetList<T>(Object target, string field, List<T> values) where T : Object
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
