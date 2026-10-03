using System.Collections.Generic;
using System.Text;
using ProjectHymn.Combat;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Runs the town: shows "[E] Shop" style prompts near stations, opens the shop, loadout, arena gate and lore
    /// panels, keeps the Notes counter up to date, and freezes the player while a panel is open.
    /// </summary>
    public class TownController : MonoBehaviour
    {
        [SerializeField] GameCatalog catalog;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] Text promptText;
        [SerializeField] Text notesText;

        readonly List<TownInteractable> _stations = new List<TownInteractable>();
        TownInteractable _near;
        Canvas _canvas;

        public static bool PanelOpen { get; private set; }

        void Start()
        {
            Time.timeScale = 1f;
            PanelOpen = false;
            _stations.AddRange(FindObjectsByType<TownInteractable>(FindObjectsSortMode.None));
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>();
            if (motor == null && input != null) motor = input.GetComponent<PlayerMotor>();
            RefreshHiddenItems();
            GameSettings.Apply();
        }

        void Update()
        {
            if (notesText != null) notesText.text = $"{SaveSystem.Current.notes} Notes";
            if (PanelOpen || PauseMenu.IsOpen || input == null) return;

            _near = null;
            float best = float.MaxValue;
            foreach (var s in _stations)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                float d = Ground.Distance(s.transform.position, input.transform.position);
                if (d <= s.radius && d < best) { best = d; _near = s; }
            }

            if (promptText != null) promptText.text = _near != null ? $"[E]  {_near.title}" : "";
            if (_near != null && input.InteractPressed) Open(_near);
        }

        void RefreshHiddenItems()
        {
            foreach (var s in _stations) if (s != null && s.Hidden) s.gameObject.SetActive(false);
        }

        void Open(TownInteractable s)
        {
            switch (s.station)
            {
                case TownStation.Shop: ShowShop(); break;
                case TownStation.Loadout: ShowLoadout(); break;
                case TownStation.ArenaGate: ShowGate(s); break;
                case TownStation.Lore: ShowLore(s); break;
            }
        }

        RectTransform BeginPanel(float height, float width = 900)
        {
            Close();
            PanelOpen = true;
            if (motor != null) motor.enabled = false;
            _canvas = UIFactory.Overlay("TownPanel");
            UIFactory.Backdrop(_canvas.transform, 0.5f);
            return UIFactory.Panel(_canvas.transform, new Vector2(width, height));
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            PanelOpen = false;
            if (motor != null) motor.enabled = true;
            if (promptText != null) promptText.text = "";
        }

        // ---------- Shop ----------

        void ShowShop()
        {
            var panel = BeginPanel(980);
            var rules = SaveSystem.Rules;
            UIFactory.Label(panel, "THE LUTHIER", 46, color: UIFactory.Gold);
            UIFactory.Label(panel, $"You have {rules.Data.notes} Notes", 28, color: UIFactory.DimText);
            Button first = null;

            if (catalog != null)
            {
                foreach (var p in catalog.phrases)
                {
                    if (p == null || p.price <= 0) continue;
                    bool owned = rules.HasPhrase(p.Id);
                    var b = UIFactory.Button(panel, owned ? $"{p.displayName}   {Pattern(p)}   — learned" : $"{p.displayName}   {Pattern(p)}   — {p.price} Notes",
                        () => { if (rules.BuyPhrase(p.Id, p.price)) { SaveSystem.Save(); ShowShop(); } }, 26, 54);
                    b.interactable = !owned && rules.CanAfford(p.price);
                    if (first == null && b.interactable) first = b;
                }
                foreach (var it in catalog.items)
                {
                    if (it == null || it.price <= 0) continue;
                    bool owned = rules.OwnsItem(it.Id);
                    var b = UIFactory.Button(panel, owned ? $"{it.displayName} ({it.kind}) — owned" : $"{it.displayName} ({it.kind}) — {it.price} Notes   ·   {it.description}",
                        () => { if (rules.BuyItem(it.Id, it.price)) { SaveSystem.Save(); ShowShop(); } }, 24, 54);
                    b.interactable = !owned && rules.CanAfford(it.price);
                    if (first == null && b.interactable) first = b;
                }
            }
            var close = UIFactory.Button(panel, "Leave", Close);
            UIFactory.Focus(first != null ? first : close);
        }

        // ---------- Loadout ----------

        void ShowLoadout()
        {
            var panel = BeginPanel(1000);
            var rules = SaveSystem.Rules;
            UIFactory.Label(panel, "WARDROBE", 46, color: UIFactory.Gold);
            UIFactory.Label(panel, $"Phrases equipped {rules.Data.equippedPhrases.Count}/{SaveData.MaxEquippedPhrases}  ·  click to equip or remove", 24, color: UIFactory.DimText);
            Button first = null;

            if (catalog != null)
            {
                foreach (var id in rules.Data.unlockedPhrases)
                {
                    var p = catalog.FindPhrase(id);
                    if (p == null) continue;
                    bool on = rules.IsEquipped(id);
                    string meter = p.requiredBeatsPerMeasure > 0 ? $"  (only in {p.requiredBeatsPerMeasure}-beat bars)" : "";
                    var b = UIFactory.Button(panel, $"[{(on ? "x" : " ")}]  {p.displayName}   {Pattern(p)}{meter}",
                        () => { if (rules.TogglePhrase(id)) { SaveSystem.Save(); ShowLoadout(); } }, 26, 50);
                    if (first == null) first = b;
                }

                UIFactory.Label(panel, "Gear", 30, color: UIFactory.Gold);
                foreach (var id in rules.Data.ownedItems)
                {
                    var it = catalog.FindItem(id);
                    if (it == null) continue;
                    bool equipped = it.kind == ItemKind.Weapon ? rules.Data.weaponId == id : rules.Data.armorId == id;
                    UIFactory.Button(panel, $"{(equipped ? "> " : "   ")}{it.displayName} ({it.kind})   ·   {it.description}", () =>
                    {
                        bool ok = it.kind == ItemKind.Weapon ? rules.EquipWeapon(id) : rules.EquipArmor(id);
                        if (ok)
                        {
                            SaveSystem.Save();
                            var applier = FindAnyObjectByType<LoadoutApplier>();
                            if (applier != null) applier.Apply();
                            ShowLoadout();
                        }
                    }, 24, 50);
                }
            }
            var close = UIFactory.Button(panel, "Done", Close);
            UIFactory.Focus(first != null ? first : close);
        }

        // ---------- Gates and lore ----------

        void ShowGate(TownInteractable s)
        {
            var panel = BeginPanel(460, 760);
            UIFactory.Label(panel, s.title.ToUpperInvariant(), 42, color: UIFactory.Gold);
            var rec = SaveSystem.Current.arenas.Find(a => a.id == s.arenaId);
            if (rec != null && rec.bestRank >= 0)
                UIFactory.Label(panel, $"Best rank {(Rank)rec.bestRank}   ·   best score {rec.bestScore:N0}", 26, color: UIFactory.DimText);

            if (s.IsLocked(out var why))
            {
                UIFactory.Label(panel, why, 28);
                UIFactory.Focus(UIFactory.Button(panel, "Back", Close));
                return;
            }
            var go = UIFactory.Button(panel, "Enter", () =>
            {
                PanelOpen = false;
                SaveSystem.Save();
                SceneManager.LoadScene(s.sceneName);
            });
            UIFactory.Button(panel, "Not yet", Close);
            UIFactory.Focus(go);
        }

        void ShowLore(TownInteractable s)
        {
            var panel = BeginPanel(520, 820);
            UIFactory.Label(panel, s.title.ToUpperInvariant(), 40, color: UIFactory.Gold);
            UIFactory.Label(panel, s.loreText, 28, TextAnchor.MiddleCenter, height: 200);
            if (SaveSystem.Rules.FindLore(s.loreId, s.loreNotes))
            {
                SaveSystem.Save();
                UIFactory.Label(panel, $"+{s.loreNotes} Notes", 30, color: UIFactory.Gold);
            }
            UIFactory.Focus(UIFactory.Button(panel, "Close", () => { Close(); s.gameObject.SetActive(false); }));
        }

        /// <summary>"/ / X" style pattern string: / Strike, X Smash, &gt; Step, O Guard, ^ Jump, · rest.</summary>
        public static string Pattern(PhraseData p)
        {
            var sb = new StringBuilder();
            foreach (var a in p.pattern)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(a switch
                {
                    BeatAction.Strike => "/",
                    BeatAction.Smash => "X",
                    BeatAction.Step => ">",
                    BeatAction.Guard => "O",
                    BeatAction.Jump => "^",
                    _ => "·",
                });
            }
            return sb.ToString();
        }
    }
}
