using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Shows in the village what the story has brought home (VS-5). On arrival every <see cref="VerseGroup"/> for a verse
    /// already home is shown and the rest hidden. The first time back after a verse comes home, that verse is celebrated:
    /// a banner names it, a bell arpeggio plays, its music layer fades in and its pieces appear one by one along the street
    /// (lanterns lighting from west to east). Then the save remembers it.
    /// </summary>
    public class VerseRestoration : MonoBehaviour
    {
        [Tooltip("Seconds after arriving before the celebration starts (the scene fade-in).")]
        public float delay = 1.2f;
        [Tooltip("Seconds between pieces appearing.")]
        public float stagger = 0.22f;
        [Tooltip("The verses this hub celebrates (Saltmere 1 to 3, the Air Bell 4 to 6); a newer verse from elsewhere waits for its own hub.")]
        public int firstVerse = 1, lastVerse = 3;

        void Start()
        {
            var s = GameSession.Instance;
            int chapter = Story.Chapter(s.Data);
            int reveal = s.FreePlay ? 0 : Story.VerseToReveal(s.Data);
            if (reveal < firstVerse || reveal > lastVerse) reveal = 0;
            var groups = FindObjectsByType<VerseGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var g in groups) g.gameObject.SetActive(g.verse <= chapter && g.verse != reveal);
            if (reveal > 0) StartCoroutine(Celebrate(reveal, groups));
        }

        IEnumerator Celebrate(int verse, VerseGroup[] groups)
        {
            var music = LayeredMusic.Active;
            if (music != null) music.HoldBack(verse);
            yield return new WaitForSecondsRealtime(delay);
            while (GameSession.Instance.Travelling || StoryCards.Playing) yield return null;

            Sfx.Play(Sfx.VerseChime, 0.9f);
            if (music != null) music.Reveal(verse, 4f);
            StartCoroutine(Banner(Story.VerseTitle(verse) + " comes home", Story.RevealLine(verse)));

            // The pieces, west to east.
            var pieces = new List<Transform>();
            foreach (var g in groups)
            {
                if (g.verse != verse) continue;
                foreach (Transform child in g.transform) { child.gameObject.SetActive(false); pieces.Add(child); }
                g.gameObject.SetActive(true);
            }
            pieces.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            foreach (var p in pieces)
            {
                StartCoroutine(Pop(p));
                yield return new WaitForSecondsRealtime(stagger);
            }

            var s = GameSession.Instance;
            Story.MarkRevealed(s.Data, verse);
            s.SaveProgress();
        }

        static IEnumerator Pop(Transform t)
        {
            var full = t.localScale;
            t.localScale = Vector3.zero;
            t.gameObject.SetActive(true);
            for (float u = 0f; u < 1f; u += Time.unscaledDeltaTime / 0.45f)
            {
                // Overshoot a little, then settle.
                float k = 1f + 2.2f * Mathf.Pow(u - 1f, 3f) + 1.2f * Mathf.Pow(u - 1f, 2f);
                t.localScale = full * Mathf.Max(0f, k);
                yield return null;
            }
            t.localScale = full;
        }

        IEnumerator Banner(string title, string line)
        {
            var c = UiKit.Canvas("Verse Banner", 150, transform);
            var group = c.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var band = UiKit.Rect("Band", c.transform, new Vector2(0f, 0.62f), new Vector2(1f, 0.82f));
            band.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.02f, 0.09f, 0.55f);
            var big = UiKit.Label(band, title, 60, TextAnchor.UpperCenter, UiKit.Gold);
            ((RectTransform)big.transform).offsetMax = new Vector2(0f, -18f);
            big.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            var small = UiKit.Label(band, line, 32, TextAnchor.LowerCenter, UiKit.Text);
            ((RectTransform)small.transform).offsetMin = new Vector2(0f, 22f);
            small.gameObject.AddComponent<Shadow>();
            for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Clamp01(Mathf.Min(t / 0.8f, (6f - t) / 1.2f));
                yield return null;
            }
            Destroy(c.gameObject);
        }
    }
}
