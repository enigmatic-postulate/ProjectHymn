using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Sound for the M2 sword prototype: hit sounds pitched to the song's key and snapped to the 16th-note grid so even
    /// a Good hit sounds in time, a soft tick for hits that deal no damage (single notes), a muted thud for off-beat
    /// notes, a chord when a rhythm sequence completes, and a small camera shake on power hits.
    /// (Hit-stop is gone: animation runs on the music's clock, so freezing time would not freeze the swing.)
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] SwordCombatController combat;
        [SerializeField] Conductor conductor;
        [SerializeField] CameraRig cameraRig;

        [Header("Hit sounds")]
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.7f;
        [Tooltip("Snap to the next 16th note if it is at most this far away; otherwise play immediately.")]
        [SerializeField, Range(0f, 0.12f)] float maxQuantizeDelay = 0.06f;

        AudioSource[] _voices;
        int _nextVoice;
        AudioClip _truePerfect, _perfect, _great, _good, _thud, _finisher, _tick;

        void Awake()
        {
            if (combat == null) combat = FindAnyObjectByType<SwordCombatController>();
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

        void Start() => BuildSounds();

        void OnEnable()
        {
            if (combat != null)
            {
                combat.NoteJudged += OnNote;
                combat.Impacted += OnImpact;
                combat.SequenceCompleted += OnSequence;
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
            }
            if (conductor != null) conductor.SongStarted -= OnSong;
        }

        void OnSong(SongData s) => BuildSounds();

        /// <summary>Synthesize the hit sounds in the current song's key.</summary>
        public void BuildSounds()
        {
            var song = conductor != null ? conductor.Song : null;
            int root = song != null ? song.keyRoot : 0;
            bool minor = song != null && song.minorKey;
            int third = minor ? 3 : 4;

            int tonic = 72 + root; // tonic in octave 5
            _perfect = Synth.Chord("HitPerfect", new[] { tonic + 12, tonic + 19, tonic + 24 }, 0.28f, 14f, bright: true);
            // True Perfect: its own sound above the Perfect chord (an octave higher, with a ring-out).
            _truePerfect = Synth.Chord("HitTruePerfect", new[] { tonic + 19, tonic + 24, tonic + 28, tonic + 31, tonic + 36 }, 0.5f, 7f, bright: true);
            _great = Synth.Chord("HitGreat", new[] { tonic, tonic + 7 }, 0.18f, 22f, bright: false);
            _good = Synth.Chord("HitGood", new[] { tonic - 12 + third + 12 }, 0.12f, 30f, bright: false);
            _finisher = Synth.Chord("HitFinisher", new[] { tonic - 12, tonic - 5, tonic, tonic + third, tonic + 7 }, 0.6f, 6f, bright: true);
            _tick = Synth.Chord("Tick", new[] { tonic + 24 }, 0.05f, 60f, bright: false);
            _thud = Synth.Thud("HitOffBeat");
        }

        void OnNote(NoteResult n)
        {
            if (!n.OnBeat) PlayNow(_thud, 0.8f);
        }

        void OnImpact(ImpactInfo i)
        {
            if (!i.Note.OnBeat) return;
            if (!i.DealtDamage) { PlayQuantized(_tick, 0.5f); return; }
            var clip = i.Note.Effective switch
            {
                Judgement.TruePerfect => _truePerfect,
                Judgement.Perfect => _perfect,
                Judgement.Great => _great,
                _ => _good,
            };
            PlayQuantized(clip, 1f);
            if (i.Note.Effective == Judgement.TruePerfect && cameraRig != null) cameraRig.Shake(0.06f, 0.1f);
            if (i.Note.Move != null && i.Note.Move.effect == "power" && cameraRig != null) cameraRig.Shake(0.1f, 0.15f);
        }

        void OnSequence(SwordSequence s) => PlayQuantized(_finisher, 1f);

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
            v.volume = sfxVolume * volume * GameSettings.SfxVolume;
            v.PlayScheduled(AudioSettings.dspTime + delay);
        }

        void PlayNow(AudioClip clip, float volume)
        {
            var v = NextVoice();
            v.clip = clip;
            v.volume = sfxVolume * volume * GameSettings.SfxVolume;
            v.Play();
        }

        AudioSource NextVoice()
        {
            var v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return v;
        }

        /// <summary>Tiny procedural synth so the prototype needs no audio assets.</summary>
        internal static class Synth
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
