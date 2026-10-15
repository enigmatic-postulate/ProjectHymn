using System.Text;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Esc / Start: pauses the music on its clock and the world, with Resume, Character (class, level, class progress, stage
    /// ranks), Songbook (the class trees of the sequence library, and the lore you found), Weapon (equip an owned weapon your class can hold), Settings, Return to the village and Quit to the title.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] bool allowReturnToVillage = true;

        PlayerInputReader _input;
        Conductor _conductor;
        GameObject _root;
        RectTransform _body;
        bool _open;
        float _openedAt;

        void Start()
        {
            _input = FindAnyObjectByType<PlayerInputReader>();
            _conductor = FindAnyObjectByType<Conductor>();
        }

        void Update()
        {
            if (_input == null) _input = FindAnyObjectByType<PlayerInputReader>();
            bool toggle = _input != null && _input.PausePressed;
            if (!_open)
            {
                if (toggle && !UiState.Blocking && !UiState.JustClosed && !GameSession.Instance.Travelling) Open();
                return;
            }
            if (Time.unscaledTime - _openedAt > 0.1f && UiInput.Cancel) Main();
        }

        void Open()
        {
            _open = true;
            _onMain = false;
            _openedAt = Time.unscaledTime;
            UiState.Push();
            Time.timeScale = 0f;
            if (_conductor != null) _conductor.Pause();
            var c = UiKit.Canvas("Pause Canvas", 300, transform);
            _root = c.gameObject;
            var panel = UiKit.Panel("Pause", c.transform, new Vector2(1000f, 820f));
            _body = panel;
            Main();
        }

        void Close()
        {
            if (!_open) return;
            _open = false;
            Destroy(_root);
            Time.timeScale = 1f;
            if (_conductor != null) _conductor.Resume();
            UiState.Pop();
        }

        RectTransform Page()
        {
            UiKit.Clear(_body);
            return UiKit.Column(_body, 10f, 34);
        }

        bool _onMain;

        void Main()
        {
            if (!_open) return;
            if (_onMain && Time.unscaledTime - _openedAt > 0.1f) { Close(); return; }     // Cancel on the main page resumes
            _onMain = true;
            var col = Page();
            UiKit.Line(col, "PAUSED", 46, UiKit.Gold, 70f);
            var resume = UiKit.Button(col, "Resume", Close);
            UiKit.Button(col, "Character", Character);
            UiKit.Button(col, "Songbook", Songbook);
            UiKit.Button(col, "Weapon", Weapons);
            UiKit.Button(col, "Settings", Settings);
            var s = GameSession.Instance;
            bool inHub = s.Db != null && s.SceneName == s.Db.hubScene;
            if (allowReturnToVillage && !inHub && s.Db != null)
                UiKit.Button(col, "Return to the village", () => { Close(); s.Travel(s.Db.hubScene, "FromCliffside", saveOnArrival: true); });
            UiKit.Button(col, "Quit to title", () => { Close(); s.ToTitle(); });
            UiKit.Line(col, s.FreePlay ? "Free play (started from the editor): nothing is saved." : "Rest at the inn to save. Returning to the village saves too.", 22, UiKit.Dim, 40f);
            UiKit.Select(resume);
        }

        void Character()
        {
            _onMain = false;
            var col = Page();
            UiKit.Line(col, "CHARACTER", 40, UiKit.Gold, 60f);
            var s = GameSession.Instance;
            var d = s.Data;
            var rules = s.Db != null ? s.Db.Rules : Progression.ProgressionRules.Default;
            var cls = s.Class;
            var sb = new StringBuilder();
            sb.Append($"<size=34><color=#ffd56a>{(cls != null ? cls.displayName : d.classId)}</color></size>   level {d.level}");
            if (d.level >= rules.LevelCap) sb.Append("   <color=#8a84a0>(the cap)</color>\n");
            else sb.Append($"   <color=#8a84a0>{d.xp} / {rules.XpToNext(d.level)} experience</color>\n");
            if (cls != null) sb.Append($"{cls.description}\n<color=#b8b2c8>Perks: {cls.PerksText()}</color>\n");
            sb.Append($"Weapon  <color=#ffd56a>{(s.Weapon != null ? s.Weapon.displayName : d.weaponId)}</color>      Notes  {d.notes}\n\n");

            // Classes: unlocked, and progress toward the two the play style opens (counted over clean clears).
            var names = new System.Collections.Generic.List<string>();
            foreach (var id in d.unlockedClasses) names.Add(s.ClassName(id));
            sb.Append($"Classes unlocked: <color=#ffd56a>{string.Join(", ", names)}</color>\n");
            var t = d.totals;
            sb.Append($"<color=#b8b2c8>Over {t.cleanClears} clean clear{(t.cleanClears == 1 ? "" : "s")}: on the beat {t.OnBeatShare:P0} (needs {rules.UnlockOnBeat:P0}), " +
                      $"guarded {t.GuardShare:P0}, dodged {t.DodgeShare:P0} of {t.EnemyAttacks} enemy attacks (a class needs {rules.UnlockShare:P0} of at least {rules.UnlockMinAttacks})</color>\n");
            if (!d.HasUnlockedClass(Progression.ProgressionRules.Warden)) sb.Append("<color=#8a84a0>Warden: parry and block most attacks, on the beat.</color>\n");
            if (!d.HasUnlockedClass(Progression.ProgressionRules.Crusader)) sb.Append("<color=#8a84a0>Crusader: dodge most attacks, on the beat.</color>\n");
            sb.Append($"<color=#8a84a0>Class changes are offered at each level-up from level {rules.ClassChangeFrom}. Dying or losing half your hearts costs a level; below level {rules.MinstrelBelow} you are a Minstrel.</color>\n\n");

            sb.Append("Stages\n");
            if (d.stageRecords.Count == 0) sb.Append("<color=#8a84a0>None cleared yet.</color>\n");
            foreach (var r in d.stageRecords)
                sb.Append($"{r.stage}   best <color=#ffd56a>{r.bestRank}</color> in {(int)(r.bestSeconds / 60.0)}:{r.bestSeconds % 60.0:00.0}   cleared {r.clears}x\n");
            if (d.history.Count > 0)
            {
                sb.Append("<color=#8a84a0>Recent: ");
                int from = Mathf.Max(0, d.history.Count - 8);
                for (int i = d.history.Count - 1; i >= from; i--) sb.Append($"{d.history[i].stage} {d.history[i].rank}{(i > from ? ", " : "")}");
                sb.Append("</color>\n");
            }
            sb.Append("\n<color=#6a6478>Playtest keys: F8 / Shift+F8 next / previous class, F10 experience to the next level, F11 level up, Shift+F11 level down, F12 force the next rank, Shift+F12 finish the stage, F7 test checkpoints, F9 open the playtest logs, Shift+F9 ask how stages felt on / off</color>");
            UiKit.Line(col, sb.ToString(), 22, UiKit.Text, 560f, TextAnchor.UpperLeft);
            var back = UiKit.Button(col, "Back", Main);
            UiKit.Button(col, "Debug: switch class (playtest)", DebugClass, 22, 44f);
            UiKit.Select(back);
        }

        /// <summary>Playtest: any class at once, without earning it (the scene restarts so it takes effect).</summary>
        void DebugClass()
        {
            _onMain = false;
            var col = Page();
            var s = GameSession.Instance;
            UiKit.Line(col, "DEBUG: SWITCH CLASS", 36, UiKit.Gold, 56f);
            UiKit.Line(col, "Playtest only. Unlocks the class (kept in a real save), teaches its tree, gives it a weapon it can hold, saves, and restarts the scene. F8 / Shift+F8 does the same in play.", 22, UiKit.Dim, 80f);
            Button first = null;
            if (s.Db != null)
                foreach (var c in s.Db.classes)
                {
                    if (c == null) continue;
                    string id = c.id;
                    bool current = id == s.Data.classId;
                    var b = UiKit.Button(col, current ? $"{c.displayName}   <color=#8a84a0>(current)</color>" : c.displayName, () =>
                    {
                        if (id == s.Data.classId) return;
                        Close();
                        s.DebugSetClass(id);
                    });
                    if (first == null || current) first = b;
                }
            var back = UiKit.Button(col, "Back", Character);
            UiKit.Select(first != null ? first : back);
        }

        /// <summary>The Songbook, by class tree (GDD update 18): each tree's sequences, their home meters and what's learned.</summary>
        void Songbook()
        {
            _onMain = false;
            var col = Page();
            UiKit.Line(col, "SONGBOOK", 40, UiKit.Gold, 60f);
            UiKit.Line(col, "Taking a class teaches its tree. Once learned, any class can play a sequence, but only in its home meter.", 22, UiKit.Dim, 56f);
            Button first = null;
            foreach (var tree in SequenceLibrary.Trees)
            {
                var entries = SequenceLibrary.Tree(tree);
                if (entries.Count == 0) continue;
                int known = 0;
                foreach (var e in entries) if (Sequences.Knows(e.Key)) known++;
                string name = GameSession.Instance.ClassName(tree);
                if (name == tree) name = char.ToUpperInvariant(tree[0]) + tree.Substring(1);
                string t = tree;
                var b = UiKit.Button(col, $"{name}   <color=#8a84a0>{known}/{entries.Count} learned</color>", () => SongbookTree(t), 28, 54f);
                if (first == null) first = b;
            }
            UiKit.Button(col, $"Lore  <color=#8a84a0>({GameSession.Instance.Data.lore.Count} found)</color>", Lore, 28, 54f);
            var back = UiKit.Button(col, "Back", Main, 28, 54f);
            UiKit.Select(first != null ? first : back);
        }

        void SongbookTree(string tree)
        {
            _onMain = false;
            var col = Page();
            var s = GameSession.Instance;
            string name = s.ClassName(tree);
            if (name == tree) name = char.ToUpperInvariant(tree[0]) + tree.Substring(1);
            UiKit.Line(col, $"{name.ToUpperInvariant()}'S TREE", 36, UiKit.Gold, 56f);
            var weapon = s.Weapon;
            var sb = new StringBuilder();
            sb.Append($"<color=#8a84a0>Moves as played with the {(weapon != null ? weapon.displayName : "weapon")}. 1 = light (J / X), 2 = heavy (K / Y), on consecutive beats.</color>\n\n");
            if (tree == SequenceLibrary.Balladeer) sb.Append("<color=#8a84a0>The Balladeer comes later in the game.</color>\n\n");
            foreach (var e in SequenceLibrary.Tree(tree))
            {
                bool known = Sequences.Knows(e.Key);
                string meter = e.Meter == SequenceLibrary.Common ? "4/4" : $"<color=#9ab8ff>{e.Meter}</color>";
                if (!known)
                {
                    sb.Append($"<color=#6a6478>{e.Name}   {e.NotesText}   {e.Meter}   not learned</color>\n\n");
                    continue;
                }
                sb.Append($"<color=#ffd56a>{e.Name}</color>   <b>{e.NotesText}</b>   {meter}   finisher x{e.Finisher:0.0#}");
                if (!string.IsNullOrEmpty(e.Note)) sb.Append($"   <color=#8ad8ff>{e.Note}</color>");
                sb.Append('\n');
                SwordSequence q = null;
                if (weapon != null) foreach (var w in weapon.sequences) if (w != null && w.key == e.Key) { q = w; break; }
                if (q != null)
                {
                    var moves = new StringBuilder();
                    foreach (var m in q.moves) { if (moves.Length > 0) moves.Append(", "); moves.Append(m != null ? m.displayName : "?"); }
                    sb.Append($"<color=#b8b2c8>{moves}</color>");
                }
                sb.Append("\n\n");
            }
            if (s.Class != null && s.Class.commonTimeOnly) sb.Append($"<color=#ffb070>As a {s.Class.displayName} you play sequences in 4/4 only.</color>");
            UiKit.Line(col, sb.ToString(), 24, UiKit.Text, 520f, TextAnchor.UpperLeft);
            UiKit.Select(UiKit.Button(col, "Back", Songbook));
        }

        void Lore()
        {
            _onMain = false;
            var col = Page();
            var s = GameSession.Instance;
            UiKit.Line(col, $"LORE  ({s.Data.lore.Count} found)", 30, UiKit.Gold, 46f);
            Button first = null;
            if (s.Db != null)
                foreach (var id in s.Data.lore)
                {
                    var item = s.Db.Item(id);
                    if (item == null) continue;
                    var b = UiKit.Button(col, item.displayName, () => DialogueBox.Show(item.displayName, new[] { item.loreText }, null), 26, 48f);
                    if (first == null) first = b;
                }
            var back = UiKit.Button(col, "Back", Songbook);
            UiKit.Select(first != null ? first : back);
        }

        void Weapons()
        {
            _onMain = false;
            var col = Page();
            UiKit.Line(col, "WEAPON", 40, UiKit.Gold, 60f);
            var s = GameSession.Instance;
            var cls = s.Class;
            UiKit.Line(col, $"The {(cls != null ? cls.displayName : "class")} can hold the weapons below. The change takes effect when you next enter a stage.", 22, UiKit.Dim, 60f);
            Button first = null;
            if (s.Db != null)
                foreach (var w in s.Db.weapons)
                {
                    if (w == null) continue;
                    bool owned = s.Data.HasWeapon(w.id);
                    bool allowed = cls == null || cls.CanEquip(w.id);
                    string kind = w.weight == WeaponWeight.Heavy ? "heavy" : "light";
                    string label;
                    if (w.id == s.Data.weaponId) label = $"{w.displayName}   <color=#ffd56a>equipped</color>";
                    else if (!owned) label = $"<color=#6a6478>{w.displayName}   ({(s.Data.weaponsForSale.Contains(w.id) ? "in the shop" : "locked")})</color>";
                    else if (!allowed) label = $"<color=#6a6478>{w.displayName}   (your class can't hold a {kind} weapon)</color>";
                    else label = w.displayName;
                    string id = w.id;
                    var b = UiKit.Button(col, label, () =>
                    {
                        if (s.SetWeapon(id)) GameHud.Toast($"Equipped: {w.displayName} (from the next stage)");
                        Weapons();
                    }, 26, 52f);
                    if (first == null) first = b;
                }
            var back = UiKit.Button(col, "Back", Main);
            UiKit.Select(first != null ? first : back);
        }

        void Settings()
        {
            _onMain = false;
            var col = Page();
            var first = SettingsPanel.Build(col, Main, () =>
            {
                Close();
                var cal = FindAnyObjectByType<CalibrationPanel>();
                if (cal != null) cal.Begin();
                else GameHud.Toast("Calibrate in a stage (it needs the music)");
            });
            UiKit.Select(first);
        }

        void OnDestroy() { if (_open) { Time.timeScale = 1f; UiState.Pop(); } }
    }
}
