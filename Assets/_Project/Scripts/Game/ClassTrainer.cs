using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>The class trainer: switch between Knight and Barbarian (reloads the scene at the trainer).</summary>
    public class ClassTrainer : Interactable
    {
        public string speaker = "Trainer";
        [TextArea(2, 4)] public string greeting = "Sword and shield, or the greatsword? Choose, and I'll see you trained.";
        public string spawnId = "Trainer";

        public override void Interact(PlayerInteractor who) =>
            DialogueBox.Show(speaker, new[] { greeting }, () => ClassMenu.Open("Choose your class", GameSession.Instance.Data.classId, id =>
            {
                var s = GameSession.Instance;
                if (id == s.Data.classId) return;
                s.SetClass(id);
                s.Travel(s.SceneName, spawnId, saveOnArrival: true);
            }));
    }
}
