using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Combat
{
    /// <summary>What the player did on one beat. Rest = deliberately nothing.</summary>
    public enum BeatAction { Rest = 0, Strike = 1, Smash = 2, Step = 3, Guard = 4 }

    /// <summary>
    /// A combo: a pattern of actions on consecutive beats. Matched live by <see cref="ComboEngine"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Phrase", fileName = "NewPhrase")]
    public class PhraseData : ScriptableObject
    {
        public string displayName = "New Phrase";
        [TextArea] public string description;

        [Tooltip("One entry per beat. Must end on an action, not a Rest.")]
        public List<BeatAction> pattern = new List<BeatAction> { BeatAction.Strike, BeatAction.Strike, BeatAction.Smash };

        [Tooltip("0 = any meter. 3 = only matches while the song is in 3 beats per measure (e.g. a Waltz phrase).")]
        [Min(0)] public int requiredBeatsPerMeasure;

        [Tooltip("Applied to the hit that completes the phrase.")]
        [Min(0f)] public float damageMultiplier = 1.5f;

        [Tooltip("Free-form id the combat controller maps to an effect (stagger, shockwave, aoe...).")]
        public string effectId = "stagger";

        public int Length => pattern?.Count ?? 0;

        public bool IsValid => Length > 0 && pattern[Length - 1] != BeatAction.Rest;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Length > 0 && !IsValid)
                Debug.LogWarning($"Phrase '{name}' ends on a Rest; phrases must end on an action.", this);
        }
#endif
    }
}
