using System.Collections.Generic;
using ProjectHymn.UI;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The Arena's waves (Slice 8): each wave is a switched-off group of enemies. The first joins at the start; each
    /// next one joins a bar after the last enemy of the one before falls. <see cref="Done"/> opens the way out.
    /// </summary>
    public class ArenaWaves : MonoBehaviour
    {
        [SerializeField] List<GameObject> waves = new List<GameObject>();
        [SerializeField] float delaySeconds = 1.6f;

        int _current = -1;
        float _nextAt = -1f;

        public bool Done { get; private set; }
        public int Wave => _current + 1;
        public int Count => waves.Count;

        void Start()
        {
            foreach (var w in waves) if (w != null) w.SetActive(false);
            Next();
        }

        void Next()
        {
            _current++;
            if (_current >= waves.Count) { Done = true; GameHud.Toast("The Arena is yours. The way out is open."); return; }
            if (waves[_current] != null) waves[_current].SetActive(true);
            GameHud.Toast($"Wave {_current + 1} of {waves.Count}");
        }

        bool CurrentCleared()
        {
            if (_current < 0 || _current >= waves.Count || waves[_current] == null) return true;
            foreach (var e in waves[_current].GetComponentsInChildren<BeatEnemy>(true)) if (!e.IsDead) return false;
            return true;
        }

        void Update()
        {
            if (Done) return;
            if (_nextAt < 0f) { if (CurrentCleared()) _nextAt = Time.time + delaySeconds; return; }
            if (Time.time >= _nextAt) { _nextAt = -1f; Next(); }
        }
    }
}
