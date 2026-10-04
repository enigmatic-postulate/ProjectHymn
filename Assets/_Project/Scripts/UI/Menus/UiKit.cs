using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Builds the Hub &amp; Loop menus in code (uGUI): canvases, panels, labels and buttons with keyboard and
    /// gamepad navigation, in one palette (deep navy panels, warm gold accents).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.11f, 0.94f);
        public static readonly Color Frame = new Color(0.85f, 0.68f, 0.32f, 0.9f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.42f);
        public static readonly Color Text = new Color(0.93f, 0.91f, 0.96f);
        public static readonly Color Dim = new Color(0.62f, 0.58f, 0.7f);
        static readonly Color ButtonColor = new Color(0.16f, 0.13f, 0.24f, 1f);
        static readonly Color ButtonHot = new Color(0.42f, 0.3f, 0.12f, 1f);

        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>A framed panel centred on the screen, <paramref name="size"/> in reference pixels.</summary>
        public static RectTransform Panel(string name, Transform parent, Vector2 size, Vector2 offset = default)
        {
            var frame = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            frame.sizeDelta = size + Vector2.one * 6f;
            frame.anchoredPosition = offset;
            frame.gameObject.AddComponent<Image>().color = Frame;
            var inner = Rect("Inner", frame, Vector2.zero, Vector2.one, Vector2.one * 3f, -Vector2.one * 3f);
            inner.gameObject.AddComponent<Image>().color = PanelColor;
            return inner;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.UpperLeft, Color? color = null)
        {
            var rt = Rect("Label", parent, Vector2.zero, Vector2.one);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Text;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>A vertical stack that sizes children to their preferred heights.</summary>
        public static RectTransform Column(Transform parent, float spacing = 10f, int padding = 24)
        {
            var rt = Rect("Column", parent, Vector2.zero, Vector2.one);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return rt;
        }

        public static Text Line(Transform column, string text, int size, Color? color = null, float height = -1f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Label(column, text, size, align, color);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height > 0 ? height : size * 1.45f;
            return t;
        }

        public static Button Button(Transform column, string label, Action onClick, int size = 30, float height = 58f)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(column, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            var b = go.GetComponent<Button>();
            var cb = b.colors;
            cb.normalColor = ButtonColor;
            cb.highlightedColor = ButtonHot;
            cb.selectedColor = ButtonHot;
            cb.pressedColor = Gold;
            cb.disabledColor = new Color(0.1f, 0.09f, 0.14f, 0.8f);
            cb.colorMultiplier = 1f;
            b.colors = cb;
            b.targetGraphic = img;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Label(go.transform, label, size, TextAnchor.MiddleLeft);
            ((RectTransform)t.transform).offsetMin = new Vector2(22f, 0f);
            return b;
        }

        public static void SetLabel(Button b, string text) { var t = b.GetComponentInChildren<Text>(); if (t != null) t.text = text; }

        public static void Select(Component c)
        {
            if (c == null) return;
            var es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(c.gameObject);
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
