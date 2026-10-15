using System;
using System.Collections.Generic;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>The weapon shop counter.</summary>
    public class ShopKeeper : Interactable
    {
        public string speaker = "Merchant";
        [TextArea(2, 4)] public string greeting = "Blades, for those who've earned the use of them. Have a look.";

        public override void Interact(PlayerInteractor who) =>
            DialogueBox.Show(speaker, new[] { greeting }, () => ShopMenu.Open(speaker), transform);
    }
}
