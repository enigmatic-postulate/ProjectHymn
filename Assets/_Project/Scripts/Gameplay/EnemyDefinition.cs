using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    public enum EnemyMovement
    {
        Hopper,      // moves in little hops on each beat, stays close (Metronome Slime)
        Skirmisher,  // keeps its distance, dashes in to strike (Snare Imp)
        Juggernaut,  // slow, steady advance, guarded (Tuba Knight)
        Flyer,       // circles the player, swoops in for each peck (Piccolo Bat)
    }

    public enum EnemyShape { Circle, Square }

    /// <summary>
    /// Data for one enemy type: stats, look, movement style and the rhythm it attacks on.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Enemy Definition", fileName = "NewEnemy")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Enemy";
        [TextArea] public string teaches;

        [Header("Stats")]
        [Min(1f)] public float maxHp = 40f;
        [Min(0f)] public float damage = 8f;
        [Tooltip("Heavy attacks need Great timing to parry instead of Good.")]
        public bool heavy;
        [Tooltip("Blocks normal hits (15% damage) until a guard-break Phrase (Syncopate) or a Bar Finisher lands.")]
        public bool guarded;
        [Min(0)] public int scoreValue = 300;

        [Header("Rhythm")]
        public EnemyPattern pattern = new EnemyPattern(1, 0, 0f);

        [Header("Movement")]
        public EnemyMovement movement = EnemyMovement.Hopper;
        [Min(0f)] public float moveSpeed = 2f;
        [Tooltip("Distance it likes to keep from the player between attacks.")]
        [Min(0f)] public float preferredDistance = 1f;
        [Tooltip("Reach of its attack, measured from its centre.")]
        [Min(0f)] public float attackRange = 1.3f;

        [Header("Look")]
        public EnemyShape shape = EnemyShape.Circle;
        public Color color = Color.green;
        public Vector2 size = new Vector2(0.8f, 0.8f);
    }
}
