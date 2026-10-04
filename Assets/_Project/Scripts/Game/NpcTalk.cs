using System;
using System.Collections.Generic;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>A villager who says a few lines (and may hand over a lore piece the first time).</summary>
    public class NpcTalk : Interactable
    {
        public string speaker = "Villager";
        [TextArea(2, 4)] public string[] lines = new string[0];
        [Tooltip("Lore id given after the first talk (empty = none).")]
        public string givesLore = "";

        public override void Interact(PlayerInteractor who)
        {
            DialogueBox.Show(speaker, lines, () =>
            {
                if (!string.IsNullOrEmpty(givesLore) && GameSession.Instance.AddLore(givesLore))
                    GameHud.Toast("Lore recorded in the Songbook");
            });
        }
    }
}
