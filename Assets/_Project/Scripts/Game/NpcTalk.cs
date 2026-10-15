using System;
using System.Collections.Generic;
using UnityEngine;

using ProjectHymn.Progression;
using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A villager who says a few lines (and may hand over a lore piece the first time). With a <see cref="storyId"/>
    /// (VS-5) the lines come from <see cref="Story"/> and change as verses come home; otherwise the fixed lines play.
    /// Talking shows a live portrait of them.
    /// </summary>
    public class NpcTalk : Interactable
    {
        public string speaker = "Villager";
        [Tooltip("A villager in Progression/Story.cs (Elder, Fisher, Lookout, Lamplighter): lines follow the story.")]
        public string storyId = "";
        [TextArea(2, 4)] public string[] lines = new string[0];
        [Tooltip("Lore id given after the first talk (empty = none).")]
        public string givesLore = "";

        public override void Interact(PlayerInteractor who)
        {
            var s = GameSession.Instance;
            string[] say = lines;
            string lore = givesLore;
            if (!string.IsNullOrEmpty(storyId))
            {
                var fromStory = Story.LinesFor(storyId, Story.Chapter(s.Data));
                if (fromStory.Length > 0) say = fromStory;
                if (string.IsNullOrEmpty(lore)) lore = Story.LoreFrom(storyId);
            }
            DialogueBox.Show(speaker, say, () =>
            {
                if (!string.IsNullOrEmpty(lore) && s.AddLore(lore))
                    GameHud.Toast("Lore recorded in the Songbook");
            }, transform);
        }
    }
}
