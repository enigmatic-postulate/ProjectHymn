using System.Collections.Generic;

namespace ProjectHymn.EditorTools.Sword
{
    /// <summary>One keyframe: a time in beats relative to the hit (0 = the moment of impact, on the beat).</summary>
    public sealed class KeySpec
    {
        public float Beat;
        public (float x, float y, float z) Root;   // body offset in metres, character space (z = forward)
        public readonly Dictionary<string, (float x, float y, float z)> Bones = new Dictionary<string, (float, float, float)>();

        public KeySpec At(float beat) { Beat = beat; return this; }
        public KeySpec Move(float x, float y, float z) { Root = (x, y, z); return this; }
        public KeySpec B(string bone, float x, float y, float z) { Bones[bone] = (x, y, z); return this; }
        public KeySpec ArmR(float ux, float uy, float uz, float lx, float hx, float hy = 0f, float hz = 0f) =>
            B("UpperArm.R", ux, uy, uz).B("LowerArm.R", lx, 0, 0).B("Hand.R", hx, hy, hz);
        public KeySpec ArmR(float[] v) => ArmR(v[0], v[1], v[2], v[3], v[4], v[5]);
        public KeySpec ArmL(float[] v) => ArmL(v[0], v[1], v[2], v[3], v[4]);
        public KeySpec ArmL(float ux, float uy, float uz, float lx, float hx = 0f) =>
            B("UpperArm.L", ux, uy, uz).B("LowerArm.L", lx, 0, 0).B("Hand.L", hx, 0, 0);
        public KeySpec Torso(float hipY, float chestX, float chestY, float chestZ = 0f) =>
            B("Hips", 0, hipY, 0).B("Chest", chestX, chestY, chestZ);
        public KeySpec Legs(float rUp, float rLow, float lUp, float lLow) =>
            B("UpperLeg.R", rUp, 0, 4).B("LowerLeg.R", rLow, 0, 0).B("UpperLeg.L", lUp, 0, -4).B("LowerLeg.L", lLow, 0, 0);
    }

    /// <summary>
    /// One attack's keyframes. Times are in beats around the hit, so the same keys play faster on a faster song
    /// and the impact always lands on the beat. Bones a key doesn't list hold the weapon's stance.
    /// </summary>
    public sealed class MoveSpec
    {
        public string Id, Name;
        public readonly List<KeySpec> Keys = new List<KeySpec>();
        public float ActiveStart = -0.08f, ActiveEnd = 0.14f;   // sword trail and hit window (beats)
        public float Damage = 10f;
        public uint TrailColor = 0xffe08a;
        public string Effect = "cut";
        /// <summary>Two-handed weapons: let go with the left hand for this move (the punch).</summary>
        public bool OffHandFree;
        /// <summary>Where the hit lands in front of the player (metres) and how wide it is.</summary>
        public float Reach = 1.25f, Radius = 0.9f;

        public KeySpec Key(float beat) { var k = new KeySpec().At(beat); Keys.Add(k); return k; }
    }

    public sealed class SequenceSpec
    {
        public string Id, Name;
        public int[] Notes;            // 1 = light button, 2 = heavy button
        public string[] Moves;         // move id per step
        public float FinisherMultiplier = 2f;
        public uint Color;
    }

    public sealed class WeaponSpec
    {
        public string Id, Name;
        public bool TwoHanded;
        public KeySpec Stance;
        public List<MoveSpec> Moves = new List<MoveSpec>();
        public List<SequenceSpec> Sequences = new List<SequenceSpec>();
        public string LightFallbackA, LightFallbackB, HeavyFallback, Guard;
        public float BladeLength;
    }

