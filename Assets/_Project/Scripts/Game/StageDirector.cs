using ProjectHymn.Progression;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Stage rules for Hub &amp; Loop: defeated enemies drop Notes, and reaching the goal clears the stage, then the
    /// player walks back into the village (which autosaves).
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        [SerializeField] string stageId = "Cliffside";
        [SerializeField] Vector2Int notesPerEnemy = new Vector2Int(20, 40);
        [SerializeField] SliceGoal goal;
        [SerializeField] string returnSpawn = "FromCliffside";
        [SerializeField] float returnDelay = 2.5f;

        readonly System.Random _rng = new System.Random();
        DamagePopups _popups;

        void Start()
        {
            _popups = FindAnyObjectByType<DamagePopups>();
            foreach (var e in BeatEnemy.All) e.Died += OnEnemyDied;
            if (goal == null) goal = FindAnyObjectByType<SliceGoal>();
            if (goal != null) goal.Reached += OnGoal;
        }

        void OnDestroy()
        {
            foreach (var e in BeatEnemy.All) e.Died -= OnEnemyDied;
            if (goal != null) goal.Reached -= OnGoal;
        }

        void OnEnemyDied(BeatEnemy e)
        {
            int n = Rewards.RollNotes(notesPerEnemy.x, notesPerEnemy.y, _rng);
            GameSession.Instance.AddNotes(n);
            if (_popups != null) _popups.Show(e.transform.position + Vector3.up * 2.2f, $"+{n} ♪", new Color(1f, 0.85f, 0.4f), 1.2f);
        }

        void OnGoal()
        {
            var s = GameSession.Instance;
            s.Data.ClearStage(stageId);
            GameHud.Toast($"{stageId} cleared!");
            if (s.Db != null) Invoke(nameof(Return), returnDelay);
        }

        void Return() => GameSession.Instance.Travel(GameSession.Instance.Db.hubScene, returnSpawn, saveOnArrival: true);
    }
}
