using System.Text;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The in-game HUD (GDD update 18): hearts only, top left. Notes, level and experience live in the pause menu's
    /// Character page and on the results screen; there is no sequence banner. Short toasts (stage name, level-downs) and
    /// the "[E] Talk" prompt over the player remain. Everything else about rhythm is told by animation, sound and
    /// effects. Made on first use if the scene has none.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        static GameHud _instance;

        Text _banner, _toast, _prompt;
        RectTransform _heartRow;
        readonly System.Collections.Generic.List<Image> _hearts = new System.Collections.Generic.List<Image>();
        static readonly Color HeartFull = new Color(1f, 0.35f, 0.29f), HeartEmpty = new Color(0.24f, 0.2f, 0.28f, 0.9f);
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
            // Hearts are drawn sprites, not a ♥ glyph: web builds have no system fonts to fall back on.
            _heartRow = UiKit.Rect("Hearts", t, new Vector2(0f, 1f), new Vector2(0f, 1f));
            _heartRow.pivot = new Vector2(0f, 1f);
            _heartRow.anchoredPosition = new Vector2(40f, -40f);
            _heartRow.sizeDelta = new Vector2(700f, 52f);
            _banner = Fixed(t, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1600f, 80f), 54, TextAnchor.MiddleCenter);
            _toast = Fixed(t, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 60f), 38, TextAnchor.MiddleCenter);
            _toast.color = UiKit.Gold;
            _prompt = Fixed(t, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 44f), 30, TextAnchor.MiddleCenter);
            foreach (var x in new[] { _banner, _toast, _prompt })
            {
                var o = x.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.75f);
                o.effectDistance = new Vector2(2f, -2f);
            }
        }

        void SetHearts(int hp, int max)
        {
            while (_hearts.Count < max)
            {
                var rt = UiKit.Rect("Heart", _heartRow, new Vector2(0f, 1f), new Vector2(0f, 1f));
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(46f, 46f);
                rt.anchoredPosition = new Vector2(_hearts.Count * 52f, 0f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = ProceduralSprites.Heart;
                img.raycastTarget = false;
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
                sh.effectDistance = new Vector2(2f, -2f);
                _hearts.Add(img);
            }
            for (int i = 0; i < _hearts.Count; i++)
            {
                bool show = i < max;
                if (_hearts[i].gameObject.activeSelf != show) _hearts[i].gameObject.SetActive(show);
                if (show) _hearts[i].color = i < hp ? HeartFull : HeartEmpty;
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
            }
            if (_player == null && _health != null) _player = _health.transform;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            Find();
            var session = GameSession.Instance;
            if (_health != null)
            {
                SetHearts(_health.Hp, _health.MaxHp);
            }
            else SetHearts(0, 0);

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
