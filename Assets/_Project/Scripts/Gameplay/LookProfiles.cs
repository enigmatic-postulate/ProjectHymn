using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.Gameplay
{
    /// <summary>Per-scene look settings: the two post-processing profiles and the Valor fog.</summary>
    [RequireComponent(typeof(Volume))]
    public class LookProfiles : MonoBehaviour
    {
        [SerializeField] VolumeProfile valorProfile;
        [SerializeField] VolumeProfile celProfile;
        [Header("Valor fog (linear)")]
        [SerializeField] Color fogColor = new Color(0.16f, 0.12f, 0.22f);
        [SerializeField] float fogStart = 20f;
        [SerializeField] float fogEnd = 60f;

        void Awake() => Apply(HymnLook.IsCel);

        public void Apply(bool cel)
        {
            var volume = GetComponent<Volume>();
            var profile = cel ? celProfile : valorProfile;
            if (volume != null && profile != null) volume.sharedProfile = profile;
            RenderSettings.fog = !cel;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }
}
