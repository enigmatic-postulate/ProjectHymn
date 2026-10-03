using System;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Original low-poly placeholder models for Project Hymn, built from simple parts.
    /// All face +Z with feet at y = 0 (1 unit = 1 metre). Weapons have the grip at the origin, pointing up +Y.
    /// M6 (Valor look): each colour region has a paint style for the painted texture, faces are painted on
    /// (eyes, brows, blush) rather than modelled, and proportions are Dragon Valor-like: bigger heads,
    /// chunky shoulders, oversized weapons. The First Conductor is the one high-budget model (~1,600 triangles).
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
            // Shoulders: chunky pads on every armor (Dragon Valor proportions)
            m.Sphere(robeDark, P(-0.27f, 1.05f, 0), P(0.14f, 0.09f, 0.14f), 0);
            m.Sphere(robeDark, P(0.27f, 1.05f, 0), P(0.14f, 0.09f, 0.14f), 0);
            // Head (slightly large), hood, painted face
            m.Sphere(Skin, P(0, 1.31f, 0.02f), P(0.24f, 0.25f, 0.23f), 1);
            m.Sphere(robe, P(0, 1.37f, -0.06f), P(0.28f, 0.29f, 0.27f), 1);
            m.Cylinder(robe, P(0, 1.55f, -0.1f), 0.14f, 0.24f, 6, 0f, P(-35, 0, 0)); // hood tip
            m.Paint(DecalKind.Eye, Skin, -0.088f, 1.32f, 0.062f, 0.1f);
            m.Paint(DecalKind.Eye, Skin, 0.088f, 1.32f, 0.062f, 0.1f);
            m.Paint(DecalKind.Brow, Skin, -0.09f, 1.405f, 0.07f, 0.014f, -6f, 0x5a3a24);
            m.Paint(DecalKind.Brow, Skin, 0.09f, 1.405f, 0.07f, 0.014f, 6f, 0x5a3a24);
            m.Paint(DecalKind.Blush, Skin, -0.145f, 1.245f, 0.075f, 0.04f, 0f, 0xff7a8a);
            m.Paint(DecalKind.Blush, Skin, 0.145f, 1.245f, 0.075f, 0.04f, 0f, 0xff7a8a);
            m.Paint(DecalKind.Mouth, Skin, 0f, 1.215f, 0.05f, 0.012f, 0f, 0x8a3a3a);
            // Music-note pendant
            m.Sphere(trim, P(0.03f, 0.95f, 0.21f), P(0.035f, 0.03f, 0.02f), 0);
            m.Box(trim, P(0.06f, 1.0f, 0.21f), P(0.015f, 0.1f, 0.015f));

            switch (armor)
            {
                case Armor.BrassPlate:
                    m.Box(Gold, P(0, 0.93f, 0.08f), P(0.44f, 0.3f, 0.22f));
                    m.Box(0xd89a10, P(0, 0.93f, 0.195f), P(0.12f, 0.2f, 0.02f));
                    m.Sphere(Gold, P(-0.3f, 1.08f, 0), P(0.17f, 0.11f, 0.17f), 0);
                    m.Sphere(Gold, P(0.3f, 1.08f, 0), P(0.17f, 0.11f, 0.17f), 0);
                    break;
                case Armor.MetronomeVest:
                    m.Box(White, P(0, 0.93f, 0.06f), P(0.36f, 0.26f, 0.2f));
                    m.Box(Teal, P(0, 0.93f, 0.165f), P(0.04f, 0.24f, 0.02f));
                    // Little metronome on the back: pyramid body and pendulum
                    m.Cylinder(DarkBrown, P(0, 0.82f, -0.26f), 0.12f, 0.36f, 4, 0.03f);
                    m.Box(Gold, P(0.03f, 1.0f, -0.36f), P(0.02f, 0.26f, 0.02f), P(0, 0, -14));
                    break;
            }
            m.Style(PaintStyle.Cloth, robe, robeDark, White, Teal).Style(PaintStyle.Metal, trim, Gold, 0xd89a10)
             .Style(PaintStyle.Skin, Skin).Style(PaintStyle.Wood, DarkBrown);
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
            m.Style(PaintStyle.Gem, Green, GreenLight).Style(PaintStyle.Metal, Gold);
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
            m.Style(PaintStyle.Skin, Orange).Style(PaintStyle.Cloth, Red, White).Style(PaintStyle.Wood, Cream)
             .Style(PaintStyle.Metal, Gold).Style(PaintStyle.Hair, 0x8c1020);
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
            m.Box(Steel, P(-0.45f, 1.15f, 0), P(0.36f, 0.22f, 0.5f), P(0, 0, 12));
            m.Box(Steel, P(0.45f, 1.15f, 0), P(0.36f, 0.22f, 0.5f), P(0, 0, -12));
            m.Box(SteelDark, P(-0.42f, 0.8f, 0.04f), P(0.16f, 0.4f, 0.18f));
            m.Box(SteelDark, P(0.42f, 0.8f, 0.04f), P(0.16f, 0.4f, 0.18f));
            m.Box(Steel, P(0, 1.42f, 0), P(0.36f, 0.4f, 0.38f));
            m.Box(Cyan, P(0, 1.44f, 0.195f), P(0.26f, 0.06f, 0.02f));
            m.Cylinder(Red, P(0, 1.62f, -0.04f), 0.05f, 0.16f, 5, 0.12f);
            // Tuba: coiled body and a big bell over the shoulder
            m.Cylinder(Gold, P(0.05f, 0.8f, -0.32f), 0.16f, 0.5f, 8);
            m.Cylinder(Gold, P(0.18f, 1.2f, -0.32f), 0.07f, 0.4f, 6, 0.32f, P(0, 0, -25));
            m.Cylinder(0xb07a08, P(0.35f, 1.56f, -0.32f), 0.3f, 0.04f, 8, 0.3f, P(0, 0, -25));
            m.Style(PaintStyle.Metal, Steel, SteelDark, Gold, 0xb07a08).Style(PaintStyle.Cloth, Red);
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
            m.Style(PaintStyle.Hair, Purple, 0xd8a8ff).Style(PaintStyle.Cloth, PurpleDark).Style(PaintStyle.Metal, Silver);
            return m.Finish();
        }

        /// <summary>
        /// The First Conductor (M6 boss model, ~1,600 triangles): a tall tailcoated maestro with gold epaulettes, a
        /// high-collared cape, wild white hair, a painted stern face and a raised glowing baton.
        /// </summary>
        public static LowPolyMesh FirstConductor()
        {
            const uint Coat = 0x2a2448, CoatDark = 0x1a1630, Cape = 0x6a1424, CapeLining = 0xa8283a, Brass = 0xe0b040;
            var m = new LowPolyMesh().Glow(Gold);
            // Legs and shoes
            m.Cylinder(Black, P(-0.16f, 0.08f, 0), 0.1f, 0.78f, 8, 0.11f);
            m.Cylinder(Black, P(0.16f, 0.08f, 0), 0.1f, 0.78f, 8, 0.11f);
            m.Box(Ink, P(-0.16f, 0.05f, 0.05f), P(0.2f, 0.1f, 0.36f));
            m.Box(Ink, P(0.16f, 0.05f, 0.05f), P(0.2f, 0.1f, 0.36f));
            // Tailcoat: torso, lapels, buttons, layered swallow tails
            m.Cylinder(Coat, P(0, 0.78f, 0), 0.36f, 0.9f, 12, 0.3f);
            m.Cylinder(CoatDark, P(0, 0.78f, 0), 0.37f, 0.08f, 12, 0.36f);
            m.Box(White, P(0, 1.32f, 0.27f), P(0.2f, 0.42f, 0.04f));
            m.Box(CoatDark, P(-0.13f, 1.34f, 0.28f), P(0.09f, 0.4f, 0.03f), P(0, 0, -14));
            m.Box(CoatDark, P(0.13f, 1.34f, 0.28f), P(0.09f, 0.4f, 0.03f), P(0, 0, 14));
            for (int i = 0; i < 4; i++) m.Sphere(Brass, P(0, 0.92f + i * 0.1f, 0.305f), P(0.025f, 0.025f, 0.02f), 0);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(Coat, P(side * 0.13f, 0.6f, -0.3f), P(0.17f, 0.62f, 0.06f), P(-12, 0, side * -6));
                m.Box(CoatDark, P(side * 0.14f, 0.32f, -0.36f), P(0.15f, 0.3f, 0.05f), P(-18, 0, side * -10));
            }
            m.Box(Red, P(-0.07f, 1.52f, 0.28f), P(0.11f, 0.08f, 0.04f), P(0, 0, 10));
            m.Box(Red, P(0.07f, 1.52f, 0.28f), P(0.11f, 0.08f, 0.04f), P(0, 0, -10));
            m.Sphere(Red, P(0, 1.52f, 0.3f), P(0.03f, 0.03f, 0.03f), 0);
            // Cape behind (three flared panels) and a high collar standing up behind the head
            m.Box(Cape, P(0, 0.98f, -0.4f), P(0.6f, 1.2f, 0.05f), P(-7, 0, 0));
            m.Box(Cape, P(-0.36f, 0.98f, -0.3f), P(0.3f, 1.16f, 0.05f), P(-6, -38, 4));
            m.Box(Cape, P(0.36f, 0.98f, -0.3f), P(0.3f, 1.16f, 0.05f), P(-6, 38, -4));
            m.Box(CapeLining, P(0, 0.98f, -0.375f), P(0.56f, 1.16f, 0.02f), P(-7, 0, 0));
            m.Box(CapeLining, P(0, 1.78f, -0.2f), P(0.62f, 0.4f, 0.05f), P(18, 0, 0));
            m.Box(CapeLining, P(-0.3f, 1.76f, -0.1f), P(0.2f, 0.36f, 0.05f), P(14, -55, 0));
            m.Box(CapeLining, P(0.3f, 1.76f, -0.1f), P(0.2f, 0.36f, 0.05f), P(14, 55, 0));
            // Epaulettes with fringe
            for (int side = -1; side <= 1; side += 2)
            {
                m.Sphere(Brass, P(side * 0.36f, 1.56f, 0), P(0.16f, 0.07f, 0.16f), 1);
                for (int f = 0; f < 5; f++)
                {
                    float a = (f - 2) * 0.06f;
                    m.Cylinder(0xd89a10, P(side * (0.44f + 0.02f * Math.Abs(f - 2)), 1.44f, a), 0.012f, 0.12f, 4);
                }
            }
            // Arms: left down, right raised with the baton
            m.Cylinder(Coat, P(-0.36f, 1.52f, 0), 0.085f, 0.62f, 8, 0.07f, P(0, 0, 165));
            m.Cylinder(White, P(-0.4f, 0.98f, 0.01f), 0.075f, 0.06f, 8, 0.075f, P(0, 0, 165));
            m.Sphere(White, P(-0.42f, 0.88f, 0.02f), P(0.085f, 0.085f, 0.085f), 1);
            m.Cylinder(Coat, P(0.36f, 1.52f, 0), 0.085f, 0.62f, 8, 0.07f, P(-20, 0, -35));
            m.Sphere(White, P(0.69f, 2.0f, 0.2f), P(0.085f, 0.085f, 0.085f), 1);
            m.Cylinder(Gold, P(0.69f, 2.0f, 0.2f), 0.045f, 0.85f, 8, 0.02f, P(-30, 0, -20));
            m.Sphere(Gold, P(0.84f, 2.7f, 0.62f), P(0.06f, 0.06f, 0.06f), 1);
            // Head (high detail), wild hair, mustache, painted stern face
            m.Sphere(Skin, P(0, 1.84f, 0.02f), P(0.25f, 0.28f, 0.25f), 2);
            m.Sphere(White, P(-0.22f, 1.96f, -0.06f), P(0.19f, 0.21f, 0.21f), 1);
            m.Sphere(White, P(0.22f, 1.96f, -0.06f), P(0.19f, 0.21f, 0.21f), 1);
            m.Sphere(White, P(0, 2.12f, -0.1f), P(0.24f, 0.17f, 0.22f), 1);
            m.Sphere(White, P(-0.12f, 2.16f, 0.05f), P(0.13f, 0.1f, 0.12f), 1);
            m.Sphere(White, P(0.14f, 2.15f, 0.04f), P(0.12f, 0.1f, 0.12f), 1);
            m.Cylinder(Silver, P(-0.02f, 1.73f, 0.24f), 0.04f, 0.14f, 5, 0f, P(0, 0, 105));
            m.Cylinder(Silver, P(0.02f, 1.73f, 0.24f), 0.04f, 0.14f, 5, 0f, P(0, 0, -105));
            m.Paint(DecalKind.Brow, Skin, -0.09f, 1.94f, 0.1f, 0.024f, -18f, 0x2a2030);
            m.Paint(DecalKind.Brow, Skin, 0.09f, 1.94f, 0.1f, 0.024f, 18f, 0x2a2030);
            m.Paint(DecalKind.Eye, Skin, -0.085f, 1.875f, 0.065f, 0.06f, 0f, 0x2a1020);
            m.Paint(DecalKind.Eye, Skin, 0.085f, 1.875f, 0.065f, 0.06f, 0f, 0x2a1020);
            m.Paint(DecalKind.Blush, Skin, 0f, 1.8f, 0.05f, 0.05f, 0f, 0xd86a6a);
            m.Style(PaintStyle.Cloth, Coat, CoatDark, Cape, CapeLining, Red, White).Style(PaintStyle.Hair, White)
             .Style(PaintStyle.Metal, Gold, Brass, 0xd89a10, Silver).Style(PaintStyle.Skin, Skin);
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
            m.Style(PaintStyle.Metal, Silver, Gold, 0x9aa8d4).Style(PaintStyle.Wood, Brown);
            return m.Finish().Scaled(1.4f);
        }

        public static LowPolyMesh DrumHammer()
        {
            var m = new LowPolyMesh();
            m.Cylinder(Brown, P(0, -0.15f, 0), 0.035f, 0.85f, 6);
            m.Cylinder(Red, P(-0.17f, 0.72f, 0), 0.15f, 0.34f, 10, -1, P(0, 0, -90));
            m.Cylinder(White, P(-0.18f, 0.72f, 0), 0.155f, 0.04f, 10, -1, P(0, 0, -90));
            m.Cylinder(White, P(0.14f, 0.72f, 0), 0.155f, 0.04f, 10, -1, P(0, 0, -90));
            m.Box(Gold, P(0, 0.72f, 0.155f), P(0.3f, 0.025f, 0.01f), P(0, 0, 25));
            m.Style(PaintStyle.Wood, Brown).Style(PaintStyle.Cloth, Red, White).Style(PaintStyle.Metal, Gold);
            return m.Finish().Scaled(1.3f);
        }

        public static LowPolyMesh LuteBow()
        {
            var m = new LowPolyMesh();
            m.Sphere(0xd8904a, P(0, 0.12f, 0), P(0.17f, 0.22f, 0.07f), 1);
            m.Sphere(DarkBrown, P(0, 0.14f, 0.055f), P(0.05f, 0.05f, 0.02f), 0);
            m.Box(Brown, P(0, 0.48f, 0), P(0.06f, 0.42f, 0.04f));
            m.Box(Brown, P(0, 0.72f, -0.03f), P(0.08f, 0.1f, 0.05f), P(-25, 0, 0));
            m.Box(Cream, P(0, 0.4f, 0.04f), P(0.012f, 0.62f, 0.006f));
            m.Style(PaintStyle.Wood, 0xd8904a, DarkBrown, Brown);
            return m.Finish().Scaled(1.25f);
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
            m.Style(PaintStyle.Cloth, Cream, Red).Style(PaintStyle.Wood, Brown);
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
