using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A button rhythm (1 = light, 2 = heavy) and the move each step plays, e.g. Crossing Cuts 1-2-1:
    /// diagonal cut, horizontal slash, rising cut. Finishing the rhythm multiplies the last hit. Since the sequence library
    /// (GDD update 18) each sequence belongs to a class tree and has a home meter it only plays in; the editor fills these
    /// from Combat.SequenceLibrary.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Sword Sequence", fileName = "NewSwordSequence")]
    public class SwordSequence : ScriptableObject
    {
        public string displayName = "Sequence";
        [Tooltip("Save key (CrossingCuts, Powerslice, Tempest): sequences are learned by key, for every class at once. Empty = from the name.")]
        [SerializeField] string id = "";
        [Tooltip("Home meter (4/4, 3/4, 7/8): the sequence only plays in it. Empty = any meter.")]
        public string meter = "4/4";
        [Tooltip("The class tree it belongs to (taking that class learns it).")]
        public string tree = "minstrel";
        [Tooltip("What the finisher does besides damage.")]
        public SequenceEffect effect = SequenceEffect.None;
        public List<NoteButton> notes = new List<NoteButton>();
        [Tooltip("One move per note, same order.")]
        public List<SwordMove> moves = new List<SwordMove>();
        [Tooltip("Damage multiplier on the hit that completes the rhythm.")]
        public float finisherMultiplier = 2f;
        public Color color = new Color(1f, 0.82f, 0.48f);

        public string key { get => string.IsNullOrEmpty(id) ? displayName.Replace(" ", "") : id; set => id = value; }
        public string NotesText => string.Join("-", notes.ConvertAll(n => ((int)n).ToString()));

        public bool IsValid => notes.Count > 0 && moves.Count == notes.Count && !moves.Contains(null);
        public SequencePattern ToPattern() => new SequencePattern(displayName, notes.ToArray(), this, meter);
    }
}
