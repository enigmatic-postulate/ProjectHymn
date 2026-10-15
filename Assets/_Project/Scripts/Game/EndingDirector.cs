using System.Collections;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The ending (VS-5), played once after the Summit's first clear: the ending's story cards, then the credits rolling
    /// up over the night sky and the battle theme, then back to Saltmere, where the last verse is celebrated. Hold Enter
    /// (or A) to roll faster; Esc skips to the village.
    /// </summary>
    public class EndingDirector : MonoBehaviour
    {
        public float scrollSpeed = 70f;
        bool _leaving;

        void Start()
        {
            UiState.Reset();
            Time.timeScale = 1f;
            StoryCards.Play(Story.Ending, () => StartCoroutine(Credits()));
        }

        IEnumerator Credits()
        {
            var c = UiKit.Canvas("Credits", 120, transform);
            var group = c.gameObject.AddComponent<CanvasGroup>();
            var roll = UiKit.Rect("Roll", c.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            roll.pivot = new Vector2(0.5f, 1f);
            roll.sizeDelta = new Vector2(1200f, 10f);
            float y = 0f;
            var logo = Resources.Load<Texture2D>("UI/HymnLogo");
            foreach (var line in Story.Credits)
            {
                if (line == "# PROJECT HYMN" && logo != null)
                {
                    var img = UiKit.Rect("Logo", roll, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                    img.pivot = new Vector2(0.5f, 1f);
                    img.sizeDelta = new Vector2(900f, 900f * logo.height / logo.width);
                    img.anchoredPosition = new Vector2(0f, -y);
                    var raw = img.gameObject.AddComponent<RawImage>();
                    raw.texture = logo;
                    raw.raycastTarget = false;
                    y += img.sizeDelta.y + 30f;
                    continue;
                }
                bool heading = line.StartsWith("# ");
                float h = line.Length == 0 ? 36f : heading ? 54f : 46f;
                var t = UiKit.Label(roll, heading ? line.Substring(2) : line, heading ? 30 : 36, TextAnchor.MiddleCenter, heading ? UiKit.Gold : UiKit.Text);
                var rt = (RectTransform)t.transform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, h);
                rt.anchoredPosition = new Vector2(0f, -y);
                t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
                y += h;
            }
            // Roll from just below the screen until the last line has passed the middle, then hold and leave.
            float pos = 0f, end = y + 540f;
            group.alpha = 0f;
            while (pos < end && !_leaving)
            {
                group.alpha = Mathf.Min(1f, group.alpha + Time.unscaledDeltaTime);
                bool fast = Held();
                pos += Time.unscaledDeltaTime * scrollSpeed * (fast ? 5f : 1f);
                roll.anchoredPosition = new Vector2(0f, pos);
                if (UiInput.Cancel) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(_leaving ? 0f : 2.5f);
            Leave();
        }

        static bool Held()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current; var gp = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && (kb.enterKey.isPressed || kb.spaceKey.isPressed)) || (gp != null && gp.buttonSouth.isPressed);
#else
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
#endif
        }

        void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            var s = GameSession.Instance;
            s.Travel(s.Db != null ? s.Db.hubScene : "Village", "Start", saveOnArrival: true);
        }
    }
}
