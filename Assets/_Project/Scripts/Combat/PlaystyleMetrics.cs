using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProjectHymn.Core;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// Tallies the five playstyle signals the GDD uses for class assignment: time-signature handling,
    /// shield (Guard) block rate, aggressiveness, weapon preference and dodging. Pure C#; a tracker
    /// component feeds it and writes <see cref="ToJson"/> to disk after each fight.
    /// </summary>
    public sealed class PlaystyleMetrics
    {
        // How long after a meter change counts as "adapting to the new meter".
        public int AdaptationMeasures = 2;

        public double FightSeconds { get; private set; }
        public double SecondsNearEnemies { get; private set; }

        public int Strikes, Smashes, Steps, Guards;
        public int OnBeatActions, OffBeatActions;

        public int EnemyAttacksFaced;   // attacks that would have connected (in range)
        public int Parried, DodgedWithStep, HitsTaken;
        public int AttacksAvoidedByPosition; // player was out of range when it landed

        public int MeterChanges;
        public int OnBeatAfterChange, ActionsAfterChange;

        readonly Dictionary<string, int[]> _byMeter = new Dictionary<string, int[]>(); // meter -> [onBeat, total]
        readonly Dictionary<string, double> _weaponSeconds = new Dictionary<string, double>();
        long _lastChangeMeasure = long.MinValue;

        public void Tick(double seconds, bool nearEnemy, string weapon)
        {
            FightSeconds += seconds;
            if (nearEnemy) SecondsNearEnemies += seconds;
            if (!string.IsNullOrEmpty(weapon))
                _weaponSeconds[weapon] = (_weaponSeconds.TryGetValue(weapon, out var s) ? s : 0) + seconds;
        }

        public void RecordMeterChange(BeatInfo beat)
        {
            MeterChanges++;
            _lastChangeMeasure = beat.Measure;
        }

        public void RecordAction(BeatAction action, Judgement j, BeatInfo beat)
        {
            switch (action)
            {
                case BeatAction.Strike: Strikes++; break;
                case BeatAction.Smash: Smashes++; break;
                case BeatAction.Step: Steps++; break;
                case BeatAction.Guard: Guards++; break;
            }

            bool onBeat = j != Judgement.OffBeat;
            if (onBeat) OnBeatActions++; else OffBeatActions++;

            var key = beat.Signature;
            if (!_byMeter.TryGetValue(key, out var t)) _byMeter[key] = t = new int[2];
            if (onBeat) t[0]++;
            t[1]++;

            if (_lastChangeMeasure != long.MinValue && beat.Measure - _lastChangeMeasure < AdaptationMeasures && beat.Measure >= _lastChangeMeasure)
            {
                ActionsAfterChange++;
                if (onBeat) OnBeatAfterChange++;
            }
        }

        public void RecordDefense(DefenseResult r)
        {
            switch (r)
            {
                case DefenseResult.OutOfRange: AttacksAvoidedByPosition++; return;
                case DefenseResult.Parried: Parried++; break;
                case DefenseResult.Dodged: DodgedWithStep++; break;
                case DefenseResult.Hit: HitsTaken++; break;
            }
            EnemyAttacksFaced++;
        }

        // ---- Derived signals (0..1 unless noted) ----

        int TotalActions => OnBeatActions + OffBeatActions;
        int Attacks => Strikes + Smashes;

        public double OnBeatAccuracy => Ratio(OnBeatActions, TotalActions);

        /// <summary>Share of in-range enemy attacks the player parried.</summary>
        public double BlockRate => Ratio(Parried, EnemyAttacksFaced);

        /// <summary>Share of in-range enemy attacks avoided with an on-beat Step.</summary>
        public double DodgeRate => Ratio(DodgedWithStep, EnemyAttacksFaced);

        public double AttacksPerMinute => FightSeconds <= 0 ? 0 : Attacks / (FightSeconds / 60.0);

        public double TimeNearEnemiesRatio => FightSeconds <= 0 ? 0 : SecondsNearEnemies / FightSeconds;

        /// <summary>Half attack rate (60 per minute = max), half time spent close to enemies.</summary>
        public double Aggressiveness => 0.5 * Math.Min(1.0, AttacksPerMinute / 60.0) + 0.5 * TimeNearEnemiesRatio;

        /// <summary>
        /// On-beat accuracy in the measures right after a meter change, relative to overall accuracy.
        /// 1 = adapts as well as usual; below 1 = struggles with changes. NaN until a change was played.
        /// </summary>
        public double MeterAdaptation
        {
            get
            {
                if (ActionsAfterChange == 0 || OnBeatAccuracy <= 0) return double.NaN;
                return Ratio(OnBeatAfterChange, ActionsAfterChange) / OnBeatAccuracy;
            }
        }

        /// <summary>Smash share of attacks: 0 = all Strikes, 1 = all Smashes.</summary>
        public double HeavyAttackShare => Ratio(Smashes, Attacks);

        public string PreferredWeapon
        {
            get
            {
                string best = null; double max = -1;
                foreach (var kv in _weaponSeconds) if (kv.Value > max) { max = kv.Value; best = kv.Key; }
                return best;
            }
        }

        public double MeterAccuracy(string signature) =>
            _byMeter.TryGetValue(signature, out var t) ? Ratio(t[0], t[1]) : double.NaN;

        static double Ratio(int a, int b) => b <= 0 ? 0 : (double)a / b;

        /// <summary>Flat JSON for the fight log (hand-written: no dictionary support needed from Unity's JsonUtility).</summary>
        public string ToJson(string song, DateTime utc)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            string N(double v) => double.IsNaN(v) ? "null" : v.ToString("0.####", ci);
            string Q(string v) => v == null ? "null" : "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            sb.Append("{\n");
            sb.Append($"  \"timestampUtc\": {Q(utc.ToString("o", ci))},\n");
            sb.Append($"  \"song\": {Q(song)},\n");
            sb.Append($"  \"fightSeconds\": {N(FightSeconds)},\n");
            sb.Append("  \"signals\": {\n");
            sb.Append($"    \"meterAdaptation\": {N(MeterAdaptation)},\n");
            sb.Append($"    \"blockRate\": {N(BlockRate)},\n");
            sb.Append($"    \"aggressiveness\": {N(Aggressiveness)},\n");
            sb.Append($"    \"preferredWeapon\": {Q(PreferredWeapon)},\n");
            sb.Append($"    \"heavyAttackShare\": {N(HeavyAttackShare)},\n");
            sb.Append($"    \"dodgeRate\": {N(DodgeRate)}\n");
            sb.Append("  },\n");
            sb.Append("  \"raw\": {\n");
            sb.Append($"    \"strikes\": {Strikes}, \"smashes\": {Smashes}, \"steps\": {Steps}, \"guards\": {Guards},\n");
            sb.Append($"    \"onBeatActions\": {OnBeatActions}, \"offBeatActions\": {OffBeatActions}, \"onBeatAccuracy\": {N(OnBeatAccuracy)},\n");
            sb.Append($"    \"enemyAttacksFaced\": {EnemyAttacksFaced}, \"parried\": {Parried}, \"dodgedWithStep\": {DodgedWithStep}, \"hitsTaken\": {HitsTaken}, \"avoidedByPosition\": {AttacksAvoidedByPosition},\n");
            sb.Append($"    \"attacksPerMinute\": {N(AttacksPerMinute)}, \"timeNearEnemiesRatio\": {N(TimeNearEnemiesRatio)},\n");
            sb.Append($"    \"meterChanges\": {MeterChanges}, \"actionsAfterChange\": {ActionsAfterChange}, \"onBeatAfterChange\": {OnBeatAfterChange},\n");
            sb.Append("    \"accuracyByMeter\": {");
            bool first = true;
            foreach (var kv in _byMeter)
            {
                sb.Append(first ? "" : ", ");
                sb.Append($"{Q(kv.Key)}: {N(Ratio(kv.Value[0], kv.Value[1]))}");
                first = false;
            }
            sb.Append("},\n");
            sb.Append("    \"weaponSeconds\": {");
            first = true;
            foreach (var kv in _weaponSeconds)
            {
                sb.Append(first ? "" : ", ");
                sb.Append($"{Q(kv.Key)}: {N(kv.Value)}");
                first = false;
            }
            sb.Append("}\n  }\n}\n");
            return sb.ToString();
        }
    }
}
