using UnityEngine;

namespace ProjectHymn.Gameplay
{
    public readonly struct HitData
    {
        public readonly float Damage;
        /// <summary>Direction of the hit on the ground (x = world X, y = world Z).</summary>
        public readonly Vector2 Direction;
        public readonly string EffectId;
        public readonly bool IsFinisher;
        /// <summary>World position where the blade connected (for sparks and numbers).</summary>
        public readonly Vector3 Point;

        public HitData(float damage, Vector2 direction, string effectId, bool isFinisher, Vector3 point = default)
        {
            Damage = damage; Direction = direction; EffectId = effectId; IsFinisher = isFinisher; Point = point;
        }
    }

    public interface IBeatDamageable
    {
        void TakeHit(HitData hit);
    }
}
