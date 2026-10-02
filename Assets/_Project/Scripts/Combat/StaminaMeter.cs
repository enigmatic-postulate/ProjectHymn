using System;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// Stamina pays for off-beat actions; on-beat actions are free (GDD). Regenerates once the player
    /// has gone <see cref="RegenDelay"/> seconds without acting. Pure C#.
    /// When an off-beat action costs more than what's left, it still comes out (GDD: off-beat attacks
    /// always come out) but the player is Winded: stamina hits zero and recovery is slower.
    /// </summary>
    public sealed class StaminaMeter
    {
        // Tunable starting values; the GDD lists the exact numbers as an open question.
        public float Max = 100f;
        public float RegenPerSecond = 35f;
        public float RegenDelay = 0.6f;
        public float StrikeCost = 15f, SmashCost = 25f, StepCost = 20f, GuardCost = 0f;

        float _sinceAction;

        public StaminaMeter() { Value = Max; }

        public float Value { get; private set; }
        public float Normalized => Max <= 0f ? 0f : Value / Max;

        public event Action<float> Changed;

        public float CostOf(BeatAction action) => action switch
        {
            BeatAction.Strike => StrikeCost,
            BeatAction.Smash => SmashCost,
            BeatAction.Step => StepCost,
            BeatAction.Guard => GuardCost,
            _ => 0f,
        };

        /// <summary>Call for every action, on-beat or not, so regeneration pauses while the player is attacking.</summary>
        public void NotifyAction() => _sinceAction = 0f;

        /// <summary>Spend for an off-beat action. Returns false (Winded) if there wasn't enough; stamina then drops to zero.</summary>
        public bool Spend(BeatAction action)
        {
            NotifyAction();
            float cost = CostOf(action);
            if (cost <= 0f) return true;
            if (Value >= cost) { Set(Value - cost); return true; }
            Set(0f);
            return false;
        }

        public void Tick(float deltaTime)
        {
            _sinceAction += deltaTime;
            if (_sinceAction >= RegenDelay && Value < Max)
                Set(Math.Min(Max, Value + RegenPerSecond * deltaTime));
        }

        public void Refill() => Set(Max);

        void Set(float v)
        {
            if (Math.Abs(v - Value) < 1e-6f) return;
            Value = v;
            Changed?.Invoke(Value);
        }
    }
}
