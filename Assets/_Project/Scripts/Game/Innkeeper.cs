using System;
using System.Collections.Generic;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>The inn: rest to full hearts and save.</summary>
    public class Innkeeper : Interactable
    {
        public string speaker = "Innkeeper";
        [TextArea(2, 4)] public string[] lines = { "Rest a while, Cantor. The road up the cliffs is long." };
        public string spawnId = "Inn";

        public override void Interact(PlayerInteractor who)
        {
            DialogueBox.Show(speaker, lines, () =>
            {
                var health = who.GetComponent<PlayerHealth>();
                if (health != null) health.HealFull();
                bool saved = GameSession.Instance.Save(spawnId);
                GameHud.Toast(saved ? "Rested and saved" : "Rested (free play: nothing is saved)");
            });
        }
    }
}
