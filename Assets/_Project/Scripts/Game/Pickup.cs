using ProjectHymn.Progression;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    public enum PickupKind { Notes, Item, Scroll, Lore }

    /// <summary>
    /// A cache of Notes, an item, a sequence scroll or a lore piece lying in a stage. Collected on touch, once per
    /// save (its id is remembered), with a toast and a chime. The visual bobs and turns.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public string pickupId = "pickup";
        public PickupKind kind;
        public int notes = 50;
        [Tooltip("Item, scroll or lore id from the game database.")]
        public string itemId = "";
        [SerializeField] Transform visual;

        Vector3 _base;
        AudioSource _audio;
        static AudioClip _chime;

        void Start()
        {
            if (GameSession.Instance.Data.IsCollected(pickupId)) { gameObject.SetActive(false); return; }
            if (visual != null) _base = visual.localPosition;
        }

        void Update()
        {
            if (visual == null) return;
            visual.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * 2.2f) * 0.12f;
            visual.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlatformerController>() == null) return;
            var s = GameSession.Instance;
            if (!s.Data.MarkCollected(pickupId)) { gameObject.SetActive(false); return; }
            var item = s.Db != null ? s.Db.Item(itemId) : null;
            switch (kind)
            {
                case PickupKind.Notes:
                    s.AddNotes(notes);
                    GameHud.Toast($"+{notes} Notes");
                    break;
                case PickupKind.Item:
                    s.AddItem(itemId);
                    GameHud.Toast($"Found {(item != null ? item.displayName : itemId)}");
                    break;
                case PickupKind.Scroll:
                    if (item != null && s.Learn(item.teaches))
                    {
                        var seq = SequenceInfo.Find(item.teaches);
                        GameHud.Toast($"Learned {(seq != null ? seq.displayName + "  " + seq.NotesText : item.teaches)}");
                    }
                    else GameHud.Toast("An old scroll (already known)");
                    break;
                case PickupKind.Lore:
                    s.AddLore(itemId);
                    if (item != null) DialogueBox.Show(item.displayName, new[] { item.loreText }, null);
                    GameHud.Toast("Lore recorded in the Songbook");
                    break;
            }
            s.NotifyChanged();
            PlayChime();
            gameObject.SetActive(false);
        }

        static void PlayChime()
        {
            if (_chime == null) _chime = CombatFeedback.Synth.Chord("Pickup", new[] { 84, 88, 91, 96 }, 0.45f, 8f, bright: true);
            var go = new GameObject("Pickup Chime", typeof(AudioSource));
            var src = go.GetComponent<AudioSource>();
            src.PlayOneShot(_chime, 0.7f);
            Destroy(go, 1.5f);
        }
    }

    /// <summary>Looks up a sequence by key in the equipped weapon (for names in toasts and menus).</summary>
    public static class SequenceInfo
    {
        public static SwordSequence Find(string key)
        {
            var s = GameSession.Instance;
            var weapon = s.Weapon;
            if (weapon != null)
                foreach (var q in weapon.sequences) if (q != null && q.key == key) return q;
            if (s.Db != null)
                foreach (var w in s.Db.weapons)
                    if (w != null)
                        foreach (var q in w.sequences) if (q != null && q.key == key) return q;
            return null;
        }
    }
}
