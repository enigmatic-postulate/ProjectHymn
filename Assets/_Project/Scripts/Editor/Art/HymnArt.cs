using System;
using System.Collections.Generic;
using C = ProjectHymn.EditorTools.PixelCanvas.Rgba;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Placeholder art for Project Hymn, generated from code: original 16-bit style characters and painted
    /// (pre-rendered) backgrounds. Pure C# so it can be previewed outside Unity; ArtGenerator saves it as PNGs.
    /// Swap any PNG for real art later without touching code.
    /// </summary>
    public static class HymnArt
    {
        static readonly C Clear = new C(0, 0, 0, 0);
        static readonly C Outline = C.Hex(0x1a1024);
        static readonly C White = C.Hex(0xf4f2fa);
        static readonly C EyeDark = C.Hex(0x14121e);
        static readonly C Skin = C.Hex(0xf2c9a0);
        static readonly C Gold = C.Hex(0xf0c040);

        static Dictionary<char, C> Pal(params (char, C)[] entries)
        {
            var d = new Dictionary<char, C> { ['k'] = Outline, ['w'] = White, ['e'] = EyeDark, ['s'] = Skin, ['g'] = Gold };
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

        // ---------------- Characters ----------------

        /// <summary>The Cantor, the player. Robe colour comes from the armor.</summary>
        public static PixelCanvas Player(C robe, C robeShade, C trim)
        {
            string[] art =
            {
                "kkkkkk",
                "kRRRRRRk",
                "kRrrrrrrRk",
                "kRhhhhhhRk",
                "kRssssssRk",
                "kRsessesRk",
                "kRssssssRk",
                "kRssssRk",
                "kkttrrttkk",
                "krrrrrrrrrrk",
                "ksrrrrRRrrrrsk",
                "ksrrrrRRrrrrsk",
                "krrrrRRrrrrk",
                "krrrrRRrrrrk",
                "krrrrrrrrrrk",
                "kttttttttttk",
                "kkkk..kkkk",
                "kbbk..kbbk",
                "kkkk..kkkk",
            };
            return PixelCanvas.FromArt(art, Pal(('r', robe), ('R', robeShade), ('t', trim), ('h', C.Hex(0x5a3a2a)), ('b', C.Hex(0x4a3020))));
        }

        /// <summary>Metronome Slime: a green blob with a little pendulum on top.</summary>
        public static PixelCanvas Slime() => PixelCanvas.FromArt(new[]
        {
            "k",
            "k",
            "kok",
            "kkkkkkk",
            "kgggggggk",
            "kgggggggggk",
            "kggweggggwegk",
            "kgggggggggggk",
            "kgggggmmmgggggk",
            "kgggggggggggggk",
            "kllgggggggggllk",
            "kkkkkkkkkkkkk",
        }, Pal(('g', C.Hex(0x5ac86e)), ('l', C.Hex(0x96eba0)), ('m', C.Hex(0x1e5a32)), ('o', Gold)));

        /// <summary>Snare Imp: orange, horned, drum belt, drumsticks.</summary>
        public static PixelCanvas Imp() => PixelCanvas.FromArt(new[]
        {
            "k...........k",
            "kk.........kk",
            "kk.......kk",
            "kkkkkkkkk",
            "koooooooook",
            "kooweooooweok",
            "koooooooooook",
            "koooommmooook",
            "koooooooook",
            "kdydydydk",
            "t..kdydydydk..t",
            "t.kdddddddk.t",
            "tkoooooookt",
            "kook.kook",
            "kkkk.kkkk",
        }, Pal(('o', C.Hex(0xf08c32)), ('d', C.Hex(0xc8323c)), ('y', C.Hex(0xf0dca0)), ('m', C.Hex(0x6e2814)), ('t', C.Hex(0xe6c88c))));

        /// <summary>Tuba Knight: steel armour, glowing visor, a brass horn on one shoulder.</summary>
        public static PixelCanvas Knight() => PixelCanvas.FromArt(new[]
        {
            "kkkkk",
            "kaaaaak",
            "kaaaaaaak",
            "kakkkkkak",
            "kakvvvkak",
            "kaaaaaaak",
            "kAAAAAk",
            "kkkkaaaaaaakkkk",
            "kbbbkaaaaaaakaaak",
            "kbbbbkaaAaAaakaaaak",
            "kbbbbkaaaaaaakaaaak",
            "kkk.kaaaaaaak.kkk",
            "kaaaaaaak",
            "kAAAAAAAk",
            "kaaa.aaak",
            "kaaa.aaak",
            "kaak.kaak",
            "kkkkk.kkkkk",
        }, Pal(('a', C.Hex(0x96a5d2)), ('A', C.Hex(0x5a648c)), ('v', C.Hex(0x78f0ff)), ('b', C.Hex(0xdcaa3c))));

        /// <summary>Piccolo Bat: purple wings, a silver flute for a beak.</summary>
        public static PixelCanvas Bat() => PixelCanvas.FromArt(new[]
        {
            "k...............k",
            "kk.............kk",
            "kpk...kkkkk...kpk",
            "kppk.kpppppk.kppk",
            "kpppkpwepwepkpppk",
            "kpppppppppppppk",
            "kkpppfffpppkk",
            "kkk.f.kkk",
            "f",
        }, Pal(('p', C.Hex(0xaa64e6)), ('f', C.Hex(0xdce1eb))));

        /// <summary>The First Conductor: white hair, tailcoat, red bow tie, gold baton raised.</summary>
        public static PixelCanvas Conductor()
        {
            string[] art =
            {
                "kkkkkkk",
                "kwwwwwwwk",
                "kwwwwwwwwwk",
                "kwssssssswk",
                "ksesssssesk",
                "ksssssssssk",
                "kssmmmmmssk",
                "ksssssssk",
                "kkrrrkk",
                "kkcccwwwccckk",
                "kccccwwrwwcccck",
                "kccccccwwwcccccck",
                "ksccccccwwwccccccsk",
                "ksccccccwwwccccccsk",
                "kccccccwwwcccccck",
                "kccccccccccccccck",
                "kccccccc.ccccccck",
                "kcccccck.kcccccck",
                "kccccck...kccccck",
                "kpppk.....kpppk",
                "kpppk.....kpppk",
                "kbbbbk...kbbbbk",
                "kkkkkk...kkkkkk",
            };
            var pal = Pal(('m', C.Hex(0xb4b4c0)), ('r', C.Hex(0xd2283c)), ('c', C.Hex(0x23203a)), ('p', C.Hex(0x3a3650)), ('b', C.Hex(0x101018)));
            var body = PixelCanvas.FromArt(art, pal);
            var c = new PixelCanvas(body.Width + 8, body.Height + 4);
            c.Stamp(ToRows(body), CharMapIdentity(body), 1, 4);
            // Baton from the raised right hand, with a bright tip.
            int hx = 1 + body.Width - 2, hy = 4 + 12;
            c.Line(hx, hy, hx + 5, hy - 12, Gold);
            c.Line(hx + 1, hy, hx + 6, hy - 12, C.Hex(0xb48a20));
            c.Set(hx + 5, hy - 13, White); c.Set(hx + 6, hy - 13, White);
            return c;
        }

        // ---------------- Items ----------------

        public static PixelCanvas Blade() => PixelCanvas.FromArt(new[]
        {
            "k", "kwk", "kwk", "kwk", "kwk", "kwk", "kwk", "kgggk", "b", "b", "kbk",
        }, Pal(('w', C.Hex(0xd8dcf0)), ('b', C.Hex(0x6e4628))));

        public static PixelCanvas DrumHammer() => PixelCanvas.FromArt(new[]
        {
            "kkkkkkkkk", "krrrrrrrk", "kwwwwwwwk", "krrrrrrrk", "kkkkkkkkk", "b", "b", "b", "b", "b", "kbk",
        }, Pal(('r', C.Hex(0xc83232)), ('b', C.Hex(0x6e4628))));

        public static PixelCanvas LuteBow() => PixelCanvas.FromArt(new[]
        {
            "k", "kbk", "kbk", "kbk", "kbsbk", "kbbsbbk", "kbbbsbbbk", "kbbkskbbk", "kbbbsbbbk", "kbbbbbk", "kkkkk",
        }, Pal(('b', C.Hex(0xb4783c)), ('s', C.Hex(0xf0e6c8))));

        /// <summary>Lute Bow shot: a small glowing eighth note.</summary>
        public static PixelCanvas NoteShot() => PixelCanvas.FromArt(new[]
        {
            "...kk..", "...kgk.", "...k.gk", "...k...", ".kkk...", "kgggk..", "kgggk..", ".kkk...",
        }, Pal());

        public static PixelCanvas LoreScroll() => PixelCanvas.FromArt(new[]
        {
            "kkkkkkkkkk",
            "kppppppppppk",
            "kplllllllpk",
            "kpllllllllpk",
            "kplllllllpk",
            "kppppppppppk",
            "kkkkkkkkkk",
        }, Pal(('p', C.Hex(0xc8a064)), ('l', C.Hex(0xf0dcb4))));

        /// <summary>Floating marker over town stations.</summary>
        public static PixelCanvas Marker() => PixelCanvas.FromArt(new[]
        {
            "k", "kgk", "kgggk", "kgggggk", "kgggk", "kgk", "k",
        }, Pal());

        // ---------------- Backgrounds ----------------

        /// <summary>
        /// The town of Cadence at dusk, pre-rendered: shops and the Hall of Measures along the top, three arena
        /// gates on the right, a cobbled plaza with a round stage below. 480 x 270 px (30 x 16.9 units at 16 PPU).
        /// Walkable plaza: y 118..262 px.
        /// </summary>
        public static PixelCanvas Town()
        {
            const int W = 480, H = 270, Base = 118;
            var c = new PixelCanvas(W, H);
            var rng = new Random(1234);

            // Sky
            c.VGradient(0, 0, W, 60, C.Hex(0x14102e), C.Hex(0x5a2870));
            c.VGradient(0, 60, W, 40, C.Hex(0x5a2870), C.Hex(0xf08c5a));
            for (int i = 0; i < 70; i++) c.Set(rng.Next(W), rng.Next(50), rng.Next(4) == 0 ? C.Hex(0xfff0c8) : C.Hex(0xb4aad2));
            c.Disc(404, 26, 11, C.Hex(0xfff2c0));
            c.Disc(409, 23, 10, C.Hex(0x2a1a48));

            // Distant hills
            for (int x = 0; x < W; x++)
            {
                int h = 82 + (int)(6 * Math.Sin(x * 0.025) + 4 * Math.Sin(x * 0.07 + 1.3));
                for (int y = h; y < Base; y++) c.Set(x, y, C.Hex(0x3a2450));
            }

            // Buildings
            House(c, 0, 36, Base, 50, C.Hex(0x6e5a78), C.Hex(0x4a2a3a), rng);
            // The Luthier (shop): wood front, red roof, a note on the sign
            House(c, 44, 118, Base, 62, C.Hex(0x82553a), C.Hex(0xaa3c32), rng, door: 74);
            c.Rect(62, Base - 48, 38, 14, C.Hex(0x3a2418));
            c.Rect(64, Base - 46, 34, 10, C.Hex(0xe6c88c));
            var signNote = NoteShot();
            for (int y = 0; y < signNote.Height; y++)
                for (int x = 0; x < signNote.Width; x++)
                    if (signNote.Get(x, y).A > 0) c.Set(77 + x, Base - 45 + y, C.Hex(0x3a2418));
            // Wardrobe house
            House(c, 126, 186, Base, 54, C.Hex(0x466e78), C.Hex(0x283c50), rng, door: 150);
            // Hall of Measures: stone, columns, pediment, organ pipes
            c.Rect(196, Base - 80, 90, 80, C.Hex(0xbeb4c8));
            for (int i = 0; i < 6; i++) { int x = 202 + i * 14; c.Rect(x, Base - 70, 6, 70, C.Hex(0xdcd2e6)); c.Rect(x + 5, Base - 70, 1, 70, C.Hex(0x968caa)); }
            c.Roof(192, 290, Base - 104, Base - 80, C.Hex(0xa096b4), Outline);
            for (int i = 0; i < 9; i++) { int x = 214 + i * 7, h = 10 + (4 - Math.Abs(i - 4)) * 5; c.Rect(x, Base - 104 - h + 8, 4, h, Gold); c.Rect(x + 3, Base - 104 - h + 8, 1, h, C.Hex(0xb48a20)); }
            c.Rect(228, Base - 30, 24, 30, C.Hex(0x3a2a48));
            c.Rect(239, Base - 30, 2, 30, C.Hex(0x281c34));
            House(c, 292, 332, Base, 48, C.Hex(0x7a6488), C.Hex(0x4a3a5a), rng);
            // Arena wall with three gates (the last is the boss gate)
            c.Rect(334, Base - 58, W - 334, 58, C.Hex(0x786e8c));
            for (int y = Base - 58; y < Base; y += 8) for (int x = 334 + ((y / 8) % 2) * 8; x < W; x += 16) c.Rect(x, y, 1, 8, C.Hex(0x5a5070));
            Gate(c, 355, Base, 13, 40, C.Hex(0x2a1e3a), Gold);
            Gate(c, 400, Base, 13, 40, C.Hex(0x2a1e3a), Gold);
            Gate(c, 448, Base, 17, 50, C.Hex(0x3a0e1a), C.Hex(0xe0404a));

            // Plaza cobbles
            for (int y = Base; y < H; y++)
            {
                int row = (y - Base) / 6;
                for (int x = 0; x < W; x++)
                {
                    int bx = (x + (row % 2) * 6) / 12;
                    bool mortar = (y - Base) % 6 == 5 || (x + (row % 2) * 6) % 12 == 11;
                    float n = Hash(bx, row) * 0.16f - 0.08f;
                    var baseCol = C.Hex(0x6e6478).Scale(1f + n);
                    c.Set(x, y, mortar ? C.Hex(0x463e50) : baseCol);
                }
            }
            // Lighter paths from each door
            foreach (int dx in new[] { 81, 156, 240, 355, 400, 448 })
                for (int y = Base; y < Base + 26; y++) for (int x = dx - 8; x <= dx + 8; x++)
                    { var p = c.Get(x, y); c.Set(x, y, new C(p.R, p.G, p.B).Scale(1.18f)); }
            // Round stage
            c.Disc(240, 196, 36, C.Hex(0x96694a));
            for (int r = 34; r > 4; r -= 6) c.Ring(240, 196, r, 1, C.Hex(0x78503a));
            c.Ring(240, 196, 36, 2, Gold);
            // Flower beds
            foreach (var (fx, fy) in new[] { (34, 246), (446, 246), (168, 252), (312, 252) }) Flowers(c, fx, fy, rng);
            // Lamp posts with warm halos
            foreach (int lx in new[] { 190, 290 })
            {
                c.Rect(lx, Base + 2, 2, 24, Outline);
                c.Rect(lx - 2, Base, 6, 4, C.Hex(0xffdc82));
                for (int r = 14; r > 0; r -= 2) c.Disc(lx + 1, Base + 2, r, new C(255, 210, 120, 12));
            }
            // Vignette at the bottom
            for (int y = H - 16; y < H; y++) for (int x = 0; x < W; x++) c.Set(x, y, new C(0, 0, 0, (byte)((y - (H - 16)) * 8)));
            return c;
        }

        static void House(PixelCanvas c, int x0, int x1, int baseY, int h, C wall, C roof, Random rng, int door = -1)
        {
            c.Rect(x0, baseY - h, x1 - x0, h, wall);
            c.Rect(x0, baseY - h, 1, h, Outline); c.Rect(x1 - 1, baseY - h, 1, h, Outline);
            c.Roof(x0 - 4, x1 + 4, baseY - h - 22, baseY - h, roof, Outline);
            for (int wx = x0 + 6; wx < x1 - 10; wx += 16)
                for (int wy = baseY - h + 8; wy < baseY - 18; wy += 16)
                {
                    bool lit = rng.Next(3) != 0;
                    c.Rect(wx, wy, 8, 8, Outline);
                    c.Rect(wx + 1, wy + 1, 6, 6, lit ? C.Hex(0xffd278) : C.Hex(0x3c3250));
                    c.Rect(wx + 4, wy + 1, 1, 6, Outline);
                }
            if (door >= 0)
            {
                c.Rect(door, baseY - 20, 14, 20, Outline);
                c.Rect(door + 1, baseY - 19, 12, 19, C.Hex(0x5a3a24));
                c.Set(door + 10, baseY - 9, Gold);
            }
        }

        static void Gate(PixelCanvas c, int cx, int baseY, int halfW, int h, C inside, C trim)
        {
            for (int y = baseY - h; y < baseY; y++)
            {
                float t = (y - (baseY - h)) / (float)halfW;
                int w = y < baseY - h + halfW ? (int)(halfW * Math.Sqrt(Math.Max(0, 1 - Math.Pow(1 - t, 2)))) : halfW;
                for (int x = cx - w; x <= cx + w; x++) c.Set(x, y, (x == cx - w || x == cx + w) ? trim : inside);
            }
            c.Rect(cx - halfW - 1, baseY - 2, halfW * 2 + 3, 2, trim);
        }

        static void Flowers(PixelCanvas c, int x, int y, Random rng)
        {
            c.Rect(x - 14, y - 6, 28, 10, C.Hex(0x2e5a32));
            c.Rect(x - 14, y + 4, 28, 1, Outline);
            for (int i = 0; i < 14; i++)
            {
                var col = (i % 3) switch { 0 => C.Hex(0xff8cc8), 1 => C.Hex(0xfff08c), _ => C.Hex(0xb48cff) };
                c.Set(x - 12 + rng.Next(25), y - 5 + rng.Next(8), col);
            }
        }

        public enum ArenaStyle { Practice, Garden, Boss }

        /// <summary>Arena backdrop: a stage floor with a musical staff running across it. 288 x 160 px (18 x 10 units).</summary>
        public static PixelCanvas Arena(ArenaStyle style)
        {
            const int W = 288, H = 160;
            var c = new PixelCanvas(W, H);
            (C floorA, C floorB, C line, C accent) = style switch
            {
                ArenaStyle.Garden => (C.Hex(0x1e3a2c), C.Hex(0x234434), C.Hex(0x8cdca0), C.Hex(0xff8cc8)),
                ArenaStyle.Boss => (C.Hex(0x2a121c), C.Hex(0x321620), Gold, C.Hex(0xe0404a)),
                _ => (C.Hex(0x221a36), C.Hex(0x281e40), Gold, C.Hex(0x78f0ff)),
            };

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    c.Set(x, y, ((y / 10) % 2 == 0) ? floorA : floorB);
            for (int y = 0; y < H; y += 10) for (int x = 0; x < W; x++) c.Set(x, y, floorA.Scale(0.75f));

            // Musical staff across the floor
            for (int i = 0; i < 5; i++) for (int x = 8; x < W - 8; x++) c.Set(x, 56 + i * 12, new C(line.R, line.G, line.B, 70));
            // Faint notes on the staff
            var rng = new Random(style.GetHashCode() + 7);
            var note = NoteShot();
            for (int i = 0; i < 14; i++)
            {
                int nx = 20 + rng.Next(W - 40), ny = 46 + rng.Next(5) * 6;
                for (int y = 0; y < note.Height; y++) for (int x = 0; x < note.Width; x++)
                {
                    var p = note.Get(x, y);
                    if (p.A > 0) c.Set(nx + x, ny + y, new C(line.R, line.G, line.B, 60));
                }
            }

            if (style == ArenaStyle.Garden)
                for (int i = 0; i < 40; i++) c.Set(rng.Next(W), rng.Next(H), new C(accent.R, accent.G, accent.B, 140));

            if (style == ArenaStyle.Boss)
            {
                // Red curtains with folds, gold trim
                for (int x = 0; x < 30; x++)
                {
                    var col = C.Hex(0x8c1428).Scale(0.75f + 0.25f * (float)Math.Abs(Math.Sin(x * 0.45)));
                    for (int y = 0; y < H; y++) { c.Set(x, y, col); c.Set(W - 1 - x, y, col); }
                }
                c.Rect(0, 0, W, 6, C.Hex(0x8c1428));
                c.Rect(0, 6, W, 2, Gold);
            }

            // Spotlight and vignette
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - W / 2f) / (W / 2f), dy = (y - H / 2f) / (H / 2f);
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d < 0.55f) c.Set(x, y, new C(255, 240, 210, (byte)((0.55f - d) * 40)));
                    if (d > 0.8f) c.Set(x, y, new C(0, 0, 0, (byte)Math.Min(200, (d - 0.8f) * 260)));
                }
            return c;
        }

        // ---------------- helpers ----------------

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h & 0xffff) / 65535f;
            }
        }

        // Re-stamp a finished canvas into a bigger one (used for the boss + baton).
        static string[] ToRows(PixelCanvas src)
        {
            var rows = new string[src.Height];
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < src.Height; y++)
            {
                sb.Clear();
                for (int x = 0; x < src.Width; x++) sb.Append(src.Get(x, y).A == 0 ? '.' : (char)(0x100 + y * src.Width + x));
                rows[y] = sb.ToString();
            }
            return rows;
        }

        static IDictionary<char, C> CharMapIdentity(PixelCanvas src)
        {
            var d = new Dictionary<char, C>();
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var p = src.Get(x, y);
                    if (p.A > 0) d[(char)(0x100 + y * src.Width + x)] = p;
                }
            return d;
        }
    }
}
