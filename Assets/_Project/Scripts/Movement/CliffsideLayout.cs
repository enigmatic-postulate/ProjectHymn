using System.Collections.Generic;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The vertical slice 1 level, "Cliffside", as plain data: the cliffs the player climbs along the lane (x right,
    /// y up) and where the checkpoints, targets and goal stand. The editor's Cliffside builder turns this into a scene;
    /// the tests check every step of the route against the jump arcs in <see cref="SliceTuning"/>.
    /// </summary>
    public static class CliffsideLayout
    {
        public enum Kind { Ground, Cliff, Pillar, Wall, Ledge }

        /// <summary>A floating ledge is a slab this thick (its underside is open).</summary>
        public const float LedgeThickness = 0.6f;

        public readonly struct Block
        {
            public readonly string Name;
            public readonly float XMin, XMax, Top;
            public readonly Kind Kind;
            public Block(string name, float xMin, float xMax, float top, Kind kind) { Name = name; XMin = xMin; XMax = xMax; Top = top; Kind = kind; }
            public float Width => XMax - XMin;
            public float CenterX => (XMin + XMax) * 0.5f;
        }

        public enum MarkerKind { Spawn, Checkpoint, Target, FloatingTarget, Goal, CameraZone, Pickup, Dancer, Boss, Dummy }

        public readonly struct Marker
        {
            public readonly string Name;
            public readonly MarkerKind Kind;
            public readonly float X, Y;
            public readonly float Width;      // camera zones: x extent
            public Marker(string name, MarkerKind kind, float x, float y, float width = 0f) { Name = name; Kind = kind; X = x; Y = y; Width = width; }
        }

        /// <summary>Lowest floor; anything that falls this far below it respawns.</summary>
        public const float KillY = -6f;
        public const float WaterY = -2.4f;

        /// <summary>The route, in walking order. Walls bound the level and are not part of the route.</summary>
        public static readonly Block[] Route =
        {
            new Block("Beach",     -12f, 18f,  0f,   Kind.Ground),
            new Block("Tier 1",     18f, 34f,  2.0f, Kind.Cliff),    // 2.0 m: double jump, or jump and grab
            new Block("Tier 2",     34f, 44f,  4.6f, Kind.Cliff),    // 2.6 m: jump and grab
            new Block("Tier 3",     44f, 56f,  8.4f, Kind.Cliff),    // 3.8 m: double jump and grab
            new Block("Far Ledge",  61f, 72f,  8.4f, Kind.Cliff),    // 5 m gap with the floating target over it
            new Block("Pillar 1",   75f, 77f, 10.0f, Kind.Pillar),
            new Block("Pillar 2",   80f, 82f, 12.0f, Kind.Pillar),
            new Block("Summit",     85f, 102f, 14.5f, Kind.Cliff),
        };

        /// <summary>Extra scenery that stands on the route but is never required.</summary>
        public static readonly Block[] Props =
        {
            new Block("Rock",        7f,  9.5f, 0.8f, Kind.Pillar),
            new Block("West Wall", -14f, -12f, 22f,  Kind.Wall),
            new Block("East Wall", 102f, 104f, 24f,  Kind.Wall),
        };

        /// <summary>Off the route, never required: a floating ledge over Tier 3 (double jump up); its scroll is gone since update 18.</summary>
        public static readonly Block[] Secrets =
        {
            new Block("High Ledge", 49.8f, 52.6f, 11.0f, Kind.Ledge),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",        MarkerKind.Spawn,      -6f,  0f),
            new Marker("Checkpoint 1", MarkerKind.Checkpoint, -6f,  0f),
            new Marker("Checkpoint 2", MarkerKind.Checkpoint, 21f,  2.0f),
            new Marker("Checkpoint 3", MarkerKind.Checkpoint, 47f,  8.4f),
            new Marker("Checkpoint 4", MarkerKind.Checkpoint, 64f,  8.4f),
            new Marker("Checkpoint 5", MarkerKind.Checkpoint, 88f, 14.5f),
            new Marker("Shield Knight", MarkerKind.Target,    29f,  2.0f),
            new Marker("Gap Wisp",     MarkerKind.FloatingTarget, 58.5f, 9.4f),
            new Marker("Goal",         MarkerKind.Goal,       97f, 14.5f),
            // Pickups: the name says what it is (the builder reads it). Only lore since GDD update 18 (no Notes caches or scrolls).
            new Marker("Lore lore_cliff",  MarkerKind.Pickup, -10.5f, 0f),
            // Camera zones: x centre, look height, width
            new Marker("Climb View",   MarkerKind.CameraZone, 45f,  0f, 24f),
            new Marker("Gap View",     MarkerKind.CameraZone, 64f,  0f, 22f),
            new Marker("Summit View",  MarkerKind.CameraZone, 92f,  0f, 22f),
        };

        public static IEnumerable<Marker> MarkersOf(MarkerKind kind)
        {
            foreach (var m in Markers) if (m.Kind == kind) yield return m;
        }

        /// <summary>Cliffside as a stage the builder and the tests can treat like any other.</summary>
        public static StageLayout Stage => new StageLayout
        {
            Id = "Cliffside", Name = "Cliffside", Scene = "Cliffside", Meter = 4,
            ParSeconds = 75f, PerfectAttacks = 14,
            Route = Route, Props = Props, Secrets = Secrets, Markers = Markers,
        };
    }
}
