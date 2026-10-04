using System.Collections.Generic;

namespace ProjectHymn.EditorTools.Sword
{
    /// <summary>
    /// The swordsman's movement poses for vertical slice 1 (pure data; SwordContent turns it into the Locomotion
    /// asset). Angles follow the rig: legs and arms hang down, negative X swings a limb forward, positive X on a
    /// knee or elbow bends it; Spine and Chest keys add to the weapon stance (lean), everything else listed replaces it.
    /// The sword arm is left out of every pose but the ledge ones, so the blade stays at the ready while moving.
    /// </summary>
    public static class LocomotionSpecs
    {
        public sealed class Set
        {
            public readonly List<KeySpec> Run = new List<KeySpec>();
            public KeySpec Jump, AirJump, Fall, Land, Hang, ClimbPull, ClimbOver;
        }

        static KeySpec K(float beat = 0f) => new KeySpec().At(beat);

        public static Set Build()
        {
            var s = new Set();

            // Run cycle, phase 0..1: right foot strikes at 0, left at 0.5. Squared hips with a little twist, a forward
            // lean, the free left arm pumping against the legs, a dip on each footfall and a lift in between.
            s.Run.Add(K(0f).B("Hips", 0, -8, 0).B("Chest", 12, 8, 0).Legs(-38, 16, 30, 62)
                .B("Foot.R", -12, 0, 0).B("Foot.L", 30, 0, 0).ArmL(-42, 0, -10, -58).B("Head", -8, 0, 0).Move(0, -0.035f, 0));
            s.Run.Add(K(0.25f).B("Hips", 0, 0, 0).B("Chest", 14, 0, 0).Legs(10, 28, -52, 105)
                .B("Foot.R", 12, 0, 0).B("Foot.L", 10, 0, 0).ArmL(-8, 0, -12, -64).B("Head", -8, 0, 0).Move(0, 0.05f, 0));
            s.Run.Add(K(0.5f).B("Hips", 0, 8, 0).B("Chest", 12, -8, 0).Legs(30, 62, -38, 16)
                .B("Foot.R", 30, 0, 0).B("Foot.L", -12, 0, 0).ArmL(34, 0, -12, -36).B("Head", -8, 0, 0).Move(0, -0.035f, 0));
            s.Run.Add(K(0.75f).B("Hips", 0, 0, 0).B("Chest", 14, 0, 0).Legs(-52, 105, 10, 28)
                .B("Foot.R", 10, 0, 0).B("Foot.L", 12, 0, 0).ArmL(-8, 0, -12, -64).B("Head", -8, 0, 0).Move(0, 0.05f, 0));

            // Take-off: leading knee up, back leg trailing, chest open, free arm thrown up.
            s.Jump = K().B("Hips", 0, 0, 0).B("Chest", -8, 0, 0).Legs(-62, 78, 18, 36)
                .B("Foot.R", 10, 0, 0).B("Foot.L", 35, 0, 0).ArmL(-150, 0, -22, -20).B("Head", -10, 0, 0);

            // Double jump: tucked tight for the flip.
            s.AirJump = K().B("Hips", 0, 0, 0).B("Chest", 28, 0, 0).Legs(-105, 130, -95, 130)
                .B("Foot.R", 20, 0, 0).B("Foot.L", 20, 0, 0).ArmL(-70, 0, -18, -95).B("Head", 22, 0, 0);

            // Falling: legs reaching for the ground, free arm out for balance.
            s.Fall = K().B("Hips", 0, 0, 0).B("Chest", 2, 0, 0).Legs(-28, 42, 14, 30)
                .B("Foot.R", -6, 0, 0).B("Foot.L", 18, 0, 0).ArmL(-60, 0, -62, -24).B("Head", 6, 0, 0);

            // Hard landing: deep crouch, chest folded over the knees.
            s.Land = K().B("Hips", 0, 0, 0).B("Chest", 26, 0, 0).Legs(-62, 100, -38, 92)
                .B("Foot.R", -36, 0, 0).B("Foot.L", -50, 0, 0).ArmL(-36, 0, -36, -40).B("Head", -14, 0, 0).Move(0, -0.3f, 0);

            // Hanging from a ledge: both arms straight up (blade swung back over the shoulder), legs dangling.
            s.Hang = K().B("Hips", 0, 0, 0).B("Chest", -4, 0, 0).Legs(-14, 22, 6, 14)
                .B("Foot.R", 25, 0, 0).B("Foot.L", 30, 0, 0)
                .ArmR(-166, 0, -6, -8, 0).ArmL(-166, 0, 6, -8).B("Head", -18, 0, 0).Move(0, 0f, 0.18f);

            // Pulling up: pushing down on the lip, chest over it, right knee coming up.
            s.ClimbPull = K().B("Hips", 0, 0, 0).B("Chest", 34, 0, 0).Legs(-95, 115, -10, 40)
                .ArmR(-40, 0, 12, -70, 0).ArmL(-40, 0, -12, -70).B("Head", -20, 0, 0).Move(0, 0f, 0.26f);

            // Over the top: crouched on the lip, ready to stand.
            s.ClimbOver = K().B("Hips", 0, 0, 0).B("Chest", 24, 0, 0).Legs(-60, 98, -36, 88)
                .B("Foot.R", -34, 0, 0).B("Foot.L", -48, 0, 0).ArmL(-40, 0, -30, -50).B("Head", -10, 0, 0).Move(0, -0.28f, 0);

            return s;
        }
    }
}