    /// <summary>
    /// The authored attacks for the M2 prototype: a one-handed saber (side-on fencing stance, quick cuts) and a
    /// two-handed broadsword (square middle guard, bigger body turns and lunges, left hand on the hilt).
    /// Sequences: Crossing Cuts 1-2-1 (diagonal, horizontal, rising diagonal), Powerslice 2-2 (rising cut into a
    /// vertical power slice) and Tempest 1-1-1-2-1-1-1 (cut, slash, cut, quick punch, cut, slash, finishing cut).
    /// Pure data: the editor turns it into SwordMove / SwordSequence / WeaponSet assets that stay editable.
    /// </summary>
    public static class MoveSpecs
    {
        public static WeaponSpec Saber() => Build("Saber", "Saber", twoHanded: false, bladeLength: 0.85f, heft: 1f,
            stance: new KeySpec()
                .Torso(-22f, 0f, -8f).Move(0f, -0.03f, 0f)
                .ArmR(ArmPoses.Saber["Stance"])
                .ArmL(18f, 0f, -22f, -35f)
                .Legs(-22f, 18f, 16f, 22f)
                .B("Foot.R", 4, 0, 0).B("Foot.L", -18, 0, 0).B("Head", 0, 18, 0));

        public static WeaponSpec Broadsword() => Build("Broadsword", "Broadsword", twoHanded: true, bladeLength: 1.15f, heft: 1.3f,
            stance: new KeySpec()
                .Torso(-8f, 4f, -4f).Move(0f, -0.06f, 0f)
                .ArmR(ArmPoses.Broadsword["Stance"])
                .ArmL(-34f, 18f, -8f, -62f)
                .Legs(-18f, 22f, 14f, 24f)
                .B("Foot.R", 0, 0, 0).B("Foot.L", -14, 0, 0).B("Head", 0, 6, 0));

