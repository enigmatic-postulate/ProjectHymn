using UnityEngine;

namespace ProjectHymn.Gameplay
{
    public readonly struct HitData
    {
        public readonly float Damage;
        public readonly Vector2 Direction;
        public readonly string EffectId;   // phrase effect, or null for a plain hit
        public readonly bool IsFinisher;

        public HitData(float damage, Vector2 direction, string effectId, bool isFinisher)
        {
            Damage = damage; Direction = direction; EffectId = effectId; IsFinisher = isFinisher;
        }
    }

    public interface IBeatDamageable
    {
        void TakeHit(HitData hit);
    }
}
