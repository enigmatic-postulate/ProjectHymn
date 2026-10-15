using System;
using System.Collections.Generic;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The village's music (VS-5): a base loop plus a layer for each verse brought home, all the same length and started
    /// together on the audio clock so they stay locked. Layers for verses already home play from the start; a verse
    /// coming home fades its layer in (<see cref="Reveal"/>). It also keeps a beat clock (<see cref="Beat"/>) from the
    /// base loop's sample position, so the village's lanterns and the dialogue's word crawl move with the waltz.
    /// </summary>
    public class LayeredMusic : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public AudioClip clip;
            [Tooltip("Plays once this many verses are home (1 to 3).")]
            public int verse = 1;
        }

        public AudioClip baseClip;
        public List<Layer> layers = new List<Layer>();
        public float bpm = 88f;
        public int beatsPerBar = 3;
        [Tooltip("Seconds into the clip of the first downbeat.")]
        public float firstBeat;
        [Range(0f, 1f)] public float volume = 0.8f;

        public static LayeredMusic Active { get; private set; }

        AudioSource _base;
        readonly List<AudioSource> _srcs = new List<AudioSource>();
        readonly List<float> _gain = new List<float>(), _target = new List<float>();
        float _fadeSpeed = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        void Awake()
        {
            Active = this;
            int chapter = GameSession.Exists ? Story.Chapter(GameSession.Instance.Data) : 0;
            _base = Source(baseClip);
            double start = AudioSettings.dspTime + 0.15;
            if (_base.clip != null) _base.PlayScheduled(start);
            foreach (var l in layers)
            {
                var s = Source(l != null ? l.clip : null);
                _srcs.Add(s);
                float g = l != null && l.verse <= chapter ? 1f : 0f;
                _gain.Add(g); _target.Add(g);
                if (s.clip != null) s.PlayScheduled(start);
            }
            Apply();
        }

        void OnDestroy() { if (Active == this) Active = null; }

        AudioSource Source(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        /// <summary>Fade in every layer up to <paramref name="verse"/> over <paramref name="seconds"/>.</summary>
        public void Reveal(int verse, float seconds = 3f)
        {
            _fadeSpeed = 1f / Mathf.Max(0.05f, seconds);
            for (int i = 0; i < layers.Count; i++) if (layers[i] != null && layers[i].verse <= verse) _target[i] = 1f;
        }

        /// <summary>Hold every layer back to <paramref name="verse"/> - 1 (the reveal fades the newest in).</summary>
        public void HoldBack(int verse)
        {
            for (int i = 0; i < layers.Count; i++)
                if (layers[i] != null && layers[i].verse >= verse) { _gain[i] = 0f; _target[i] = 0f; }
            Apply();
        }

        /// <summary>Beats since the first downbeat of the loop, or -1 when nothing plays.</summary>
        public double Beat
        {
            get
            {
                if (_base == null || _base.clip == null || !_base.isPlaying) return -1.0;
                double t = _base.timeSamples / (double)_base.clip.frequency - firstBeat;
                return Math.Max(0.0, t) * bpm / 60.0;
            }
        }

        void Update()
        {
            for (int i = 0; i < _gain.Count; i++) _gain[i] = Mathf.MoveTowards(_gain[i], _target[i], Time.unscaledDeltaTime * _fadeSpeed);
            Apply();
        }

        void Apply()
        {
            float v = volume * GameSettings.MusicVolume;
            if (_base != null) _base.volume = v;
            for (int i = 0; i < _srcs.Count; i++) _srcs[i].volume = v * _gain[i];
        }
    }
}
