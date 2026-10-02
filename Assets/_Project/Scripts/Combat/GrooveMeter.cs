using System;
using ProjectHymn.Core;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// Groove: 0–100, four tiers. Each tier multiplies score (x1 to x3) and adds a little damage (+0% to +25%).
    /// Rises with accurate play, falls on off-beat actions and
    /// on taking damage, decays when the player stops acting. Pure C#.
    /// </summary>
    public sealed class GrooveMeter
    {
        public static readonly float[] TierThresholds = { 0f, 25f, 50f, 80f };
        public static readonly float[] TierScoreMultipliers = { 1f, 1.5f, 2f, 3f };
        public static readonly float[] TierDamageMultipliers = { 1f, 1.0833f, 1.1667f, 1.25f };

        public float PerfectGain = 6f, GreatGain = 3f, GoodGain = 0f, OffBeatLoss = 9f, HurtLoss = 20f;
        public float IdleDelay = 2f, DecayPerSecond = 5f;

        float _idle;

        public float Value { get; private set; }
        public int Tier
        {
            get
            {
                for (int i = TierThresholds.Length - 1; i >= 0; i--)
                    if (Value >= TierThresholds[i]) return i;
                return 0;
            }
        }
        public float ScoreMultiplier => TierScoreMultipliers[Tier];
        public float DamageMultiplier => TierDamageMultipliers[Tier];

        public event Action<int> TierChanged;

        public void Add(Judgement j)
        {
            _idle = 0f;
            float delta = j switch
            {
                Judgement.Perfect => PerfectGain,
                Judgement.Great => GreatGain,
                Judgement.Good => GoodGain,
                _ => -OffBeatLoss,
            };
            Set(Value + delta);
        }

        public void Hurt() => Set(Value - HurtLoss);

        public void Tick(float deltaTime)
        {
            _idle += deltaTime;
            if (_idle > IdleDelay) Set(Value - DecayPerSecond * deltaTime);
        }

        public void Reset() { _idle = 0f; Set(0f); }

        void Set(float v)
        {
            int before = Tier;
            Value = Math.Clamp(v, 0f, 100f);
            if (Tier != before) TierChanged?.Invoke(Tier);
        }
    }
}
