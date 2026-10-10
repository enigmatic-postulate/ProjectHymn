using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static ProjectHymn.EditorTools.ProjectHymnSetup;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// Art pass, characters: turns the KayKit packs (CC0, Kay Lousberg) into the Cliffside cast.
    /// The hero is the Knight with a sword and shield or a greatsword; every move is an authored KayKit clip whose
    /// impact frame (measured from the hand's peak speed) is placed on the beat by the RhythmAnimator. The shield-knight
    /// enemy is the Skeleton Warrior with blade and large shield, driven the same way.
    /// </summary>
    public static class KayKitBuilder
    {
        const string DataDir = "Assets/_Project/Data/KayKit";
        const double ReferenceSpb = 60.0 / 130.0;

        /// <summary>Impact frames (normalized) measured offline from each clip: the weapon hand's peak speed.</summary>
        static readonly Dictionary<string, float> Hit = new Dictionary<string, float>
        {
            ["Melee_1H_Attack_Chop"] = 0.54f, ["Melee_1H_Attack_Jump_Chop"] = 0.547f, ["Melee_1H_Attack_Slice_Diagonal"] = 0.438f,
            ["Melee_1H_Attack_Slice_Horizontal"] = 0.203f, ["Melee_1H_Attack_Stab"] = 0.25f,
            ["Melee_2H_Attack_Chop"] = 0.46f, ["Melee_2H_Attack_Slice"] = 0.365f, ["Melee_2H_Attack_Spin"] = 0.482f,
            ["Melee_2H_Attack_Spinning"] = 0.719f, ["Melee_2H_Attack_Stab"] = 0.25f,
            ["Melee_Unarmed_Attack_Kick"] = 0.341f, ["Melee_Unarmed_Attack_Punch_A"] = 0.375f,
            ["Melee_Block"] = 0.14f, ["Melee_Block_Attack"] = 0.34f, ["Hit_A"] = 0.1f,
        };

        static readonly string[] UpperBody = { "spine", "chest", "head", "upperarm.l", "lowerarm.l", "wrist.l", "hand.l", "handslot.l", "upperarm.r", "lowerarm.r", "wrist.r", "hand.r", "handslot.r" };

        // ---------------- Content ----------------

        sealed class M
        {
            public string Id, Name, Clip, Effect = "cut";
            public float Damage = 10f, Reach = 1.25f, Radius = 0.9f;
            public uint Trail = 0xffe08a;
            public M(string id, string name, string clip, float damage, uint trail, string effect = "cut", float reach = 1.25f, float radius = 0.9f)
            { Id = id; Name = name; Clip = clip; Damage = damage; Trail = trail; Effect = effect; Reach = reach; Radius = radius; }
        }

        public static List<WeaponSet> EnsureWeapons()
        {
            Directory.CreateDirectory(DataDir + "/Moves");
            Directory.CreateDirectory(DataDir + "/Sequences");
            var sword = Weapon("SwordAndShield", "Sword & Shield", "Sword", "Shield", "Idle_A", 1f,
                new[]
                {
                    new M("Diagonal", "Diagonal Cut", "Melee_1H_Attack_Slice_Diagonal", 10f, 0xffe08a),
                    new M("Horizontal", "Horizontal Slash", "Melee_1H_Attack_Slice_Horizontal", 12f, 0x9ce8ff, radius: 1.05f),
                    new M("Chop", "Overhead Chop", "Melee_1H_Attack_Chop", 14f, 0xffc070, "power"),
                    new M("ShieldBash", "Shield Bash", "Melee_Block_Attack", 8f, 0xd0a0ff, "punch", 1.0f, 0.8f),
                    new M("LeapChop", "Leaping Power Slice", "Melee_1H_Attack_Jump_Chop", 22f, 0xff7a5a, "power", 1.45f, 1.0f),
                    new M("Kick", "Kick", "Melee_Unarmed_Attack_Kick", 7f, 0xffffff, "punch", 0.95f, 0.75f),
                    new M("Stab", "Finishing Thrust", "Melee_1H_Attack_Stab", 14f, 0x7affc8, "finish", 1.5f, 1.1f),
                    new M("Guard", "Guard", "Melee_Block", 0f, 0xffffff, "guard"),
                },
                light: ("Diagonal", "Horizontal"), heavy: "Chop", guard: "Guard",
                crossing: new[] { "Diagonal", "Chop", "Horizontal" }, powerslice: new[] { "ShieldBash", "LeapChop" },
                tempest: new[] { "Diagonal", "Horizontal", "Diagonal", "Kick", "Horizontal", "Diagonal", "Stab" });
            // Heft 1.0 on the moves; the weapon's damage scale (1.3) gives the greatsword its weight (weapons are
            // separate from classes since Rank & Class).
            var great = Weapon("Greatsword", "Greatsword", "Greatsword", "", "Melee_2H_Idle", 1f,
                new[]
                {
                    new M("Slice", "Great Slice", "Melee_2H_Attack_Slice", 12f, 0xffe08a, radius: 1.1f),
                    new M("Stab", "Great Thrust", "Melee_2H_Attack_Stab", 12f, 0x9ce8ff, reach: 1.5f),
                    new M("Chop", "Great Chop", "Melee_2H_Attack_Chop", 18f, 0xff7a5a, "power", 1.45f, 1.0f),
                    new M("Whirl", "Whirl", "Melee_2H_Attack_Spinning", 10f, 0xd0a0ff, radius: 1.3f),
                    new M("Kick", "Kick", "Melee_Unarmed_Attack_Kick", 7f, 0xffffff, "punch", 0.95f, 0.75f),
                    new M("Spin", "Tempest Spin", "Melee_2H_Attack_Spin", 16f, 0x7affc8, "finish", 1.4f, 1.5f),
                    new M("Guard", "Guard", "Melee_Block", 0f, 0xffffff, "guard"),
                },
                light: ("Slice", "Stab"), heavy: "Chop", guard: "Guard",
                crossing: new[] { "Slice", "Chop", "Stab" }, powerslice: new[] { "Whirl", "Chop" },
                tempest: new[] { "Slice", "Stab", "Slice", "Kick", "Stab", "Slice", "Spin" });
            sword.id = "sword";
            great.id = "greatsword";
            great.damageScale = 1.3f;
            EditorUtility.SetDirty(sword);
            EditorUtility.SetDirty(great);
            AssetDatabase.SaveAssets();
            return new List<WeaponSet> { sword, great };
        }

        static WeaponSet Weapon(string id, string name, string swordObject, string offHand, string idleClip, float heft, M[] moves,
                                (string, string) light, string heavy, string guard, string[] crossing, string[] powerslice, string[] tempest)
        {
            var built = new Dictionary<string, SwordMove>();
            foreach (var m in moves) built[m.Id] = Move(id, m, heft);
            string wpath = $"{DataDir}/{id}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponSet>(wpath);
            bool fresh = w == null;
            if (fresh) w = ScriptableObject.CreateInstance<WeaponSet>();
            w.displayName = name;
            w.swordObject = swordObject;
            w.offHandObject = offHand;
            w.twoHanded = false;                 // the KayKit two-handed clips already hold the hilt with both hands
            w.stance = new MoveKey();
            w.idleClip = ArtLibrary.Clip(idleClip);
            w.lightA = built[light.Item1];
            w.lightB = built[light.Item2];
            w.heavy = built[heavy];
            w.guard = built[guard];
            w.damageScale = 1f;
            w.sequences = new List<SwordSequence>
            {
                Sequence(id, "CrossingCuts", "Crossing Cuts", new[] { 1, 2, 1 }, crossing, built, 1.8f, 0xffd27a),
                Sequence(id, "Powerslice", "Powerslice", new[] { 2, 2 }, powerslice, built, 1.6f, 0xff7a5a),
                Sequence(id, "Tempest", "Tempest", new[] { 1, 1, 1, 2, 1, 1, 1 }, tempest, built, 2.5f, 0x7affc8),
            };
            if (fresh) AssetDatabase.CreateAsset(w, wpath); else EditorUtility.SetDirty(w);
            return w;
        }

        static SwordMove Move(string weaponId, M m, float heft)
        {
            string path = $"{DataDir}/Moves/{weaponId}_{m.Id}.asset";
            var a = AssetDatabase.LoadAssetAtPath<SwordMove>(path);
            bool fresh = a == null;
            if (fresh) a = ScriptableObject.CreateInstance<SwordMove>();
            a.displayName = m.Name;
            a.clip = ArtLibrary.Clip(m.Clip);
            a.clipHit = Hit.TryGetValue(m.Clip, out var h) ? h : 0.45f;
            a.clipStart = 0f;
            a.clipEnd = 1f;
            float pre = 0.5f, post = 0.6f;
            if (a.clip != null) ClipTiming.Span(a.clip.length, a.clipStart, a.clipHit, a.clipEnd, ReferenceSpb, 0.3f, 0.75f, 0.4f, 1.1f, out pre, out post);
            if (m.Effect == "guard") { pre = 0.1f; post = 0.8f; }
            a.keys = new List<MoveKey> { new MoveKey { beat = -pre }, new MoveKey { beat = 0f }, new MoveKey { beat = post } };
            a.activeStart = m.Effect == "guard" ? 1f : -Mathf.Min(0.12f, pre * 0.4f);
            a.activeEnd = m.Effect == "guard" ? 0f : 0.14f;
            a.damage = m.Damage * heft;
            a.reach = m.Reach;
            a.radius = m.Radius;
            a.effect = m.Effect;
            a.trailColor = new Color(((m.Trail >> 16) & 255) / 255f, ((m.Trail >> 8) & 255) / 255f, (m.Trail & 255) / 255f);
            a.offHandFree = false;
            if (fresh) AssetDatabase.CreateAsset(a, path); else EditorUtility.SetDirty(a);
            return a;
        }

        static SwordSequence Sequence(string weaponId, string id, string name, int[] notes, string[] moves, Dictionary<string, SwordMove> built, float mult, uint color)
        {
            string path = $"{DataDir}/Sequences/{weaponId}_{id}.asset";
            var s = AssetDatabase.LoadAssetAtPath<SwordSequence>(path);
            bool fresh = s == null;
            if (fresh) s = ScriptableObject.CreateInstance<SwordSequence>();
            s.displayName = name;
            s.notes = notes.Select(n => (NoteButton)n).ToList();
            s.moves = moves.Select(m => built[m]).ToList();
            s.finisherMultiplier = mult;
            s.color = new Color(((color >> 16) & 255) / 255f, ((color >> 8) & 255) / 255f, (color & 255) / 255f);
            if (fresh) AssetDatabase.CreateAsset(s, path); else EditorUtility.SetDirty(s);
            return s;
        }

        public static LocomotionSet EnsureLocomotion()
        {
            string path = $"{DataDir}/Locomotion.asset";
            var a = AssetDatabase.LoadAssetAtPath<LocomotionSet>(path);
            bool fresh = a == null;
            if (fresh) a = ScriptableObject.CreateInstance<LocomotionSet>();
            a.run = new List<MoveKey>();
            a.runClip = ArtLibrary.Clip("Running_A");
            a.runRightFootDown = 0.684f;
            a.jumpClip = ArtLibrary.Clip("Jump_Start");
            a.airClip = ArtLibrary.Clip("Jump_Idle");
            a.landClip = ArtLibrary.Clip("Jump_Land");
            a.hangClip = ArtLibrary.Clip("Jump_Idle");
            a.hangClipTime = 0.5f;
            a.minStride = 0.4f;
            a.dodgeClip = ArtLibrary.Clip("Dodge_Forward");
            a.hurt = Move("Hero", new M("Hurt", "Hurt", "Hit_A", 0f, 0xffffff, "guard"), 1f);
            a.hurt.keys = new List<MoveKey> { new MoveKey { beat = -0.05f }, new MoveKey { beat = 0f }, new MoveKey { beat = 0.9f } };
            a.hurt.activeStart = 1f; a.hurt.activeEnd = 0f;
            EditorUtility.SetDirty(a.hurt);
            if (fresh) AssetDatabase.CreateAsset(a, path); else EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return a;
        }

        // ---------------- Characters ----------------

        static Material Mat(string name, string texture) =>
            ArtLibrary.Textured(name, $"{ArtLibrary.KayKit}/Characters/{texture}", null, Color.white, 0.2f);

        /// <summary>
        /// Put a KayKit character under <paramref name="parent"/>: a "Fit" transform turns it to face +Z, scales it to
        /// <paramref name="height"/> and stands it on y = 0. Returns the Fit transform and the rig's root bone.
        /// </summary>
        static (Transform fit, Transform rigRoot, GameObject body) Place(string fbx, Transform parent, float height, Material mat)
        {
            var fit = new GameObject("Fit").transform;
            fit.SetParent(parent, false);
            var body = ArtLibrary.Instance(fbx, fit);
            ArtLibrary.Paint(body, mat);
            var rigRoot = ArtLibrary.FindDeep(body.transform, "root");
            // Face +Z: the toes point forward.
            var foot = ArtLibrary.FindDeep(body.transform, "foot.l");
            var toes = ArtLibrary.FindDeep(body.transform, "toes.l");
            if (foot != null && toes != null)
            {
                var fwd = toes.position - foot.position; fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-6f) fit.localRotation = Quaternion.FromToRotation(fwd.normalized, Vector3.forward);
            }
            var b = ArtLibrary.RendererBounds(body.transform);
            float h = Mathf.Max(0.01f, b.size.y);
            fit.localScale = Vector3.one * (height / h);
            b = ArtLibrary.RendererBounds(body.transform);
            fit.localPosition += Vector3.up * (parent.position.y - b.min.y);
            return (fit, rigRoot, body);
        }

        static GameObject Attach(string fbx, Transform slot, string name, Material mat)
        {
            var go = ArtLibrary.Instance(fbx, slot, name);
            if (go == null) return null;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            ArtLibrary.Paint(go, mat);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        /// <summary>A trail at the blade's far end: the mesh's longest axis, on the side away from the grip.</summary>
        static void AddTip(GameObject weapon, Material additive, float width)
        {
            var mf = weapon.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var bounds = mf.sharedMesh.bounds;
            int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
            var min = bounds.min; var max = bounds.max;
            Vector3 local = bounds.center;
            local[axis] = Mathf.Abs(max[axis]) >= Mathf.Abs(min[axis]) ? max[axis] : min[axis];
            var world = mf.transform.TransformPoint(local);
            var tip = new GameObject("Tip", typeof(TrailRenderer)).transform;
            tip.SetParent(weapon.transform, false);
            tip.position = world;
            var trail = tip.GetComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.02f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
            trail.sharedMaterial = additive;
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        /// <summary>Weapon models by the object name a <see cref="WeaponSet"/> asks for, with their trail widths.</summary>
        static readonly Dictionary<string, (string fbx, float trail)> WeaponModels = new Dictionary<string, (string, float)>
        {
            ["Sword"] = ("sword_1handed.fbx", 0.22f),
            ["Greatsword"] = ("sword_2handed_color.fbx", 0.3f),
            ["Shield"] = ("shield_badge_color.fbx", 0f),
        };

        public static string CharacterFbx(string character) => $"{ArtLibrary.KayKit}/Characters/{character}.fbx";
        public static bool HasCharacter(string character) => File.Exists(CharacterFbx(character));
        /// <summary>
        /// The character's texture atlas. Variants share their base character's atlas (Rogue_Hooded uses
        /// rogue_texture.png): without this fallback the hooded rogue (the armsmaster) came out untextured white.
        /// </summary>
        static Material CharacterMat(string character)
        {
            string own = $"{character.ToLowerInvariant()}_texture.png";
            if (File.Exists($"{ArtLibrary.KayKit}/Characters/{own}")) return Mat($"KayKit_{character}", own);
            string baseName = character.Split('_')[0];
            return Mat($"KayKit_{baseName}", $"{baseName.ToLowerInvariant()}_texture.png");
        }
        /// <summary>The weapons share the knight's texture atlas, whoever holds them.</summary>
        static Material WeaponMat => Mat("KayKit_Knight", "knight_texture.png");

        /// <summary>
        /// A KayKit adventurer (<paramref name="character"/>: Knight, Barbarian, ...) as the player's body: model, the
        /// weapons its <paramref name="weapons"/> name on the hand slots, trails, and the RhythmAnimator wired to the
        /// KayKit rig. Returns the combat controller.
        /// </summary>
        public static SwordCombatController RigHero(GameObject player, Transform visual, Conductor conductor, List<WeaponSet> weapons,
                                                    LocomotionSet locomotion, Material additive, out float hangDrop,
                                                    string character = "Knight", float height = 1.85f)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(visual, false);
            if (!HasCharacter(character)) character = "Knight";
            var (fit, rigRoot, body) = Place(CharacterFbx(character), model, height, CharacterMat(character));
            var bones = rigRoot.GetComponentsInChildren<Transform>(true);          // before the weapons are added

            var handR = ArtLibrary.FindDeep(rigRoot, "handslot.r");
            var handL = ArtLibrary.FindDeep(rigRoot, "handslot.l");
            AttachWeapons(weapons, handR, handL, additive);

            var anim = player.GetComponent<RhythmAnimator>();
            SetRef(anim, "conductor", conductor);
            SetRef(anim, "modelRoot", model);
            SetRef(anim, "grip", handR);
            SetRef(anim, "offGrip", handL);
            SetRef(anim, "weapon", weapons[0]);
            SetRef(anim, "locomotion", locomotion);
            SetArray(anim, "bones", bones);
            var so = new SerializedObject(anim);
            so.FindProperty("rootBone").stringValue = "root";
            so.FindProperty("hipsBone").stringValue = "hips";
            so.FindProperty("spineBone").stringValue = "spine";
            so.FindProperty("chestBone").stringValue = "chest";
            SetStrings(so.FindProperty("upperBodyBones"), UpperBody);
            SetStrings(so.FindProperty("leftArm"), new[] { "upperarm.l", "lowerarm.l", "wrist.l" });
            SetStrings(so.FindProperty("rightArm"), new[] { "upperarm.r", "lowerarm.r", "wrist.r" });
            var shL = ArtLibrary.FindDeep(rigRoot, "upperarm.l"); var shR = ArtLibrary.FindDeep(rigRoot, "upperarm.r");
            var elbow = ArtLibrary.FindDeep(rigRoot, "lowerarm.l"); var wrist = ArtLibrary.FindDeep(rigRoot, "wrist.l");
            float spread = shL != null && shR != null ? Vector3.Distance(shL.position, shR.position) * 0.4f : 0.17f;
            so.FindProperty("ledgeHandSpread").floatValue = spread;
            so.FindProperty("idleBob").floatValue = 0.02f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Hang so the hands, stretched up, just reach the lip.
            hangDrop = 1.2f;
            if (shL != null && elbow != null && wrist != null)
            {
                float shoulder = shL.position.y - visual.position.y;
                float reach = Vector3.Distance(shL.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
                hangDrop = shoulder + reach * 0.92f;
            }

            var combat = player.GetComponent<SwordCombatController>();
            SetRef(combat, "conductor", conductor);
            SetRef(combat, "animator", anim);
            SetList(combat, "weapons", weapons);
            return combat;
        }

        static void AttachWeapons(List<WeaponSet> weapons, Transform handR, Transform handL, Material additive)
        {
            var done = new HashSet<string>();
            foreach (var w in weapons)
            {
                if (w == null) continue;
                foreach (var (name, hand) in new[] { (w.swordObject, handR), (w.offHandObject, handL) })
                {
                    if (string.IsNullOrEmpty(name) || hand == null || !done.Add(name) || !WeaponModels.TryGetValue(name, out var m)) continue;
                    var go = Attach($"{ArtLibrary.KayKit}/Weapons/{m.fbx}", hand, name, WeaponMat);
                    if (go != null && m.trail > 0f && additive != null) AddTip(go, additive, m.trail);
                }
            }
        }

        /// <summary>
        /// A posed KayKit adventurer with no gameplay: loops <paramref name="idle"/> and turns toward the player
        /// (village folk, the title screen). <paramref name="props"/> are weapon object names to hold.
        /// </summary>
        public static GameObject BuildFigure(Transform parent, string name, string character, Vector3 position, float restYaw,
                                             string idle = "Idle_A", string near = "Idle_B", float height = 1.8f, params string[] props)
        {
            var root = new GameObject(name, typeof(Villager));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            if (!HasCharacter(character)) character = "Knight";
            var (fit, rigRoot, model) = Place(CharacterFbx(character), body, height, CharacterMat(character));
            var handR = ArtLibrary.FindDeep(rigRoot, "handslot.r");
            var handL = ArtLibrary.FindDeep(rigRoot, "handslot.l");
            foreach (var p in props)
                if (WeaponModels.TryGetValue(p, out var m)) Attach($"{ArtLibrary.KayKit}/Weapons/{m.fbx}", p == "Shield" ? handL : handR, p, WeaponMat);
            var v = root.GetComponent<Villager>();
            SetRef(v, "rigRoot", rigRoot);
            SetRef(v, "body", body);
            SetRef(v, "idleClip", ArtLibrary.Clip(idle));
            if (!string.IsNullOrEmpty(near)) SetRef(v, "nearClip", ArtLibrary.Clip(near));
            var so = new SerializedObject(v);
            so.FindProperty("restYaw").floatValue = restYaw;
            so.ApplyModifiedPropertiesWithoutUndo();
            body.localRotation = Quaternion.Euler(0f, restYaw, 0f);
            return root;
        }

        static void SetStrings(SerializedProperty p, string[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        /// <summary>The Skeleton Warrior (blade and large shield) as the shield-knight enemy, driven by KayKit clips.</summary>
        public static ShieldKnight BuildSkeletonKnight(Transform parent, Vector3 position, Vector2 bounds, Conductor conductor)
        {
            var root = new GameObject("Skeleton Knight", typeof(CapsuleCollider), typeof(ShieldKnight));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.45f; cap.height = 2.0f; cap.center = new Vector3(0f, 1.0f, 0f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var mat = Mat("KayKit_Skeleton", "skeleton_texture.png");
            var (fit, rigRoot, model) = Place(ArtLibrary.SkeletonFbx, body, 2.05f, mat);
            string W(string f) => $"{ArtLibrary.KayKit}/Weapons/{f}";
            Attach(W("Skeleton_Blade.fbx"), ArtLibrary.FindDeep(rigRoot, "handslot.r"), "Blade", mat);
            Attach(W("Skeleton_Shield_Large_A.fbx"), ArtLibrary.FindDeep(rigRoot, "handslot.l"), "Shield", mat);

            var k = root.GetComponent<ShieldKnight>();
            SetRef(k, "conductor", conductor);
            SetRef(k, "body", body);
            SetRef(k, "rigRoot", rigRoot);
            SetArray(k, "flashRenderers", root.GetComponentsInChildren<Renderer>(true));
            SetRef(k, "idleClip", ArtLibrary.Clip("Skeletons_Idle"));
            SetRef(k, "guardClip", ArtLibrary.Clip("Melee_Blocking"));
            SetRef(k, "walkClip", ArtLibrary.Clip("Skeletons_Walking"));
            SetRef(k, "cutClip", ArtLibrary.Clip("Melee_1H_Attack_Chop"));
            SetRef(k, "flurryAClip", ArtLibrary.Clip("Melee_1H_Attack_Slice_Diagonal"));
            SetRef(k, "flurryBClip", ArtLibrary.Clip("Melee_1H_Attack_Slice_Horizontal"));
            SetRef(k, "blockHitClip", ArtLibrary.Clip("Melee_Block_Hit"));
            SetRef(k, "staggerClip", ArtLibrary.Clip("Hit_B"));
            SetRef(k, "deathClip", ArtLibrary.Clip("Skeletons_Death"));
            var so = new SerializedObject(k);
            so.FindProperty("displayName").stringValue = "Skeleton Knight";
            so.FindProperty("maxHp").floatValue = 180f;
            so.FindProperty("aggroRange").floatValue = 7f;
            so.FindProperty("barHeight").floatValue = 2.4f;
            so.FindProperty("bounds").vector2Value = bounds;
            so.FindProperty("cutHit").floatValue = Hit["Melee_1H_Attack_Chop"];
            so.FindProperty("flurryAHit").floatValue = Hit["Melee_1H_Attack_Slice_Diagonal"];
            so.FindProperty("flurryBHit").floatValue = Hit["Melee_1H_Attack_Slice_Horizontal"];
            so.ApplyModifiedPropertiesWithoutUndo();
            body.rotation = Quaternion.Euler(0f, -90f + 14f, 0f);
            return k;
        }

        /// <summary>
        /// VS-2's bone dancer: the KayKit skeleton rogue (the warrior if the rogue isn't imported) with a blade in each
        /// hand. Its waltz is the dual-wield slice, stab and chop on beats one, two and three; the turn is the two-handed
        /// spin; the hesitation is chop and slice on one and three.
        /// </summary>
        public static BoneDancer BuildSkeletonDancer(Transform parent, string name, Vector3 position, Vector2 bounds, Conductor conductor, float maxHp)
        {
            var root = new GameObject(name, typeof(CapsuleCollider), typeof(BoneDancer));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.42f; cap.height = 2.0f; cap.center = new Vector3(0f, 1.0f, 0f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var mat = Mat("KayKit_Skeleton", "skeleton_texture.png");
            string fbx = File.Exists(ArtLibrary.SkeletonRogueFbx) ? ArtLibrary.SkeletonRogueFbx : ArtLibrary.SkeletonFbx;
            var (fit, rigRoot, model) = Place(fbx, body, 2.0f, mat);
            string W(string f) => $"{ArtLibrary.KayKit}/Weapons/{f}";
            Attach(W("Skeleton_Blade.fbx"), ArtLibrary.FindDeep(rigRoot, "handslot.r"), "Blade R", mat);
            Attach(W("Skeleton_Blade.fbx"), ArtLibrary.FindDeep(rigRoot, "handslot.l"), "Blade L", mat);

            var d = root.GetComponent<BoneDancer>();
            SetRef(d, "conductor", conductor);
            SetRef(d, "body", body);
            SetRef(d, "rigRoot", rigRoot);
            SetArray(d, "flashRenderers", root.GetComponentsInChildren<Renderer>(true));
            SetRef(d, "idleClip", ArtLibrary.Clip("Skeletons_Idle"));
            SetRef(d, "stepClip", ArtLibrary.Clip("Skeletons_Walking"));
            SetRef(d, "staggerClip", ArtLibrary.Clip("Hit_B"));
            SetRef(d, "deathClip", ArtLibrary.Clip("Skeletons_Death"));
            SetRef(d, "tauntClip", ArtLibrary.Clip("Skeletons_Taunt"));
            var so = new SerializedObject(d);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("maxHp").floatValue = maxHp;
            so.FindProperty("aggroRange").floatValue = 7f;
            so.FindProperty("barHeight").floatValue = 2.35f;
            so.FindProperty("bounds").vector2Value = bounds;
            var clips = new[]
            {
                new[] { ("Melee_Dualwield_Attack_Slice", 0.482f), ("Melee_Dualwield_Attack_Stab", 0.25f), ("Melee_Dualwield_Attack_Chop", 0.45f) },
                new[] { ("Melee_2H_Attack_Spin", 0.482f) },
                new[] { ("Melee_Dualwield_Attack_Chop", 0.45f), ("Melee_Dualwield_Attack_Slice", 0.482f) },
            };
            var list = so.FindProperty("patternClips");
            list.arraySize = clips.Length;
            for (int p = 0; p < clips.Length; p++)
            {
                var hits = list.GetArrayElementAtIndex(p).FindPropertyRelative("hits");
                hits.arraySize = clips[p].Length;
                for (int i = 0; i < clips[p].Length; i++)
                {
                    var e = hits.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("clip").objectReferenceValue = ArtLibrary.Clip(clips[p][i].Item1);
                    e.FindPropertyRelative("hit").floatValue = clips[p][i].Item2;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            body.rotation = Quaternion.Euler(0f, -90f + 14f, 0f);
            return d;
        }

        /// <summary>
        /// VS-3's summit boss: the KayKit skeleton mage (the warrior if the mage isn't imported), 3.4 m tall, with a staff
        /// (a blade if the staff isn't imported). One-handed clips for his cuts, the jump chop for the shockwave, the taunt
        /// for conducting.
        /// </summary>
        public static BoneCantor BuildSkeletonCantor(Transform parent, string name, Vector3 position, Vector2 bounds, Conductor conductor, float maxHp)
        {
            var root = new GameObject(name, typeof(CapsuleCollider), typeof(BoneCantor));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.7f; cap.height = 3.4f; cap.center = new Vector3(0f, 1.7f, 0f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var mat = Mat("KayKit_Skeleton", "skeleton_texture.png");
            string fbx = File.Exists(ArtLibrary.SkeletonMageFbx) ? ArtLibrary.SkeletonMageFbx : ArtLibrary.SkeletonFbx;
            var (fit, rigRoot, model) = Place(fbx, body, 3.4f, mat);
            string W(string f) => $"{ArtLibrary.KayKit}/Weapons/{f}";
            string staff = File.Exists(W("Skeleton_Staff.fbx")) ? W("Skeleton_Staff.fbx") : W("Skeleton_Blade.fbx");
            Attach(staff, ArtLibrary.FindDeep(rigRoot, "handslot.r"), "Staff", mat);

            var b = root.GetComponent<BoneCantor>();
            SetRef(b, "conductor", conductor);
            SetRef(b, "body", body);
            SetRef(b, "rigRoot", rigRoot);
            SetArray(b, "flashRenderers", root.GetComponentsInChildren<Renderer>(true));
            SetRef(b, "idleClip", ArtLibrary.Clip("Skeletons_Idle"));
            SetRef(b, "walkClip", ArtLibrary.Clip("Skeletons_Walking"));
            SetRef(b, "staggerClip", ArtLibrary.Clip("Hit_B"));
            SetRef(b, "deathClip", ArtLibrary.Clip("Skeletons_Death"));
            SetRef(b, "conductClip", ArtLibrary.Clip("Skeletons_Taunt"));
            var so = new SerializedObject(b);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("maxHp").floatValue = maxHp;
            so.FindProperty("aggroRange").floatValue = 16f;
            so.FindProperty("aggroHeight").floatValue = 4f;
            so.FindProperty("leashRange").floatValue = 40f;
            so.FindProperty("barHeight").floatValue = 3.9f;
            so.FindProperty("bounds").vector2Value = bounds;
            const string Diag = "Melee_1H_Attack_Slice_Diagonal", Horiz = "Melee_1H_Attack_Slice_Horizontal",
                         Chop = "Melee_1H_Attack_Chop", Stab = "Melee_1H_Attack_Stab", Jump = "Melee_1H_Attack_Jump_Chop";
            var clips = new (string pattern, string[] hits)[]
            {
                ("Gavel", new[] { Chop }),
                ("Down-Up", new[] { Diag, Horiz }),
                ("Waltz", new[] { Diag, Horiz, Chop }),
                ("Limp", new[] { Diag, Horiz, Stab }),
                ("Four on the Floor", new[] { Diag, Horiz, Diag, Chop }),
                ("Hesitation", new[] { Chop, Diag }),
                ("Shockwave", new[] { Jump }),
                ("Limp Run", new[] { Diag, Horiz, Diag, Stab }),
            };
            var list = so.FindProperty("attackClips");
            list.arraySize = clips.Length;
            for (int p = 0; p < clips.Length; p++)
            {
                var e = list.GetArrayElementAtIndex(p);
                e.FindPropertyRelative("pattern").stringValue = clips[p].pattern;
                var hits = e.FindPropertyRelative("hits");
                hits.arraySize = clips[p].hits.Length;
                for (int i = 0; i < clips[p].hits.Length; i++)
                {
                    var h = hits.GetArrayElementAtIndex(i);
                    h.FindPropertyRelative("clip").objectReferenceValue = ArtLibrary.Clip(clips[p].hits[i]);
                    h.FindPropertyRelative("hit").floatValue = Hit.TryGetValue(clips[p].hits[i], out var f) ? f : 0.45f;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            body.rotation = Quaternion.Euler(0f, -90f + 16f, 0f);
            return b;
        }
    }
}
