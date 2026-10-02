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

        // Adaptive (phase-looping) playback
        AdaptiveTimeline _timeline;
        AudioSource[] _blockSources;
        int _nextBlockSource;
        int _blocksScheduled;
        int _currentPhase;
        bool _warnedClipLength;

        public SongData Song => song;
        public TempoMap Map { get; private set; }
        public bool IsPlaying { get; private set; }

        /// <summary>Seconds since beat 0 as heard. Negative during the count-in.</summary>
        public double SongTime { get; private set; }

        /// <summary>Song time corrected for the player's audio/input latency. Judge input against this.</summary>
        public double InputSongTime => SongTime - CalibrationSeconds;

        public double CalibrationSeconds { get; set; }

        public BeatInfo CurrentBeat { get; private set; }
        /// <summary>0 at the beat, approaching 1 just before the next one. Drive pulses with this.</summary>
        public float BeatPhase { get; private set; }

        public event Action<BeatInfo> BeatStarted;
        public event Action<BeatInfo> MeasureStarted;
        public event Action<BeatInfo> SectionChanged;   // fires when tempo or meter changes
        /// <summary>Fires on the downbeat one measure before a tempo or meter change (GDD: count-in and swell). Arg = first beat after the change.</summary>
        public event Action<BeatInfo> SectionChangeUpcoming;
        /// <summary>Adaptive songs only: the music has moved into a new phase (fires on that phase's first beat).</summary>
        public event Action<int, BeatInfo> PhaseChanged;

        public bool IsAdaptive => _timeline != null;
        /// <summary>Phase currently heard (0 for non-adaptive songs).</summary>
        public int CurrentPhase => _currentPhase;
        public int RequestedPhase => _timeline != null ? _timeline.RequestedPhase : 0;
        public int PhaseCount => _timeline != null ? _timeline.PhaseCount : 1;
        public string PhaseName(int phase) => _timeline?.PhaseName(phase);

        /// <summary>
        /// Adaptive songs: move to another phase. Blocks already scheduled (a few bars ahead) still play,
        /// so the change lands on the next loop boundary of composed music.
        /// </summary>
        public void RequestPhase(int phase) { if (_timeline != null) _timeline.RequestPhase(phase); }

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
            _blockSources = new AudioSource[4];
            for (int i = 0; i < _blockSources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                _blockSources[i] = src;
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
            // Scheduled playback needs the samples ready; clips imported without "Preload Audio Data" aren't.
            if (song.clip != null && song.clip.loadState != AudioDataLoadState.Loaded) song.clip.LoadAudioData();
            _timeline = null;
            _currentPhase = 0;
            _blocksScheduled = 0;
            _warnedClipLength = false;

            double now = AudioSettings.dspTime;
            _dspSongStart = now + startDelay;
            _lastDsp = _smoothedDsp = now;

            if (song.IsAdaptive)
            {
                _timeline = new AdaptiveTimeline(song.sections, song.phases, 0);
                _timeline.Extend(LookaheadSeconds(_timeline.Map.GetBeat(0)));
                Map = _timeline.Map;
                ScheduleBlocks();
            }
            else
            {
                Map = song.BuildTempoMap();
            }

            if (song.clip != null && !song.IsAdaptive)
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
        }

        public void Stop()
        {
            IsPlaying = false;
            _music.Stop();
            foreach (var s in _clickSources) s.Stop();
            foreach (var s in _blockSources) s.Stop();
        }

        public void SaveCalibration(float ms)
        {
            CalibrationSeconds = ms / 1000.0;
            PlayerPrefs.SetFloat(CalibrationPrefKey, ms);
            PlayerPrefs.Save();
        }

        void Update()
        {
            if (!IsPlaying) return;
            UpdateClock();
            if (_timeline != null)
            {
                if (_timeline.Extend(SongTime + LookaheadSeconds(CurrentBeat)))
                {
                    Map = _timeline.Map;
                    ScheduleBlocks();
                }
            }
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

            if (_timeline != null && SongTime >= 0)
            {
                int phase = _timeline.PhaseAt(SongTime);
                if (phase != _currentPhase)
                {
                    _currentPhase = phase;
                    PhaseChanged?.Invoke(phase, beat);
                }
            }

            if (beat.IsDownbeat)
            {
                // One measure early: is the next bar in a different tempo or meter?
                var next = Map.GetBeat(Map.MeasureStartBeat(beat.Measure + 1));
                if (next.SectionIndex != beat.SectionIndex) SectionChangeUpcoming?.Invoke(next);
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

        /// <summary>Plan at least three measures ahead (and 3 s), so enemy telegraphs never cross unplanned music.</summary>
        static double LookaheadSeconds(BeatInfo beat) => Math.Max(3.0, 3 * beat.BeatsPerMeasure * beat.SecondsPerBeat);

        /// <summary>
        /// Schedules each new timeline block's slice of the clip on the audio clock, back to back, so loops
        /// and phase switches are sample-accurate. (Use Decompress On Load or Compressed In Memory for
        /// adaptive songs; streamed clips may not honour a start position set before PlayScheduled.)
        /// </summary>
        void ScheduleBlocks()
        {
            if (_timeline == null) return;
            var blocks = _timeline.Blocks;
            for (; _blocksScheduled < blocks.Count; _blocksScheduled++)
            {
                if (song.clip == null) continue;
                var b = blocks[_blocksScheduled];
                var clip = song.clip;
                double clipStart = song.firstBeatOffset + b.SourceStart;
                double clipEnd = song.firstBeatOffset + b.SourceEnd;
                if (clipEnd > clip.length + 0.01 && !_warnedClipLength)
                {
                    _warnedClipLength = true;
                    Debug.LogWarning($"[Conductor] '{song.name}' phase '{_timeline.PhaseName(b.Phase)}' runs past the end of the clip ({clipEnd:F2}s > {clip.length:F2}s). Check its sections.", this);
                }

                var src = _blockSources[_nextBlockSource];
                _nextBlockSource = (_nextBlockSource + 1) % _blockSources.Length;
                src.Stop();
                src.clip = clip;
                double dspEnd = _dspSongStart + song.firstBeatOffset + b.EndTime;
                bool firstFromTop = _blocksScheduled == 0 && b.SourceStart <= 1e-6;
                if (firstFromTop)
                {
                    // Include any intro before beat 0.
                    src.timeSamples = 0;
                    src.PlayScheduled(_dspSongStart);
                }
                else
                {
                    src.timeSamples = Mathf.Clamp((int)(clipStart * clip.frequency), 0, Math.Max(0, clip.samples - 1));
                    src.PlayScheduled(_dspSongStart + song.firstBeatOffset + b.StartTime);
                }
                src.SetScheduledEndTime(dspEnd);
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
