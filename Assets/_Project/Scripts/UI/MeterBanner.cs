using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The time signature on screen (VS-3): the current meter small in the corner, and through the bar before a meter
    /// change a big "next: 7/8" with the beats left, so every change is telegraphed one bar ahead. Builds its own canvas.
    /// </summary>
    public class MeterBanner : MonoBehaviour
    {
        Conductor _conductor;
        Text _corner, _big;

        void Start()
        {
            _conductor = FindAnyObjectByType<Conductor>();
            var c = UiKit.Canvas("Meter Canvas", 110, transform);
            var corner = UiKit.Rect("Meter", c.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-220f, -150f), new Vector2(-40f, -96f));
            _corner = corner.gameObject.AddComponent<Text>();
            _corner.font = UiKit.Font; _corner.fontSize = 40; _corner.alignment = TextAnchor.MiddleRight; _corner.color = UiKit.Text;
            var big = UiKit.Rect("Next Meter", c.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400f, 150f), new Vector2(400f, 290f));
            _big = big.gameObject.AddComponent<Text>();
            _big.font = UiKit.Font; _big.fontSize = 64; _big.alignment = TextAnchor.MiddleCenter; _big.color = UiKit.Gold;
            _big.supportRichText = true;
        }

        static Color Tint(string sig) => sig.StartsWith("3/") ? new Color(0.65f, 0.85f, 1f) : sig.StartsWith("7/") ? new Color(1f, 0.6f, 0.85f)
                                       : sig.StartsWith("6/") ? new Color(0.45f, 0.95f, 0.9f) : sig.StartsWith("9/") ? new Color(0.75f, 0.7f, 1f) : UiKit.Gold;

        void Update()
        {
            if (_corner == null) return;
            if (_conductor == null || !_conductor.IsPlaying || _conductor.Map == null) { _corner.text = ""; _big.text = ""; return; }
            var cue = BossPlanner.Cue(_conductor.Map, _conductor.Map.TimeToBeat(_conductor.SongTime));
            _corner.text = cue.Current;
            _corner.color = Tint(cue.Current);
            if (cue.Changing)
            {
                float punch = 1f + 0.25f * Mathf.Exp(-_conductor.BeatPhase * 6f);
                _big.text = $"<size=34>next bar</size>\n{cue.Next}   <size=40>{cue.BeatsLeft}</size>";
                _big.color = Tint(cue.Next);
                _big.transform.localScale = Vector3.one * punch;
            }
            else _big.text = "";
        }
    }
}
