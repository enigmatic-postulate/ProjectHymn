using System.Collections.Generic;

namespace ProjectHymn.EditorTools.Sword
{
    /// <summary>
    /// Arm angles for every keyframe (Euler degrees: upper arm x, y, z, elbow x, wrist x, y), solved offline so the
    /// hand and blade reach authored targets (e.g. a diagonal cut crossing in front of the chest, tip low-left at the hit).
    /// Generated data: edit the SwordMove assets in Unity to fine-tune, or regenerate this table.
    /// </summary>
    public static class ArmPoses
    {
        public static readonly Dictionary<string, float[]> Saber = new Dictionary<string, float[]>
        {
            ["Stance"] = new[] { -13f, 23f, 7f, -79f, 47f, 22f },
            ["DiagonalCut@-0.2"] = new[] { -109f, 9f, 13f, -50f, 46f, 78f },
            ["DiagonalCut@0"] = new[] { -28f, -1f, 37f, -73f, 120f, 18f },
            ["DiagonalCut@0.2"] = new[] { -39f, 17f, -22f, -13f, 66f, 56f },
            ["HorizontalSlash@-0.2"] = new[] { -67f, 24f, -25f, -22f, 24f, 77f },
            ["HorizontalSlash@0"] = new[] { -58f, 15f, -3f, -41f, 120f, 73f },
            ["HorizontalSlash@0.2"] = new[] { -65f, 1f, -23f, -25f, 116f, 77f },
            ["RisingCut@-0.2"] = new[] { -50f, 20f, -21f, -6f, 78f, 35f },
            ["RisingCut@0"] = new[] { -83f, 8f, -22f, -8f, 46f, 38f },
            ["RisingCut@0.2"] = new[] { -117f, 11f, 9f, -24f, 60f, 59f },
            ["RisingGuard@-0.2"] = new[] { -44f, 12f, -2f, -24f, 79f, 18f },
            ["RisingGuard@0"] = new[] { -127f, 7f, -1f, -18f, 76f, 9f },
            ["RisingGuard@0.3"] = new[] { -149f, 6f, -3f, -46f, -5f, 0f },
            ["RisingGuard@0.5"] = new[] { -147f, 3f, -3f, -45f, -9f, -3f },
            ["PowerSlice@-0.5"] = new[] { -143f, 12f, -4f, -54f, -4f, 4f },
            ["PowerSlice@-0.15"] = new[] { -145f, 6f, -9f, -27f, -35f, -8f },
            ["PowerSlice@0"] = new[] { -75f, -2f, -16f, -38f, 120f, -30f },
            ["PowerSlice@0.25"] = new[] { -28f, -4f, -16f, -56f, 119f, -29f },
            ["Punch@-0.15"] = new[] { 0f, -15f, -8f, -101f, 3f, 4f },
            ["Punch.L@-0.15"] = new[] { 60f, 8f, 3f, -140f, 78f, 0f },
            ["Punch@0"] = new[] { 35f, -120f, -40f, -116f, -6f, -49f },
            ["Punch.L@0"] = new[] { -63f, 25f, 40f, -38f, 28f, 0f },
            ["Punch@0.15"] = new[] { 48f, -120f, -40f, -121f, -16f, -46f },
            ["Punch.L@0.15"] = new[] { -61f, 22f, 39f, -36f, 14f, 0f },
            ["Chop@-0.2"] = new[] { -130f, 11f, 13f, -50f, 51f, 50f },
            ["Chop@0"] = new[] { -76f, -4f, 1f, -18f, 110f, -17f },
            ["Chop@0.2"] = new[] { -45f, 25f, -23f, 0f, 81f, 48f },
            ["TempestFinish@-0.2"] = new[] { -51f, 18f, -17f, -6f, 77f, 31f },
            ["TempestFinish@0"] = new[] { -86f, 4f, -19f, -6f, 50f, 40f },
            ["TempestFinish@0.25"] = new[] { -115f, 9f, 4f, -2f, 49f, 45f },
        };
        public static readonly Dictionary<string, float[]> Broadsword = new Dictionary<string, float[]>
        {
            ["Stance"] = new[] { -29f, 10f, -1f, -70f, 50f, 14f },
            ["DiagonalCut@-0.2"] = new[] { -112f, -1f, 11f, -37f, 42f, 69f },
            ["DiagonalCut@0"] = new[] { -6f, 24f, 36f, -97f, 120f, 14f },
            ["DiagonalCut@0.2"] = new[] { -11f, 45f, -32f, -37f, 73f, 39f },
            ["HorizontalSlash@-0.2"] = new[] { -75f, 9f, -26f, -11f, 24f, 77f },
            ["HorizontalSlash@0"] = new[] { -46f, 7f, 13f, -63f, 120f, 61f },
            ["HorizontalSlash@0.2"] = new[] { -55f, -10f, 1f, -50f, 112f, 47f },
            ["RisingCut@-0.2"] = new[] { -48f, 22f, -9f, -7f, 76f, 35f },
            ["RisingCut@0"] = new[] { -82f, 3f, -21f, -10f, 47f, 38f },
            ["RisingCut@0.2"] = new[] { -120f, 7f, 7f, -12f, 57f, 54f },
            ["RisingGuard@-0.2"] = new[] { -42f, 14f, -1f, -24f, 78f, 17f },
            ["RisingGuard@0"] = new[] { -127f, 7f, -1f, -20f, 78f, 10f },
            ["RisingGuard@0.3"] = new[] { -150f, 6f, -3f, -46f, -4f, 0f },
            ["RisingGuard@0.5"] = new[] { -147f, 3f, -3f, -45f, -8f, -3f },
            ["PowerSlice@-0.5"] = new[] { -143f, 6f, -3f, -53f, -4f, 0f },
            ["PowerSlice@-0.15"] = new[] { -145f, 3f, -8f, -27f, -35f, -10f },
            ["PowerSlice@0"] = new[] { -42f, 8f, -40f, -80f, 120f, -28f },
            ["PowerSlice@0.25"] = new[] { -7f, 2f, -25f, -79f, 111f, -20f },
            ["Punch@-0.15"] = new[] { -7f, -19f, -12f, -93f, 3f, 1f },
            ["Punch.L@-0.15"] = new[] { 60f, 65f, 22f, -139f, 90f, 0f },
            ["Punch@0"] = new[] { 10f, -120f, -40f, -103f, 13f, -52f },
            ["Punch.L@0"] = new[] { -59f, 36f, 40f, -44f, 34f, 0f },
            ["Punch@0.15"] = new[] { 22f, -120f, -40f, -110f, 4f, -51f },
            ["Punch.L@0.15"] = new[] { -57f, 30f, 40f, -41f, 18f, 0f },
            ["Chop@-0.2"] = new[] { -129f, 3f, 14f, -46f, 48f, 47f },
            ["Chop@0"] = new[] { -61f, -2f, 2f, -44f, 120f, -16f },
            ["Chop@0.2"] = new[] { -31f, -11f, -5f, -35f, 115f, -54f },
            ["TempestFinish@-0.2"] = new[] { -46f, 22f, -4f, -7f, 74f, 31f },
            ["TempestFinish@0"] = new[] { -86f, -1f, -20f, -8f, 53f, 40f },
            ["TempestFinish@0.25"] = new[] { -112f, 3f, 1f, -2f, 50f, 48f },
        };
    }
}
