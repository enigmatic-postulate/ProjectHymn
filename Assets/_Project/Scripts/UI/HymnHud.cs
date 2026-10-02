using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Prototype HUD: measure pips (one per beat of the current meter), time signature, judgement
    /// pop-up with ms offset, phrase name, groove, stamina, score, finisher banner.
    /// No beat ring by default (GDD): it is kept only as an optional accessibility cue.
    /// </summary>
    public class HymnHud : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] BeatCombatController combat;
        [SerializeField] PlayerHealth health;
        [SerializeField] ArenaDirector arena;

        [Header("Optional beat cue (accessibility, off by default)")]
        [SerializeField] bool showBeatCue = false;
        [SerializeField] RectTransform beatRing;
        [SerializeField] Image beatRingImage;
        [SerializeField] RectTransform pipRow;
        [SerializeField] Text signatureText;
        [SerializeField] Text judgementText;
        [SerializeField] Text phraseText;
        [SerializeField] Text grooveText;
        [SerializeField] Image grooveFill;
        [SerializeField] Text bannerText;
        [SerializeField] Image staminaFill;
        [SerializeField] Text scoreText;
        [SerializeField] Image hpFill;
        [SerializeField] Text waveText;

        [Header("Colours")]
        [SerializeField] Color perfect = new Color(1f, 0.84f, 0.25f);
        [SerializeField] Color great = Color.white;
        [SerializeField] Color good = new Color(0.55f, 0.85f, 1f);
        [SerializeField] Color offBeat = new Color(0.75f, 0.75f, 0.8f);
        [SerializeField] Color stamina = new Color(0.35f, 0.9f, 0.55f);
        [SerializeField] Color staminaLow = new Color(1f, 0.35f, 0.35f);
        [SerializeField] Color pipIdle = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] Color pipHit = new Color(0.4f, 1f, 0.6f);
        [SerializeField] Color pipNow = new Color(1f, 1f, 1f, 0.6f);

        readonly List<Image> _pips = new List<Image>();
        readonly HashSet<int> _hitBeatsInMeasure = new HashSet<int>();
        long _measure = long.MinValue;
        float _judgeTimer, _phraseTimer, _bannerTimer;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
            if (health == null && combat != null) health = combat.GetComponent<PlayerHealth>();
            if (arena == null) arena = FindAnyObjectByType<ArenaDirector>();
        }

        void OnEnable()
        {
            if (combat != null)
            {
                combat.ActionJudged += OnJudged;
                combat.PhraseTriggered += OnPhrase;
                combat.FinisherReady += OnFinisherReady;
                combat.FinisherFired += OnFinisherFired;
            }
            if (conductor != null) conductor.SectionChanged += OnSection;
            if (combat != null) combat.Parried += OnParried;
            if (arena != null)
            {
                arena.WaveStarted += OnWaveStarted;
                arena.WaveCleared += OnWaveCleared;
                arena.PlayerDowned += OnDowned;
                arena.ArenaCleared += OnArenaCleared;
                arena.EnemyGuarded += OnGuarded;
                arena.EnemyGuardBroken += OnGuardBroken;
            }
        }

        void OnDisable()
        {
            if (combat != null)
            {
                combat.ActionJudged -= OnJudged;
                combat.PhraseTriggered -= OnPhrase;
                combat.FinisherReady -= OnFinisherReady;
                combat.FinisherFired -= OnFinisherFired;
            }
            if (conductor != null) conductor.SectionChanged -= OnSection;
            if (combat != null) combat.Parried -= OnParried;
            if (arena != null)
            {
                arena.WaveStarted -= OnWaveStarted;
                arena.WaveCleared -= OnWaveCleared;
                arena.PlayerDowned -= OnDowned;
                arena.ArenaCleared -= OnArenaCleared;
                arena.EnemyGuarded -= OnGuarded;
                arena.EnemyGuardBroken -= OnGuardBroken;
            }
        }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying) return;
            var beat = conductor.CurrentBeat;

            // Optional beat ring: shrinks toward the player as the next beat approaches, flashes on the beat.
            if (beatRing != null && beatRing.gameObject.activeSelf != showBeatCue)
                beatRing.gameObject.SetActive(showBeatCue);
            if (showBeatCue && beatRing != null)
            {
                float closing = 1f - conductor.BeatPhase;
                beatRing.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, closing * closing);
                if (beatRingImage != null)
                {
                    var c = beat.IsDownbeat ? perfect : great;
                    c.a = Mathf.Lerp(0.15f, 0.9f, 1f - closing);
                    beatRingImage.color = c;
                }
            }

            // Measure pips
            if (beat.Measure != _measure)
            {
                _measure = beat.Measure;
                _hitBeatsInMeasure.Clear();
                BuildPips(beat.BeatsPerMeasure);
            }
            for (int i = 0; i < _pips.Count; i++)
            {
                _pips[i].color = _hitBeatsInMeasure.Contains(i) ? pipHit : i == beat.BeatInMeasure ? pipNow : pipIdle;
                _pips[i].rectTransform.localScale = Vector3.one * (i == 0 ? 1.3f : 1f);
            }

            if (signatureText != null)
                signatureText.text = $"{beat.Signature}  {beat.Bpm:0} BPM" + (combat != null && combat.FinisherQueued ? "  FINISHER!" : "");

            if (combat != null)
            {
                var g = combat.Groove;
                if (grooveFill != null) grooveFill.fillAmount = g.Value / 100f;
                if (grooveText != null) grooveText.text = $"GROOVE x{g.ScoreMultiplier:0.#}";
                var st = combat.Stamina;
                if (staminaFill != null)
                {
                    staminaFill.fillAmount = st.Normalized;
                    staminaFill.color = Color.Lerp(staminaLow, stamina, Mathf.Clamp01(st.Normalized * 2f));
                }
                if (scoreText != null) scoreText.text = combat.Score.Total.ToString("N0");
            }

            if (health != null && hpFill != null) hpFill.fillAmount = health.Hp / health.MaxHp;
            if (arena != null && waveText != null)
            {
                if (arena.IsCleared) waveText.text = "ARENA CLEAR";
                else if (arena.IsRunning)
                {
                    int t = Mathf.CeilToInt(arena.TimeLeft);
                    waveText.text = $"WAVE {arena.WaveNumber}   {t / 60}:{t % 60:00}";
                }
                else waveText.text = "GET READY";
            }

            Fade(judgementText, ref _judgeTimer, 0.5f);
            Fade(phraseText, ref _phraseTimer, 1.2f);
            Fade(bannerText, ref _bannerTimer, 1.5f);
        }

        void BuildPips(int count)
        {
            if (pipRow == null || _pips.Count == count) return;
            foreach (var p in _pips) Destroy(p.gameObject);
            _pips.Clear();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Pip{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(pipRow, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(22, 22);
                _pips.Add(go.GetComponent<Image>());
            }
        }

        void OnJudged(BeatActionResult r)
        {
            var j = r.Effective;
            if (r.Outcome == RegisterOutcome.Accepted && r.Judge.Beat.Measure == _measure)
                _hitBeatsInMeasure.Add(r.Judge.Beat.BeatInMeasure);
            if (j == Judgement.OffBeat) _hitBeatsInMeasure.Clear();

            if (judgementText == null) return;
            string label = r.Outcome == RegisterOutcome.DoubleInput ? "DOUBLE"
                         : j == Judgement.OffBeat ? "OFF-BEAT"
                         : j.ToString().ToUpperInvariant();
            if (r.Winded) label += "  WINDED";
            judgementText.text = $"{label}  {r.Judge.OffsetMs:+0;-0;0} ms";
            judgementText.color = j switch
            {
                Judgement.Perfect => perfect,
                Judgement.Great => great,
                Judgement.Good => good,
                _ => offBeat,
            };
            _judgeTimer = 0.5f;
        }

        void OnPhrase(PhrasePattern p)
        {
            if (phraseText == null) return;
            phraseText.text = p.Name;
            _phraseTimer = 1.2f;
        }

        void OnFinisherReady(BeatInfo b) => ShowBanner("BAR COMPLETE");
        void OnFinisherFired(float dmg) => ShowBanner($"FINISHER  {dmg:0}");
        void OnSection(BeatInfo b) => ShowBanner($"{b.Signature} @ {b.Bpm:0}");

        void OnParried(string enemy) => ShowBanner("PARRY!");
        void OnWaveStarted(int n, string name) => ShowBanner($"WAVE {n}: {name.ToUpperInvariant()}");
        void OnWaveCleared(int n) => ShowBanner("WAVE CLEAR");
        void OnDowned() => ShowBanner("DOWN — RETRYING WAVE");
        void OnArenaCleared(long score) => ShowBanner($"ARENA CLEAR  {score:N0}", 6f);
        void OnGuardBroken(BeatEnemy e) => ShowBanner("GUARD BROKEN!");

        void OnGuarded(BeatEnemy e)
        {
            if (phraseText == null) return;
            phraseText.text = "GUARDED — try Syncopate (Strike, rest, Smash)";
            _phraseTimer = 1.2f;
        }

        void ShowBanner(string text, float seconds)
        {
            if (bannerText == null) return;
            bannerText.text = text;
            _bannerTimer = seconds;
        }

        void ShowBanner(string text)
        {
            if (bannerText == null) return;
            bannerText.text = text;
            _bannerTimer = 1.5f;
        }

        static void Fade(Text t, ref float timer, float duration)
        {
            if (t == null) return;
            timer = Mathf.Max(0f, timer - Time.deltaTime);
            var c = t.color;
            c.a = Mathf.Clamp01(timer / duration * 2f);
            t.color = c;
        }
    }
}
