using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The swordsman's movement poses for vertical slice 1, as keyframes on the same rig as the sword moves.
    /// The run cycle's <see cref="MoveKey.beat"/> is its phase (0..1: right foot down at 0, left at 0.5); the
    /// <see cref="RhythmAnimator"/> reads the phase from the music so footfalls land on beats. Bones a key doesn't list
    /// hold the weapon stance (so the sword arm stays ready); bones in <see cref="additiveBones"/> are applied on top of
    /// the stance instead of replacing it. While a sword move plays, the move owns the upper body and these poses keep
    /// the legs and hips.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Locomotion Set", fileName = "Locomotion")]
    public class LocomotionSet : ScriptableObject
    {
        public List<MoveKey> run = new List<MoveKey>();
        public MoveKey jump = new MoveKey();
        [Tooltip("The double jump's tucked pose; the hips also flip once around it.")]
        public MoveKey airJump = new MoveKey();
        public MoveKey fall = new MoveKey();
        public MoveKey land = new MoveKey();
        public MoveKey hang = new MoveKey();
        public MoveKey climbPull = new MoveKey();
        public MoveKey climbOver = new MoveKey();

        [Header("Clips (art pass; used instead of the keys when Run Clip is set)")]
        public BakedClip runClip;
        [Tooltip("Normalized time in the run clip where the right foot touches down.")]
        [Range(0f, 1f)] public float runRightFootDown = 0.684f;
        public BakedClip jumpClip;
        public BakedClip airClip;
        public BakedClip landClip;
        [Tooltip("Base pose for hanging and climbing; the hands are then placed on the ledge by IK.")]
        public BakedClip hangClip;
        [Range(0f, 1f)] public float hangClipTime = 0.5f;
        [Tooltip("Played (full body, briefly) when the player is hurt.")]
        public SwordMove hurt;

        public bool UsesClips => runClip != null;

        [Header("Timing")]
        [Tooltip("Natural running cadence (steps per second); the beat-locked stride stays close to it.")]
        public float cadence = 2.8f;
        public float flipSeconds = 0.42f;
        public float landSeconds = 0.14f;
        [Tooltip("How much of the run cycle shows at a crawl (1 = full stride at any speed).")]
        [Range(0f, 1f)] public float minStride = 0.35f;

        [Tooltip("Bones whose keys add to the stance rather than replace it (the torso lean).")]
        public List<string> additiveBones = new List<string> { "Spine", "Chest" };

        public bool IsAdditive(string bone) => additiveBones.Contains(bone);

        void OnValidate() => run.Sort((a, b) => a.beat.CompareTo(b.beat));
    }
}
