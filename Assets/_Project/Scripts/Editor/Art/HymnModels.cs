namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Original low-poly placeholder models for Project Hymn, built from simple parts in saturated colours.
    /// All face +Z with feet at y = 0 (1 unit = 1 metre). Weapons have the grip at the origin, pointing up +Y.
    /// Pure C# so they can be previewed outside Unity; ModelGenerator saves them as meshes and prefabs.
    /// </summary>
    public static class HymnModels
    {
        // ---- shared palette (0xRRGGBB) ----
        public const uint Skin = 0xffc48c, Ink = 0x24163a, White = 0xfaf6ff, Gold = 0xffc21a, Brown = 0x7a4a2a,
                          DarkBrown = 0x4a2a18, Silver = 0xd4dcf2, Steel = 0x8ea2e8, SteelDark = 0x4c5aa8, Cyan = 0x38f4ff,
                          Red = 0xf2283c, Orange = 0xff7a1a, Green = 0x3fe05a, GreenLight = 0x9cff8c, Purple = 0xa64cff,
                          PurpleDark = 0x5a249a, Black = 0x1c1a2c, Cream = 0xfff0c8, Pink = 0xff5aa8, Blue = 0x2f5bff,
                          BlueDark = 0x1c349c, Teal = 0x18c8a8, Plum = 0x7a3a8c;

        static V3 P(float x, float y, float z) => new V3(x, y, z);

        // ---------------- The Cantor (player) ----------------

        public enum Armor { Robe, BrassPlate, MetronomeVest }

        /// <summary>The hero: a hooded singer in a flared robe. The armor changes colours and adds pieces.</summary>
        public static LowPolyMesh Cantor(Armor armor)
        {
            uint robe, robeDark, trim;
            switch (armor)
            {
                case Armor.BrassPlate: robe = 0xc8283c; robeDark = 0x7c1424; trim = Gold; break;
                case Armor.MetronomeVest: robe = Teal; robeDark = 0x0c7a6a; trim = White; break;
                default: robe = Blue; robeDark = BlueDark; trim = Gold; break;
            }
            var m = new LowPolyMesh();
            // Boots
            m.Box(DarkBrown, P(-0.13f, 0.06f, 0.03f), P(0.16f, 0.12f, 0.26f));
            m.Box(DarkBrown, P(0.13f, 0.06f, 0.03f), P(0.16f, 0.12f, 0.26f));
            // Robe: flared octagonal frustum, hem band
            m.Cylinder(robe, P(0, 0.1f, 0), 0.42f, 0.72f, 8, 0.24f);
            m.Cylinder(trim, P(0, 0.1f, 0), 0.43f, 0.07f, 8, 0.415f);
            // Belt and chest
            m.Cylinder(trim, P(0, 0.78f, 0), 0.25f, 0.07f, 8);
            m.Cylinder(robe, P(0, 0.84f, 0), 0.25f, 0.26f, 8, 0.2f);
            // Sleeves and hands (right hand at ~(0.36, 0.72, 0.1) holds the weapon)
            m.Cylinder(robeDark, P(-0.26f, 0.98f, 0), 0.09f, 0.32f, 6, 0.12f, P(0, 0, 160));
            m.Cylinder(robeDark, P(0.26f, 0.98f, 0), 0.09f, 0.32f, 6, 0.12f, P(-20, 0, -150));
            m.Sphere(Skin, P(-0.37f, 0.7f, 0.0f), P(0.07f, 0.07f, 0.07f), 0);
            m.Sphere(Skin, P(0.36f, 0.72f, 0.1f), P(0.07f, 0.07f, 0.07f), 0);
            // Collar
            m.Cylinder(trim, P(0, 1.08f, 0), 0.17f, 0.05f, 8);
            // Head, hood and face
            m.Sphere(Skin, P(0, 1.28f, 0.02f), P(0.2f, 0.21f, 0.19f), 1);
            m.Sphere(robe, P(0, 1.33f, -0.05f), P(0.24f, 0.25f, 0.23f), 1);
            m.Cylinder(robe, P(0, 1.48f, -0.08f), 0.12f, 0.2f, 6, 0f, P(-35, 0, 0)); // hood tip
            m.Box(Ink, P(-0.075f, 1.29f, 0.19f), P(0.045f, 0.08f, 0.03f));
            m.Box(Ink, P(0.075f, 1.29f, 0.19f), P(0.045f, 0.08f, 0.03f));
            m.Box(0xff8a8a, P(-0.13f, 1.22f, 0.165f), P(0.05f, 0.03f, 0.02f));
            m.Box(0xff8a8a, P(0.13f, 1.22f, 0.165f), P(0.05f, 0.03f, 0.02f));
            // Music-note pendant
            m.Sphere(trim, P(0.03f, 0.95f, 0.21f), P(0.035f, 0.03f, 0.02f), 0);
            m.Box(trim, P(0.06f, 1.0f, 0.21f), P(0.015f, 0.1f, 0.015f));

            switch (armor)
            {
                case Armor.BrassPlate:
                    m.Box(Gold, P(0, 0.93f, 0.08f), P(0.42f, 0.28f, 0.2f));
                    m.Box(0xd89a10, P(0, 0.93f, 0.185f), P(0.12f, 0.2f, 0.02f));
                    m.Sphere(Gold, P(-0.27f, 1.05f, 0), P(0.13f, 0.08f, 0.13f), 0);
                    m.Sphere(Gold, P(0.27f, 1.05f, 0), P(0.13f, 0.08f, 0.13f), 0);
                    break;
                case Armor.MetronomeVest:
                    m.Box(White, P(0, 0.93f, 0.06f), P(0.36f, 0.26f, 0.2f));
                    m.Box(Teal, P(0, 0.93f, 0.165f), P(0.04f, 0.24f, 0.02f));
                    // Little metronome on the back: pyramid body and pendulum
                    m.Cylinder(DarkBrown, P(0, 0.82f, -0.26f), 0.12f, 0.36f, 4, 0.03f);
                    m.Box(Gold, P(0.03f, 1.0f, -0.36f), P(0.02f, 0.26f, 0.02f), P(0, 0, -14));
                    break;
            }
            return m.Finish();
        }

        // ---------------- Enemies ----------------

        /// <summary>Metronome Slime: a squashy green blob with a pendulum antenna.</summary>
        public static LowPolyMesh Slime()
        {
            var m = new LowPolyMesh();
            m.Sphere(Green, P(0, 0.3f, 0), P(0.46f, 0.32f, 0.44f), 1);
            m.Sphere(GreenLight, P(0, 0.22f, 0.16f), P(0.3f, 0.18f, 0.3f), 1);
            m.Sphere(White, P(-0.15f, 0.42f, 0.33f), P(0.1f, 0.12f, 0.06f), 0);
            m.Sphere(White, P(0.15f, 0.42f, 0.33f), P(0.1f, 0.12f, 0.06f), 0);
            m.Box(Ink, P(-0.15f, 0.42f, 0.385f), P(0.05f, 0.08f, 0.02f));
            m.Box(Ink, P(0.15f, 0.42f, 0.385f), P(0.05f, 0.08f, 0.02f));
            m.Box(0x16602a, P(0, 0.29f, 0.4f), P(0.14f, 0.04f, 0.02f));
            m.Box(Gold, P(0.04f, 0.74f, 0), P(0.025f, 0.24f, 0.025f), P(0, 0, -12));
            m.Sphere(Gold, P(0.07f, 0.86f, 0), P(0.06f, 0.06f, 0.06f), 0);
            return m.Finish();
        }

        /// <summary>Snare Imp: an orange horned gremlin with a drum belt and two drumsticks.</summary>
        public static LowPolyMesh Imp()
        {
            var m = new LowPolyMesh();
            m.Box(0xa83c10, P(-0.11f, 0.1f, 0.02f), P(0.12f, 0.2f, 0.16f));
            m.Box(0xa83c10, P(0.11f, 0.1f, 0.02f), P(0.12f, 0.2f, 0.16f));
            m.Sphere(Orange, P(0, 0.42f, 0), P(0.28f, 0.26f, 0.24f), 1);
            m.Cylinder(Red, P(0, 0.3f, 0), 0.3f, 0.18f, 10);
            m.Cylinder(White, P(0, 0.3f, 0), 0.31f, 0.03f, 10);
            m.Cylinder(White, P(0, 0.45f, 0), 0.31f, 0.03f, 10);
            m.Sphere(Orange, P(0, 0.78f, 0.02f), P(0.25f, 0.22f, 0.22f), 1);
            m.Cylinder(0x8c1020, P(-0.14f, 0.92f, 0), 0.06f, 0.24f, 5, 0f, P(0, 0, 28));
            m.Cylinder(0x8c1020, P(0.14f, 0.92f, 0), 0.06f, 0.24f, 5, 0f, P(0, 0, -28));
            m.Box(Gold, P(-0.08f, 0.8f, 0.2f), P(0.07f, 0.05f, 0.03f));
            m.Box(Gold, P(0.08f, 0.8f, 0.2f), P(0.07f, 0.05f, 0.03f));
            m.Box(Ink, P(0, 0.7f, 0.215f), P(0.14f, 0.03f, 0.02f));
            // Arms with drumsticks raised
            m.Cylinder(Orange, P(-0.26f, 0.5f, 0.05f), 0.05f, 0.26f, 5, 0.04f, P(-30, 0, 30));
            m.Cylinder(Orange, P(0.26f, 0.5f, 0.05f), 0.05f, 0.26f, 5, 0.04f, P(-30, 0, -30));
            m.Cylinder(Cream, P(-0.36f, 0.68f, 0.18f), 0.02f, 0.4f, 5, 0.02f, P(40, 0, 15));
            m.Cylinder(Cream, P(0.36f, 0.68f, 0.18f), 0.02f, 0.4f, 5, 0.02f, P(40, 0, -15));
            m.Sphere(Cream, P(-0.4f, 0.93f, 0.43f), P(0.04f, 0.04f, 0.04f), 0);
            m.Sphere(Cream, P(0.4f, 0.93f, 0.43f), P(0.04f, 0.04f, 0.04f), 0);
            return m.Finish();
        }

        /// <summary>Tuba Knight: steel armour with a glowing visor and a brass horn on its back.</summary>
        public static LowPolyMesh Knight()
        {
            var m = new LowPolyMesh().Glow(Cyan);
            m.Box(SteelDark, P(-0.17f, 0.25f, 0), P(0.2f, 0.5f, 0.26f));
            m.Box(SteelDark, P(0.17f, 0.25f, 0), P(0.2f, 0.5f, 0.26f));
            m.Box(Steel, P(0, 0.6f, 0), P(0.5f, 0.24f, 0.32f));
            m.Box(Steel, P(0, 0.95f, 0), P(0.62f, 0.5f, 0.4f));
            m.Box(SteelDark, P(0, 0.95f, 0.205f), P(0.2f, 0.36f, 0.02f));
            m.Box(Steel, P(-0.42f, 1.13f, 0), P(0.3f, 0.18f, 0.44f), P(0, 0, 12));
            m.Box(Steel, P(0.42f, 1.13f, 0), P(0.3f, 0.18f, 0.44f), P(0, 0, -12));
            m.Box(SteelDark, P(-0.42f, 0.8f, 0.04f), P(0.16f, 0.4f, 0.18f));
            m.Box(SteelDark, P(0.42f, 0.8f, 0.04f), P(0.16f, 0.4f, 0.18f));
            m.Box(Steel, P(0, 1.42f, 0), P(0.36f, 0.4f, 0.38f));
            m.Box(Cyan, P(0, 1.44f, 0.195f), P(0.26f, 0.06f, 0.02f));
            m.Cylinder(Red, P(0, 1.62f, -0.04f), 0.05f, 0.16f, 5, 0.12f);
            // Tuba: coiled body and a big bell over the shoulder
            m.Cylinder(Gold, P(0.05f, 0.8f, -0.32f), 0.16f, 0.5f, 8);
            m.Cylinder(Gold, P(0.18f, 1.2f, -0.32f), 0.07f, 0.4f, 6, 0.32f, P(0, 0, -25));
            m.Cylinder(0xb07a08, P(0.35f, 1.56f, -0.32f), 0.3f, 0.04f, 8, 0.3f, P(0, 0, -25));
            return m.Finish();
        }

        /// <summary>Piccolo Bat: a round purple bat with a silver flute for a beak.</summary>
        public static LowPolyMesh Bat()
        {
            var m = new LowPolyMesh();
            m.Sphere(Purple, P(0, 0.3f, 0), P(0.24f, 0.22f, 0.22f), 1);
            m.Sphere(0xd8a8ff, P(0, 0.26f, 0.1f), P(0.15f, 0.13f, 0.12f), 0);
            m.Cylinder(PurpleDark, P(-0.12f, 0.46f, 0), 0.06f, 0.16f, 4, 0f, P(0, 0, 18));
            m.Cylinder(PurpleDark, P(0.12f, 0.46f, 0), 0.06f, 0.16f, 4, 0f, P(0, 0, -18));
            m.Box(White, P(-0.08f, 0.36f, 0.19f), P(0.06f, 0.07f, 0.03f));
            m.Box(White, P(0.08f, 0.36f, 0.19f), P(0.06f, 0.07f, 0.03f));
            m.Box(Ink, P(-0.08f, 0.355f, 0.21f), P(0.03f, 0.04f, 0.02f));
            m.Box(Ink, P(0.08f, 0.355f, 0.21f), P(0.03f, 0.04f, 0.02f));
            // Flute beak
            m.Cylinder(Silver, P(0, 0.27f, 0.18f), 0.03f, 0.34f, 6, 0.03f, P(90, 0, 0));
            m.Box(Ink, P(0, 0.30f, 0.34f), P(0.02f, 0.012f, 0.02f));
            // Wings: thin swept blades
            m.Box(PurpleDark, P(-0.4f, 0.36f, -0.02f), P(0.46f, 0.03f, 0.22f), P(0, 12, 20));
            m.Box(PurpleDark, P(-0.62f, 0.44f, -0.06f), P(0.24f, 0.03f, 0.16f), P(0, 24, 34));
            m.Box(PurpleDark, P(0.4f, 0.36f, -0.02f), P(0.46f, 0.03f, 0.22f), P(0, -12, -20));
            m.Box(PurpleDark, P(0.62f, 0.44f, -0.06f), P(0.24f, 0.03f, 0.16f), P(0, -24, -34));
            return m.Finish();
        }

        /// <summary>The First Conductor: a tall tailcoated maestro with wild white hair and a raised golden baton.</summary>
        public static LowPolyMesh FirstConductor()
        {
            var m = new LowPolyMesh().Glow(Gold);
            m.Box(Black, P(-0.16f, 0.4f, 0), P(0.18f, 0.8f, 0.22f));
            m.Box(Black, P(0.16f, 0.4f, 0), P(0.18f, 0.8f, 0.22f));
            m.Box(Ink, P(-0.16f, 0.05f, 0.05f), P(0.2f, 0.1f, 0.34f));
            m.Box(Ink, P(0.16f, 0.05f, 0.05f), P(0.2f, 0.1f, 0.34f));
            // Tailcoat: torso and swallow tails behind
            m.Cylinder(0x2a2448, P(0, 0.8f, 0), 0.34f, 0.85f, 8, 0.28f);
            m.Box(0x2a2448, P(-0.12f, 0.55f, -0.28f), P(0.16f, 0.6f, 0.06f), P(-12, 0, 6));
            m.Box(0x2a2448, P(0.12f, 0.55f, -0.28f), P(0.16f, 0.6f, 0.06f), P(-12, 0, -6));
            m.Box(White, P(0, 1.3f, 0.27f), P(0.2f, 0.4f, 0.03f));
            m.Box(Gold, P(0, 1.16f, 0.29f), P(0.03f, 0.03f, 0.02f));
            m.Box(Gold, P(0, 1.26f, 0.29f), P(0.03f, 0.03f, 0.02f));
            m.Box(Red, P(-0.07f, 1.5f, 0.28f), P(0.11f, 0.08f, 0.04f), P(0, 0, 10));
            m.Box(Red, P(0.07f, 1.5f, 0.28f), P(0.11f, 0.08f, 0.04f), P(0, 0, -10));
            m.Cylinder(0x2a2448, P(0, 1.55f, 0), 0.24f, 0.08f, 8);
            // Arms: left down, right raised with the baton
            m.Cylinder(0x2a2448, P(-0.34f, 1.5f, 0), 0.08f, 0.62f, 6, 0.07f, P(0, 0, 165));
            m.Sphere(White, P(-0.42f, 0.9f, 0.02f), P(0.08f, 0.08f, 0.08f), 0);
            m.Cylinder(0x2a2448, P(0.34f, 1.5f, 0), 0.08f, 0.62f, 6, 0.07f, P(-20, 0, -35));
            m.Sphere(White, P(0.68f, 1.98f, 0.2f), P(0.08f, 0.08f, 0.08f), 0);
            m.Cylinder(Gold, P(0.68f, 1.98f, 0.2f), 0.045f, 0.8f, 6, 0.02f, P(-30, 0, -20));
            m.Sphere(White, P(0.82f, 2.65f, 0.6f), P(0.05f, 0.05f, 0.05f), 0);
            // Head, wild hair, stern face
            m.Sphere(Skin, P(0, 1.82f, 0.02f), P(0.23f, 0.26f, 0.23f), 1);
            m.Sphere(White, P(-0.2f, 1.94f, -0.06f), P(0.18f, 0.2f, 0.2f), 0);
            m.Sphere(White, P(0.2f, 1.94f, -0.06f), P(0.18f, 0.2f, 0.2f), 0);
            m.Sphere(White, P(0, 2.08f, -0.1f), P(0.22f, 0.16f, 0.2f), 0);
            m.Box(Ink, P(-0.08f, 1.86f, 0.22f), P(0.06f, 0.025f, 0.02f), P(0, 0, -15));
            m.Box(Ink, P(0.08f, 1.86f, 0.22f), P(0.06f, 0.025f, 0.02f), P(0, 0, 15));
            m.Box(Silver, P(0, 1.72f, 0.21f), P(0.16f, 0.05f, 0.03f));
            return m.Finish();
        }

        // ---------------- Weapons (grip at origin, along +Y) ----------------

        public static LowPolyMesh Blade()
        {
            var m = new LowPolyMesh();
            m.Cylinder(Brown, P(0, -0.12f, 0), 0.03f, 0.22f, 6);
            m.Sphere(Gold, P(0, -0.13f, 0), P(0.045f, 0.045f, 0.045f), 0);
            m.Box(Gold, P(0, 0.12f, 0), P(0.26f, 0.05f, 0.06f));
            m.Box(Silver, P(0, 0.48f, 0), P(0.08f, 0.66f, 0.025f));
            m.Cylinder(Silver, P(0, 0.81f, 0), 0.057f, 0.14f, 4, 0f, P(0, 45, 0), P(1, 1, 0.3f));
            m.Box(0x9aa8d4, P(0, 0.45f, 0.014f), P(0.016f, 0.56f, 0.004f));
            return m.Finish();
        }

        public static LowPolyMesh DrumHammer()
        {
            var m = new LowPolyMesh();
            m.Cylinder(Brown, P(0, -0.15f, 0), 0.035f, 0.85f, 6);
            m.Cylinder(Red, P(-0.17f, 0.72f, 0), 0.15f, 0.34f, 10, -1, P(0, 0, -90));
            m.Cylinder(White, P(-0.18f, 0.72f, 0), 0.155f, 0.04f, 10, -1, P(0, 0, -90));
            m.Cylinder(White, P(0.14f, 0.72f, 0), 0.155f, 0.04f, 10, -1, P(0, 0, -90));
            m.Box(Gold, P(0, 0.72f, 0.155f), P(0.3f, 0.025f, 0.01f), P(0, 0, 25));
            return m.Finish();
        }

        public static LowPolyMesh LuteBow()
        {
            var m = new LowPolyMesh();
            m.Sphere(0xd8904a, P(0, 0.12f, 0), P(0.17f, 0.22f, 0.07f), 1);
            m.Sphere(DarkBrown, P(0, 0.14f, 0.055f), P(0.05f, 0.05f, 0.02f), 0);
            m.Box(Brown, P(0, 0.48f, 0), P(0.06f, 0.42f, 0.04f));
            m.Box(Brown, P(0, 0.72f, -0.03f), P(0.08f, 0.1f, 0.05f), P(-25, 0, 0));
            m.Box(Cream, P(0, 0.4f, 0.04f), P(0.012f, 0.62f, 0.006f));
            return m.Finish();
        }

        /// <summary>Lute Bow shot: a glowing golden eighth note.</summary>
        public static LowPolyMesh NoteShot()
        {
            var m = new LowPolyMesh().Glow(Gold);
            m.Sphere(Gold, P(0, 0, 0), P(0.12f, 0.09f, 0.1f), 0, P(0, 0, -20));
            m.Box(Gold, P(0.1f, 0.18f, 0), P(0.03f, 0.34f, 0.03f));
            m.Box(Gold, P(0.17f, 0.3f, 0), P(0.14f, 0.05f, 0.03f), P(0, 0, -35));
            return m.Finish();
        }

        // ---------------- Props ----------------

        public static LowPolyMesh LoreScroll()
        {
            var m = new LowPolyMesh();
            m.Cylinder(Cream, P(-0.3f, 0.12f, 0), 0.1f, 0.6f, 8, -1, P(0, 0, -90));
            m.Cylinder(Brown, P(-0.36f, 0.12f, 0), 0.12f, 0.06f, 8, -1, P(0, 0, -90));
            m.Cylinder(Brown, P(0.3f, 0.12f, 0), 0.12f, 0.06f, 8, -1, P(0, 0, -90));
            m.Box(Red, P(0, 0.12f, 0.1f), P(0.08f, 0.2f, 0.03f));
            return m.Finish();
        }

        /// <summary>Floating station marker: a glowing gold diamond.</summary>
        public static LowPolyMesh Marker()
        {
            var m = new LowPolyMesh().Glow(Gold);
            m.Cylinder(Gold, P(0, 0, 0), 0.18f, 0.28f, 4, 0f);
            m.Cylinder(Gold, P(0, 0, 0), 0.18f, 0.28f, 4, 0f, P(180, 0, 0));
            return m.Finish();
        }

        // ---------------- Building blocks (white; coloured by material) ----------------

        public static LowPolyMesh UnitBox() => new LowPolyMesh().Box(White, P(0, 0, 0), P(1, 1, 1)).Finish();
        public static LowPolyMesh UnitCylinder(int sides) => new LowPolyMesh().Cylinder(White, P(0, -0.5f, 0), 0.5f, 1f, sides).Finish();
        public static LowPolyMesh UnitCone(int sides) => new LowPolyMesh().Cylinder(White, P(0, 0, 0), 0.5f, 1f, sides, 0f).Finish();
        public static LowPolyMesh UnitRoof() => new LowPolyMesh().Roof(White, P(0, 0, 0), 1, 1, 1).Finish();
        public static LowPolyMesh UnitGem() => new LowPolyMesh().Sphere(White, P(0, 0, 0), P(0.5f, 0.5f, 0.5f), 1).Finish();
    }
}
