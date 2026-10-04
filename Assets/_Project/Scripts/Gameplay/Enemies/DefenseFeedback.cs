using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// What a parry, a block and a hit look and sound like (vertical slice 2): a gold "PARRY!" with a bright
    /// chord and a camera kick, a pale "BLOCK" with a short ring, a red number and a thud when the player is hurt.
    /// </summary>
    public class DefenseFeedback : MonoBehaviour
    {
        [SerializeField] PlayerGuard guard;
        [SerializeField] PlayerHealth health;
        [SerializeField] DamagePopups popups;
        [SerializeField] CameraRig cameraRig;
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;

        AudioSource _src;
        AudioClip _parry, _block, _hurt, _guard;

        void Awake()
        {
            if (guard == null) guard = FindAnyObjectByType<PlayerGuard>();
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();
            if (popups == null) popups = FindAnyObjectByType<DamagePopups>();
            if (cameraRig == null) cameraRig = FindAnyObjectByType<CameraRig>();
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _parry = CombatFeedback.Synth.Chord("Parry", new[] { 88, 95, 100 }, 0.35f, 10f, bright: true);
            _block = CombatFeedback.Synth.Chord("Block", new[] { 81 }, 0.12f, 35f, bright: true);
            _guard = CombatFeedback.Synth.Chord("Guard", new[] { 76 }, 0.05f, 60f, bright: false);
            _hurt = CombatFeedback.Synth.Thud("Hurt");
        }

        void OnEnable()
        {
            if (guard != null) { guard.Defended += OnDefended; guard.Raised += OnRaised; }
            if (health != null) { health.Hurt += OnHurt; health.Died += OnDied; }
        }

        void OnDisable()
        {
            if (guard != null) { guard.Defended -= OnDefended; guard.Raised -= OnRaised; }
            if (health != null) { health.Hurt -= OnHurt; health.Died -= OnDied; }
        }

        void OnRaised() => _src.PlayOneShot(_guard, volume * 0.4f);

        void OnDefended(DefenseResult r, Vector3 from)
        {
            var at = guard.transform.position + Vector3.up * 1.9f;
            switch (r)
            {
                case DefenseResult.Parried:
                    _src.PlayOneShot(_parry, volume);
                    if (popups != null) popups.Show(at, "PARRY!", new Color(1f, 0.82f, 0.25f), 1.5f);
                    if (cameraRig != null) cameraRig.Shake(0.14f, 0.18f);
                    break;
                case DefenseResult.Blocked:
                    _src.PlayOneShot(_block, volume * 0.8f);
                    if (popups != null) popups.Show(at, "BLOCK", new Color(0.7f, 0.85f, 1f), 1f);
                    break;
                case DefenseResult.Dodged:
                    _src.PlayOneShot(_guard, volume * 0.6f);
                    if (popups != null) popups.Show(at, "DODGE", new Color(0.6f, 1f, 0.75f), 1f);
                    break;
            }
        }

        void OnHurt(int amount)
        {
            _src.PlayOneShot(_hurt, volume);
            if (popups != null) popups.Show(health.transform.position + Vector3.up * 1.9f, $"-{amount}", new Color(1f, 0.3f, 0.25f), 1.2f);
            if (cameraRig != null) cameraRig.Shake(0.18f, 0.22f);
        }

        void OnDied()
        {
            if (popups != null) popups.Show(health.transform.position + Vector3.up * 2.2f, "DOWN", new Color(1f, 0.35f, 0.3f), 1.6f);
        }
    }
}
