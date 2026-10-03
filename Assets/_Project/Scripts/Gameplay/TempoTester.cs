using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Try the sword at different tempos (GDD M2: keyframes follow the BPM): F1 plays the scene's song, F2/F3/F4 a
    /// 4/4 metronome at 90, 130 and 170 BPM. Every move's timing and swing size follow the new tempo.
    /// </summary>
    public class TempoTester : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] PlayerInputReader input;
        [SerializeField] float[] testBpm = { 90f, 130f, 170f };

        SongData _song;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>();
        }

        void Start() { if (conductor != null) _song = conductor.Song; }

        void Update()
        {
            if (input == null || conductor == null || input.TempoKey == 0) return;
            int k = input.TempoKey;
            if (k == 1) { if (_song != null) conductor.Play(_song); return; }
            int i = k - 2;
            if (i < 0 || i >= testBpm.Length) return;
            var s = ScriptableObject.CreateInstance<SongData>();
            s.name = $"Metronome {testBpm[i]:0} BPM";
            s.loop = true;
            s.sections = new System.Collections.Generic.List<TempoSection> { new TempoSection(64, testBpm[i], 4) };
            conductor.MetronomeEnabled = true;
            conductor.Play(s);
        }
    }
}
