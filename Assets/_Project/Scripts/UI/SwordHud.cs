using System.Collections.Generic;
using System.Text;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// HUD for the M2 sword prototype, built around seeing the rhythm:
    /// - a beat track: the last four beats and the next three, with the notes you played on them (1 / 2) coloured by
    ///   timing, the current beat pulsing;
    /// - the weapon's sequences (1-2-1, 2-2, 1-1-1-2-1-1-1) with the steps you've matched lit and the next note marked;
    /// - the chain counter ("damage on" from the second on-beat note), judgement, banners, score and Groove;
    /// - weapon, BPM, animation offset and the test-tempo keys.
    /// </summary>
    public class SwordHud : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] SwordCombatController combat;
        [SerializeField] Text judgementText, chainText, bannerText, sequenceText, beatTrackText, infoText, scoreText, grooveText;
        [SerializeField] Image grooveFill;

        static readonly Color Gold = new Color(1f, 0.82f, 0.25f), Dim = new Color(0.45f, 0.42f, 0.55f);
        readonly Dictionary<long, (NoteButton button, Judgement j)> _history = new Dictionary<long, (NoteButton, Judgement)>();
        float _judgeTimer, _bannerTimer;
        long _lastBeat = long.MinValue;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (combat == null) combat = FindAnyObjectByType<SwordCombatController>();
        }

        void OnEnable()
        {
            if (combat != null)
            {
                combat.NoteJudged += OnNote;
                combat.Impacted += OnImpact;
                combat.SequenceCompleted += OnSequence;
                combat.WeaponChanged += OnWeapon;
            }
            if (conductor != null) conductor.SongStarted += OnSong;
        }

        void OnDisable()
        {
            if (combat != null)
            {
                combat.NoteJudged -= OnNote;
                combat.Impacted -= OnImpact;
                combat.SequenceCompleted -= OnSequence;
                combat.WeaponChanged -= OnWeapon;
            }
            if (conductor != null) conductor.SongStarted -= OnSong;
        }

        void OnSong(SongData s) { _history.Clear(); Banner($"{s.name.ToUpperInvariant()}", 1.5f); }
        void OnWeapon(WeaponSet w) => Banner(w.displayName.ToUpperInvariant(), 1.2f);

        void OnNote(NoteResult n)
        {
            _history[n.Judge.Beat.Index] = (n.Button, n.Effective);
            if (judgementText == null) return;
            string label = n.Step.Broke && n.Judge.IsOnBeat ? "DOUBLE" : !n.OnBeat ? "OFF-BEAT" : n.Effective == Judgement.TruePerfect ? "TRUE PERFECT" : n.Effective.ToString().ToUpperInvariant();
            judgementText.text = $"{label}  {n.Judge.OffsetMs:+0;-0;0} ms";
            judgementText.color = n.Effective switch
            {
                Judgement.TruePerfect => new Color(0.6f, 1f, 1f),
                Judgement.Perfect => Gold,
                Judgement.Great => Color.white,
                Judgement.Good => new Color(0.5f, 0.8f, 1f),
                _ => new Color(0.6f, 0.6f, 0.65f),
            };
            _judgeTimer = 0.6f;
        }

        void OnImpact(ImpactInfo i)
        {
        }

        void OnSequence(SwordSequence s) => Banner($"{s.displayName.ToUpperInvariant()}!", 1.8f, color: s.color);

        void Banner(string text, float seconds, bool small = false, Color? color = null)
        {
            if (bannerText == null) return;
            bannerText.text = text;
            bannerText.fontSize = small ? 30 : 56;
            bannerText.color = color ?? Color.white;
            _bannerTimer = seconds;
        }

        void Update()
        {
            if (conductor == null || combat == null || !conductor.IsPlaying) return;
            var beat = conductor.CurrentBeat;
            if (beat.Index != _lastBeat) { _lastBeat = beat.Index; Prune(beat.Index); }

            if (beatTrackText != null) beatTrackText.text = BeatTrack(beat);
            if (sequenceText != null) sequenceText.text = Sequences();
            if (chainText != null)
            {
                int n = combat.Chain.ChainLength;
                chainText.text = n <= 0 ? "<color=#8a84a0>CHAIN —  play notes on consecutive beats</color>"
                               : n == 1 ? "CHAIN 1  <color=#8a84a0>· next on-beat note deals damage</color>"
                               : $"<color=#ffd040>CHAIN {n}</color>  · damage on";
            }
            if (scoreText != null) scoreText.text = combat.Score.Total.ToString("N0");
            if (grooveFill != null) grooveFill.fillAmount = combat.Groove.Value / 100f;
            if (grooveText != null) grooveText.text = $"GROOVE x{combat.Groove.ScoreMultiplier:0.#}";
            if (infoText != null)
            {
                var w = combat.Weapon;
                float off = combat.Animator != null ? combat.Animator.AnimationOffsetMs : 0f;
                infoText.text = $"{(w != null ? w.displayName.ToUpperInvariant() : "")}  (Tab)     {beat.Bpm:0} BPM {beat.Signature}     animation offset {off:+0;-0;0} ms  ( [ ] )     F1 song · F2 90 · F3 130 · F4 170 BPM";
            }

            Fade(judgementText, ref _judgeTimer, 0.6f);
            Fade(bannerText, ref _bannerTimer, 1.2f);
        }

        string BeatTrack(BeatInfo now)
        {
            var sb = new StringBuilder();
            float pulse = Mathf.Exp(-conductor.BeatPhase * 5f);
            for (long b = now.Index - 4; b <= now.Index + 3; b++)
            {
                var info = conductor.Map.GetBeat(b);
                string glyph;
                if (_history.TryGetValue(b, out var h))
                {
                    var c = h.j switch
                    {
                        Judgement.TruePerfect => "#a0ffff",
                        Judgement.Perfect => "#ffd040",
                        Judgement.Great => "#ffffff",
                        Judgement.Good => "#80ccff",
                        _ => "#7a7488",
                    };
                    glyph = $"<color={c}>{(int)h.button}</color>";
                }
                else glyph = b < now.Index ? "<color=#4a4458>·</color>" : b == now.Index ? "<color=#8a84a0>·</color>" : "<color=#5a5468>○</color>";
                // Enemy hits counting in (slice 2): guard on these beats.
                int threat = b >= now.Index ? EnemyThreats.At(b) : 0;
                if (threat > 0) glyph = threat >= 2 ? "<color=#ff3a2a>!!</color>" : "<color=#ff6a3a>!</color>";
                if (b == now.Index)
                {
                    int size = Mathf.RoundToInt(44 + 18 * pulse);
                    glyph = $"<size={size}>[{glyph}]</size>";
                }
                if (info.IsDownbeat) sb.Append("<color=#5a5468>|</color> ");
                sb.Append(glyph).Append("  ");
            }
            return sb.ToString();
        }

        string Sequences()
        {
            var chain = combat.Chain;
            bool predicted = chain.Predict(out int nextSeq, out int nextStep, out _);
            var sb = new StringBuilder();
            for (int i = 0; i < chain.Sequences.Count; i++)
            {
                var s = chain.Sequences[i];
                var asset = s.Source as SwordSequence;
                string col = asset != null ? "#" + ColorUtility.ToHtmlStringRGB(asset.color) : "#ffd27a";
                int progress = chain.Progress(i);
                sb.Append($"<color={col}>{s.Name.ToUpperInvariant()}</color>   ");
                for (int k = 0; k < s.Length; k++)
                {
                    string n = ((int)s.Notes[k]).ToString();
                    if (k < progress) sb.Append($"<color=#ffd040>[{n}]</color> ");
                    else if (predicted && i == nextSeq && k == nextStep) sb.Append($"<b><color=#ffffff>[{n}]</color></b> ");
                    else sb.Append($"<color=#6a6478>[{n}]</color> ");
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        void Prune(long current)
        {
            var old = new List<long>();
            foreach (var k in _history.Keys) if (k < current - 8) old.Add(k);
            foreach (var k in old) _history.Remove(k);
        }

        static void Fade(Text t, ref float timer, float duration)
        {
            if (t == null) return;
            timer = Mathf.Max(0f, timer - Time.unscaledDeltaTime);
            var c = t.color;
            c.a = Mathf.Clamp01(timer / duration * 2f);
            t.color = c;
        }
    }
}
