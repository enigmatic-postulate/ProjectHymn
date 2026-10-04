using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Something the player can use by pressing up next to it (talk, shop, rest, travel).</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [Tooltip("Shown above the player as \"▲ <prompt>\".")]
        public string prompt = "Talk";
        public float range = 1.8f;

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() => All.Remove(this);

        public abstract void Interact(PlayerInteractor who);

        /// <summary>Where the prompt is measured from.</summary>
        public virtual Vector3 Anchor => transform.position;
    }
}
