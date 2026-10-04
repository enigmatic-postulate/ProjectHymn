using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The vertical slice's run status: a small timer and fall count, a toast for checkpoints, and the clear message
    /// at the goal. R restarts from the first checkpoint.
    /// </summary>
    public class SliceRun : MonoBehaviour
    {
        [SerializeField] PlatformerController player;
        [SerializeField] SliceGoal goal;
        [SerializeField] Text status;
        [SerializeField] Text toast;
        [SerializeField] Text hearts;
        [SerializeField] PlayerHealth health;

        float _time, _toastTime;
        int _falls, _foes, _defeated;
        bool _done;
        Vector3 _start;

        void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlatformerController>();
            if (goal == null) goal = FindAnyObjectByType<SliceGoal>();
            if (player != null) _start = player.transform.position;
            if (health == null && player != null) health = player.GetComponent<PlayerHealth>();
        }

        void Start()
        {
            foreach (var e in BeatEnemy.All) { _foes++; e.Died += OnFoeDown; }
        }

        void OnFoeDown(BeatEnemy e)
        {
            _defeated++;
            Toast($"{e.DisplayName} defeated");
        }

        void OnEnable()
        {
            if (player != null)
            {
                player.Respawned += OnRespawned;
                player.CheckpointReached += OnCheckpoint;
            }
            if (goal != null) goal.Reached += OnGoal;
        }

        void OnDisable()
        {
            if (player != null)
            {
                player.Respawned -= OnRespawned;
                player.CheckpointReached -= OnCheckpoint;
            }
            if (goal != null) goal.Reached -= OnGoal;
        }

        void OnRespawned() { if (!_done) _falls++; }

        void OnCheckpoint(Checkpoint cp) { if (Mathf.Abs(cp.transform.position.x - _start.x) > 1f) Toast("Checkpoint"); }

        void OnGoal()
        {
            _done = true;
            Toast($"Summit reached   {Clock(_time)}   falls {_falls}   foes {_defeated}/{_foes}", 6f);
        }

        void Toast(string text, float seconds = 1.6f)
        {
            if (toast == null) return;
            toast.text = text;
            _toastTime = seconds;
        }

        void Update()
        {
            if (!_done) _time += Time.deltaTime;
            if (status != null) status.text = _foes > 0 ? $"{Clock(_time)}   falls {_falls}   foes {_defeated}/{_foes}" : $"{Clock(_time)}   falls {_falls}";
            if (hearts != null && health != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < health.MaxHp; i++) sb.Append(i < health.Hp ? "<color=#ff5a4a>♥</color>" : "<color=#4a4050>♥</color>");
                hearts.text = sb.ToString();
            }
            if (toast != null)
            {
                _toastTime -= Time.deltaTime;
                var c = toast.color;
                c.a = Mathf.Clamp01(_toastTime * 2f);
                toast.color = c;
            }
            if (RestartPressed() && player != null)
            {
                player.SetRespawn(_start);
                player.Respawn();
                _time = 0f; _falls = 0; _done = false;
            }
        }

        static bool RestartPressed()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        static string Clock(float t) => $"{(int)(t / 60f)}:{t % 60f:00.0}";
    }
}
