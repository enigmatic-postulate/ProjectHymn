using System;
using System.Collections.Generic;
using ProjectHymn.Core;

namespace ProjectHymn.Combat
{
    /// <summary>One boss attack and when it may be used: the meter it fits (0 = any) and the first phase that has it.</summary>
    public sealed class BossMove
    {
        public EnemyAttackPattern Pattern;
        public int Meter;
        public int MinPhase;
        public BossMove(EnemyAttackPattern pattern, int meter, int minPhase = 0) { Pattern = pattern; Meter = meter; MinPhase = minPhase; }
    }

    /// <summary>What the music does next bar: the time signature now, the next bar's, and beats left in this bar.</summary>
    public readonly struct MeterCue
    {
        public readonly string Current, Next;
        public readonly int BeatsLeft;
        public bool Changing => Current != Next;
        public MeterCue(string current, string next, int beatsLeft) { Current = current; Next = next; BeatsLeft = beatsLeft; }
    }

    /// <summary>
    /// The summit boss's planning (VS-3), pure C#. Attacks are telegraphed for one bar and fit the meter they land in
    /// (4/4, 3/4, 7/8 grouped 2+2+3). An attack never straddles a meter change: if the count-in bar would be in the old
    /// meter and the hit in the new one, the count-in waits for the new section, so the warning is always counted in the
    /// meter the hit lands in. Later phases add moves.
    /// </summary>
    public static class BossPlanner
    {
        static readonly EnemyAttackPattern Probe = new EnemyAttackPattern("Probe", new[] { 0.0 }, 4, 0, 0);

        /// <summary>The first beat of the tempo-map section <paramref name="beat"/> is in (searches back up to 512 beats).</summary>
        public static long SectionStart(TempoMap map, long beat)
        {
            int s = map.GetBeat(beat).SectionIndex;
            long b = beat;
            for (int guard = 0; guard < 512 && b > 0 && map.GetBeat(b - 1).SectionIndex == s; guard++) b--;
            return b;
        }

        public static MeterCue Cue(TempoMap map, double beat)
        {
            long i = (long)Math.Floor(beat);
            var cur = map.GetBeat(i);
            long nextBar = i + (cur.BeatsPerMeasure - cur.BeatInMeasure);
            var next = map.GetBeat(nextBar);
            return new MeterCue(cur.Signature, next.Signature, (int)(nextBar - i));
        }

        /// <summary>Where a one-bar count-in from <paramref name="now"/> lands its hit, kept inside one section.</summary>
        public static PlannedAttack Slot(TempoMap map, double now, double notBefore)
        {
            var probe = EnemyScheduler.Plan(map, Probe, now, notBefore, oneBarLead: true);
            long hit = (long)Math.Round(probe.FirstHitBeat);
            long tele = (long)Math.Floor(probe.TelegraphBeat + 1e-6);
            if (map.GetBeat(tele).SectionIndex != map.GetBeat(hit).SectionIndex)
            {
                long start = SectionStart(map, hit);
                probe = EnemyScheduler.Plan(map, Probe, Math.Max(start, now), Math.Max(start, notBefore), oneBarLead: true);
            }
            return probe;
        }

        /// <summary>
        /// Plan the next attack: find its slot, then take the next move (in turn) that fits the slot's meter and the
        /// phase, and whose hits stay in the slot's section. <paramref name="turn"/> advances on every plan.
        /// </summary>
        public static PlannedAttack Plan(TempoMap map, IReadOnlyList<BossMove> moves, int phase, ref int turn, double now, double notBefore)
        {
            var slot = Slot(map, now, notBefore);
            long hit = (long)Math.Round(slot.FirstHitBeat);
            var info = map.GetBeat(hit);
            int lead = (int)Math.Round(slot.FirstHitBeat - slot.TelegraphBeat);
            var fits = new List<EnemyAttackPattern>();
            foreach (var m in moves)
            {
                if (m == null || m.Pattern == null || m.MinPhase > phase) continue;
                if (m.Meter != 0 && m.Meter != info.BeatsPerMeasure) continue;
                long last = hit + (long)Math.Ceiling(m.Pattern.Span);
                if (map.GetBeat(last).SectionIndex != info.SectionIndex) continue;
                fits.Add(m.Pattern);
            }
            if (fits.Count == 0)
                foreach (var m in moves) if (m != null && m.Pattern != null && m.Meter == 0) { fits.Add(m.Pattern); break; }
            if (fits.Count == 0) return default;
            var pick = fits[((turn % fits.Count) + fits.Count) % fits.Count];
            turn++;
            return new PlannedAttack(pick, slot.FirstHitBeat, lead);
        }

        /// <summary>Phase from health: 0 above two thirds, 1 above one third, 2 below.</summary>
        public static int PhaseFor(float hp, float maxHp)
        {
            float k = maxHp <= 0f ? 0f : hp / maxHp;
            return k > 2f / 3f ? 0 : k > 1f / 3f ? 1 : 2;
        }
    }
}
