using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Core
{
    /// <summary>One stretch of a song with a fixed tempo and time signature.</summary>
    [Serializable]
    public struct TempoSection
    {
        [Min(1)] public int measures;
        [Tooltip("Beats per minute, where a 'beat' is the unit counted below (e.g. eighth notes in 7/8).")]
        [Min(1f)] public float bpm;
        [Range(1, 16)] public int beatsPerMeasure;
        [Tooltip("Display only: 4 = quarter-note beat, 8 = eighth-note beat.")]
        public int beatUnit;

        public TempoSection(int measures, float bpm, int beatsPerMeasure, int beatUnit = 4)
        {
            this.measures = measures;
            this.bpm = bpm;
            this.beatsPerMeasure = beatsPerMeasure;
            this.beatUnit = beatUnit;
        }

        public string Signature => $"{beatsPerMeasure}/{(beatUnit <= 0 ? 4 : beatUnit)}";
    }

    /// <summary>
    /// A stretch of the song that loops while the fight is in one phase (adaptive music). The engine only
    /// chooses which composed stretch plays next; meter changes stay composed into the music (GDD).
    /// </summary>
    [Serializable]
    public struct SongPhase
    {
        public string name;
        [Tooltip("Index into Sections where this phase starts.")]
        [Min(0)] public int firstSection;
        [Tooltip("How many consecutive sections make up this phase's loop.")]
        [Min(1)] public int sectionCount;

        public SongPhase(string name, int firstSection, int sectionCount)
        {
            this.name = name; this.firstSection = firstSection; this.sectionCount = sectionCount;
        }
    }

    /// <summary>
    /// A battle theme: the audio clip plus its tempo map. Tempo and time-signature changes are
    /// first-class so a boss can shift from 4/4 to 7/8 mid-fight.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Song Data", fileName = "NewSong")]
    public class SongData : ScriptableObject
    {
        [Tooltip("Optional. Leave empty to run on the metronome alone while prototyping.")]
        public AudioClip clip;

        [Tooltip("Seconds into the clip where beat 0 (the first downbeat) lands. Use 0 for looping songs (trim the clip to start on a downbeat).")]
        [Min(0f)] public double firstBeatOffset;

        [Tooltip("Repeat the tempo map forever. If a clip is assigned it loops too, so make the clip exactly one tempo-map long.")]
        public bool loop = true;

        [Tooltip("Where the reference track lives (e.g. a YouTube link). Notes only.")]
        public string referenceUrl;

        [Header("Key (for pitched hit sounds)")]
        [Tooltip("Tonic pitch class: 0 = C, 1 = C#, 2 = D ... 4 = E ... 11 = B.")]
        [Range(0, 11)] public int keyRoot;
        public bool minorKey;

        public List<TempoSection> sections = new List<TempoSection> { new TempoSection(4, 140f, 4) };

        [Tooltip("Optional. When set, the Conductor plays adaptively: it loops the current phase's sections until asked to move to another phase, switching at the end of a loop.")]
        public List<SongPhase> phases = new List<SongPhase>();

        public bool IsAdaptive => phases != null && phases.Count > 0;

        public TempoMap BuildTempoMap() => new TempoMap(sections, loop);

#if UNITY_EDITOR
        void OnValidate()
        {
            if (clip == null || !loop || sections == null || sections.Count == 0) return;
            var map = BuildTempoMap();
            double expected = firstBeatOffset + map.LengthSeconds;
            if (Math.Abs(clip.length - expected) > 0.01)
                Debug.LogWarning($"[{name}] Clip is {clip.length:F3}s but offset + tempo map is {expected:F3}s. " +
                                 "A looping clip must match exactly or the beat grid will drift.", this);
        }
#endif
    }
}
