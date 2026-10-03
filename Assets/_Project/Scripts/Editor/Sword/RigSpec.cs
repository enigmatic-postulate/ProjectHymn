using System.Collections.Generic;

namespace ProjectHymn.EditorTools.Sword
{
    /// <summary>
    /// The generated swordsman's skeleton and body parts, as plain data (no Unity types) so poses can be previewed
    /// outside Unity. The bind pose has every bone unrotated: arms hang down, legs straight, facing +Z, right = +X.
    /// Bone positions are local to the parent, in metres. Parts are rigidly skinned to one bone each.
    /// </summary>
    public static class RigSpec
    {
        public readonly struct Bone
        {
            public readonly string Name, Parent;
            public readonly float X, Y, Z;
            public Bone(string name, string parent, float x, float y, float z) { Name = name; Parent = parent; X = x; Y = y; Z = z; }
        }

        public enum Shape { Box, Cylinder, Sphere }

        public readonly struct Part
        {
            public readonly string Bone;
            public readonly Shape Shape;
            public readonly float CX, CY, CZ;   // centre, bone-local
            public readonly float SX, SY, SZ;   // size (cylinder: diameter x height x diameter, along Y)
            public readonly uint Color;
            public readonly float Taper;        // cylinder top radius ratio
            public Part(string bone, Shape shape, float cx, float cy, float cz, float sx, float sy, float sz, uint color, float taper = 1f)
            {
                Bone = bone; Shape = shape; CX = cx; CY = cy; CZ = cz; SX = sx; SY = sy; SZ = sz; Color = color; Taper = taper;
            }
        }

        public const uint Skin = 0xf0c08a, Tunic = 0x2f5bd0, TunicDark = 0x1c3484, Trim = 0xe0b040, Pants = 0x3a3248,
                          Boots = 0x5a3820, Hair = 0x5a3424, Belt = 0x6a4428, Eye = 0x1c1426;

        public static readonly Bone[] Bones =
        {
            new Bone("Root", null, 0, 0, 0),
            new Bone("Hips", "Root", 0, 0.95f, 0),
            new Bone("Spine", "Hips", 0, 0.12f, 0),
            new Bone("Chest", "Spine", 0, 0.2f, 0),
            new Bone("Neck", "Chest", 0, 0.24f, 0),
            new Bone("Head", "Neck", 0, 0.07f, 0),
            new Bone("UpperArm.R", "Chest", 0.22f, 0.19f, 0),
            new Bone("LowerArm.R", "UpperArm.R", 0, -0.27f, 0),
            new Bone("Hand.R", "LowerArm.R", 0, -0.25f, 0),
            new Bone("UpperArm.L", "Chest", -0.22f, 0.19f, 0),
            new Bone("LowerArm.L", "UpperArm.L", 0, -0.27f, 0),
            new Bone("Hand.L", "LowerArm.L", 0, -0.25f, 0),
            new Bone("UpperLeg.R", "Hips", 0.1f, -0.03f, 0),
            new Bone("LowerLeg.R", "UpperLeg.R", 0, -0.42f, 0),
            new Bone("Foot.R", "LowerLeg.R", 0, -0.42f, 0),
            new Bone("UpperLeg.L", "Hips", -0.1f, -0.03f, 0),
            new Bone("LowerLeg.L", "UpperLeg.L", 0, -0.42f, 0),
            new Bone("Foot.L", "LowerLeg.L", 0, -0.42f, 0),
        };

        public static readonly Part[] Parts = BuildParts();

        static Part[] BuildParts()
        {
            var p = new List<Part>
            {
                // Pelvis and belt
                new Part("Hips", Shape.Box, 0, 0.02f, 0, 0.32f, 0.18f, 0.2f, Pants),
                new Part("Hips", Shape.Box, 0, 0.1f, 0, 0.34f, 0.06f, 0.22f, Belt),
                new Part("Spine", Shape.Box, 0, 0.08f, 0, 0.3f, 0.18f, 0.19f, Tunic),
                // Torso with a tunic skirt hanging from the waist
                new Part("Chest", Shape.Box, 0, 0.08f, 0, 0.38f, 0.3f, 0.22f, Tunic),
                new Part("Chest", Shape.Box, 0, 0.08f, 0.115f, 0.1f, 0.26f, 0.01f, Trim),
                new Part("Hips", Shape.Cylinder, 0, -0.16f, 0, 0.42f, 0.24f, 0.3f, TunicDark, 1.25f),
                // Shoulder pads
                new Part("Chest", Shape.Sphere, 0.22f, 0.2f, 0, 0.17f, 0.11f, 0.17f, Trim),
                new Part("Chest", Shape.Sphere, -0.22f, 0.2f, 0, 0.17f, 0.11f, 0.17f, Trim),
                // Neck, head, hair, eyes
                new Part("Neck", Shape.Cylinder, 0, 0.03f, 0, 0.09f, 0.08f, 0.09f, Skin),
                new Part("Head", Shape.Sphere, 0, 0.13f, 0.01f, 0.26f, 0.28f, 0.25f, Skin),
                new Part("Head", Shape.Sphere, 0, 0.19f, -0.03f, 0.29f, 0.24f, 0.28f, Hair),
                new Part("Head", Shape.Box, -0.05f, 0.13f, 0.125f, 0.035f, 0.05f, 0.02f, Eye),
                new Part("Head", Shape.Box, 0.05f, 0.13f, 0.125f, 0.035f, 0.05f, 0.02f, Eye),
            };
            foreach (var side in new[] { "R", "L" })
            {
                // Arms: sleeve, forearm, glove
                p.Add(new Part($"UpperArm.{side}", Shape.Cylinder, 0, -0.27f / 2f, 0, 0.11f, 0.27f, 0.11f, Tunic, 0.85f));
                p.Add(new Part($"LowerArm.{side}", Shape.Cylinder, 0, -0.25f / 2f, 0, 0.09f, 0.25f, 0.09f, Skin, 0.8f));
                p.Add(new Part($"LowerArm.{side}", Shape.Cylinder, 0, -0.2f, 0, 0.1f, 0.08f, 0.1f, Belt));
                p.Add(new Part($"Hand.{side}", Shape.Box, 0, -0.045f, 0, 0.08f, 0.09f, 0.08f, Belt));
                // Legs and boots
                p.Add(new Part($"UpperLeg.{side}", Shape.Cylinder, 0, -0.21f, 0, 0.15f, 0.42f, 0.15f, Pants, 0.8f));
                p.Add(new Part($"LowerLeg.{side}", Shape.Cylinder, 0, -0.21f, 0, 0.12f, 0.42f, 0.12f, Boots, 0.85f));
                p.Add(new Part($"Foot.{side}", Shape.Box, 0, -0.04f, 0.05f, 0.11f, 0.08f, 0.24f, Boots));
            }
            return p.ToArray();
        }

        /// <summary>Where the sword's grip sits in the right hand (hand-local) and how it is turned (Euler degrees).</summary>
        public const float GripY = -0.05f;
        public const float GripEulerX = 90f; // blade along hand +Z (forward when the arm hangs down)
    }
}
