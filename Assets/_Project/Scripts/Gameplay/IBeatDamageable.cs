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
        /// <summary>A sequence finisher's extra effect (Rising Verse launches, Bulwark pushes).</summary>
        public readonly ProjectHymn.Combat.SequenceEffect Effect;

        public HitData(float damage, Vector2 direction, string effectId, bool isFinisher, Vector3 point = default,
                       ProjectHymn.Combat.SequenceEffect effect = ProjectHymn.Combat.SequenceEffect.None)
        {
            Damage = damage; Direction = direction; EffectId = effectId; IsFinisher = isFinisher; Point = point; Effect = effect;
        }
    }

    public interface IBeatDamageable
    {
        void TakeHit(HitData hit);
    }
}
