using System;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Plays illustrated story cards full screen (VS-5): the intro on New Game and the ending after the Summit. Each card
    /// is a painted picture (Resources/UI/Story, painted by Tools/paint_story.py) drifting slowly closer, with its text
    /// appearing a word at a time on the music's eighth notes. Pictures cross-fade with a page-turn sound. Advance
    /// (Enter, Space, A) shows the rest of the text, then the next card; Esc skips them all. Ends on black.
    /// </summary>
    public class StoryCards : MonoBehaviour
    {
        static StoryCards _playing;
        public static bool Playing => _playing != null;

        StoryCard[] _cards;
        Action _done;
        RawImage _back, _front;
        Text _text, _hint;
        CanvasGroup _group;
        int _card = -1;
        string[] _words;
        int _shown;
        long _lastTick = long.MinValue;
        float _cardStart, _fade = 1f, _openedAt;
        bool _ending;
        Vector2 _drift;

        const float CrossFade = 0.9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _playing = null;

        public static void Play(StoryCard[] cards, Action onDone)
        {
            if (_playing != null || cards == null || cards.Length == 0) { onDone?.Invoke(); return; }
            var go = new GameObject("Story Cards");
            _playing = go.AddComponent<StoryCards>();
            _playing.Build(cards, onDone);
        }

        void Build(StoryCard[] cards, Action onDone)
        {
            UiState.Push();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _cards = cards;
            _done = onDone;
            _openedAt = Time.unscaledTime;
            var c = UiKit.Canvas("Story Canvas", 900, transform);
            _group = c.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var black = UiKit.Rect("Black", c.transform, Vector2.zero, Vector2.one);
            black.gameObject.AddComponent<Image>().color = Color.black;
            _back = Picture(c.transform, "Back");
            _front = Picture(c.transform, "Front");
            // A dark band under the text so it reads over any picture.
            var band = UiKit.Rect("Band", c.transform, Vector2.zero, new Vector2(1f, 0.3f));
            band.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.05f, 0.62f);
            _text = UiKit.Label(band, "", 40, TextAnchor.MiddleCenter, UiKit.Text);
            ((RectTransform)_text.transform).offsetMin = new Vector2(220f, 30f);
            ((RectTransform)_text.transform).offsetMax = new Vector2(-220f, -20f);
            _text.lineSpacing = 1.15f;
            var sh = _text.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sh.effectDistance = new Vector2(2f, -2f);
            _hint = UiKit.Label(c.transform, "Enter: next     Esc: skip", 22, TextAnchor.LowerRight, UiKit.Dim);
            ((RectTransform)_hint.transform).offsetMin = new Vector2(0f, 18f);
            ((RectTransform)_hint.transform).offsetMax = new Vector2(-30f, 0f);
            Next();
        }

        static RawImage Picture(Transform parent, string name)
        {
            var rt = UiKit.Rect(name, parent, Vector2.zero, Vector2.one);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        void Next()
        {
            _card++;
            if (_card >= _cards.Length) { _ending = true; _fade = 0f; return; }
            if (_card > 0) Sfx.Play(Sfx.PageTurn, 0.8f);
            // The old front becomes the back; the new picture fades in over it.
            _back.texture = _front.texture;
            _back.color = new Color(1f, 1f, 1f, _front.texture != null ? 1f : 0f);
            _back.rectTransform.localScale = _front.rectTransform.localScale;
            _back.rectTransform.anchoredPosition = _front.rectTransform.anchoredPosition;
            _front.texture = Resources.Load<Texture2D>("UI/Story/" + _cards[_card].Image);
            _front.color = new Color(1f, 1f, 1f, 0f);
            _drift = new Vector2(_card % 2 == 0 ? -1f : 1f, _card % 3 == 0 ? 0.5f : -0.5f) * 22f;
            _cardStart = Time.unscaledTime;
            _fade = 0f;
            _words = _cards[_card].Text.Split(' ');
            _shown = 0;
            _lastTick = long.MinValue;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_ending)
            {
                // Fade the last picture and the text to black, then hand over (the screen stays black: the caller's
                // scene change or the credits take it from here).
                _fade = Mathf.Min(1f, _fade + dt / 1.2f);
                _front.color = new Color(1f, 1f, 1f, 1f - _fade);
                _back.color = new Color(1f, 1f, 1f, 0f);
                _text.color = new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, 1f - _fade);
                _hint.enabled = false;
                if (_fade >= 1f) Finish();
                return;
            }
            _group.alpha = Mathf.Min(1f, _group.alpha + dt / 0.6f);

            // The picture: fade in over the old one, then drift slowly closer.
            _fade = Mathf.Min(1f, _fade + dt / CrossFade);
            _front.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, _fade));
            if (_fade >= 1f) _back.color = new Color(1f, 1f, 1f, 0f);
            float age = Time.unscaledTime - _cardStart;
            float k = 1f - Mathf.Exp(-age / 14f);
            _front.rectTransform.localScale = Vector3.one * (1.02f + 0.07f * k);
            _front.rectTransform.anchoredPosition = _drift * k;

            // The words, on the eighth notes, once the picture is mostly in.
            if (_shown < _words.Length && _fade > 0.5f)
            {
                long tick = (long)Math.Floor(BeatClock.Beat * 2.0);
                if (tick != _lastTick) { _lastTick = tick; _shown++; }
            }
            _text.text = string.Join(" ", _words, 0, Mathf.Min(_shown, _words.Length));

            if (Time.unscaledTime - _openedAt < 0.25f) return;
            if (UiInput.Cancel) { _card = _cards.Length; _ending = true; _fade = 0f; Sfx.Play(Sfx.MenuBack, 0.7f); return; }
            if (!UiInput.Advance) return;
            if (_shown < _words.Length || _fade < 1f) { _shown = _words.Length; _fade = 1f; return; }
            Next();
        }

        void Finish()
        {
            if (_playing == this) _playing = null;
            UiState.Pop();
            var done = _done;
            _done = null;
            enabled = false;
            done?.Invoke();
            Destroy(gameObject, 1.5f);          // stays black under the scene fade a moment longer
        }

        void OnDestroy()
        {
            if (_playing == this) { _playing = null; UiState.Pop(); }
        }
    }
}
