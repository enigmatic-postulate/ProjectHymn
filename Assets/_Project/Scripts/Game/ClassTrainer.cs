using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The village armsmaster (Rank &amp; Class): classes are earned in the field, so here you only change weapons, among
    /// the unlocked ones your class can equip. Changing reloads the scene at the trainer.
    /// </summary>
    public class ClassTrainer : Interactable
    {
        public string speaker = "Armsmaster";
        public string greeting = "Your class you earn out there. Your blade you choose in here.";
        public string spawnId = "Trainer";

        public override void Interact(PlayerInteractor who) =>
            DialogueBox.Show(speaker, new[] { greeting }, Rack);

        void Rack()
        {
            var s = GameSession.Instance;
            var options = ClassMenu.WeaponOptions(out string select);
            string cls = s.Class != null ? s.Class.displayName : "?";
            ClassMenu.Open("Weapon rack", $"{cls}, level {s.Data.level}", options, select, id =>
            {
                if (id == s.Data.weaponId) return;
                if (!s.Data.HasWeapon(id)) { DialogueBox.Show(speaker, new[] { "Not yet. Earn a B on the cliffs and I'll hand you the greatsword." }, null); return; }
                if (!s.SetWeapon(id)) { DialogueBox.Show(speaker, new[] { $"That blade isn't for a {cls}." }, null); return; }
                s.Travel(s.SceneName, spawnId, saveOnArrival: true);
            }, () => { });
        }
    }
}
