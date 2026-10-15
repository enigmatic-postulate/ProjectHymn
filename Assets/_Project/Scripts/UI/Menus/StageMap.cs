using System.Collections.Generic;
using System.Text;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The road out of Saltmere (VS-5 Part B), opened at the village gate. The story path runs left to right (Practice Yard,
    /// Cliffside, Belfry Ridge, the Summit) with the side roads branching below the stage that opens them. Each stop shows
    /// its time signature, whether it holds a verse, and your best rank as a big coloured letter; locked ones are dimmed.
    /// The next story stop is marked NEXT and selected. Details for the highlighted stop sit along the bottom. Arrow keys
    /// or the stick choose, Enter goes, Esc steps back.
    /// </summary>
    public class StageMap : MonoBehaviour
    {
        static StageMap _open;
        public static bool IsOpen => _open != null;

        Text _info;
        RectTransform _nextTag;
        float _openedAt;

        static readonly Color Line = new Color(0.85f, 0.68f, 0.32f, 0.85f), LineDim = new Color(0.4f, 0.36f, 0.5f, 0.6f);
        static readonly Color Locked = new Color(0.42f, 0.4f, 0.5f);

        public static void Open()
        {
            if (_open != null) return;
            _open = new GameObject("Stage Map").AddComponent<StageMap>();
            _open.Build();
        }

        void Build()
        {
            UiState.Push();
            _openedAt = Time.unscaledTime;
            var s = GameSession.Instance;
            var c = UiKit.Canvas("Stage Map Canvas", 270, transform);
            var dim = UiKit.Rect("Dim", c.transform, Vector2.zero, Vector2.one);
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.05f, 0.55f);
            var panel = UiKit.Panel("Map", c.transform, new Vector2(1640f, 880f));

            var title = UiKit.Label(panel, "THE ROAD FROM SALTMERE", 40, TextAnchor.UpperCenter, UiKit.Gold);
            ((RectTransform)title.transform).offsetMax = new Vector2(0f, -26f);
            var sub = UiKit.Label(panel, "Each road is fought in its own time. Clear a stage to open the next.", 24, TextAnchor.UpperCenter, UiKit.Dim);
            ((RectTransform)sub.transform).offsetMax = new Vector2(0f, -80f);

            // The story path, left to right, and the side roads below what opens them.
            var pos = new Dictionary<string, Vector2>();
            float[] xs = { -570f, -190f, 190f, 570f };
            for (int i = 0; i < StageRoute.Path.Length; i++) pos[StageRoute.Path[i].Id] = new Vector2(xs[i], 170f);
            foreach (var r in StageRoute.SideRoads)
                pos[r.Id] = pos.TryGetValue(r.Requires, out var above) ? above + new Vector2(0f, -250f) : new Vector2(0f, -80f);

            // Roads first, so the stops draw over them.
            for (int i = 0; i + 1 < StageRoute.Path.Length; i++)
            {
                var b = StageRoute.Path[i + 1];
                Bar(panel, pos[StageRoute.Path[i].Id], pos[b.Id], StageRoute.IsOpen(s.Data, b, s.FreePlay));
            }
            foreach (var r in StageRoute.SideRoads)
                if (pos.TryGetValue(r.Requires, out var from)) Bar(panel, from, pos[r.Id], StageRoute.IsOpen(s.Data, r, s.FreePlay));

            var next = StageRoute.Next(s.Data, s.FreePlay);
            Button select = null;
            foreach (var r in StageRoute.All)
            {
                var b = Stop(panel, r, pos[r.Id], s);
                if (next != null && r.Id == next.Id) { select = b; _nextTag = NextTag(panel, pos[r.Id]); }
            }
            if (select == null)
            {
                // The story is done (or nothing is open): start on the last stop played on the path.
                foreach (var r in StageRoute.Path) if (StageRoute.IsOpen(s.Data, r, s.FreePlay)) select = _stops[r.Id];
            }

            var infoFrame = UiKit.Rect("Info", panel, new Vector2(0f, 0f), new Vector2(1f, 0f));
            infoFrame.pivot = new Vector2(0.5f, 0f);
            infoFrame.sizeDelta = new Vector2(-80f, 200f);
            infoFrame.anchoredPosition = new Vector2(0f, 66f);
            infoFrame.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            _info = UiKit.Label(infoFrame, "", 25, TextAnchor.UpperLeft, UiKit.Text);
            ((RectTransform)_info.transform).offsetMin = new Vector2(26f, 14f);
            ((RectTransform)_info.transform).offsetMax = new Vector2(-26f, -14f);
            var hint = UiKit.Label(panel, "Arrows: choose     Enter: go     Esc: back", 22, TextAnchor.LowerCenter, UiKit.Dim);
            ((RectTransform)hint.transform).offsetMin = new Vector2(0f, 22f);
            UiKit.Select(select);
        }

        readonly Dictionary<string, Button> _stops = new Dictionary<string, Button>();

        Button Stop(RectTransform panel, RouteStop r, Vector2 at, GameSession s)
        {
            bool open = StageRoute.IsOpen(s.Data, r, s.FreePlay);
            var b = UiKit.Button(panel, "", () => Choose(r), 30, 0f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = r.Side ? new Vector2(320f, 130f) : new Vector2(330f, 170f);
            rt.anchoredPosition = at;
            var label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            ((RectTransform)label.transform).offsetMin = new Vector2(22f, 0f);
            ((RectTransform)label.transform).offsetMax = new Vector2(-96f, 0f);
            string tag = r.Practice ? "practice" : r.Side ? "side road" : r.Verse > 0 ? $"♪ verse {r.Verse}" : "";
            label.text = $"<size={(r.Side ? 30 : 34)}>{r.Name}</size>\n<size=22>{r.Meter}</size>\n<size=22>{tag}   ·   {StageRoute.Status(s.Data, r, s.FreePlay)}</size>";
            label.color = open ? UiKit.Text : Locked;

            // The best rank, big, on the right.
            string letter = "";
            Color col = UiKit.Dim;
            if (open && !r.Practice && s.Data.TryBestRank(r.Id, out StageRank rank)) { letter = rank.ToString(); col = StageResultsPanel.RankColor(rank); }
            if (letter.Length > 0)
            {
                var big = UiKit.Label(b.transform, letter, r.Side ? 64 : 80, TextAnchor.MiddleRight, open ? col : Locked);
                ((RectTransform)big.transform).offsetMax = new Vector2(-20f, 0f);
                if (open) big.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            }
            b.gameObject.AddComponent<SelectHint>().OnSelected = () => _info.text = Info(r, s);
            _stops[r.Id] = b;
            return b;
        }

        static RectTransform NextTag(RectTransform panel, Vector2 at)
        {
            var t = UiKit.Label(panel, "NEXT ▼", 24, TextAnchor.MiddleCenter, UiKit.Gold);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 34f);
            rt.anchoredPosition = at + new Vector2(0f, 108f);
            return rt;
        }

        static void Bar(RectTransform panel, Vector2 a, Vector2 b, bool open)
        {
            var rt = UiKit.Rect("Road", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var d = b - a;
            rt.sizeDelta = new Vector2(d.magnitude, open ? 8f : 5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = open ? Line : LineDim;
            img.raycastTarget = false;
        }

        static string Info(RouteStop r, GameSession s)
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#ffd56a><size=32>{r.Name}</size></color>   <color=#8a84a0>{r.Meter}");
            if (r.Verse > 0) sb.Append($"   ·   holds {Story.VerseNames[r.Verse - 1]}");
            else if (r.Side) sb.Append("   ·   side road");
            sb.Append("</color>\n").Append(r.Blurb).Append('\n');
            if (!StageRoute.IsOpen(s.Data, r, s.FreePlay)) sb.Append($"<color=#ff8a7a>{StageRoute.LockReason(s.Data, r, s.FreePlay)}</color>");
            else if (r.Practice) sb.Append(s.Data.tutorialDone ? "<color=#8a84a0>Finished. Come back any time to practise.</color>" : "<color=#ffd56a>New to the beat? Start here.</color>");
            else
            {
                var rec = s.Data.Record(r.Id);
                sb.Append(rec != null && !string.IsNullOrEmpty(rec.bestRank)
                    ? $"<color=#8a84a0>Best {rec.bestRank} in {StageRoute.Time(rec.bestSeconds)}, cleared {rec.clears}×.</color>"
                    : "<color=#8a84a0>Not cleared yet.</color>");
            }
            return sb.ToString();
        }

        void Choose(RouteStop r)
        {
            var s = GameSession.Instance;
            if (!StageRoute.IsOpen(s.Data, r, s.FreePlay))
            {
                _info.text = Info(r, s);
                Sfx.Play(Sfx.MenuBack, 0.7f);
                return;
            }
            Close();
            s.Travel(r.Scene, "Start");
        }

        void Update()
        {
            if (_nextTag != null)
                _nextTag.localScale = Vector3.one * (1f + 0.08f * BeatClock.Pulse(4f));
            if (Time.unscaledTime - _openedAt > 0.15f && UiInput.Cancel) { Sfx.Play(Sfx.MenuBack, 0.7f); Close(); }
        }

        void Close()
        {
            if (_open != this) return;
            _open = null;
            UiState.Pop();
            Destroy(gameObject);
        }

        void OnDestroy() { if (_open == this) { _open = null; UiState.Pop(); } }
    }
}
