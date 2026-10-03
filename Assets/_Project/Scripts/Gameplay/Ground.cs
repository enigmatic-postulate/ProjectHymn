using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The game is played on the 3D ground plane (X, Z). Gameplay maths stays in 2D "plane" coordinates
    /// (Vector2: x = world X, y = world Z); these helpers convert at the edges.
    /// </summary>
    public static class Ground
    {
        public static Vector3 ToWorld(Vector2 p, float height = 0f) => new Vector3(p.x, height, p.y);
        public static Vector2 ToPlane(Vector3 p) => new Vector2(p.x, p.z);
        /// <summary>A plane direction as a flat world direction.</summary>
        public static Vector3 Dir(Vector2 d) => new Vector3(d.x, 0f, d.y);
        public static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(ToPlane(a), ToPlane(b));

        /// <summary>Rotation that faces a plane direction (yaw only).</summary>
        public static Quaternion Facing(Vector2 dir) =>
            dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(Dir(dir).normalized, Vector3.up) : Quaternion.identity;

        /// <summary>Turn toward a facing at a limited speed (degrees per second).</summary>
        public static Quaternion TurnToward(Quaternion current, Vector2 dir, float degreesPerSecond, float dt) =>
            dir.sqrMagnitude > 1e-6f ? Quaternion.RotateTowards(current, Facing(dir), degreesPerSecond * dt) : current;
    }
}