        static WeaponSpec Build(string id, string name, bool twoHanded, float bladeLength, float heft, KeySpec stance)
        {
            var w = new WeaponSpec { Id = id, Name = name, TwoHanded = twoHanded, Stance = stance, BladeLength = bladeLength };
            string P(string move) => $"{id}_{move}";
            var arms = twoHanded ? ArmPoses.Broadsword : ArmPoses.Saber;
            float[] A(string key) => arms[key];
            float H(float v) => v * heft;           // heavier weapon: bigger body turns and steps

            // ---- Diagonal cut: high right to low left ----
            var diagDown = new MoveSpec { Id = P("DiagonalCut"), Name = "Diagonal Cut", Damage = 10f * heft, TrailColor = 0xffe08a };
            diagDown.Key(-0.5f);
            diagDown.Key(-0.2f).ArmR(A("DiagonalCut@-0.2")).Torso(H(10f), -6f, H(32f)).Move(0, 0, -0.02f);
            diagDown.Key(0f).ArmR(A("DiagonalCut@0")).Torso(H(-18f), 10f, H(-34f)).Move(0, -0.04f, H(0.12f)).Legs(-30f, 26f, 18f, 24f);
            diagDown.Key(0.2f).ArmR(A("DiagonalCut@0.2")).Torso(H(-24f), 14f, H(-44f)).Move(0, -0.05f, H(0.14f)).Legs(-30f, 26f, 18f, 24f);
            diagDown.Key(0.5f);
            w.Moves.Add(diagDown);

            // ---- Horizontal slash: right to left at chest height ----
            var horiz = new MoveSpec { Id = P("HorizontalSlash"), Name = "Horizontal Slash", Damage = 12f * heft, TrailColor = 0x9ce8ff, Radius = 1.05f };
            horiz.Key(-0.5f);
            horiz.Key(-0.2f).ArmR(A("HorizontalSlash@-0.2")).Torso(H(18f), 0f, H(40f)).Move(0, -0.02f, 0);
            horiz.Key(0f).ArmR(A("HorizontalSlash@0")).Torso(H(-10f), 0f, H(-12f)).Move(0, -0.04f, H(0.1f)).Legs(-28f, 24f, 18f, 24f);
            horiz.Key(0.2f).ArmR(A("HorizontalSlash@0.2")).Torso(H(-26f), 0f, H(-46f)).Move(0, -0.04f, H(0.12f)).Legs(-28f, 24f, 18f, 24f);
            horiz.Key(0.5f);
            w.Moves.Add(horiz);

            // ---- Rising diagonal: low left to high right ----
            var diagUp = new MoveSpec { Id = P("RisingCut"), Name = "Rising Cut", Damage = 10f * heft, TrailColor = 0xffc070 };
            diagUp.Key(-0.5f);
            diagUp.Key(-0.2f).ArmR(A("RisingCut@-0.2")).Torso(H(-20f), 12f, H(-38f)).Move(0, -0.06f, 0);
            diagUp.Key(0f).ArmR(A("RisingCut@0")).Torso(H(6f), -6f, H(10f)).Move(0, -0.02f, H(0.1f)).Legs(-28f, 22f, 18f, 22f);
            diagUp.Key(0.2f).ArmR(A("RisingCut@0.2")).Torso(H(14f), -12f, H(28f)).Move(0, 0f, H(0.12f));
            diagUp.Key(0.5f);
            w.Moves.Add(diagUp);

            // ---- Rising guard (2-2, first note): an upward cut that ends with the blade raised overhead ----
            var rise = new MoveSpec { Id = P("RisingGuard"), Name = "Rising Guard", Damage = 8f * heft, TrailColor = 0xd0a0ff, ActiveStart = -0.1f, ActiveEnd = 0.1f };
            rise.Key(-0.5f);
            rise.Key(-0.2f).ArmR(A("RisingGuard@-0.2")).Torso(H(-6f), 18f, H(-6f)).Move(0, -0.1f, 0).Legs(-30f, 40f, 22f, 36f);
            rise.Key(0f).ArmR(A("RisingGuard@0")).Torso(H(0f), -10f, 0f).Move(0, 0f, H(0.04f));
            rise.Key(0.3f).ArmR(A("RisingGuard@0.3")).Torso(H(0f), -14f, 0f).Move(0, 0.02f, H(0.02f));
            rise.Key(0.5f).ArmR(A("RisingGuard@0.5")).Torso(H(0f), -14f, 0f).Move(0, 0.02f, 0f);
            w.Moves.Add(rise);

            // ---- Power slice (2-2, second note): overhead straight down with a lunge ----
            var power = new MoveSpec { Id = P("PowerSlice"), Name = "Power Slice", Damage = 22f * heft, TrailColor = 0xff7a5a, Effect = "power", Reach = 1.45f, Radius = 1.0f };
            power.Key(-0.5f).ArmR(A("PowerSlice@-0.5")).Torso(0f, -14f, 0f).Move(0, 0.02f, 0f);
            power.Key(-0.15f).ArmR(A("PowerSlice@-0.15")).Torso(0f, -22f, 0f).Move(0, 0.04f, -0.04f);
            power.Key(0f).ArmR(A("PowerSlice@0")).Torso(H(0f), H(22f), 0f).Move(0, -0.14f, H(0.3f)).Legs(-48f, 40f, 30f, 30f);
            power.Key(0.25f).ArmR(A("PowerSlice@0.25")).Torso(H(0f), H(30f), 0f).Move(0, -0.18f, H(0.32f)).Legs(-50f, 46f, 32f, 32f);
            power.Key(0.6f);
            w.Moves.Add(power);

            // ---- Quick punch (Tempest's 2): left jab, sword pulled back ----
            var punch = new MoveSpec { Id = P("Punch"), Name = "Quick Punch", Damage = 7f * heft, TrailColor = 0xffffff, Effect = "punch", OffHandFree = true, Reach = 0.9f, Radius = 0.7f, ActiveStart = -0.04f, ActiveEnd = 0.08f };
            punch.Key(-0.5f);
            punch.Key(-0.15f).ArmL(A("Punch.L@-0.15")).ArmR(A("Punch@-0.15")).Torso(H(14f), 0f, H(22f));
            punch.Key(0f).ArmL(A("Punch.L@0")).ArmR(A("Punch@0")).Torso(H(-20f), 4f, H(-30f)).Move(0, -0.03f, H(0.14f));
            punch.Key(0.15f).ArmL(A("Punch.L@0.15")).ArmR(A("Punch@0.15")).Torso(H(-18f), 4f, H(-26f)).Move(0, -0.03f, H(0.12f));
            punch.Key(0.45f);
            w.Moves.Add(punch);

            // ---- Chop (a heavy note that isn't part of a sequence step) ----
            var chop = new MoveSpec { Id = P("Chop"), Name = "Chop", Damage = 14f * heft, TrailColor = 0xffa060 };
            chop.Key(-0.5f);
            chop.Key(-0.2f).ArmR(A("Chop@-0.2")).Torso(H(6f), -10f, H(16f));
            chop.Key(0f).ArmR(A("Chop@0")).Torso(H(-6f), 16f, H(-10f)).Move(0, -0.08f, H(0.16f)).Legs(-34f, 30f, 22f, 26f);
            chop.Key(0.2f).ArmR(A("Chop@0.2")).Torso(H(-8f), 20f, H(-14f)).Move(0, -0.09f, H(0.16f));
            chop.Key(0.5f);
            w.Moves.Add(chop);

            // ---- Tempest finisher: the last rising cut, bigger ----
            var finish = new MoveSpec { Id = P("TempestFinish"), Name = "Tempest Finish", Damage = 14f * heft, TrailColor = 0x7affc8, Effect = "finish", Radius = 1.1f };
            finish.Key(-0.5f);
            finish.Key(-0.2f).ArmR(A("TempestFinish@-0.2")).Torso(H(-24f), 14f, H(-46f)).Move(0, -0.08f, 0).Legs(-30f, 34f, 20f, 30f);
            finish.Key(0f).ArmR(A("TempestFinish@0")).Torso(H(8f), -10f, H(16f)).Move(0, 0.02f, H(0.16f));
            finish.Key(0.25f).ArmR(A("TempestFinish@0.25")).Torso(H(20f), -18f, H(36f)).Move(0, 0.04f, H(0.18f));
            finish.Key(0.6f);
            w.Moves.Add(finish);

            // ---- Guard (vertical slice 2): blade raised flat overhead, knees bent, for parrying enemy hits ----
            var guard = new MoveSpec { Id = P("Guard"), Name = "Guard", Damage = 0f, ActiveStart = 1f, ActiveEnd = 0f, Effect = "guard" };
            guard.Key(-0.12f);
            guard.Key(0f).ArmR(A("RisingGuard@0.3")).Torso(0f, -8f, 0f).Move(0, -0.08f, -0.04f).Legs(-26f, 34f, 20f, 30f);
            guard.Key(0.45f).ArmR(A("RisingGuard@0.3")).Torso(0f, -8f, 0f).Move(0, -0.08f, -0.04f).Legs(-26f, 34f, 20f, 30f);
            guard.Key(0.75f);
            w.Moves.Add(guard);
            w.Guard = guard.Id;

            w.LightFallbackA = diagDown.Id;
            w.LightFallbackB = diagUp.Id;
            w.HeavyFallback = chop.Id;

            w.Sequences.Add(new SequenceSpec
            {
                Id = "CrossingCuts", Name = "Crossing Cuts", Notes = new[] { 1, 2, 1 }, Color = 0xffd27a,
                Moves = new[] { diagDown.Id, horiz.Id, diagUp.Id }, FinisherMultiplier = 1.8f,
            });
            w.Sequences.Add(new SequenceSpec
            {
                Id = "Powerslice", Name = "Powerslice", Notes = new[] { 2, 2 }, Color = 0xff7a5a,
                Moves = new[] { rise.Id, power.Id }, FinisherMultiplier = 1.6f,
            });
            w.Sequences.Add(new SequenceSpec
            {
                Id = "Tempest", Name = "Tempest", Notes = new[] { 1, 1, 1, 2, 1, 1, 1 }, Color = 0x7affc8,
                Moves = new[] { diagDown.Id, horiz.Id, diagUp.Id, punch.Id, diagDown.Id, horiz.Id, finish.Id }, FinisherMultiplier = 2.5f,
            });
            return w;
        }
    }
}
