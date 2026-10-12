using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A looping background track off the beat grid (the village), at the player's music volume.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicLoop : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.8f;
        AudioSource _src;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = true;
            _src.playOnAwake = true;
            _src.spatialBlend = 0f;
        }

        void Start() { if (!_src.isPlaying) _src.Play(); }

        void Update() => _src.volume = volume * GameSettings.MusicVolume;
    }
}
