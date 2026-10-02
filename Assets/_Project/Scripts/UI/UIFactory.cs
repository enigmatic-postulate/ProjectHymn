using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Builds simple uGUI menus from code (panels, labels, buttons), so menus need no prefabs and look
    /// consistent. Buttons work with mouse, keyboard arrows and gamepad through the scene's EventSystem.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.13f, 0.94f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.17f, 0.36f, 1f);
        public static readonly Color ButtonHighlight = new Color(0.95f, 0.75f, 0.3f, 1f);
        public static readonly Color TextColor = new Color(0.96f, 0.94f, 1f, 1f);
        public static readonly Color DimText = new Color(0.7f, 0.68f, 0.8f, 1f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.3f, 1f);

        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>A full-screen overlay canvas on top of everything (sorting order 100).</summary>
        public static Canvas Overlay(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        /// <summary>A centred panel with a vertical list layout.</summary>
        public static RectTransform Panel(Transform parent, Vector2 size, string name = "Panel")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = PanelColor;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 36, 36);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>A dimming backdrop behind a panel.</summary>
        public static RectTransform Backdrop(Transform parent, float alpha = 0.6f)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
            return rt;
        }

        public static Text Label(Transform parent, string text, int size = 32, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null, float height = 0)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0, height > 0 ? height : size * 1.5f);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, int fontSize = 30, float height = 64)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0, height);
            go.GetComponent<Image>().color = Color.white;
            var b = go.GetComponent<Button>();
            var colors = b.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = Color.Lerp(ButtonColor, ButtonHighlight, 0.45f);
            colors.selectedColor = Color.Lerp(ButtonColor, ButtonHighlight, 0.6f);
            colors.pressedColor = ButtonHighlight;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.25f, 0.6f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());

            var text = Label(go.transform, label, fontSize);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16, 0); trt.offsetMax = new Vector2(-16, 0);
            return b;
        }

        public static void SetButtonLabel(Button b, string label)
        {
            var t = b != null ? b.GetComponentInChildren<Text>() : null;
            if (t != null) t.text = label;
        }

        /// <summary>Give keyboard/gamepad focus to a control.</summary>
        public static void Focus(Selectable s)
        {
            if (s == null) return;
            EnsureEventSystem();
            var es = EventSystem.current != null ? EventSystem.current : UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (es != null) es.SetSelectedGameObject(s.gameObject);
        }
    }
}
