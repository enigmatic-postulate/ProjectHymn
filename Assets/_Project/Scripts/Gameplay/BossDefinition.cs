using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>One boss phase: how it fights while the music plays the matching song phase.</summary>
    [Serializable]
    public class BossPhase
    {
        public string name = "Phase";
        [Tooltip("The boss moves to this phase when its HP fraction drops to this value (first phase: 1).")]
        [Range(0f, 1f)] public float enterAtHpFraction = 1f;

        [Header("Attacks")]
        public EnemyPattern pattern = new EnemyPattern(1, 0, 0f);
        [Min(0f)] public float damage = 15f;
        [Min(0f)] public float attackRange = 2.3f;
        public bool heavy;

        [Header("Defence")]
        [Tooltip("Normal hits do 15% while the guard is up.")]
        public bool guarded;
        [Tooltip("Phrase effect that breaks the guard (a Bar Finisher always does). 'spin' = Waltz, 'guard_break' = Syncopate.")]
        public string guardBreakEffect = "guard_break";
        [Tooltip("Final phase: HP stops at 1 and only a Bar Finisher can end the fight.")]
        public bool finisherOnly;

        [Header("Look and adds")]
        public Color color = Color.white;
        [Tooltip("Enemies summoned when this phase begins.")]
        public EnemyDefinition[] summons = new EnemyDefinition[0];
        [Tooltip("Shown as a banner when the phase begins.")]
        public string hint;
    }

    /// <summary>A boss: a base EnemyDefinition (look, movement, HP) plus phases tied to the song's phases.</summary>
    [CreateAssetMenu(menuName = "Project Hymn/Boss Definition", fileName = "NewBoss")]
    public class BossDefinition : ScriptableObject
    {
        public string displayName = "Boss";
        [Tooltip("Look, movement and max HP. Its pattern and damage are replaced by each phase.")]
        public EnemyDefinition body;
        [Tooltip("Must line up with the song's phases: boss phase i plays over song phase i.")]
        public List<BossPhase> phases = new List<BossPhase>();
    }
}
