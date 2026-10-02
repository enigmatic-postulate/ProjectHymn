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
        [SerializeField] Transform cameraRig;

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
        Coroutine _hitStop, _shake;
        Vector3 _rigBase;

        void Awake()
        {
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (cameraRig == null && Camera.main != null) cameraRig = Camera.main.transform;
            if (cameraRig != null) _rigBase = cameraRig.localPosition;

            _voices = new AudioSource[8];
            for (int i = 0; i < _voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _voices[i] = src;
            }
        }

        void Start() => BuildSounds();

        void OnEnable()
        {
            if (combat == null) return;
            combat.ActionJudged += OnJudged;
            combat.FinisherFired += OnFinisher;
        }

        void OnDisable()
        {
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
            if (cameraRig != null)
            {
                if (_shake != null) StopCoroutine(_shake);
                _shake = StartCoroutine(Shake());
            }
        }

        void PlayQuantized(AudioClip clip, float volume)
        {
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

        IEnumerator Shake()
        {
            float t = 0f;
            while (t < shakeDuration)
            {
                float k = 1f - t / shakeDuration;
                cameraRig.localPosition = _rigBase + (Vector3)(Random.insideUnitCircle * shakeAmount * k);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            cameraRig.localPosition = _rigBase;
            _shake = null;
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
