namespace ProjectHymn.Gameplay
{
    /// <summary>A way to another scene (the path to Cliffside).</summary>
    public class SceneDoor : Interactable
    {
        public string scene = "Cliffside";
        public string spawn = "Start";
        public bool saveOnArrival;

        public override void Interact(PlayerInteractor who) => GameSession.Instance.Travel(scene, spawn, saveOnArrival);
    }
}
