using System;
using UnityEngine;

namespace ProjectHymn.Core
{
    /// <summary>
    /// The single source of musical time. Schedules the song on the DSP clock and exposes song time,
    /// beat events and the tempo map. Gameplay judges input against <see cref="InputSongTime"/>;
    /// visuals listen to <see cref="BeatStarted"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : MonoBehaviour
    {
        public const string CalibrationPrefKey = "ProjectHymn.CalibrationMs";

        [SerializeField] SongData song;
        [SerializeField] bool playOnStart = true;
        [Tooltip("Seconds between Play() and beat 0, so the audio is scheduled sample-accurately and the player gets a count-in.")]
        [SerializeField, Min(0.05f)] float startDelay = 1.0f;

        [Header("Metronome")]
        [SerializeField] bool metronome = true;
        [SerializeField, Range(0f, 1f)] float metronomeVolume = 0.8f;

        AudioSource _music;
        AudioSource[] _clickSources;
        AudioClip _clickHigh, _clickLow;
        int _nextClickSource;
        long _nextClickBeat;

        double _dspSongStart;     // dsp time at which the clip starts
        double _lastDsp, _smoothedDsp;
        long _lastBeatFired = long.MinValue;
        long _lastMeasureFired = long.MinValue;
        int _lastSection = -1;

        public SongData Song => song;
        public TempoMap Map { get; private set; }
        public bool IsPlaying { get; private set; }
        /// <summary>Paused by a menu: song time holds still and the music is stopped until <see cref="Resume"/>.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>Seconds since beat 0 as heard. Negative during the count-in.</summary>
        public double SongTime { get; private set; }

        /// <summary>Song time corrected for the player's audio/input latency. Judge input against this.</summary>
        public double InputSongTime => SongTime - CalibrationSeconds;

        /// <summary>
        /// Calibrated song time at a moment on the <c>Time.realtimeSinceStartupAsDouble</c> clock, such as the moment an
        /// input event happened. Judging presses by their own time rather than the frame they were read in removes up to a
        /// frame (16.7 ms at 60 fps) of error, which True Perfect (\u00b115 ms) can't afford.
        /// </summary>
        public double InputSongTimeAt(double realtime)
        {
            if (double.IsNaN(realtime) || IsPaused || !IsPlaying) return InputSongTime;
            double dt = realtime - _songTimeRealtime;
            if (dt > 0.25 || dt < -0.25) dt = 0.0;          // a stale stamp: fall back to the frame's time
            return SongTime + dt - CalibrationSeconds;
        }

        double _songTimeRealtime;

        public double CalibrationSeconds { get; set; }

        public BeatInfo CurrentBeat { get; private set; }
        /// <summary>0 at the beat, approaching 1 just before the next one. Drive pulses with this.</summary>
        public float BeatPhase { get; private set; }

        public event Action<BeatInfo> BeatStarted;
        public event Action<BeatInfo> MeasureStarted;
        public event Action<BeatInfo> SectionChanged;   // fires when tempo or meter changes
        /// <summary>A song (or a test tempo) was started. Beat indices and song time restart.</summary>
        public event Action<SongData> SongStarted;

        public bool MetronomeEnabled { get => metronome; set => metronome = value; }

        void Awake()
        {
            _music = GetComponent<AudioSource>();
            _music.playOnAwake = false;
            CalibrationSeconds = PlayerPrefs.GetFloat(CalibrationPrefKey, 0f) / 1000.0;

            _clickHigh = MakeClick(1760f, "ClickHigh");
            _clickLow = MakeClick(1046f, "ClickLow");
            _clickSources = new AudioSource[4];
            for (int i = 0; i < _clickSources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _clickSources[i] = src;
            }
        }

        void Start()
        {
            if (!playOnStart) return;
            if (song == null)
            {
                Debug.LogWarning("[Conductor] No Song assigned; running a 140 BPM 4/4 metronome so the scene still works.", this);
                song = ScriptableObject.CreateInstance<SongData>();
                song.name = "FallbackMetronome";
            }
            Play(song);
        }

        public void Play(SongData data)
        {
            song = data;
            Map = song.BuildTempoMap();
            _music.Stop();
            foreach (var s in _clickSources) s.Stop();
            // Scheduled playback needs the samples ready; clips imported without "Preload Audio Data" aren't.
            if (song.clip != null && song.clip.loadState != AudioDataLoadState.Loaded) song.clip.LoadAudioData();

            double now = AudioSettings.dspTime;
            _dspSongStart = now + startDelay;
            _lastDsp = _smoothedDsp = now;

            if (song.clip != null)
            {
                _music.clip = song.clip;
                _music.loop = song.loop;
                _music.PlayScheduled(_dspSongStart);
            }

            IsPlaying = true;
            _lastBeatFired = long.MinValue;
            _lastMeasureFired = long.MinValue;
            _lastSection = -1;
            _nextClickBeat = (long)Math.Ceiling(Map.TimeToBeat(-song.firstBeatOffset - startDelay + 0.05));
            UpdateClock();
            SongStarted?.Invoke(song);
        }

        /// <summary>Hold the music and song time (pause menu). Safe to call twice.</summary>
        public void Pause()
        {
            if (!IsPlaying || IsPaused) return;
            IsPaused = true;
            _music.Stop();
            foreach (var s in _clickSources) s.Stop();
        }

        /// <summary>Carry on from where <see cref="Pause"/> held, rescheduled on the DSP clock so the grid stays exact.</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            const double lead = 0.12;
            double resumeAt = AudioSettings.dspTime + lead;
            double clipPos = SongTime + song.firstBeatOffset;          // where in the clip we stopped
            _dspSongStart = resumeAt - clipPos;
            _lastDsp = _smoothedDsp = AudioSettings.dspTime;
            if (song.clip != null)
            {
                if (clipPos >= 0)
                {
                    double len = song.clip.length;
                    if (len <= 0 || (!song.loop && clipPos >= len)) { UpdateClock(); return; }
                    double pos = song.loop && len > 0 ? clipPos % len : clipPos;
                    _music.timeSamples = Mathf.Clamp((int)(pos * song.clip.frequency), 0, Mathf.Max(0, song.clip.samples - 1));
                    _music.PlayScheduled(resumeAt);
                }
                else
                {
                    _music.timeSamples = 0;
                    _music.PlayScheduled(_dspSongStart);
                }
            }
            _nextClickBeat = CurrentBeat.Index + 1;
            UpdateClock();
        }

