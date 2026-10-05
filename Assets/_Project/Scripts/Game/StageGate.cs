using System.Collections.Generic;
using ProjectHymn.Progression;
using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The village gate (VS-2): pick a stage, Mega Man style. Each stage shows its time signature and your best rank; a
    /// stage opens once the one before it is cleared (Belfry Ridge after Cliffside). Free play opens everything.
    /// </summary>
    public class StageGate : Interactable
    {
        [System.Serializable]
        public class Destination
        {
            public string stageId = "Cliffside";
            public string scene = "Cliffside";
            public string label = "Cliffside";
            public string meter = "4/4";
            [UnityEngine.Tooltip("Stage that must be cleared first (empty = always open).")]
            public string requires = "";
        }

        public List<Destination> stages = new List<Destination>();
        public string speaker = "Gatewarden";

        bool Open(Destination d)
        {
            var s = GameSession.Instance;
            return s.FreePlay || string.IsNullOrEmpty(d.requires) || s.Data.HasCleared(d.requires);
        }

        public override void Interact(PlayerInteractor who)
        {
            var s = GameSession.Instance;
            var options = new List<ClassMenu.Option>();
            string select = null;
            foreach (var d in stages)
            {
                bool open = Open(d);
                string best = s.Data.TryBestRank(d.stageId, out StageRank r) ? $"best {r}" : "not cleared";
                string label = open ? $"{d.label}   <color=#8a84a0>{d.meter} · {best}</color>" : $"<color=#6a6478>{d.label}   (locked)</color>";
                var rec = s.Data.Record(d.stageId);
                string info = open
                    ? $"Time signature {d.meter}.\n{(rec != null ? $"Best {rec.bestRank} in {(int)(rec.bestSeconds / 60.0)}:{rec.bestSeconds % 60.0:00.0}, cleared {rec.clears}×." : "Not cleared yet.")}"
                    : $"Clear {d.requires} to open the road.";
                options.Add(new ClassMenu.Option(d.stageId, label, info));
                if (open && (select == null || !s.Data.HasCleared(d.stageId))) select = d.stageId;
            }
            ClassMenu.Open("Where to?", "Each road is fought in its own time.", options, select, id =>
            {
                var d = stages.Find(x => x.stageId == id);
                if (d == null) return;
                if (!Open(d)) { DialogueBox.Show(speaker, new[] { $"The road to {d.label} is closed. Clear {d.requires} first." }, null); return; }
                s.Travel(d.scene, "Start");
            }, () => { });
        }
    }
}
