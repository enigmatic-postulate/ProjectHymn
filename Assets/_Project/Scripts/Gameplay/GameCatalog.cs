using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Everything the shop and loadout know about: all Phrases and items. One asset, referenced by scenes.</summary>
    [CreateAssetMenu(menuName = "Project Hymn/Game Catalog", fileName = "GameCatalog")]
    public class GameCatalog : ScriptableObject
    {
        public List<PhraseData> phrases = new List<PhraseData>();
        public List<ItemDefinition> items = new List<ItemDefinition>();

        public PhraseData FindPhrase(string id) => phrases.Find(p => p != null && p.Id == id);
        public ItemDefinition FindItem(string id) => items.Find(i => i != null && i.Id == id);
    }
}