        public void Stop()
        {
            IsPaused = false;
            IsPlaying = false;
            _music.Stop();
            foreach (var s in _clickSources) s.Stop();
        }

        public void SaveCalibration(float ms)
        {
            CalibrationSeconds = ms / 1000.0;
            PlayerPrefs.SetFloat(CalibrationPrefKey, ms);
            PlayerPrefs.Save();
        }

        void Update()
        {
            if (!IsPlaying || IsPaused) return;
            UpdateClock();
            ScheduleClicks();
            FireBeatEvents();
        }

        void UpdateClock()
        {
            // dspTime advances in audio-buffer steps; smooth it with frame time between steps.
            double dsp = AudioSettings.dspTime;
            if (dsp != _lastDsp)
            {
                _lastDsp = dsp;
                _smoothedDsp = dsp;
            }
            else
            {
                _smoothedDsp += Time.unscaledDeltaTime;
            }

            SongTime = _smoothedDsp - _dspSongStart - song.firstBeatOffset;
            _songTimeRealtime = Time.realtimeSinceStartupAsDouble;

            double beatPos = Map.TimeToBeat(SongTime);
            long beatIndex = (long)Math.Floor(beatPos);
            CurrentBeat = Map.GetBeat(beatIndex);
            BeatPhase = (float)(beatPos - beatIndex);
        }

        void FireBeatEvents()
        {
            var beat = CurrentBeat;
            if (beat.Index == _lastBeatFired) return;
            _lastBeatFired = beat.Index;

            if (beat.SectionIndex != _lastSection)
            {
                _lastSection = beat.SectionIndex;
                SectionChanged?.Invoke(beat);
            }

            if (beat.IsDownbeat && beat.Measure != _lastMeasureFired)
            {
                _lastMeasureFired = beat.Measure;
                MeasureStarted?.Invoke(beat);
            }

            BeatStarted?.Invoke(beat);
        }

        void ScheduleClicks()
        {
            if (!metronome) { _nextClickBeat = CurrentBeat.Index + 1; return; }

            const double lookAhead = 0.25;
            while (true)
            {
                var b = Map.GetBeat(_nextClickBeat);
                double dspAt = _dspSongStart + song.firstBeatOffset + b.Time;
                if (dspAt > AudioSettings.dspTime + lookAhead) break;
                if (dspAt >= AudioSettings.dspTime)
                {
                    var src = _clickSources[_nextClickSource];
                    _nextClickSource = (_nextClickSource + 1) % _clickSources.Length;
                    src.clip = b.IsDownbeat ? _clickHigh : _clickLow;
                    src.volume = metronomeVolume;
                    src.PlayScheduled(dspAt);
                }
                _nextClickBeat++;
            }
        }

        static AudioClip MakeClick(float freq, string name)
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int length = rate / 15; // ~65 ms
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 60f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
