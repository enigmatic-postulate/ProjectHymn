using System.Collections.Generic;
using System.IO;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Movement;
using ProjectHymn.UI;
using ProjectHymn.EditorTools.Art;
using ProjectHymn.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ProjectHymn.EditorTools.ProjectHymnSetup;
using Lit = ProjectHymn.EditorTools.Sword.SwordsmanBuilder;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Hub &amp; Loop slice: the game database (weapons, classes, items, shop), the player rig (one per weapon), and the Title and
    /// Village scenes. Menu: Project Hymn ▸ Build Hub &amp; Loop builds the database and all three scenes (Title, Village,
    /// Cliffside) and puts them first in the build list. Safe to re-run.
    /// </summary>
    public static class HubBuilder
    {
        const string DataDir = "Assets/_Project/Data/Game";
        const string ResourcesDir = "Assets/_Project/Resources";
        const string VillagePath = SceneDir + "/Village.unity";
        const string TitlePath = SceneDir + "/Title.unity";
        const string CliffsidePath = SceneDir + "/Cliffside.unity";
        const string BelfryPath = SceneDir + "/Belfry.unity";
        const string SummitPath = SceneDir + "/Summit.unity";

        [MenuItem("Project Hymn/Build Hub & Loop", priority = 0)]
        public static void BuildAll()
        {
            CliffsideBuilder.Build();
            CliffsideBuilder.BuildBelfry();
            CliffsideBuilder.BuildSummit();
            BuildVillage();
            BuildTitle();
            SetBuildOrder();
            EditorSceneManager.OpenScene(TitlePath);
            Debug.Log("[Project Hymn] Hub & Loop built: Title, Village, Cliffside, Belfry Ridge and the Summit are first in File ▸ Build Profiles. " +
                      "Press Play in Title for the full loop (or in any scene for free play). W / Up talks and uses doors, " +
                      "E drinks the quick item, Q changes it, Esc pauses (Songbook, Items, Settings).");
        }

        // ---------------- Database ----------------

        static T Asset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        static Color Hex(uint c) => new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);

        static ItemDefinition Item(string id, string name, ItemKind kind, int price, string description, uint color)
        {
            var i = Asset<ItemDefinition>($"{DataDir}/Items/{id}.asset");
            i.id = id; i.displayName = name; i.kind = kind; i.price = price; i.description = description; i.color = Hex(color);
            EditorUtility.SetDirty(i);
            return i;
        }

        static ClassDefinition Class(string id, string name, string description, ClassBracket bracket, int minLevel, PlayLean lean,
                                     int hearts, float damage, List<WeaponSet> weapons)
        {
            var c = Asset<ClassDefinition>($"{DataDir}/Classes/{name}.asset");
            c.id = id; c.displayName = name; c.description = description;
            c.bracket = bracket; c.minLevel = minLevel; c.lean = lean;
            c.hearts = hearts; c.damage = damage;
            c.weapons = weapons;
            c.startingSequences = new List<string> { "CrossingCuts" };
            c.blockBonus = 0f; c.blockDamage = 0f; c.parryDamage = 0f; c.blockSkipsNote = false;
            c.dodgeInvulnerable = 0.25f; c.commonTimeOnly = false;
            return c;
        }

        /// <summary>
        /// Resources/HymnDatabase: the weapons (sword and shield, greatsword), the four slice classes (Minstrel, Fighter,
        /// Warden, Crusader; placeholder numbers), the items, the shop's stock and what a new game starts with.
        /// </summary>
        public static GameDatabase EnsureDatabase(List<WeaponSet> weapons)
        {
            Directory.CreateDirectory(DataDir + "/Items");
            Directory.CreateDirectory(DataDir + "/Classes");
            Directory.CreateDirectory(ResourcesDir);
            AssetDatabase.Refresh();

            // Weapons are separate from classes: give them the ids the save and the classes use.
            var sword = weapons.Count > 0 ? weapons[0] : null;
            var great = weapons.Count > 1 ? weapons[1] : null;
            if (sword != null) { sword.id = ProgressionRules.Sword; EditorUtility.SetDirty(sword); }
            if (great != null) { great.id = ProgressionRules.Greatsword; EditorUtility.SetDirty(great); }
            var both = new List<WeaponSet>(); if (sword != null) both.Add(sword); if (great != null) both.Add(great);
            var swordOnly = new List<WeaponSet>(); if (sword != null) swordOnly.Add(sword);

            // The Hub & Loop classes are gone (the weapon was the class then).
            foreach (var old in new[] { "Knight", "Barbarian" })
                if (AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{DataDir}/Classes/{old}.asset") != null)
                    AssetDatabase.DeleteAsset($"{DataDir}/Classes/{old}.asset");

            var minstrel = Class(ProgressionRules.Minstrel, "Minstrel",
                "Everyone starts here: a wandering player who fights to the beat. What you become depends on how you play.",
                ClassBracket.Base, 0, PlayLean.Balanced, 5, 1f, both);
            var fighter = Class(ProgressionRules.Fighter, "Fighter",
                "The D-rank brawler. Common time only and a wider block window, and blocks hit back. Only an S or A clear moves a Fighter up.",
                ClassBracket.Low, 0, PlayLean.Balanced, 6, 1f, both);
            fighter.blockBonus = 0.04f;           // about 2–3 frames at 60 fps either side; parry timing is unchanged
            fighter.blockDamage = 6f;
            fighter.parryDamage = 12f;
            fighter.commonTimeOnly = true;
            var warden = Class(ProgressionRules.Warden, "Warden",
                "Earned by mastery that leans on the guard. A parry cuts the attacker; a block makes it skip a note.",
                ClassBracket.High, 6, PlayLean.Guard, 6, 1f, swordOnly);
            warden.parryDamage = 20f;
            warden.blockSkipsNote = true;
            var crusader = Class(ProgressionRules.Crusader, "Crusader",
                "Earned by mastery that rarely blocks and prefers to dodge. Longer dodge frames and heavier blows, fewer hearts. (Brandish to come.)",
                ClassBracket.High, 6, PlayLean.Dodge, 4, 1.15f, both);
            crusader.dodgeInvulnerable = 0.35f;
            foreach (var c in new[] { minstrel, fighter, warden, crusader }) EditorUtility.SetDirty(c);

            var potion = Item("potion", "Potion", ItemKind.Potion, 60, "Restores 2 hearts.", 0xff6a5a);
            potion.heal = 2;
            var tonic = Item("tonic", "Battle Tonic", ItemKind.Tonic, 120, "Blows strike 25% harder for 16 beats.", 0xffb04a);
            tonic.damageBoost = 1.25f; tonic.boostBeats = 16;
            var power = Item("scroll_powerslice", "Scroll: Powerslice", ItemKind.Scroll, 250, "Teaches Powerslice (2-2): a short, heavy finisher.", 0xff7a5a);
            power.teaches = "Powerslice";
            var tempest = Item("scroll_tempest", "Scroll: Tempest", ItemKind.Scroll, 0, "Teaches Tempest (1-1-1-2-1-1-1): seven blows on seven beats.", 0x7affc8);
            tempest.teaches = "Tempest";
            var loreVillage = Item("lore_village", "The Hymn of Saltmere", ItemKind.Lore, 0, "Told by the village elder.", 0x9ab8ff);
            loreVillage.loreText = "Before the walls, Saltmere had a song. Fishers sang it to keep the oars together, and the sea kept time with them. " +
                                   "When the cliffs began to stir, it was the old cantors who answered, striking only when the tide struck.";
            var loreCliff = Item("lore_cliff", "Cairn on the Shore", ItemKind.Lore, 0, "Carved into a cairn on the beach below the cliffs.", 0x9ab8ff);
            loreCliff.loreText = "HERE THE FIRST CANTOR CLIMBED. HE COUNTED FOUR AND STRUCK ON ONE. THE BONES ON THE LEDGES COUNT TOO — LISTEN FOR THEIR BAR.";
            var loreLedge = Item("lore_ledge", "Lookout's Note", ItemKind.Lore, 0, "Tucked under a stone on a ledge above the street.", 0x9ab8ff);
            loreLedge.loreText = "Third night the knights in bone walked the tiers. They raise the blade a full bar before they swing. " +
                                 "Wait for it. Guard on the beat and they stagger like drunks.";
            var loreBelfry = Item("lore_belfry", "The Cantors' Count", ItemKind.Lore, 0, "Scratched on a cairn at the foot of the belfry ridge.", 0x9ab8ff);
            loreBelfry.loreText = "UP HERE THE BELL COUNTS THREE. THE DEAD STILL DANCE TO IT: ONE, THEY STEP; TWO, THREE, THEY SWAY. " +
                                  "WHEN THEY RAISE BOTH BLADES, THEY STRIKE ONE-TWO-THREE. WHEN THEY HOLD ON TWO, THEY STRIKE ON THREE.";
            var loreSummit = Item("lore_summit", "The Last Cantor", ItemKind.Lore, 0, "Cut into the first step of the summit path.", 0x9ab8ff);
            loreSummit.loreText = "THE LAST CANTOR WOULD NOT STOP COUNTING. HE COUNTS FOUR, THEN THREE, THEN SEVEN, AND RAISES HIS STAFF A BAR BEFORE HE CHANGES. " +
                                  "WHEN HE LEAPS, THE GROUND ITSELF STRIKES: NO BLADE TURNS IT. LEAVE THE GROUND.";
            foreach (var i in new[] { potion, tonic, power, tempest, loreVillage, loreCliff, loreLedge, loreBelfry, loreSummit }) EditorUtility.SetDirty(i);

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>($"{ResourcesDir}/{GameDatabase.ResourcePath}.asset");
            if (db == null) { db = ScriptableObject.CreateInstance<GameDatabase>(); AssetDatabase.CreateAsset(db, $"{ResourcesDir}/{GameDatabase.ResourcePath}.asset"); }
            db.classes = new List<ClassDefinition> { minstrel, fighter, warden, crusader };
            db.weapons = both;
            db.items = new List<ItemDefinition> { potion, tonic, power, tempest, loreVillage, loreCliff, loreLedge, loreBelfry, loreSummit };
            db.shopStock = new List<ItemDefinition> { potion, tonic, power };
            db.startingItems = new List<GameDatabase.ItemCount> { new GameDatabase.ItemCount { id = "potion", count = 1 } };
            db.hubScene = "Village";
            db.titleScene = "Title";
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        // ---------------- Players ----------------

        /// <summary>
        /// One player per weapon at <paramref name="at"/> (the first active, the rest switched off) and a PlayerSpawner that
        /// keeps the session's weapon and applies its class at run time. Nothing else in the scene may point at a player: everything that needs
        /// one finds the active player when it wakes. Returns the first (active) player.
        /// </summary>
        public static GameObject BuildPlayers(Vector3 at, Conductor conductor, GameDatabase db, LocomotionSet locomotion, Material additive, bool art)
        {
            var spawnerGo = new GameObject("Players", typeof(PlayerSpawner));
            GameObject first = null;
            var entries = new List<(string id, GameObject go)>();
            foreach (var weapon in db.weapons)
            {
                if (weapon == null) continue;
                // The KayKit hero for the weapon: the knight with sword and shield, the barbarian with the greatsword.
                string character = weapon.id == ProgressionRules.Greatsword ? "Barbarian" : "Knight";
                var player = new GameObject($"Player ({weapon.displayName})", typeof(CharacterController), typeof(PlayerInputReader), typeof(PlatformerController),
                    typeof(RhythmAnimator), typeof(SwordCombatController), typeof(PlayerGuard), typeof(PlayerHealth),
                    typeof(PlayerInteractor), typeof(QuickItems));
                player.transform.SetParent(spawnerGo.transform, false);
                player.transform.position = at + Vector3.up * 0.05f;
                var cc = player.GetComponent<CharacterController>();
                cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.04f; cc.stepOffset = 0.3f; cc.slopeLimit = 50f;
                var visual = new GameObject("Visual").transform;
                visual.SetParent(player.transform, false);
                SetRef(player.GetComponent<PlatformerController>(), "visual", visual);
                var weapons = new List<WeaponSet> { weapon };
                if (art)
                {
                    KayKitBuilder.RigHero(player, visual, conductor, weapons, locomotion, additive, out float hangDrop, character);
                    var pso = new SerializedObject(player.GetComponent<PlatformerController>());
                    pso.FindProperty("hangDrop").floatValue = hangDrop;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }
                else RigSwordsman(player, visual, conductor, weapons, locomotion);
                SetRef(player.GetComponent<PlayerGuard>(), "conductor", conductor);
                SetRef(player.GetComponent<PlayerGuard>(), "animator", player.GetComponent<RhythmAnimator>());
                if (first == null) first = player; else player.SetActive(false);
                entries.Add((weapon.id, player));
            }

            if (entries.Count == 0)
                Debug.LogError("[Project Hymn] No player was built: the game database has no weapons. " +
                               "Check Resources/HymnDatabase, then build again.");
            var so = new SerializedObject(spawnerGo.GetComponent<PlayerSpawner>());
            var list = so.FindProperty("players");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("weaponId").stringValue = entries[i].id;
                e.FindPropertyRelative("player").objectReferenceValue = entries[i].go;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return first;
        }

        public static SpawnPoint Spawn(Transform parent, string id, Vector3 at)
        {
            var go = new GameObject($"Spawn {id}", typeof(SpawnPoint));
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.GetComponent<SpawnPoint>().id = id;
            return go.GetComponent<SpawnPoint>();
        }

        /// <summary>The side-scrolling camera; it follows whichever player is active.</summary>
        public static SideScrollCamera BuildCamera(Vector3 near, float distance = 11f, float height = 2.3f)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SideScrollCamera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.58f, 0.68f, 0.86f);
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            camGo.transform.position = near + new Vector3(0f, height, -distance);
            var so = new SerializedObject(camGo.GetComponent<SideScrollCamera>());
            so.FindProperty("distance").floatValue = distance;
            so.FindProperty("height").floatValue = height;
            so.ApplyModifiedPropertiesWithoutUndo();
            return camGo.GetComponent<SideScrollCamera>();
        }

        /// <summary>The pause menu (the HUD and the dialogue box build themselves on demand).</summary>
        public static GameObject BuildGameUi(bool hub)
        {
            var go = new GameObject("Game UI", typeof(PauseMenu));
            var so = new SerializedObject(go.GetComponent<PauseMenu>());
            so.FindProperty("allowReturnToVillage").boolValue = !hub;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // ---------------- Pickups ----------------

        /// <summary>A pickup from a layout name: "Notes &lt;where&gt;", "Scroll &lt;sequence&gt;", "Lore &lt;id&gt;" or "Item &lt;id&gt;".</summary>
        public static Pickup BuildPickup(Transform parent, string name, Vector3 at, string scene, int notes = 50)
        {
            var go = new GameObject(name, typeof(SphereCollider), typeof(Pickup));
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 0.7f; col.center = new Vector3(0f, 0.9f, 0f);
            var p = go.GetComponent<Pickup>();
            p.pickupId = $"{scene.ToLowerInvariant()}/{name.Replace(' ', '_').ToLowerInvariant()}";   // remembered per save
            string arg = name.Contains(" ") ? name.Substring(name.IndexOf(' ') + 1) : "";

            PrimitiveType shape; Vector3 scale; uint color;
            if (name.StartsWith("Scroll "))
            {
                p.kind = PickupKind.Scroll; p.itemId = "scroll_" + arg.ToLowerInvariant();
                shape = PrimitiveType.Cylinder; scale = new Vector3(0.22f, 0.3f, 0.22f); color = 0xf2e2b0;
            }
            else if (name.StartsWith("Lore "))
            {
                p.kind = PickupKind.Lore; p.itemId = arg;
                shape = PrimitiveType.Cube; scale = new Vector3(0.42f, 0.55f, 0.12f); color = 0x6a88d8;
            }
            else if (name.StartsWith("Item "))
            {
                p.kind = PickupKind.Item; p.itemId = arg;
                shape = PrimitiveType.Sphere; scale = Vector3.one * 0.35f; color = 0xff6a5a;
            }
            else
            {
                p.kind = PickupKind.Notes; p.notes = notes;
                shape = PrimitiveType.Cube; scale = Vector3.one * 0.38f; color = 0xffd050;
            }

            var visual = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = scale;
            if (p.kind == PickupKind.Notes) visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            if (p.kind == PickupKind.Scroll) visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var mat = Lit.Lit(color, 0.7f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", Hex(color) * 0.35f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var lightGo = new GameObject("Glow", typeof(Light));
            lightGo.transform.SetParent(visual.transform, false);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Point; l.range = 2.6f; l.intensity = 1.1f; l.color = Hex(color);
            SetRef(p, "visual", visual.transform);
            return p;
        }

        // ---------------- Village ----------------

        const float LaneFront = -2.6f;

        public static void BuildVillage()
        {
            EnvironmentArt.MeshScope = "Village";
            PrepareContent();
            ArtLibrary.EnsureImportSettings();
            AssetDatabase.Refresh();
            bool art = ArtLibrary.HasKayKit, village = ArtLibrary.HasVillage;
            // Open the new scene first: opening a scene unloads assets nothing references yet, which would turn the
            // classes, weapons and moves loaded before it into null references (and leave the village with no player).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var weapons = art ? KayKitBuilder.EnsureWeapons() : LoadWeapons();
            var db = EnsureDatabase(weapons);
            var additive = LoadAdditive();
            var locomotion = art ? KayKitBuilder.EnsureLocomotion() : Sword.SwordContent.EnsureLocomotion();
            CliffsideBuilder.BuildLighting();
            var level = new GameObject("Village").transform;

            // Street, end walls, and the open ground behind the lane that the houses stand on.
            float x0 = VillageLayout.XMin, x1 = VillageLayout.XMax, top = VillageLayout.GroundTop;
            // No block overlaps another: where the street used to run on under the end walls, its hidden faces first
            // z-fought with the walls' (coplanar fronts) and then, nudged apart, cast striped self-shadow (acne) through
            // the walls' lower faces as the shadow cascades shifted with the camera. The street now stops at the walls,
            // and two strips carry the ground on behind them.
            const float WallBack = 12f;
            Slab(level, "Street", new Vector3(x0, -8f, LaneFront), new Vector3(x1, top, 40f), village, ground: true);
            Slab(level, "Street West", new Vector3(x0 - 3f, -8f, WallBack), new Vector3(x0, top, 40f), village, ground: true);
            Slab(level, "Street East", new Vector3(x1, -8f, WallBack), new Vector3(x1 + 3f, top, 40f), village, ground: true);
            Slab(level, "West Wall", new Vector3(x0 - 3f, -8f, LaneFront), new Vector3(x0, 12f, WallBack), village, ground: false);
            Slab(level, "East Wall", new Vector3(x1, -8f, LaneFront), new Vector3(x1 + 3f, 12f, WallBack), village, ground: false);
            Slab(level, "High Ledge", new Vector3(VillageLayout.LedgeXMin, VillageLayout.LedgeTop - VillageLayout.LedgeThickness, -1.4f),
                 new Vector3(VillageLayout.LedgeXMax, VillageLayout.LedgeTop, 1.4f), village, ground: false);
            BuildPickup(level, "Lore lore_ledge", new Vector3((VillageLayout.LedgeXMin + VillageLayout.LedgeXMax) * 0.5f, VillageLayout.LedgeTop, 0f), "Village");

            // Buildings
            var dress = new GameObject("Buildings").transform;
            dress.SetParent(level, false);
            if (village)
            {
                EnvironmentArt.House(dress, new Vector3(VillageLayout.InnX, top, 4.5f));
                EnvironmentArt.House(dress, new Vector3(VillageLayout.InnX - 5f, top, 7f));
                EnvironmentArt.House(dress, new Vector3(VillageLayout.ShopX, top, 4.5f));
                EnvironmentArt.House(dress, new Vector3(VillageLayout.TrainerX + 6f, top, 6f));
                EnvironmentArt.House(dress, new Vector3(-20f, top, 5f));
                EnvironmentArt.House(dress, new Vector3(38f, top, 9f));
                EnvironmentArt.Piece(dress, "Prop_Wagon", new Vector3(VillageLayout.ShopX + 3.6f, top, 3.2f), 0f);
                EnvironmentArt.Piece(dress, "Prop_Crate", new Vector3(VillageLayout.ShopX - 3.1f, top, 2.4f), 10f);
                EnvironmentArt.Piece(dress, "Prop_Crate", new Vector3(VillageLayout.ShopX - 3.9f, top, 2.8f), -20f);
                EnvironmentArt.Piece(dress, "Prop_Crate", new Vector3(VillageLayout.ShopX - 3.5f, top + 1.06f, 2.6f), 35f, 0.9f);
                for (float x = VillageLayout.TrainerX - 3f; x < VillageLayout.TrainerX + 3.2f; x += 2.06f)
                    EnvironmentArt.Piece(dress, "Prop_WoodenFence_Single", new Vector3(x, top, 3.4f), 0f);
                for (float x = 14f; x < 22f; x += 2.06f) EnvironmentArt.Piece(dress, "Prop_WoodenFence_Single", new Vector3(x, top, 5.5f), 0f);
                EnvironmentArt.Piece(dress, "Prop_Crate", new Vector3(46f, top, 2.4f), 5f);
                EnvironmentArt.Piece(dress, "Prop_Crate", new Vector3(47.2f, top, 2.7f), -25f);
                EnvironmentArt.Gate(dress, new Vector3(VillageLayout.GateX, top, 0f));
            }
            else
            {
                foreach (var x in new[] { VillageLayout.InnX, VillageLayout.ShopX, VillageLayout.TrainerX + 6f, -20f, 38f })
                    CliffsideBuilder.Box(dress, "House", new Vector3(x - 2f, top, 3f), new Vector3(x + 2f, top + 5f, 7f), 0xb09878, false);
                CliffsideBuilder.Box(dress, "Gate", new Vector3(VillageLayout.GateX - 2.2f, top, 1.2f), new Vector3(VillageLayout.GateX + 2.2f, top + 4.6f, 2f), 0x6a5c52, false);
            }
            BuildVillageBackdrop(level, village);

            // People
            var people = new GameObject("People").transform;
            people.SetParent(level, false);
            const float npcZ = 1.3f;
            var inn = Figure(people, "Innkeeper", "Rogue", new Vector3(VillageLayout.InnX + 1.6f, top, npcZ), art).AddComponent<Innkeeper>();
            inn.prompt = "Rest (save)";
            var shop = Figure(people, "Merchant", "Ranger", new Vector3(VillageLayout.ShopX + 1.2f, top, npcZ), art).AddComponent<ShopKeeper>();
            shop.prompt = "Shop";
            var trainer = Figure(people, "Trainer", "Rogue_Hooded", new Vector3(VillageLayout.TrainerX + 1.5f, top, npcZ), art).AddComponent<ClassTrainer>();
            trainer.prompt = "Weapon rack";
            var talk = new (string name, string model, string[] lines, string lore)[]
            {
                ("Elder", "Mage", new[]
                {
                    "So you're the new cantor. Good. The cliffs have been restless.",
                    "Strike on the beat and the blow lands true. Strike off it and you're only waving a sword.",
                    "Here, let me tell you the old song...",
                }, "lore_village"),
                ("Fisher", "Rogue", new[]
                {
                    "Bone knights on the tiers again. They lift that blade a whole bar before it comes down.",
                    "Count it. Guard on the beat it lands and they reel. That's when you cut.",
                }, ""),
                ("Lookout", "Ranger", new[]
                {
                    "Path to the cliffs is through the gate, east end of the street.",
                    "Some say a scroll's tucked up high over the third tier. Need two jumps to reach it, if it's there at all.",
                }, ""),
            };
            for (int i = 0; i < talk.Length && i < VillageLayout.Villagers.Length; i++)
            {
                var t = talk[i];
                var npc = Figure(people, t.name, t.model, new Vector3(VillageLayout.Villagers[i].X, top, npcZ), art).AddComponent<NpcTalk>();
                npc.speaker = t.name; npc.lines = t.lines; npc.givesLore = t.lore; npc.prompt = "Talk";
            }

            // The way out: the gate opens onto every stage (Belfry Ridge once Cliffside is cleared).
            var doorGo = new GameObject("Stage Gate", typeof(StageGate));
            doorGo.transform.SetParent(level, false);
            doorGo.transform.position = new Vector3(VillageLayout.GateX, top, 0f);
            var gate = doorGo.GetComponent<StageGate>();
            gate.prompt = "Leave the village"; gate.range = 2.2f;
            gate.stages = new List<StageGate.Destination>
            {
                new StageGate.Destination { stageId = "Cliffside", scene = "Cliffside", label = "Cliffside", meter = "4/4" },
                new StageGate.Destination { stageId = "Belfry", scene = "Belfry", label = "Belfry Ridge", meter = "3/4", requires = "Cliffside" },
                new StageGate.Destination { stageId = "Summit", scene = "Summit", label = "The Summit (boss)", meter = "4/4 · 3/4 · 7/8", requires = "Belfry" },
            };

            // Spawns, players, camera, UI
            var spawns = new GameObject("Spawns").transform;
            foreach (var s in VillageLayout.Spawns) Spawn(spawns, s.Name, new Vector3(s.X, top, 0f));
            var start = new Vector3(VillageLayout.Spawns[0].X, top, 0f);
            BuildPlayers(start, null, db, locomotion, additive, art);
            BuildCamera(start, 12f, 2.6f);
            BuildGameUi(hub: true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillagePath);
            Debug.Log("[Project Hymn] Village built.");
        }

        static void Slab(Transform parent, string name, Vector3 min, Vector3 max, bool village, bool ground)
        {
            if (village)
                EnvironmentArt.Block(parent, name, min, max, ground ? EnvironmentArt.Stone : EnvironmentArt.DarkStone,
                    ground ? EnvironmentArt.Grass : EnvironmentArt.Grass, 3.2f, 5f, collider: true);
            else CliffsideBuilder.Box(parent, name, min, max, ground ? 0x6c9a48u : 0x8c7c6cu, collider: true);
        }

        static void BuildVillageBackdrop(Transform level, bool village)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(level, false);
            var rng = new System.Random(23);
            var layers = new[] { (z: 48f, min: 8f, max: 20f, w: 14f), (z: 80f, min: 14f, max: 34f, w: 22f), (z: 130f, min: 20f, max: 50f, w: 30f) };
            for (int li = 0; li < layers.Length; li++)
            {
                var layer = layers[li];
                for (float x = -70f; x < 140f;)
                {
                    float w = layer.w * (0.6f + (float)rng.NextDouble() * 0.9f);
                    float h = layer.min + (float)rng.NextDouble() * (layer.max - layer.min);
                    if (village) EnvironmentArt.FarMesa(root, rng, li, x, w, h, layer.z, layer.w, -12f);
                    else CliffsideBuilder.Box(root, "Far Cliff", new Vector3(x, -12f, layer.z), new Vector3(x + w, h, layer.z + layer.w), 0x95a3c0, false, 0f);
                    x += w * (0.7f + (float)rng.NextDouble() * 0.6f);
                }
            }
        }

        /// <summary>A villager: a KayKit adventurer when the pack is in, otherwise a capsule.</summary>
        static GameObject Figure(Transform parent, string name, string model, Vector3 at, bool art)
        {
            if (art) return KayKitBuilder.BuildFigure(parent, name, model, at, 180f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Lit.Lit(0x8a6ab0);
            return go;
        }

        // ---------------- Title ----------------

        public static void BuildTitle()
        {
            EnvironmentArt.MeshScope = "Title";
            PrepareContent();
            ArtLibrary.EnsureImportSettings();
            AssetDatabase.Refresh();
            bool art = ArtLibrary.HasKayKit, village = ArtLibrary.HasVillage;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var weapons = art ? KayKitBuilder.EnsureWeapons() : LoadWeapons();
            EnsureDatabase(weapons);
            CliffsideBuilder.BuildLighting();
            var set = new GameObject("Set").transform;
            Slab(set, "Ground", new Vector3(-20f, -6f, -6f), new Vector3(20f, 0f, 30f), village, ground: true);
            if (village)
            {
                EnvironmentArt.House(set, new Vector3(-1f, 0f, 9f));
                EnvironmentArt.Gate(set, new Vector3(7.5f, 0f, 0f));
                EnvironmentArt.Piece(set, "Prop_Crate", new Vector3(5.2f, 0f, 2.6f), 15f);
            }
            BuildVillageBackdrop(set, village);

            // The hero with each weapon, posed on the right of the frame (the menu sits on the left).
            if (art)
            {
                KayKitBuilder.BuildFigure(set, "Knight", "Knight", new Vector3(3.2f, 0f, 0.4f), 200f, "Idle_A", null, 1.85f, "Sword", "Shield");
                KayKitBuilder.BuildFigure(set, "Barbarian", "Barbarian", new Vector3(5.1f, 0f, 0.9f), 192f, "Melee_2H_Idle", null, 1.9f, "Greatsword");
            }

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.58f, 0.68f, 0.86f);
            cam.fieldOfView = 34f;
            cam.farClipPlane = 400f;
            camGo.transform.position = new Vector3(1.2f, 1.6f, -6.8f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(2.8f, 1.15f, 0.6f) - camGo.transform.position, Vector3.up);

            // Music under the menu: the battle theme without the click.
            var conductor = BuildConductor(LoadTheme());
            var cso = new SerializedObject(conductor);
            cso.FindProperty("metronome").boolValue = false;
            cso.FindProperty("startDelay").floatValue = 0.3f;
            cso.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("Title Menu", typeof(TitleMenu));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TitlePath);
            Debug.Log("[Project Hymn] Title built.");
        }

        // ---------------- Build order ----------------

        static void SetBuildOrder()
        {
            var order = new[] { TitlePath, VillagePath, CliffsidePath, BelfryPath, SummitPath };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var p in order) scenes.Add(new EditorBuildSettingsScene(p, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (System.Array.IndexOf(order, s.path) < 0) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
