using ProjectHymn.Core;
using ProjectHymn.Movement;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Sound effects that aren't hits (VS-4, Look &amp; Sound): footsteps, jumps and landings, enemy wind-up cues and menu
    /// clicks, synthesized at start-up (no assets), played on a small pool of voices that survives scene loads, scaled by
    /// the effects volume. It also pushes the music and effects volumes to the scene's Conductor each frame.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx _instance;
        AudioSource[] _voices;
        int _next;
        Conductor _conductor;

        public static AudioClip Step, Jump, AirJump, Land, LandHard, WindUp, WindUpViolet, MenuMove, MenuConfirm, MenuBack;

        public static Sfx Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Sfx>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        void Awake()
        {
            _voices = new AudioSource[10];
            for (int i = 0; i < _voices.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _voices[i] = s;
            }
            Build();
        }

        void Update()
        {
            if (_conductor == null) _conductor = FindAnyObjectByType<Conductor>();
            if (_conductor != null)
            {
                _conductor.MusicVolume = GameSettings.MusicVolume;
                _conductor.ClickVolume = GameSettings.SfxVolume;
            }
        }

        /// <summary>Play a clip now at <paramref name="volume"/> (times the effects volume), with a little pitch spread.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitchSpread = 0f)
        {
            if (clip == null) return;
            var s = Instance;
            var v = s._voices[s._next];
            s._next = (s._next + 1) % s._voices.Length;
            v.pitch = 1f + (pitchSpread > 0f ? Random.Range(-pitchSpread, pitchSpread) : 0f);
            v.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        /// <summary>Make sure the pool exists (and the volumes reach the music) from the first scene on.</summary>
        public static void Ensure() { var _ = Instance; }

        // ---------------- Synthesis ----------------

        const int Rate = 44100;

        static void Build()
        {
            if (Step != null) return;
            var rng = new System.Random(3);
            Step = Make("Step", 0.09f, (t, i) =>
            {
                float n = Noise(rng);
                return (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 70f, t / 0.09f) * t) * 0.6f + n * 0.35f) * Mathf.Exp(-t * 55f);
            }, lowpass: 0.12f);
            Jump = Make("Jump", 0.16f, (t, i) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(380f, 760f, t / 0.16f) * t) * Env(t, 0.004f, 22f) * 0.35f
                                                 + Noise(rng) * Env(t, 0.002f, 40f) * 0.15f, lowpass: 0.3f);
            AirJump = Make("AirJump", 0.2f, (t, i) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(520f, 1040f, t / 0.2f) * t)
                                                       + 0.4f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(780f, 1560f, t / 0.2f) * t)) * Env(t, 0.004f, 18f) * 0.25f);
            Land = Make("Land", 0.14f, (t, i) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 50f, t / 0.14f) * t) * 0.8f + Noise(rng) * 0.5f) * Mathf.Exp(-t * 32f), lowpass: 0.1f);
            LandHard = Make("LandHard", 0.25f, (t, i) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, t / 0.25f) * t) + Noise(rng) * 0.7f) * Mathf.Exp(-t * 18f), lowpass: 0.08f);
            // Enemy wind-up: a short rising metallic swell, the beat before a blow; violet (unblockable) is lower and rougher.
            WindUp = Make("WindUp", 0.22f, (t, i) =>
            {
                float f = Mathf.Lerp(600f, 1300f, t / 0.22f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 1.5f * t)) * Mathf.Clamp01(t / 0.18f) * Mathf.Exp(-Mathf.Max(0f, t - 0.18f) * 60f) * 0.18f;
            });
            WindUpViolet = Make("WindUpViolet", 0.26f, (t, i) =>
            {
                float f = Mathf.Lerp(220f, 440f, t / 0.26f);
                float saw = 2f * ((f * t) % 1f) - 1f;
                return (saw * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 0.5f * t)) * Mathf.Clamp01(t / 0.22f) * Mathf.Exp(-Mathf.Max(0f, t - 0.22f) * 50f) * 0.2f;
            }, lowpass: 0.35f);
            MenuMove = Make("MenuMove", 0.05f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-t * 90f) * 0.2f);
            MenuConfirm = Make("MenuConfirm", 0.18f, (t, i) => (Mathf.Sin(2f * Mathf.PI * (t < 0.06f ? 1318.5f : 1975.5f) * t)) * Mathf.Exp(-t * 18f) * 0.22f);
            MenuBack = Make("MenuBack", 0.12f, (t, i) => Mathf.Sin(2f * Mathf.PI * (t < 0.05f ? 987.8f : 659.3f) * t) * Mathf.Exp(-t * 24f) * 0.2f);
        }

        static float Env(float t, float attack, float decay) => Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decay);
        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> f, float lowpass = 1f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = f(t, i) * Mathf.Min(1f, t * 600f);           // no click at the start
                lp += lowpass * (x - lp);
                data[i] = lp;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

}
