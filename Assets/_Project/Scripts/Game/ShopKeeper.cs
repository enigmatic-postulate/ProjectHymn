using System;
using System.Collections.Generic;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>The shop counter.</summary>
    public class ShopKeeper : Interactable
    {
        public string speaker = "Merchant";
        [TextArea(2, 4)] public string greeting = "Potions, tonics and old songs. What'll it be?";

        public override void Interact(PlayerInteractor who) =>
            DialogueBox.Show(speaker, new[] { greeting }, () => ShopMenu.Open(speaker));
    }
}
