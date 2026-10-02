using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Phrase trail (GDD HUD): the last four actions as symbols, coloured by timing, flashing gold when they
    /// complete a Phrase. / Strike   X Smash   &gt; Step   O Guard.
    /// </summary>
    public class PhraseTrail : MonoBehaviour
    {
        [SerializeField] BeatCombatController combat;
        [SerializeField] Text[] slots = new Text[0];

        readonly System.Collections.Generic.List<(string sym, Color col)> _trail = new System.Collections.Generic.List<(string, Color)>();
        float _flash;

        void Awake() { if (combat == null) combat = FindAnyObjectByType<BeatCombatController>(); }

        void OnEnable()
        {
            if (combat == null) return;
            combat.ActionJudged += OnAction;
            combat.PhraseTriggered += OnPhrase;
        }

        void OnDisable()
        {
            if (combat == null) return;
            combat.ActionJudged -= OnAction;
            combat.PhraseTriggered -= OnPhrase;
        }

        void OnAction(BeatActionResult r)
        {
            string sym = r.Action switch
            {
                ProjectHymn.Combat.BeatAction.Strike => "/",
                ProjectHymn.Combat.BeatAction.Smash => "X",
                ProjectHymn.Combat.BeatAction.Step => ">",
                ProjectHymn.Combat.BeatAction.Guard => "O",
                _ => "·",
            };
            Color c = r.Effective switch
            {
                Judgement.Perfect => UIFactory.Gold,
                Judgement.Great => Color.white,
                Judgement.Good => new Color(0.55f, 0.85f, 1f),
                _ => new Color(0.55f, 0.55f, 0.6f),
            };
            _trail.Add((sym, c));
            while (_trail.Count > slots.Length) _trail.RemoveAt(0);
            Refresh();
        }

        void OnPhrase(ProjectHymn.Combat.PhrasePattern p) => _flash = 0.45f;

        void Update()
        {
            if (_flash <= 0f) return;
            _flash -= Time.unscaledDeltaTime;
            Refresh();
        }

        void Refresh()
        {
            int offset = slots.Length - _trail.Count;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                int k = i - offset;
                if (k < 0) { slots[i].text = ""; continue; }
                slots[i].text = _trail[k].sym;
                slots[i].color = _flash > 0f ? UIFactory.Gold : _trail[k].col;
            }
        }
    }
}
