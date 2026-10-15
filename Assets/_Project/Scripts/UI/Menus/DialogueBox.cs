using System;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// A speech box along the bottom of the screen. Lines advance on Enter, Space, J, up or A. VS-5: words appear on the
    /// music's sixteenth notes (eighths at fast tempos) with a short voice blip pitched for each speaker, and when the
    /// speaker is someone in the scene, a live portrait of them (a small camera on their face) sits at the left.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        static DialogueBox _open;

        Text _name, _body, _hint;
        string[] _lines;
        string[] _words;
        int _line, _shownWords;
        long _lastTick = long.MinValue;
        Action _done;
        float _openedAt;
        float _voice = 1f;

        // The portrait
        Camera _portraitCam;
        RenderTexture _portraitTex;
        Light _portraitLight;
        Villager _villager;

        public static bool IsOpen => _open != null;

        public static void Show(string speaker, string[] lines, Action onDone) => Show(speaker, lines, onDone, null);

        /// <summary>Show lines; <paramref name="face"/> (a character in the scene) gets a live portrait.</summary>
        public static void Show(string speaker, string[] lines, Action onDone, Transform face)
        {
            if (_open != null) return;
            var go = new GameObject("Dialogue");
            _open = go.AddComponent<DialogueBox>();
            _open.Build(speaker, lines ?? new string[0], onDone, face);
        }

        void Build(string speaker, string[] lines, Action onDone, Transform face)
        {
            UiState.Push();
            _lines = lines.Length > 0 ? lines : new[] { "..." };
            _done = onDone;
            _openedAt = Time.unscaledTime;
            _voice = VoiceOf(speaker);
            var c = UiKit.Canvas("Dialogue Canvas", 200, transform);
            var frame = UiKit.Rect("Frame", c.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            frame.pivot = new Vector2(0.5f, 0f);
            frame.sizeDelta = new Vector2(1500f, 250f);
            frame.anchoredPosition = new Vector2(0f, 40f);
            frame.gameObject.AddComponent<Image>().color = UiKit.Frame;
            var inner = UiKit.Rect("Inner", frame, Vector2.zero, Vector2.one, Vector2.one * 3f, -Vector2.one * 3f);
            inner.gameObject.AddComponent<Image>().color = UiKit.PanelColor;

            float left = 34f;
            if (face != null && BuildPortrait(inner, face)) left = 250f;
            _name = UiKit.Label(inner, speaker, 32, TextAnchor.UpperLeft, UiKit.Gold);
            ((RectTransform)_name.transform).offsetMin = new Vector2(left, 0f);
            ((RectTransform)_name.transform).offsetMax = new Vector2(-34f, -18f);
            _body = UiKit.Label(inner, "", 32, TextAnchor.UpperLeft);
            ((RectTransform)_body.transform).offsetMin = new Vector2(left, 30f);
            ((RectTransform)_body.transform).offsetMax = new Vector2(-34f, -66f);
            _hint = UiKit.Label(inner, "", 22, TextAnchor.LowerRight, UiKit.Dim);
            ((RectTransform)_hint.transform).offsetMin = new Vector2(34f, 14f);
            ((RectTransform)_hint.transform).offsetMax = new Vector2(-30f, 0f);
            StartLine();
        }

        /// <summary>Each speaker's blip sits at their own pitch (stable from their name).</summary>
        static float VoiceOf(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return 1f;
            int h = 17;
            foreach (char ch in speaker) h = h * 31 + ch;
            float[] steps = { 0.75f, 0.84f, 0.89f, 1f, 1.12f, 1.19f, 1.33f };      // a scale, so voices sound musical together
            return steps[(h & 0x7fffffff) % steps.Length];
        }

        void StartLine()
        {
            _words = _lines[_line].Split(' ');
            _shownWords = 0;
            _lastTick = long.MinValue;
        }

        void Update()
        {
            bool full = _shownWords >= _words.Length;
            if (!full)
            {
                // A word on each sixteenth note (eighths when the tempo is quick), so the text crawls in time.
                double bps = BeatClock.BeatsPerSecond;
                int sub = bps > 2.4 ? 2 : 4;
                long tick = (long)Math.Floor(BeatClock.Beat * sub);
                if (tick != _lastTick)
                {
                    _lastTick = tick;
                    _shownWords++;
                    if (_shownWords % 2 == 1) Sfx.PlayPitched(Sfx.Blip, 0.55f, _voice * UnityEngine.Random.Range(0.97f, 1.03f));
                }
                full = _shownWords >= _words.Length;
            }
            _body.text = string.Join(" ", _words, 0, Mathf.Min(_shownWords, _words.Length));
            _hint.text = full ? (_line < _lines.Length - 1 ? "▼ next" : "▼ close") : "";
            if (Time.unscaledTime - _openedAt < 0.15f) return;     // the press that opened it doesn't skip it
            if (!UiInput.Advance && !UiInput.Cancel) return;
            if (!full) { _shownWords = _words.Length; return; }
            if (++_line < _lines.Length) { StartLine(); return; }
            Close();
        }

        // ---------------- Portrait ----------------

        bool BuildPortrait(RectTransform inner, Transform face)
        {
            // They turn to the camera while they talk, so the portrait sees them from the front.
            _villager = face.GetComponent<Villager>();
            if (_villager != null) _villager.facingViewer = true;
            var head = FindHead(face);
            // KayKit heads are big: the head bone sits at the neck, the face about a third of a metre above it. Frame the
            // head and shoulders from straight in front (toward the viewer, a touch above).
            Vector3 eye = head != null ? head.position + Vector3.up * 0.3f : face.position + Vector3.up * 1.6f;
            Vector3 aim = eye + Vector3.down * 0.18f;
            const float Distance = 2.3f;
            _portraitTex = new RenderTexture(384, 384, 24) { name = "Portrait" };
            var camGo = new GameObject("Portrait Camera", typeof(Camera));
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = new Vector3(face.position.x, aim.y + 0.12f, face.position.z - Distance);
            camGo.transform.rotation = Quaternion.LookRotation(aim - camGo.transform.position, Vector3.up);
            _portraitCam = camGo.GetComponent<Camera>();
            _portraitCam.fieldOfView = 30f;
            // The player stands on the lane between this camera and the speaker: clip everything nearer than the speaker's
            // front so they never block the face.
            _portraitCam.nearClipPlane = Distance - 0.75f;
            _portraitCam.farClipPlane = 60f;
            _portraitCam.clearFlags = CameraClearFlags.SolidColor;
            _portraitCam.backgroundColor = new Color(0.16f, 0.12f, 0.24f);
            _portraitCam.targetTexture = _portraitTex;
            _portraitCam.depth = -5f;
            // A soft warm light on their face while they talk.
            var lightGo = new GameObject("Portrait Light", typeof(Light));
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.position = eye + new Vector3(-0.5f, 0.4f, -0.9f);
            _portraitLight = lightGo.GetComponent<Light>();
            _portraitLight.type = LightType.Point;
            _portraitLight.range = 2.2f;
            _portraitLight.intensity = 1.6f;
            _portraitLight.color = new Color(1f, 0.86f, 0.7f);

            var box = UiKit.Rect("Portrait Frame", inner, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            box.pivot = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(206f, 206f);
            box.anchoredPosition = new Vector2(18f, 0f);
            box.gameObject.AddComponent<Image>().color = UiKit.Gold;
            var pic = UiKit.Rect("Portrait", box, Vector2.zero, Vector2.one, Vector2.one * 3f, -Vector2.one * 3f);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = _portraitTex;
            raw.raycastTarget = false;
            return true;
        }

        static Transform FindHead(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Equals("head", StringComparison.OrdinalIgnoreCase) || t.name.EndsWith("Head", StringComparison.Ordinal)) return t;
            return null;
        }

        void ReleasePortrait()
        {
            if (_villager != null) { _villager.facingViewer = false; _villager = null; }
            if (_portraitCam != null) _portraitCam.targetTexture = null;
            if (_portraitTex != null) { _portraitTex.Release(); Destroy(_portraitTex); _portraitTex = null; }
        }

        void Close()
        {
            _open = null;
            UiState.Pop();
            ReleasePortrait();
            Destroy(gameObject);
            _done?.Invoke();
        }

        void OnDestroy()
        {
            ReleasePortrait();
            if (_open == this) { _open = null; UiState.Pop(); }
        }
    }
}
