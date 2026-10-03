using System.Collections;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Game feel for M1 (GDD): hit-stop on Perfect hits, hit sounds pitched to the song's key and
    /// snapped to the 16th-note grid so even a Good hit sounds in time, a muted thud for off-beat
    /// hits, and a short camera shake on Bar Finishers.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] BeatCombatController combat;
        [SerializeField] Conductor conductor;
        [Tooltip("The angled camera rig; shakes go through it so they never fight the follow.")]
        [SerializeField] CameraRig cameraRig;

        [Header("Hit-stop")]
        [SerializeField] float perfectHitStop = 0.04f;
        [SerializeField] float finisherHitStop = 0.08f;

        [Header("Hit sounds")]
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.7f;
        [Tooltip("Snap to the next 16th note if it is at most this far away; otherwise play immediately.")]
        [SerializeField, Range(0f, 0.12f)] float maxQuantizeDelay = 0.06f;

        [Header("Finisher shake")]
        [SerializeField] float shakeAmount = 0.18f;
        [SerializeField] float shakeDuration = 0.15f;

        AudioSource[] _voices;
        int _nextVoice;
        AudioClip _perfect, _great, _good, _thud, _finisher;
        AudioClip _telegraph, _telegraphHeavy, _parry, _hurt;
        Coroutine _hitStop;

        void Awake()
        {
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (cameraRig == null) cameraRig = FindAnyObjectByType<CameraRig>();

            _voices = new AudioSource[8];
            for (int i = 0; i < _voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _voices[i] = src;
            }
        }

        public float SfxVolume { get => sfxVolume; set => sfxVolume = Mathf.Clamp01(value); }

        void Start() => BuildSounds();

        void OnEnable()
        {
            if (conductor != null) conductor.SectionChangeUpcoming += OnSectionUpcoming;
            if (combat == null) return;
            combat.ActionJudged += OnJudged;
            combat.FinisherFired += OnFinisher;
        }

        void OnDisable()
        {
            if (conductor != null) conductor.SectionChangeUpcoming -= OnSectionUpcoming;
            if (combat == null) return;
            combat.ActionJudged -= OnJudged;
            combat.FinisherFired -= OnFinisher;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        /// <summary>Synthesize the hit sounds in the current song's key. Call again after changing songs.</summary>
        public void BuildSounds()
        {
            var song = conductor != null ? conductor.Song : null;
            int root = song != null ? song.keyRoot : 0;
            bool minor = song != null && song.minorKey;
            int third = minor ? 3 : 4;

            int tonic = 72 + root; // tonic in octave 5
            _perfect = Synth.Chord("HitPerfect", new[] { tonic + 12, tonic + 19, tonic + 24 }, 0.28f, 14f, bright: true);
            _great = Synth.Chord("HitGreat", new[] { tonic, tonic + 7 }, 0.18f, 22f, bright: false);
            _good = Synth.Chord("HitGood", new[] { tonic - 12 + third + 12 }, 0.12f, 30f, bright: false);
            _finisher = Synth.Chord("HitFinisher", new[] { tonic - 12, tonic - 5, tonic, tonic + third, tonic + 7 }, 0.6f, 6f, bright: true);
            _thud = Synth.Thud("HitOffBeat");

            // Enemy cues: a low tonic blip one measure before each attack (lower and longer for heavies),
            // a bright clang for a parry, a dull hit when the player is struck.
            _telegraph = Synth.Chord("EnemyTelegraph", new[] { tonic - 12 }, 0.09f, 40f, bright: false);
            _telegraphHeavy = Synth.Chord("EnemyTelegraphHeavy", new[] { tonic - 24, tonic - 17 }, 0.22f, 16f, bright: false);
            _parry = Synth.Chord("Parry", new[] { tonic + 24, tonic + 28, tonic + 31 }, 0.35f, 10f, bright: true);
            _hurt = Synth.Thud("PlayerHurt");
        }

        void OnJudged(BeatActionResult r)
        {
            if (!r.IsAttack) return;
            var j = r.Effective;

            if (j == Judgement.OffBeat) { PlayNow(_thud, 0.8f); return; }

            var clip = j switch
            {
                Judgement.Perfect => _perfect,
                Judgement.Great => _great,
                _ => _good,
            };
            PlayQuantized(clip, 1f);
            if (j == Judgement.Perfect) HitStop(perfectHitStop);
        }

        void OnFinisher(float damage)
        {
            PlayQuantized(_finisher, 1f);
            HitStop(finisherHitStop);
            if (cameraRig != null) cameraRig.Shake(shakeAmount, shakeDuration);
        }

        /// <summary>GDD: a cymbal swell over the bar before a tempo or meter change.</summary>
        void OnSectionUpcoming(BeatInfo next)
        {
            var now = conductor.CurrentBeat;
            float bar = (float)(now.BeatsPerMeasure * now.SecondsPerBeat);
            PlayNow(Synth.Swell("MeterSwell", Mathf.Clamp(bar, 0.5f, 4f)), 0.7f);
        }

        /// <summary>Enemy attack telegraph cue (one measure before it lands).</summary>
        public void PlayTelegraph(bool heavy) => PlayNow(heavy ? _telegraphHeavy : _telegraph, heavy ? 0.9f : 0.6f);

        public void PlayParry()
        {
            PlayQuantized(_parry, 1f);
            HitStop(perfectHitStop * 1.5f);
        }

        public void PlayHurt()
        {
            PlayNow(_hurt, 1f);
            if (cameraRig != null) cameraRig.Shake(shakeAmount, shakeDuration);
        }

        void PlayQuantized(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (conductor == null || !conductor.IsPlaying) { PlayNow(clip, volume); return; }
            // Next 16th note on the tempo map, measured from what the player hears.
            double now = conductor.SongTime;
            double pos = conductor.Map.TimeToBeat(now);
            double next16 = System.Math.Ceiling(pos * 4.0 - 1e-6) / 4.0;
            double delay = conductor.Map.BeatToTime(next16) - now;
            if (delay < 0 || delay > maxQuantizeDelay) { PlayNow(clip, volume); return; }

            var v = NextVoice();
            v.clip = clip;
            v.volume = sfxVolume * volume;
            v.PlayScheduled(AudioSettings.dspTime + delay);
        }

        void PlayNow(AudioClip clip, float volume)
        {
            if (clip == null) return;
            var v = NextVoice();
            v.clip = clip;
            v.volume = sfxVolume * volume;
            v.Play();
        }

        AudioSource NextVoice()
        {
            var v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return v;
        }

        void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            if (_hitStop != null) StopCoroutine(_hitStop);
            _hitStop = StartCoroutine(HitStopRoutine(seconds));
        }

        IEnumerator HitStopRoutine(float seconds)
        {
            // Freezes physics and animation only. The Conductor runs on the audio clock, so the beat never slips.
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            _hitStop = null;
        }

        /// <summary>Tiny procedural synth so the prototype needs no audio assets.</summary>
        static class Synth
        {
            const int Rate = 48000;

            static float MidiToHz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

            public static AudioClip Chord(string name, int[] notes, float seconds, float decay, bool bright)
            {
                int n = Mathf.CeilToInt(seconds * Rate);
                var data = new float[n];
                float norm = 0.6f / notes.Length;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 400f); // 2.5 ms attack, no click
                    float s = 0f;
                    foreach (var m in notes)
                    {
                        float f = MidiToHz(m);
                        s += Mathf.Sin(2f * Mathf.PI * f * t);
                        if (bright) s += 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) * Mathf.Exp(-t * decay * 2f);
                    }
                    data[i] = s * norm * env;
                }
                var clip = AudioClip.Create(name, n, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }

            /// <summary>Rising, brightening noise that peaks right at the end: a reverse-cymbal swell.</summary>
            public static AudioClip Swell(string name, float seconds)
            {
                int n = Mathf.CeilToInt(seconds * Rate);
                var data = new float[n];
                var rng = new System.Random(11);
                float hp = 0f, prev = 0f;
                for (int i = 0; i < n; i++)
                {
                    float k = i / (float)n;
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    hp = 0.92f * (hp + noise - prev); // high-passed hiss
                    prev = noise;
                    float env = k * k * k;            // slow start, fast rise
                    data[i] = hp * env * 0.5f;
                }
                var clip = AudioClip.Create(name, n, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }

            public static AudioClip Thud(string name)
            {
                int n = Rate / 10; // 100 ms
                var data = new float[n];
                var rng = new System.Random(7);
                float lp = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 45f);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += 0.06f * (noise - lp); // dull, low-passed
                    float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 60f, t * 10f) * t);
                    data[i] = (0.6f * body + 1.5f * lp) * env * 0.6f;
                }
                var clip = AudioClip.Create(name, n, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }
        }
    }
}
