using System;
using System.Collections.Generic;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>On the player: finds the nearest interactable in reach, shows its prompt, uses it on up.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        PlayerInputReader _input;
        PlatformerController _body;
        Interactable _near;

        public Interactable Near => _near;

        void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _body = GetComponent<PlatformerController>();
        }

        /// <summary>Someone to talk to (or a door) is in reach: E talks instead of using the quick item.</summary>
        public bool HasTarget => _near != null;

        void Update()
        {
            _near = null;
            if (UiState.Blocking || (_body != null && !_body.Grounded)) { GameHud.Prompt(null); return; }
            float best = float.MaxValue;
            var p = transform.position;
            foreach (var i in Interactable.All)
            {
                var a = i.Anchor;
                float dx = Mathf.Abs(a.x - p.x), dy = Mathf.Abs(a.y - p.y);
                if (dx > i.range || dy > 2f) continue;
                if (dx < best) { best = dx; _near = i; }
            }
            GameHud.Prompt(_near != null ? _near.prompt : null);
            // E (gamepad B) talks when someone is near; otherwise it uses the quick item. W / Up works too.
            if (_near != null && _input != null && (_input.InteractPressed || _input.UseItemPressed) && !UiState.JustClosed) _near.Interact(this);
        }
    }
}
