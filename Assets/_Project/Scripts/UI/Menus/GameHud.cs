using System.Text;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The minimal in-game HUD (Hub &amp; Loop): hearts and Notes top left, the quick-slot item bottom left, a banner
    /// naming a sequence when one completes, toasts, and the "▲ Talk" prompt over the player. Everything else about
    /// rhythm is told by animation, sound and effects. Made on first use if the scene has none.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        static GameHud _instance;

        Text _hearts, _notes, _item, _banner, _toast, _prompt, _boost;
        float _bannerTime, _toastTime;
        PlayerHealth _health;
        SwordCombatController _combat;
        Transform _player;
        string _promptText;

        public static GameHud Instance
        {
            get
            {
                if (_instance == null) _instance = new GameObject("Game HUD").AddComponent<GameHud>();
                return _instance;
            }
        }

        public static void Toast(string text, float seconds = 2.2f) { var h = Instance; h._toast.text = text; h._toastTime = seconds; }
        public static void Banner(string text, Color color, float seconds = 1.6f) { var h = Instance; h._banner.text = text; h._banner.color = color; h._bannerTime = seconds; }
        public static void Prompt(string text) { if (_instance != null || text != null) Instance._promptText = text; }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            var c = UiKit.Canvas("HUD Canvas", 50, transform);
            var t = c.transform;
            _hearts = Fixed(t, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(700f, 60f), 46, TextAnchor.UpperLeft);
            _notes = Fixed(t, new Vector2(0f, 1f), new Vector2(42f, -100f), new Vector2(500f, 44f), 32, TextAnchor.UpperLeft);
            _notes.color = UiKit.Gold;
            _boost = Fixed(t, new Vector2(0f, 1f), new Vector2(42f, -146f), new Vector2(500f, 36f), 24, TextAnchor.UpperLeft);
            _boost.color = new Color(1f, 0.55f, 0.35f);
            _item = Fixed(t, new Vector2(0f, 0f), new Vector2(42f, 40f), new Vector2(700f, 44f), 28, TextAnchor.LowerLeft);
            _banner = Fixed(t, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1600f, 80f), 54, TextAnchor.MiddleCenter);
            _toast = Fixed(t, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 60f), 38, TextAnchor.MiddleCenter);
            _toast.color = UiKit.Gold;
            _prompt = Fixed(t, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 44f), 30, TextAnchor.MiddleCenter);
            foreach (var x in new[] { _hearts, _notes, _item, _banner, _toast, _prompt, _boost })
            {
                var o = x.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.75f);
                o.effectDistance = new Vector2(2f, -2f);
            }
        }

        static Text Fixed(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = UiKit.Rect("Text", parent, anchor, anchor);
            rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiKit.Font; t.fontSize = fontSize; t.alignment = align; t.color = UiKit.Text;
            t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void Find()
        {
            if (_health == null) _health = FindAnyObjectByType<PlayerHealth>();
            if (_combat == null)
            {
                _combat = FindAnyObjectByType<SwordCombatController>();
                if (_combat != null) _combat.SequenceCompleted += OnSequence;
            }
            if (_player == null && _health != null) _player = _health.transform;
        }

        void OnDestroy()
        {
            if (_combat != null) _combat.SequenceCompleted -= OnSequence;
            if (_instance == this) _instance = null;
        }

        void OnSequence(SwordSequence s) => Banner($"{s.displayName.ToUpperInvariant()}", s.color);

        void Update()
        {
            Find();
            var session = GameSession.Instance;
            if (_health != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < _health.MaxHp; i++) sb.Append(i < _health.Hp ? "<color=#ff5a4a>♥</color>" : "<color=#3c3448>♥</color>");
                _hearts.text = sb.ToString();
            }
            else _hearts.text = "";
            _notes.text = $"♪ {session.Data.notes}";
            var item = session.Db != null ? session.Db.Item(session.Data.quickItem) : null;
            _item.text = item != null ? $"<color=#ffd56a>[E]</color> {item.displayName} ×{session.Data.Count(item.id)}   <color=#8a84a0>[Q] next</color>" : "<color=#8a84a0>No item</color>";
            _boost.text = _combat != null && _combat.Boosted ? "Tonic: damage up" : "";

            Fade(_banner, ref _bannerTime, 0.5f);
            Fade(_toast, ref _toastTime, 0.6f);

            if (!string.IsNullOrEmpty(_promptText) && _player != null && Camera.main != null && !UiState.Blocking)
            {
                var sp = Camera.main.WorldToScreenPoint(_player.position + Vector3.up * 2.4f);
                var canvasRt = (RectTransform)_prompt.canvas.transform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
                var prt = (RectTransform)_prompt.transform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = local;
                _prompt.text = $"<color=#ffd56a>[E]</color> {_promptText}";
            }
            else _prompt.text = "";
        }

        static void Fade(Text t, ref float time, float fade)
        {
            time -= Time.unscaledDeltaTime;
            var c = t.color;
            c.a = Mathf.Clamp01(time / fade);
            t.color = c;
        }
    }
}
