using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A gate between two rooms of a stage (GDD M6). Blocks the way while closed; opens by sinking into the
    /// floor (the stage director opens it on a downbeat when the room is cleared) and closes behind the player.
    /// </summary>
    public class StageDoor : MonoBehaviour
    {
        [SerializeField] Collider blocker;
        [Tooltip("The part that sinks into the floor.")]
        [SerializeField] Transform visual;
        [SerializeField] float sinkDepth = 3f;
        [SerializeField] bool startOpen;

        Vector3 _closedPos;
        Coroutine _anim;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (visual != null) _closedPos = visual.localPosition;
            Set(startOpen, 0f);
        }

        public void Open(float seconds = 0.4f) => Set(true, seconds);
        public void Close(float seconds = 0.25f) => Set(false, seconds);

        void Set(bool open, float seconds)
        {
            IsOpen = open;
            if (blocker != null) blocker.enabled = !open;
            if (visual == null) return;
            if (_anim != null) StopCoroutine(_anim);
            var target = _closedPos + (open ? Vector3.down * sinkDepth : Vector3.zero);
            if (seconds <= 0f || !isActiveAndEnabled) { visual.localPosition = target; return; }
            _anim = StartCoroutine(Move(target, seconds));
        }

        IEnumerator Move(Vector3 target, float seconds)
        {
            var from = visual.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                visual.localPosition = Vector3.Lerp(from, target, k * k * (3f - 2f * k));
                yield return null;
            }
            visual.localPosition = target;
        }
    }
}
