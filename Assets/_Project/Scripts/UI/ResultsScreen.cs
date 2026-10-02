using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// End of an arena: rank (S–D from accuracy, Groove peak and downs), score, Notes earned, first-clear
    /// Phrase and remix unlock. Saves progress, then offers Return to Town or Retry.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [SerializeField] ArenaDirector arena;
        [SerializeField] BeatCombatController combat;
        [SerializeField] string arenaId = "arena1";
        [SerializeField] string arenaName = "Arena";
        [Tooltip("Phrase asset name learned on the first clear (empty = none).")]
        [SerializeField] string firstClearPhrase;
        [SerializeField] float delaySeconds = 2.5f;

        Canvas _canvas;
        public static bool IsOpen { get; private set; }

        void Awake()
        {
            if (arena == null) arena = FindAnyObjectByType<ArenaDirector>();
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
            IsOpen = false;
        }

        void OnEnable() { if (arena != null) arena.ArenaCleared += OnCleared; }
        void OnDisable() { if (arena != null) arena.ArenaCleared -= OnCleared; }

        void OnCleared(long score)
        {
            var rank = RankCalculator.Calculate(combat != null ? combat.Accuracy : 0, combat != null ? combat.PeakGroove : 0, arena.TimesDowned);
            var rules = SaveSystem.Rules;
            var reward = rules.RecordArena(arenaId, rank, score, firstClearPhrase);
            SaveSystem.Save();
            StartCoroutine(ShowAfterDelay(rank, score, reward));
        }

        System.Collections.IEnumerator ShowAfterDelay(Rank rank, long score, RunReward reward)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            IsOpen = true;
            _canvas = UIFactory.Overlay("Results");
            UIFactory.Backdrop(_canvas.transform, 0.7f);
            var panel = UIFactory.Panel(_canvas.transform, new Vector2(820, 860));

            UIFactory.Label(panel, arenaName.ToUpperInvariant(), 40, color: UIFactory.DimText);
            UIFactory.Label(panel, rank.ToString(), 160, color: RankColor(rank), height: 190);
            if (reward.NewBestRank) UIFactory.Label(panel, "NEW BEST RANK", 26, color: UIFactory.Gold);

            float acc = combat != null ? (float)combat.Accuracy : 0f;
            float peak = combat != null ? combat.PeakGroove : 0f;
            UIFactory.Label(panel, $"Score  {score:N0}", 34);
            UIFactory.Label(panel, $"On-beat accuracy  {acc * 100f:0}%     Groove peak  {peak:0}     Downs  {arena.TimesDowned}", 26, color: UIFactory.DimText);
            UIFactory.Label(panel, $"+{reward.NotesEarned} Notes   (total {SaveSystem.Current.notes})", 32, color: UIFactory.Gold);
            if (!string.IsNullOrEmpty(reward.PhraseUnlocked)) UIFactory.Label(panel, $"New Phrase learned: {reward.PhraseUnlocked}", 30, color: UIFactory.Gold);
            if (reward.RemixUnlocked) UIFactory.Label(panel, "S rank: remix of this theme unlocked", 26, color: UIFactory.Gold);

            var back = UIFactory.Button(panel, "Return to Town", () => Leave(SceneNames.Town));
            UIFactory.Button(panel, "Retry", () => Leave(SceneManager.GetActiveScene().name));
            UIFactory.Focus(back);
        }

        static void Leave(string scene)
        {
            IsOpen = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        static Color RankColor(Rank r) => r switch
        {
            Rank.S => UIFactory.Gold,
            Rank.A => new Color(0.5f, 1f, 0.6f),
            Rank.B => new Color(0.55f, 0.8f, 1f),
            Rank.C => new Color(0.85f, 0.8f, 0.95f),
            _ => new Color(0.75f, 0.6f, 0.6f),
        };
    }
}
