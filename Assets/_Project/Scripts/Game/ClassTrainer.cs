using System.Collections.Generic;
using ProjectHymn.Progression;
using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The village mentor (GDD update 18; was the armsmaster): explains ranks and how classes are earned, tells you how
    /// close your play is to the Warden or the Crusader, and keeps the weapon rack (changing reloads the scene here).
    /// </summary>
    public class ClassTrainer : Interactable
    {
        public string speaker = "Mentor";
        public string greeting = "I watch how you fight. Ask, and I'll tell you what I see.";
        public string spawnId = "Trainer";

        public override void Interact(PlayerInteractor who) =>
            DialogueBox.Show(speaker, new[] { greeting }, Ask, transform);

        void Ask()
        {
            var options = new List<ClassMenu.Option>
            {
                new ClassMenu.Option("progress", "How am I doing?", "Your class progress, from your clean clears."),
                new ClassMenu.Option("ranks", "How are ranks earned?", "S, A, B and C."),
                new ClassMenu.Option("classes", "How do classes work?", "Levels, level-downs and the classes."),
                new ClassMenu.Option("rack", "The weapon rack", "Equip a weapon you own."),
            };
            ClassMenu.Open(speaker, "", options, "progress", id =>
            {
                switch (id)
                {
                    case "progress": DialogueBox.Show(speaker, Progress(), Ask, transform); break;
                    case "ranks": DialogueBox.Show(speaker, Ranks, Ask, transform); break;
                    case "classes": DialogueBox.Show(speaker, Classes(), Ask, transform); break;
                    default: Rack(); break;
                }
            }, () => { });
        }

        static readonly string[] Ranks =
        {
            "Every stage has a par time. Beat every enemy, take few hits, play on the beat, and come in under par for an S.",
            "Miss an enemy and the best you'll see is a B. Take too many hits and it's a B too. On the cliffs, one hit rules out the S.",
            "A C is a fail: back to the start. But clear it clean, every enemy down and never below half your hearts, and you've a B at least.",
        };

        string[] Classes()
        {
            var rules = GameSession.Instance.Db != null ? GameSession.Instance.Db.Rules : ProgressionRules.Default;
            return new[]
            {
                $"Clears earn experience, up to level {rules.LevelCap}. Dying, or losing half your hearts in a stage, costs a level, once a run.",
                $"Below level {rules.MinstrelBelow} you're a Minstrel. From level {rules.ClassChangeFrom}, each level-up lets you change to any class you've unlocked.",
                "Parry and block most of what comes at you, on the beat, and you'll be a Warden: light blades and shields.",
                "Dodge most of it instead, on the beat, and you'll be a Crusader: heavy blades, no shield.",
                $"Pass level {rules.MinstrelBelow} with neither and you'll be a Fighter. No shame in it.",
            };
        }

        string[] Progress()
        {
            var s = GameSession.Instance;
            var d = s.Data;
            var rules = s.Db != null ? s.Db.Rules : ProgressionRules.Default;
            var t = d.totals;
            var lines = new List<string>();
            if (t.cleanClears == 0) { lines.Add("I've not seen you clear a stage clean yet. Every enemy down, never below half your hearts. Then we'll talk."); return lines.ToArray(); }
            lines.Add($"{t.cleanClears} clean clear{(t.cleanClears == 1 ? "" : "s")}. Of {t.EnemyAttacks} attacks, you guarded {t.GuardShare:P0} and dodged {t.DodgeShare:P0}. {t.OnBeatShare:P0} of your hits were on the beat.");
            if (t.EnemyAttacks < rules.UnlockMinAttacks) lines.Add("Too few attacks to judge. Let them come at you.");
            else if (t.OnBeatShare < rules.UnlockOnBeat) lines.Add($"Your timing is the problem. I need {rules.UnlockOnBeat:P0} of your hits on the beat before any class will take you.");
            foreach (var (id, share, verb) in new[] { (ProgressionRules.Warden, t.GuardShare, "Guard"), (ProgressionRules.Crusader, t.DodgeShare, "Dodge") })
            {
                if (d.HasUnlockedClass(id)) lines.Add($"The {s.ClassName(id)} is yours to take{(d.level >= rules.ClassChangeFrom ? " at your next level-up" : $" once you reach level {rules.ClassChangeFrom}")}.");
                else if (share >= rules.UnlockShare * 0.6) lines.Add($"You're close to the {s.ClassName(id)}. {verb} a bit more.");
            }
            return lines.ToArray();
        }

        void Rack()
        {
            var s = GameSession.Instance;
            var options = ClassMenu.WeaponOptions(out string select);
            string cls = s.Class != null ? s.Class.displayName : "?";
            ClassMenu.Open("Weapon rack", $"{cls}, level {s.Data.level}", options, select, id =>
            {
                if (id == s.Data.weaponId) return;
                if (!s.Data.HasWeapon(id)) { DialogueBox.Show(speaker, new[] { "Not yours yet. The shop sells what you've earned." }, null); return; }
                if (!s.SetWeapon(id)) { DialogueBox.Show(speaker, new[] { $"That blade isn't for a {cls}." }, null); return; }
                s.Travel(s.SceneName, spawnId, saveOnArrival: true);
            }, Ask);
        }
    }
}
