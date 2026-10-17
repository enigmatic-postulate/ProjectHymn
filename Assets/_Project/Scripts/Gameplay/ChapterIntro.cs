using System.Collections;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Plays a chapter's prologue cards the first time you arrive in its hub (VS-6: chapter two at the Air Bell), then
    /// remembers it in the save. Free play skips it.
    /// </summary>
    public class ChapterIntro : MonoBehaviour
    {
        [Tooltip("Save flag that marks the prologue as seen.")]
        public string flag = Story.DeepIntroSeen;
        public float delay = 0.6f;

        IEnumerator Start()
        {
            if (!GameSession.Exists) yield break;
            var s = GameSession.Instance;
            if (s.FreePlay || s.Data.HasStory(flag)) yield break;
            yield return new WaitForSecondsRealtime(delay);
            while (s.Travelling) yield return null;
            s.Data.MarkStory(flag);
            s.SaveProgress();
            StoryCards.Play(Story.DeepIntro, null);
        }
    }
}
