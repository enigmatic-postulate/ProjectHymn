using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The village gate: opens the road out of Saltmere (VS-5 Part B, <see cref="StageMap"/>). The stops and what opens them
    /// are in Progression/StageRoute.cs: the story path Practice Yard, Cliffside, Belfry Ridge, the Summit, and the side roads
    /// (the Arena after Cliffside, Starlit Spring after Belfry Ridge). Free play opens everything.
    /// </summary>
    public class StageGate : Interactable
    {
        [UnityEngine.Tooltip("The region whose map this gate opens (0 = Saltmere, 1 = the Drowned Nave, from the Air Bell).")]
        public int region;

        public override void Interact(PlayerInteractor who) => StageMap.Open(region);
    }
}
