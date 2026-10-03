using System.Collections.Generic;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using K = ProjectHymn.EditorTools.LookBuilder;
using S = ProjectHymn.EditorTools.ProjectHymnSetup;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Builds M6 stages: rooms laid out left to right along X, joined by doors that sink into the floor. Lane rooms
    /// are 4 m deep (side-lane camera); arena rooms are 8 m deep (angled camera). Rooms can carry hurdles across
    /// the main path, an off-path alcove with a Note cache behind a hurdle, and a bell plate that must be rung.
    /// </summary>
    public static class StageBuilder
    {
        public enum Theme { Practice, Garden }

        public class RoomSpec
        {
            public string Name = "Room";
            public float Length = 16f;
            public bool Arena;
            public (string name, EnemyDefinition[] enemies)[] Waves = new (string, EnemyDefinition[])[0];
            public bool Boss;
            public int MusicPhase = -1;
            public SongData Song;
            /// <summary>Full-depth hurdles on the main path (x offsets from the room's start).</summary>
            public float[] Hurdles = new float[0];
            /// <summary>Off-path alcove in the back wall with a Note cache behind a hurdle (x offset, -1 = none).</summary>
            public float CacheAt = -1f;
            public int CacheNotes = 75;
            /// <summary>Bell plate near the back wall that must be rung before the door opens (x offset, -1 = none).</summary>
            public float SwitchAt = -1f;
            public float Depth => Arena ? 8f : 4f;
        }

        public class Result
        {
            public readonly List<StageRoom> Rooms = new List<StageRoom>();
            public readonly List<Renderer> Glow = new List<Renderer>();
            public Rect Whole;
        }

        /// <summary>Colours per theme. The Valor look (M6 Slice B) uses painted, less saturated bases.</summary>
        public struct Palette
        {
            public uint FloorA, FloorB, Wall, Trim, Staff, Door, Accent;
        }

        public static Palette Colors(Theme theme) => theme switch
        {
            Theme.Garden => new Palette { FloorA = 0x4a6a3a, FloorB = 0x56784a, Wall = 0x5a4a3a, Trim = 0xc8a050, Staff = 0x9cffb8, Door = 0x6a4a2a, Accent = 0x3a6a32 },
            _ => new Palette { FloorA = 0x5a4a6a, FloorB = 0x66567a, Wall = 0x6a4a5a, Trim = 0xd8a840, Staff = 0x7af0ff, Door = 0x4a2e3a, Accent = 0x8a6a40 },
        };

        public static Result Build(Theme theme, IList<RoomSpec> specs, float startX)
        {
            var root = new GameObject("Stage Set").transform;
            var res = new Result();
            var pal = Colors(theme);
            float x = startX;
            float maxDepth = 0f;
            var rects = new List<Rect>();
            foreach (var spec in specs)
            {
                var r = new Rect(x, -spec.Depth / 2f, spec.Length, spec.Depth);
                rects.Add(r);
                maxDepth = Mathf.Max(maxDepth, spec.Depth);
                x += spec.Length;
            }

            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var r = rects[i];
                var roomRoot = new GameObject($"Room {i + 1} - {spec.Name}").transform;
                roomRoot.SetParent(root, false);
                BuildRoom(roomRoot, theme, pal, spec, r, res.Glow, out var sw);

                // Wall at the room's start (only the first room) and the door / wall at its end.
                if (i == 0) EndWall(roomRoot, pal, r.xMin, spec.Depth, spec.Depth, res.Glow);
                StageDoor door = null;
                if (i < specs.Count - 1)
                {
                    var nextDepth = specs[i + 1].Depth;
                    float opening = Mathf.Min(spec.Depth, nextDepth);
                    EndWall(roomRoot, pal, r.xMax, Mathf.Max(spec.Depth, nextDepth), opening, res.Glow);
                    door = Door(roomRoot, pal, r.xMax, opening);
                }
                else EndWall(roomRoot, pal, r.xMax, spec.Depth, spec.Depth, res.Glow);

                res.Rooms.Add(new StageRoom
                {
                    name = spec.Name,
                    bounds = r,
                    arenaCamera = spec.Arena,
                    laneZ = 0f,
                    waves = MakeWaves(spec.Waves),
                    boss = spec.Boss,
                    exitDoor = door,
                    requiredSwitch = sw,
                    musicPhase = spec.MusicPhase,
                    song = spec.Song,
                });
            }

            res.Whole = new Rect(startX, -maxDepth / 2f, x - startX, maxDepth + 3f);
            K.Bounds(root, res.Whole, 4f);
            return res;
        }

        static List<ArenaWave> MakeWaves((string name, EnemyDefinition[] enemies)[] waves)
        {
            var list = new List<ArenaWave>();
            foreach (var w in waves) list.Add(new ArenaWave { name = w.name, enemies = w.enemies });
            return list;
        }

        // ---------------- Room ----------------

        static void BuildRoom(Transform root, Theme theme, Palette pal, RoomSpec spec, Rect r, List<Renderer> glow, out StageSwitch sw)
        {
            sw = null;
            // Floor: stripes across the room every 2 m, extending under the walls.
            int stripes = Mathf.CeilToInt(r.width / 2f);
            for (int i = 0; i < stripes; i++)
            {
                float w = Mathf.Min(2f, r.width - i * 2f);
                K.Block(root, "Block_Box", i % 2 == 0 ? pal.FloorA : pal.FloorB,
                    new Vector3(r.xMin + i * 2f + w / 2f, -0.3f, r.center.y), new Vector3(w, 0.3f, r.height + 2f), name: "Floor");
            }

            // Musical staff: five glowing lines along the room, pulsed on the beat.
            for (int i = 0; i < 5; i++)
            {
                var line = K.Block(root, "Block_Box", pal.Staff, new Vector3(r.center.x, 0f, -1.2f + i * 0.6f),
                    new Vector3(r.width - 0.6f, 0.02f, 0.06f), glow: true, name: "Staff Line");
                line.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                glow.Add(line.GetComponent<Renderer>());
            }

            // Back wall (with an alcove gap), low front wall (camera side), trim.
            float backZ = r.yMax + 0.5f;
            if (spec.CacheAt >= 0f)
            {
                float cx = r.xMin + spec.CacheAt, gap = 2.2f;
                float leftW = cx - gap / 2f - r.xMin, rightW = r.xMax - (cx + gap / 2f);
                if (leftW > 0.05f) K.Block(root, "Block_Box", pal.Wall, new Vector3(r.xMin + leftW / 2f, 0f, backZ), new Vector3(leftW, 1.4f, 1f), collider: true, name: "Back Wall");
                if (rightW > 0.05f) K.Block(root, "Block_Box", pal.Wall, new Vector3(r.xMax - rightW / 2f, 0f, backZ), new Vector3(rightW, 1.4f, 1f), collider: true, name: "Back Wall");
                Alcove(root, pal, cx, r.yMax, gap, spec.CacheNotes, glow);
            }
            else K.Block(root, "Block_Box", pal.Wall, new Vector3(r.center.x, 0f, backZ), new Vector3(r.width, 1.4f, 1f), collider: true, name: "Back Wall");
            K.Block(root, "Block_Box", pal.Trim, new Vector3(r.center.x, 1.4f, backZ), new Vector3(r.width, 0.12f, 1.05f), name: "Back Trim");
            K.Block(root, "Block_Box", pal.Wall, new Vector3(r.center.x, 0f, r.yMin - 0.5f), new Vector3(r.width, 0.35f, 1f), collider: true, name: "Front Wall");

            foreach (var h in spec.Hurdles) Hurdle(root, pal, new Vector3(r.xMin + h, 0f, r.center.y), r.height, "Hurdle");

            if (spec.SwitchAt >= 0f) sw = BellPlate(root, pal, new Vector3(r.xMin + spec.SwitchAt, 0f, r.yMax - 1.3f), glow);

            Dress(root, theme, pal, spec, r, glow);
        }

        static void Dress(Transform root, Theme theme, Palette pal, RoomSpec spec, Rect r, List<Renderer> glow)
        {
            switch (theme)
            {
                case Theme.Practice:
                    // Organ pipes behind the back wall, a dark panel behind them.
                    int pipes = Mathf.FloorToInt(r.width / 1.4f);
                    for (int i = 0; i < pipes; i++)
                    {
                        float px = r.xMin + 0.7f + i * 1.4f;
                        if (spec.CacheAt >= 0f && Mathf.Abs(px - (r.xMin + spec.CacheAt)) < 2f) continue;
                        float h = 2.4f + Mathf.PingPong(i * 0.55f, 1.6f);
                        K.Block(root, "Block_Cylinder", 0xc89a3a, new Vector3(px, 0f, r.yMax + 1.6f), new Vector3(0.6f, h, 0.6f), name: "Organ Pipe");
                        K.Block(root, "Block_Cone", 0xa87a2a, new Vector3(px, h, r.yMax + 1.6f), new Vector3(0.6f, 0.3f, 0.6f), name: "Pipe Cap");
                    }
                    K.Block(root, "Block_Box", 0x2e2238, new Vector3(r.center.x, 0f, r.yMax + 2.6f), new Vector3(r.width, 4.5f, 0.6f), name: "Back Panel");
                    break;

                case Theme.Garden:
                    // Hedge on the wall, trees behind it, a few flowers off the staff, the moon over the garden.
                    K.Block(root, "Block_Box", pal.Accent, new Vector3(r.center.x, 1.4f, r.yMax + 0.5f), new Vector3(r.width, 0.5f, 1.2f), name: "Hedge");
                    for (float tx = r.xMin + 1.5f; tx < r.xMax; tx += 3.6f)
                    {
                        if (spec.CacheAt >= 0f && Mathf.Abs(tx - (r.xMin + spec.CacheAt)) < 2.2f) continue;
                        float tz = r.yMax + 2.6f + Mathf.PingPong(tx, 1.2f);
                        K.Block(root, "Block_Cylinder", 0x5a3a22, new Vector3(tx, 0f, tz), new Vector3(0.4f, 1.1f, 0.4f), name: "Trunk");
                        K.Block(root, "Block_Cone", 0x2e6a32, new Vector3(tx, 0.9f, tz), new Vector3(2.2f, 1.8f, 2.2f), name: "Tree");
                        K.Block(root, "Block_Cone", 0x3e8040, new Vector3(tx, 2f, tz), new Vector3(1.6f, 1.5f, 1.6f), name: "Tree Top");
                    }
                    var rng = new System.Random(Mathf.RoundToInt(r.xMin * 10f));
                    for (int i = 0; i < Mathf.RoundToInt(r.width * 0.8f); i++)
                    {
                        float fx = r.xMin + (float)rng.NextDouble() * r.width, fz = r.yMin + (float)rng.NextDouble() * r.height;
                        if (Mathf.Abs(fz) < 1.6f) continue; // keep the staff clear
                        uint c = (i % 3) switch { 0 => 0xd86a9au, 1 => 0xe8d070u, _ => 0x9a7ad0u };
                        K.Block(root, "Block_Gem", c, new Vector3(fx, 0f, fz), new Vector3(0.18f, 0.18f, 0.18f), name: "Flower");
                    }
                    break;
            }

            if (spec.Arena)
            {
                // Corner lamps; boss rooms get footlights along the front edge.
                foreach (var (cx, cz) in new[] { (r.xMin + 0.6f, r.yMax + 0.5f), (r.xMax - 0.6f, r.yMax + 0.5f) })
                {
                    K.Block(root, "Block_Cylinder", pal.Wall, new Vector3(cx, 0f, cz), new Vector3(1.1f, 2f, 1.1f), collider: true, name: "Pillar");
                    var lamp = K.Block(root, "Block_Gem", pal.Trim, new Vector3(cx, 2f, cz), new Vector3(0.6f, 0.6f, 0.6f), glow: true, name: "Lamp");
                    glow.Add(lamp.GetComponent<Renderer>());
                }
                if (spec.Boss)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        var fl = K.Block(root, "Block_Gem", 0xffe08a, new Vector3(r.center.x - 6f + i * 1.5f, 0.35f, r.yMin - 0.45f), new Vector3(0.35f, 0.25f, 0.35f), glow: true, name: "Footlight");
                        glow.Add(fl.GetComponent<Renderer>());
                    }
                    for (int i = 0; i < 7; i++)
                        K.Block(root, "Block_Cylinder", i % 2 == 0 ? 0x8a1a2au : 0x701424u, new Vector3(r.xMin + 1.5f + i * (r.width - 3f) / 6f, 0f, r.yMax + 1.9f), new Vector3(1.4f, 5f, 0.9f), name: "Curtain");
                }
            }
        }

        // ---------------- Pieces ----------------

        /// <summary>Wall across the stage at x, as deep as <paramref name="depth"/>, with a centred opening.</summary>
        static void EndWall(Transform root, Palette pal, float x, float depth, float opening, List<Renderer> glow)
        {
            float half = depth / 2f + 1f, open = opening >= depth ? -1f : opening / 2f;
            if (open < 0f)
            {
                K.Block(root, "Block_Box", pal.Wall, new Vector3(x, 0f, 0f), new Vector3(1f, 1.6f, depth + 2f), collider: true, name: "End Wall");
            }
            else
            {
                float seg = half - open;
                K.Block(root, "Block_Box", pal.Wall, new Vector3(x, 0f, open + seg / 2f), new Vector3(1f, 1.6f, seg), collider: true, name: "Door Wall");
                K.Block(root, "Block_Box", pal.Wall, new Vector3(x, 0f, -open - seg / 2f), new Vector3(1f, 1.6f, seg), collider: true, name: "Door Wall");
                // Posts with lamps either side of the opening
                foreach (float z in new[] { open + 0.35f, -open - 0.35f })
                {
                    K.Block(root, "Block_Box", pal.Trim, new Vector3(x, 0f, z), new Vector3(0.7f, 2.6f, 0.7f), collider: true, name: "Door Post");
                    var lamp = K.Block(root, "Block_Gem", 0xffd27a, new Vector3(x, 2.6f, z), new Vector3(0.45f, 0.45f, 0.45f), glow: true, name: "Lamp");
                    glow.Add(lamp.GetComponent<Renderer>());
                }
            }
        }

        static StageDoor Door(Transform root, Palette pal, float x, float opening)
        {
            var go = new GameObject("Door", typeof(BoxCollider), typeof(StageDoor));
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            var col = go.GetComponent<BoxCollider>();
            col.size = new Vector3(0.6f, 3f, opening);
            col.center = new Vector3(0f, 1.5f, 0f);
            var visual = new GameObject("Gate").transform;
            visual.SetParent(go.transform, false);
            K.Block(visual, "Block_Box", pal.Door, new Vector3(0f, 0f, 0f), new Vector3(0.4f, 2.3f, opening), name: "Gate Panel");
            K.Block(visual, "Block_Box", pal.Trim, new Vector3(0f, 2.3f, 0f), new Vector3(0.5f, 0.15f, opening), name: "Gate Trim");
            for (int i = 0; i < 3; i++)
                K.Block(visual, "Block_Box", pal.Trim, new Vector3(-0.22f, 0.5f + i * 0.6f, 0f), new Vector3(0.05f, 0.08f, opening * 0.8f), name: "Gate Band");
            foreach (Transform t in visual) t.gameObject.isStatic = false; // it moves
            var door = go.GetComponent<StageDoor>();
            S.SetRef(door, "blocker", col);
            S.SetRef(door, "visual", visual);
            return door;
        }

        /// <summary>A low bar the player must jump. Yaw 0 = across the lane (along Z); 90 = along X.</summary>
        static void Hurdle(Transform root, Palette pal, Vector3 center, float width, string name, float yaw = 0f)
        {
            var block = K.Block(root, "Block_Box", pal.Trim, center, new Vector3(0.35f, 0.5f, width), yaw, collider: true, name: name);
            var h = block.AddComponent<Hurdle>();
            S.SetRef(h, "blocker", block.GetComponent<BoxCollider>());
            // Dark band so it reads as "jump over me"
            K.Block(root, "Block_Box", 0x2a1a20, center + new Vector3(0f, 0.18f, 0f), new Vector3(0.37f, 0.08f, width), yaw, name: "Hurdle Stripe");
        }

        static void Alcove(Transform root, Palette pal, float cx, float roomBack, float gap, int notes, List<Renderer> glow)
        {
            float depth = 2f, z0 = roomBack;
            K.Block(root, "Block_Box", pal.FloorB, new Vector3(cx, -0.3f, z0 + depth / 2f + 0.5f), new Vector3(gap + 1f, 0.3f, depth + 1f), name: "Alcove Floor");
            K.Block(root, "Block_Box", pal.Wall, new Vector3(cx - gap / 2f - 0.25f, 0f, z0 + depth / 2f + 0.5f), new Vector3(0.5f, 1.4f, depth + 1f), collider: true, name: "Alcove Wall");
            K.Block(root, "Block_Box", pal.Wall, new Vector3(cx + gap / 2f + 0.25f, 0f, z0 + depth / 2f + 0.5f), new Vector3(0.5f, 1.4f, depth + 1f), collider: true, name: "Alcove Wall");
            K.Block(root, "Block_Box", pal.Wall, new Vector3(cx, 0f, z0 + depth + 1.25f), new Vector3(gap + 1f, 1.4f, 0.5f), collider: true, name: "Alcove Back");
            Hurdle(root, pal, new Vector3(cx, 0f, z0 + 0.5f), gap, "Alcove Hurdle", 90f);

            var cache = new GameObject("Note Cache", typeof(SphereCollider), typeof(NoteCache));
            cache.transform.SetParent(root, false);
            cache.transform.localPosition = new Vector3(cx, 0f, z0 + 1.6f);
            var sc = cache.GetComponent<SphereCollider>();
            sc.radius = 0.6f;
            sc.center = new Vector3(0f, 0.6f, 0f);
            var model = K.Model(cache.transform, "NoteShot", new Vector3(0f, 0.6f, 0f), 0f, 2.2f);
            var nc = cache.GetComponent<NoteCache>();
            if (model != null)
            {
                S.SetRef(nc, "model", model.transform);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) glow.Add(r);
            }
            var so = new SerializedObject(nc);
            so.FindProperty("notes").intValue = notes;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static StageSwitch BellPlate(Transform root, Palette pal, Vector3 pos, List<Renderer> glow)
        {
            var go = new GameObject("Bell Plate", typeof(BoxCollider), typeof(StageSwitch), typeof(ToonTint));
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            var col = go.GetComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1f, 1.2f);
            col.center = new Vector3(0f, 0.5f, 0f);
            var plate = new GameObject("Plate").transform;
            plate.SetParent(go.transform, false);
            var disc = K.Block(plate, "Block_Cylinder", pal.Trim, Vector3.zero, new Vector3(1.2f, 0.14f, 1.2f), name: "Plate Disc");
            disc.isStatic = false;
            var bell = K.Block(plate, "Block_Cone", 0xe8c060, new Vector3(0f, 0.14f, 0f), new Vector3(0.5f, 0.35f, 0.5f), name: "Bell");
            bell.isStatic = false;
            glow.Add(bell.GetComponent<Renderer>());
            S.SetRef(go.GetComponent<StageSwitch>(), "plate", plate);
            return go.GetComponent<StageSwitch>();
        }

        // ---------------- Director wiring ----------------

        /// <summary>Write the rooms into an ArenaDirector (nested serialized lists).</summary>
        public static void SetRooms(ArenaDirector arena, List<StageRoom> rooms)
        {
            var so = new SerializedObject(arena);
            var list = so.FindProperty("rooms");
            list.arraySize = rooms.Count;
            for (int i = 0; i < rooms.Count; i++)
            {
                var src = rooms[i];
                var p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("name").stringValue = src.name;
                p.FindPropertyRelative("bounds").rectValue = src.bounds;
                p.FindPropertyRelative("arenaCamera").boolValue = src.arenaCamera;
                p.FindPropertyRelative("laneZ").floatValue = src.laneZ;
                p.FindPropertyRelative("boss").boolValue = src.boss;
                p.FindPropertyRelative("exitDoor").objectReferenceValue = src.exitDoor;
                p.FindPropertyRelative("requiredSwitch").objectReferenceValue = src.requiredSwitch;
                p.FindPropertyRelative("musicPhase").intValue = src.musicPhase;
                p.FindPropertyRelative("song").objectReferenceValue = src.song;
                var waves = p.FindPropertyRelative("waves");
                waves.arraySize = src.waves.Count;
                for (int w = 0; w < src.waves.Count; w++)
                {
                    var wp = waves.GetArrayElementAtIndex(w);
                    wp.FindPropertyRelative("name").stringValue = src.waves[w].name;
                    var arr = wp.FindPropertyRelative("enemies");
                    arr.arraySize = src.waves[w].enemies.Length;
                    for (int e = 0; e < src.waves[w].enemies.Length; e++)
                        arr.GetArrayElementAtIndex(e).objectReferenceValue = src.waves[w].enemies[e];
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
