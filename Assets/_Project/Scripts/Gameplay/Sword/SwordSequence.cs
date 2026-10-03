using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A button rhythm (1 = light, 2 = heavy) and the move each step plays, e.g. Crossing Cuts 1-2-1:
    /// diagonal cut, horizontal slash, rising cut. Finishing the rhythm multiplies the last hit.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Sword Sequence", fileName = "NewSwordSequence")]
    public class SwordSequence : ScriptableObject
    {
        public string displayName = "Sequence";
        public List<NoteButton> notes = new List<NoteButton>();
        [Tooltip("One move per note, same order.")]
        public List<SwordMove> moves = new List<SwordMove>();
        [Tooltip("Damage multiplier on the hit that completes the rhythm.")]
        public float finisherMultiplier = 2f;
        public Color color = new Color(1f, 0.82f, 0.48f);

        public bool IsValid => notes.Count > 0 && moves.Count == notes.Count && !moves.Contains(null);
        public SequencePattern ToPattern() => new SequencePattern(displayName, notes.ToArray(), this);
    }
}
