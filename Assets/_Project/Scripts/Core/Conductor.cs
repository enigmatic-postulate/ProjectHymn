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

        AudioSource _music; // kept for older scenes; music now plays on child voices
        AudioSource[] _clickSources;
        AudioClip _clickHigh, _clickLow;
        int _nextClickSource;
        long _nextClickBeat;

        double _dspSongStart;     // dsp time at which song time = -firstBeatOffset (clip time 0)
        double _lastDsp, _smoothedDsp;
        long _lastBeatFired = long.MinValue;
        long _lastMeasureFired = long.MinValue;
        int _lastSection = -1;

        // Adaptive (phase-looping) playback
        AdaptiveTimeline _timeline;
        int _blocksScheduled;
        int _currentPhase;
        bool _warnedClipLength;

        // Music layers: stems, or the single clip. Each layer has a small pool of voices on child objects
        // (one AudioLowPassFilter each), so blocks can be scheduled back to back.
        const int VoicesPerLayer = 4;
        class Voice { public AudioSource Src; public AudioLowPassFilter Filter; }
        readonly System.Collections.Generic.List<AudioClip> _layers = new System.Collections.Generic.List<AudioClip>();
        Voice[][] _voices = new Voice[0][];
        int[] _nextVoice = new int[0];
        float[] _layerVolume = new float[0];
        float _musicVolume = 1f;
        float _lowPassHz = 22000f;
        Transform _voiceRoot;

        // Pause
        double _pausedSongTime;
        const double ResumeDelay = 0.15;

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
        public bool IsPaused { get; private set; }

        /// <summary>Number of music layers playing (stems, or 1 for a single clip, 0 for metronome only).</summary>
        public int LayerCount => _layers.Count;
        public bool HasStems => song != null && song.stems != null && song.stems.Exists(c => c != null);

        /// <summary>Overall music volume (settings).</summary>
        public float MusicVolume { get => _musicVolume; set { _musicVolume = Mathf.Clamp01(value); ApplyMix(); } }

        /// <summary>Per-layer volume 0..1 (the music mixer fades stems in and out with Groove).</summary>
        public void SetLayerVolume(int layer, float volume)
        {
            if (layer < 0 || layer >= _layerVolume.Length) return;
            _layerVolume[layer] = Mathf.Clamp01(volume);
            ApplyMix();
        }

        /// <summary>Low-pass cutoff on all music (22000 = open).</summary>
        public void SetLowPass(float hz)
        {
            _lowPassHz = Mathf.Clamp(hz, 200f, 22000f);
            ApplyMix();
        }

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
            StopVoices();
            song = data;
            _timeline = null;
            _currentPhase = 0;
            _blocksScheduled = 0;
            _warnedClipLength = false;
            IsPaused = false;
            BuildLayers();

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
                StartLinear(-song.firstBeatOffset);
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
            IsPaused = false;
            if (_music != null) _music.Stop();
            foreach (var s in _clickSources) s.Stop();
            StopVoices();
        }

        /// <summary>Freeze song time and silence everything (pause menu).</summary>
        public void Pause()
        {
            if (!IsPlaying || IsPaused) return;
            IsPaused = true;
            _pausedSongTime = SongTime;
            foreach (var s in _clickSources) s.Stop();
            StopVoices();
        }

        /// <summary>Continue from the paused beat: audio is re-scheduled from that point after a short gap.</summary>
        public void Resume()
        {
            if (!IsPlaying || !IsPaused) return;
            IsPaused = false;
            double t = _pausedSongTime;
            double now = AudioSettings.dspTime;
            _dspSongStart = now + ResumeDelay - song.firstBeatOffset - t;
            _lastDsp = _smoothedDsp = now;

            if (_timeline != null)
            {
                int k = _timeline.BlockIndexAt(Math.Max(t, _timeline.Blocks[0].StartTime));
                ScheduleBlock(k, t);
                _blocksScheduled = k + 1;
                ScheduleBlocks();
            }
            else StartLinear(t);

            _nextClickBeat = (long)Math.Ceiling(Map.TimeToBeat(t) - 1e-6);
            UpdateClock();
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

        double DspAt(double songTime) => _dspSongStart + song.firstBeatOffset + songTime;

        void ScheduleBlocks()
        {
            if (_timeline == null) return;
            var blocks = _timeline.Blocks;
            for (; _blocksScheduled < blocks.Count; _blocksScheduled++)
            {
                var b = blocks[_blocksScheduled];
                // The very first block may start with the intro before beat 0 if it begins at the top of the song.
                bool fromTop = _blocksScheduled == 0 && b.SourceStart <= 1e-6;
                ScheduleBlock(_blocksScheduled, fromTop ? -song.firstBeatOffset : b.StartTime);
            }
        }

        /// <summary>
        /// Schedule one timeline block's slice of every layer on the audio clock, starting at song time
        /// <paramref name="from"/> (mid-block after a pause). Use Decompress On Load or Compressed In Memory
        /// for adaptive songs; streamed clips may not honour a start position set before PlayScheduled.
        /// </summary>
        void ScheduleBlock(int index, double from)
        {
            if (_layers.Count == 0) return;
            var b = _timeline.Blocks[index];
            bool fromTop = index == 0 && b.SourceStart <= 1e-6;
            double earliest = fromTop ? -song.firstBeatOffset : b.StartTime;
            from = Math.Max(from, earliest);
            if (from >= b.EndTime) return;

            double clipTime = song.firstBeatOffset + b.SourceStart + (from - b.StartTime);
            for (int l = 0; l < _layers.Count; l++)
            {
                var clip = _layers[l];
                if (clip == null) continue;
                double clipEnd = song.firstBeatOffset + b.SourceEnd;
                if (clipEnd > clip.length + 0.01 && !_warnedClipLength)
                {
                    _warnedClipLength = true;
                    Debug.LogWarning($"[Conductor] '{song.name}' phase '{_timeline.PhaseName(b.Phase)}' runs past the end of the clip ({clipEnd:F2}s > {clip.length:F2}s). Check its sections.", this);
                }
                var v = NextVoice(l);
                v.Src.Stop();
                v.Src.clip = clip;
                v.Src.loop = false;
                v.Src.timeSamples = Mathf.Clamp((int)(Math.Max(0, clipTime) * clip.frequency), 0, Math.Max(0, clip.samples - 1));
                v.Src.PlayScheduled(DspAt(from));
                v.Src.SetScheduledEndTime(DspAt(b.EndTime));
            }
        }

        /// <summary>Non-adaptive songs: play every layer from song time <paramref name="from"/> (start or resume).</summary>
        void StartLinear(double from)
        {
            for (int l = 0; l < _layers.Count; l++)
            {
                var clip = _layers[l];
                if (clip == null) continue;
                double clipTime = song.firstBeatOffset + from;
                if (clipTime < 0) { from -= clipTime; clipTime = 0; }
                if (song.loop && clip.length > 0) clipTime %= clip.length;
                if (!song.loop && clipTime >= clip.length) continue; // song already over
                var v = NextVoice(l);
                v.Src.Stop();
                v.Src.clip = clip;
                v.Src.loop = song.loop;
                v.Src.timeSamples = Mathf.Clamp((int)(clipTime * clip.frequency), 0, Math.Max(0, clip.samples - 1));
                v.Src.PlayScheduled(DspAt(from));
            }
        }

        void BuildLayers()
        {
            _layers.Clear();
            if (song.stems != null) foreach (var c in song.stems) if (c != null) _layers.Add(c);
            if (_layers.Count == 0 && song.clip != null) _layers.Add(song.clip);
            // Scheduled playback needs the samples ready; clips imported without "Preload Audio Data" aren't.
            foreach (var c in _layers) if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();

            if (_voiceRoot != null) Destroy(_voiceRoot.gameObject);
            _voiceRoot = new GameObject("Music Voices").transform;
            _voiceRoot.SetParent(transform, false);
            _voices = new Voice[_layers.Count][];
            _nextVoice = new int[_layers.Count];
            _layerVolume = new float[_layers.Count];
            for (int l = 0; l < _layers.Count; l++)
            {
                _layerVolume[l] = 1f;
                _voices[l] = new Voice[VoicesPerLayer];
                for (int i = 0; i < VoicesPerLayer; i++)
                {
                    var go = new GameObject($"Layer{l}_Voice{i}");
                    go.transform.SetParent(_voiceRoot, false);
                    var src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    var lp = go.AddComponent<AudioLowPassFilter>();
                    _voices[l][i] = new Voice { Src = src, Filter = lp };
                }
            }
            ApplyMix();
        }

        Voice NextVoice(int layer)
        {
            var v = _voices[layer][_nextVoice[layer]];
            _nextVoice[layer] = (_nextVoice[layer] + 1) % VoicesPerLayer;
            return v;
        }

        void ApplyMix()
        {
            for (int l = 0; l < _voices.Length; l++)
                foreach (var v in _voices[l])
                {
                    if (v == null) continue;
                    v.Src.volume = _musicVolume * _layerVolume[l];
                    v.Filter.cutoffFrequency = _lowPassHz;
                    v.Filter.enabled = _lowPassHz < 21999f;
                }
        }

        void StopVoices()
        {
            foreach (var layer in _voices)
                foreach (var v in layer)
                    if (v != null) v.Src.Stop();
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
