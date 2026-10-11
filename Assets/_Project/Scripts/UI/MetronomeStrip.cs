using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The Practice Yard's metronome (Slice 10; the rest of the game shows hearts only). A lane along the bottom of the
    /// screen: beat marks slide right to left into a hit line, downbeats bigger; the current beat pulses. Notes you play
    /// are marked on their beat, coloured by timing (an off-beat note shows OFF BEAT at the line instead). The lesson can
    /// prompt notes ("1-2-1-.") on the coming beats, Guitar Hero style. Above the lane: the lesson's task and progress.
    /// </summary>
    public class MetronomeStrip : MonoBehaviour
    {
        const float Spacing = 150f, HitX = -380f, Before = 2.6f, After = 5.4f;
        const int Pool = 12;

        static readonly Color LightNote = new Color(0.55f, 0.85f, 1f), HeavyNote = new Color(1f, 0.6f, 0.3f);
        static readonly Color OffColor = new Color(0.55f, 0.52f, 0.6f);

        Conductor _conductor;
        SwordCombatController _combat;
        RectTransform _lane;
        readonly List<Image> _pips = new List<Image>();
        readonly List<Text> _prompts = new List<Text>();
        readonly List<Text> _played = new List<Text>();
        Image _hitLine;
        Text _task, _progress, _judgement;
        float _judgeT;
        readonly Dictionary<long, (NoteButton b, Judgement j)> _notes = new Dictionary<long, (NoteButton, Judgement)>();

        /// <summary>Notes to prompt from each bar's downbeat ("1-2-1-."), or empty for none.</summary>
        public string Prompt { get; set; } = "";

        public static MetronomeStrip Create(Conductor conductor, SwordCombatController combat)
        {
            var go = new GameObject("Metronome Strip");
            var m = go.AddComponent<MetronomeStrip>();
            m._conductor = conductor;
            m._combat = combat;
            m.Build();
            return m;
        }

        public void SetTask(string task, string progress)
        {
            if (_task != null) _task.text = task ?? "";
            if (_progress != null) _progress.text = progress ?? "";
        }

        public void Show(bool on) { if (_lane != null) _lane.gameObject.SetActive(on); }

        void Build()
        {
            var c = UiKit.Canvas("Metronome Canvas", 140, transform);
            var frame = UiKit.Rect("Metronome", c.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            frame.pivot = new Vector2(0.5f, 0f);
            frame.sizeDelta = new Vector2(1320f, 210f);
            frame.anchoredPosition = new Vector2(0f, 28f);
            _lane = frame;
            var bg = frame.gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.03f, 0.08f, 0.72f);
            bg.raycastTarget = false;

            var mask = UiKit.Rect("Lane", frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 10f), new Vector2(-8f, 130f));
            mask.gameObject.AddComponent<RectMask2D>();
            var rail = Img(mask, "Rail", new Color(1f, 1f, 1f, 0.12f), new Vector2(1300f, 3f), new Vector2(0f, 60f));
            rail.raycastTarget = false;
            _hitLine = Img(mask, "Hit Line", UiKit.Gold, new Vector2(6f, 116f), new Vector2(HitX, 60f));
            for (int i = 0; i < Pool; i++)
            {
                _pips.Add(Img(mask, "Beat", Color.white, new Vector2(16f, 16f), Vector2.zero));
                _prompts.Add(Glyph(mask, 40));
                _played.Add(Glyph(mask, 34));
            }
            _judgement = UiKit.Label(frame, "", 30, TextAnchor.LowerCenter, UiKit.Gold);
            Place(_judgement.rectTransform, new Vector2(HitX, 154f), new Vector2(360f, 44f));
            _task = UiKit.Label(frame, "", 26, TextAnchor.UpperLeft, UiKit.Text);
            Place(_task.rectTransform, new Vector2(-120f, 184f), new Vector2(1040f, 36f));
            _progress = UiKit.Label(frame, "", 26, TextAnchor.UpperRight, UiKit.Gold);
            Place(_progress.rectTransform, new Vector2(420f, 184f), new Vector2(440f, 36f));
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static Image Img(Transform parent, string name, Color color, Vector2 size, Vector2 pos)
        {
            var rt = UiKit.Rect(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ProceduralSprites.White;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Glyph(Transform parent, int size)
        {
            var t = UiKit.Label(parent, "", size, TextAnchor.MiddleCenter);
            Place(t.rectTransform, Vector2.zero, new Vector2(60f, 50f));
            return t;
        }

        void OnEnable() { if (_combat != null) _combat.NoteJudged += OnNote; }
        void OnDisable() { if (_combat != null) _combat.NoteJudged -= OnNote; }
        void Start() { if (_combat != null) { _combat.NoteJudged -= OnNote; _combat.NoteJudged += OnNote; } }

        void OnNote(NoteResult n)
        {
            var beat = n.Judge.Beat;
            if (!n.OnBeat) _notes.Remove(beat.Index);
            if (n.OnBeat) _notes[beat.Index] = (n.Button, n.Effective);
            _judgement.text = Name(n.Effective);
            _judgement.color = ColorOf(n.Effective);
            _judgeT = 1f;
        }

        static string Name(Judgement j) => j == Judgement.TruePerfect ? "TRUE PERFECT" : j == Judgement.OffBeat ? "OFF BEAT" : j.ToString().ToUpperInvariant();

        static Color ColorOf(Judgement j)
        {
            switch (j)
            {
                case Judgement.TruePerfect: return new Color(1f, 0.95f, 0.55f);
                case Judgement.Perfect: return new Color(1f, 0.82f, 0.3f);
                case Judgement.Great: return new Color(0.5f, 0.95f, 0.6f);
                case Judgement.Good: return new Color(0.55f, 0.8f, 1f);
                default: return OffColor;
            }
        }

        void Update()
        {
            if (_lane == null || !_lane.gameObject.activeSelf) return;
            _judgeT = Mathf.Max(0f, _judgeT - Time.unscaledDeltaTime * 1.4f);
            var jc = _judgement.color; jc.a = Mathf.Clamp01(_judgeT * 1.5f); _judgement.color = jc;

            if (_conductor == null || _conductor.Map == null || !_conductor.IsPlaying)
            {
                foreach (var p in _pips) p.enabled = false;
                foreach (var t in _prompts) t.text = "";
                foreach (var t in _played) t.text = "";
                return;
            }
            var map = _conductor.Map;
            double now = map.TimeToBeat(_conductor.InputSongTime);
            long first = (long)System.Math.Ceiling(now - Before);
            float pulse = 1f - Mathf.Clamp01((float)(now - System.Math.Floor(now)) * 3f);

            for (int i = 0; i < Pool; i++)
            {
                long b = first + i;
                var pip = _pips[i];
                var prompt = _prompts[i];
                var played = _played[i];
                if (b > now + After || b < 0) { pip.enabled = false; prompt.text = ""; played.text = ""; continue; }
                var info = map.GetBeat(b);
                float x = HitX + (float)(b - now) * Spacing;
                bool down = info.IsDownbeat;
                bool current = System.Math.Abs(b - now) < 0.5;
                float size = (down ? 26f : 16f) * (current ? 1f + 0.35f * pulse : 1f);
                pip.enabled = true;
                pip.rectTransform.anchoredPosition = new Vector2(x, 60f);
                pip.rectTransform.sizeDelta = new Vector2(size, size);
                float fade = b < now - 0.5 ? 0.35f : 1f;
                pip.color = down ? new Color(1f, 0.84f, 0.42f, fade) : new Color(0.9f, 0.88f, 0.96f, 0.85f * fade);

                // Prompts on the coming beats (and the one just played, fading).
                var want = TutorialPlan.PromptAt(Prompt, info.BeatInMeasure, info.Measure, info.BeatsPerMeasure);
                prompt.rectTransform.anchoredPosition = new Vector2(x, 102f);
                if (want.HasValue && b >= now - 0.6)
                {
                    prompt.text = want.Value == NoteButton.One ? "1" : "2";
                    var col = want.Value == NoteButton.One ? LightNote : HeavyNote;
                    col.a = b < now - 0.1 ? 0.3f : 1f;
                    prompt.color = col;
                    prompt.fontStyle = FontStyle.Bold;
                }
                else prompt.text = "";

                // What you played on it.
                played.rectTransform.anchoredPosition = new Vector2(x, 22f);
                if (_notes.TryGetValue(b, out var n))
                {
                    played.text = n.b == NoteButton.One ? "1" : "2";
                    played.color = ColorOf(n.j);
                }
                else played.text = "";
            }

            var stale = new List<long>();
            foreach (var k in _notes.Keys) if (k < now - Before - 2) stale.Add(k);
            foreach (var k in stale) _notes.Remove(k);
        }
    }
}
