using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Every gameplay scene holds one player per weapon (each weapon has its own model and moveset), built by the scene
    /// builder. Before anything else wakes, this keeps the session's weapon, switches the others off, applies the
    /// class's hearts, damage and perks, and puts the player at the spawn point the session asked for.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PlayerSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponPlayer
        {
            public string weaponId;
            public GameObject player;
        }

        [SerializeField] List<WeaponPlayer> players = new List<WeaponPlayer>();
        [SerializeField] string defaultSpawn = "Start";

        public GameObject Active { get; private set; }

        void Awake()
        {
            var session = GameSession.Instance;
            string weapon = session.Data.weaponId;
            WeaponPlayer chosen = players.Find(p => p.player != null && p.weaponId == weapon) ?? players.Find(p => p.player != null);
            if (chosen == null)
            {
                Debug.LogError("[Project Hymn] This scene has no player for any weapon. Rebuild it (Project Hymn ▸ Build Hub & Loop).");
                return;
            }
            foreach (var p in players) if (p.player != null && p != chosen) p.player.SetActive(false);

            // Place the player before it wakes (an inactive class player wakes on SetActive), so its first respawn
            // point is the spawn, then switch it on.
            string spawnId = string.IsNullOrEmpty(session.PendingSpawn) ? defaultSpawn : session.PendingSpawn;
            var spawn = SpawnPoint.Find(spawnId);
            Active = chosen.player;
            if (spawn != null) Active.transform.position = spawn.transform.position + Vector3.up * 0.05f;
            Active.SetActive(true);
            var body = Active.GetComponent<PlatformerController>();
            if (body != null) body.SetRespawn(Active.transform.position);
            Physics.SyncTransforms();

            var def = session.Class;
            if (def != null)
            {
                var health = Active.GetComponent<PlayerHealth>();
                if (health != null) health.SetMaxHearts(def.hearts);
                var combat = Active.GetComponent<SwordCombatController>();
                if (combat != null) { combat.DamageMultiplier = def.damage; combat.CommonTimeOnly = def.commonTimeOnly; }
                var guard = Active.GetComponent<PlayerGuard>();
                if (guard != null) guard.ApplyClass(def);
            }
            session.PendingSpawn = null;
            var hud = ProjectHymn.UI.GameHud.Instance;      // hearts, Notes and the quick item from the first frame
        }
    }
}
