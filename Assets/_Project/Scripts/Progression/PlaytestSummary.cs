using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectHymn.Progression
{
    /// <summary>
    /// Turns a pile of run logs into a playtest report (VS-5 Part C), one section per stage: how runs ended, clear times
    /// against par with a suggested par, the rank spread, why runs missed S, timing, where players died, fell, got hurt
    /// and spent their time (the stage split into six stretches by distance), and how it felt. Markdown, read by people
    /// and by Claude for the tuning pass. Plain C#, tested.
    /// </summary>
    public static class PlaytestSummary
    {
        public const int Stretches = 6;

        public static string Report(IEnumerable<RunLog> runs, DateTime? now = null)
        {
            var given = runs.Where(r => r != null && !string.IsNullOrEmpty(r.stage)).ToList();
            var all = given.Where(r => !r.debug).ToList();
            int debug = given.Count - all.Count;
            var sb = new StringBuilder();
            sb.Append("# Project Hymn playtest summary\n\n");
            sb.Append($"{all.Count} run{(all.Count == 1 ? "" : "s")}");
            if (all.Count > 0)
            {
                var dates = all.Select(r => r.started).Where(s => !string.IsNullOrEmpty(s)).OrderBy(s => s).ToList();
                if (dates.Count > 0) sb.Append($", {dates[0]} to {dates[dates.Count - 1]}");
            }
            if (debug > 0) sb.Append($" ({debug} more left out: finished or ranked with a playtest key)");
            if (now.HasValue) sb.Append($". Written {now.Value:yyyy-MM-dd HH:mm}");
            sb.Append(".\n");
            if (all.Count == 0)
            {
                sb.Append("\nNo runs yet. Play a stage (logs are written as each run ends), then build this again.\n");
                return sb.ToString();
            }
            foreach (var stage in all.GroupBy(r => r.stage).OrderBy(g => Order(g.Key)))
                Stage(sb, stage.Key, stage.ToList());
            return sb.ToString();
        }

        static int Order(string stage)
        {
            int i = Array.FindIndex(StageRoute.Path, r => r.Id == stage);
            if (i >= 0) return i;
            i = Array.FindIndex(StageRoute.SideRoads, r => r.Id == stage);
            return i >= 0 ? 10 + i : 100;
        }

        static void Stage(StringBuilder sb, string stage, List<RunLog> runs)
        {
            var name = StageRoute.Find(stage)?.Name ?? stage;
            sb.Append($"\n## {name}\n\n");

            // How runs ended
            var ends = runs.GroupBy(r => string.IsNullOrEmpty(r.outcome) ? "unknown" : r.outcome)
                           .OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}");
            sb.Append($"- **Runs:** {runs.Count} ({string.Join(", ", ends)})\n");

            // Clear times and par
            var clears = runs.Where(r => r.Cleared).ToList();
            var par = runs.Select(r => r.par).Where(p => p > 0f).DefaultIfEmpty(0f).Max();
            if (clears.Count > 0)
            {
                var times = clears.Select(r => (double)r.seconds).OrderBy(t => t).ToList();
                sb.Append($"- **Clear times:** fastest {Clock(times[0])}, median {Clock(Median(times))}, slowest {Clock(times[times.Count - 1])}");
                if (par > 0f)
                {
                    int under = times.Count(t => t <= par);
                    sb.Append($"; par {Clock(par)} (made by {under} of {times.Count})");
                }
                sb.Append('\n');
                if (times.Count >= 2)
                    sb.Append($"- **Suggested par:** {Clock(SuggestedPar(times))} (the fastest third of clears would earn S on time)\n");
                var ranks = clears.GroupBy(r => r.rank).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}");
                sb.Append($"- **Ranks:** {string.Join(", ", ranks)}");
                int failed = runs.Count(r => r.outcome == "failed");
                if (failed > 0) sb.Append($", {failed} C (failed)");
                sb.Append('\n');
            }
            else sb.Append("- **Clear times:** no clears yet\n");

            // Why runs missed S
            var why = runs.Where(r => r.rank.Length > 0 && r.rank != "S").SelectMany(r => r.missedS.Select(Reason))
                          .GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} ×{g.Count()}").ToList();
            if (why.Count > 0) sb.Append($"- **Missed S because:** {string.Join("; ", why)}\n");

            // Timing and defence
            var played = runs.Where(r => r.attacks > 0).ToList();
            if (played.Count > 0)
            {
                int hits = played.Sum(r => r.attacks);
                double onBeat = played.Sum(r => r.attacks - r.offBeat) / (double)hits;
                double perfect = played.Sum(r => r.perfect + r.truePerfect) / (double)hits;
                sb.Append($"- **Timing:** {onBeat * 100.0:0}% of {hits} hits on the beat, {perfect * 100.0:0}% Perfect or better, " +
                          $"{played.Sum(r => r.sequences)} sequences\n");
            }
            int answered = runs.Sum(r => r.parries + r.blocks + r.dodges), taken = runs.Sum(r => r.hitsTaken);
            if (answered + taken > 0)
                sb.Append($"- **Defence:** {runs.Sum(r => r.parries)} parries, {runs.Sum(r => r.blocks)} blocks, {runs.Sum(r => r.dodges)} dodges, " +
                          $"{taken} hits taken ({taken * 100.0 / (answered + taken):0}% of enemy attacks landed)\n");

            // Where things happen
            var lo = runs.SelectMany(r => r.track).Select(s => s.x).DefaultIfEmpty(0f).Min();
            var hi = runs.SelectMany(r => r.track).Select(s => s.x).DefaultIfEmpty(0f).Max();
            if (hi - lo > 1f)
            {
                var time = new double[Stretches];
                var deaths = new int[Stretches];
                var falls = new int[Stretches];
                var hurts = new int[Stretches];
                foreach (var r in runs)
                {
                    foreach (var s in r.track) time[Stretch(s.x, lo, hi)] += RunLog.SampleEvery;
                    foreach (var e in r.events)
                    {
                        int k = Stretch(e.x, lo, hi);
                        if (e.kind == "died") deaths[k]++;
                        else if (e.kind == "fell") falls[k]++;
                        else if (e.kind == "hurt") hurts[k]++;
                    }
                }
                sb.Append($"\n| Stretch (x) | Avg time | Hurt | Deaths | Falls |\n|---|---|---|---|---|\n");
                for (int k = 0; k < Stretches; k++)
                {
                    float a = lo + (hi - lo) * k / Stretches, b = lo + (hi - lo) * (k + 1) / Stretches;
                    sb.Append($"| {k + 1}: {a:0} to {b:0} | {time[k] / runs.Count:0.0}s | {hurts[k]} | {deaths[k]} | {falls[k]} |\n");
                }
            }

            // How it felt
            var felt = runs.Where(r => r.feel >= 1 && r.feel <= 5).Select(r => r.feel).ToList();
            if (felt.Count > 0)
                sb.Append($"\n- **Felt:** {felt.Average():0.0} / 5 from {felt.Count} answer{(felt.Count == 1 ? "" : "s")} ({string.Join(", ", felt)})\n");
        }

        /// <summary>The stretch (0 to 5) a position falls in.</summary>
        public static int Stretch(float x, float lo, float hi)
        {
            if (hi <= lo) return 0;
            int k = (int)((x - lo) / (hi - lo) * Stretches);
            return Math.Max(0, Math.Min(Stretches - 1, k));
        }

        /// <summary>A par the fastest third of these clear times would make: the time at the one-third mark.</summary>
        public static double SuggestedPar(IList<double> sortedTimes)
        {
            if (sortedTimes.Count == 0) return 0.0;
            int i = Math.Max(0, (int)Math.Ceiling(sortedTimes.Count / 3.0) - 1);
            return Math.Ceiling(sortedTimes[i]);
        }

        /// <summary>Group reasons: "time: …" as "over par", others by what comes after the first colon.</summary>
        public static string Reason(string why)
        {
            if (string.IsNullOrEmpty(why)) return "unknown";
            if (why.StartsWith("time:")) return "over par";
            int c = why.IndexOf(": ", StringComparison.Ordinal);
            return c >= 0 ? why.Substring(c + 2) : why;
        }

        public static double Median(IList<double> sorted)
        {
            if (sorted.Count == 0) return 0.0;
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        public static string Clock(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = (int)(seconds / 60.0);
            return $"{m}:{seconds - m * 60.0:00.0}";
        }
    }
}
