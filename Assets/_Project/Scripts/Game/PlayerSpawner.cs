using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Every gameplay scene holds one player per class, built by the scene builder. Before anything else wakes,
    /// this keeps the session's class, switches the others off, applies the class's hearts, damage and guard,
    /// and puts the player at the spawn point the session asked for.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PlayerSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class ClassPlayer
        {
            public string classId;
            public GameObject player;
        }

        [SerializeField] List<ClassPlayer> players = new List<ClassPlayer>();
        [SerializeField] string defaultSpawn = "Start";

        public GameObject Active { get; private set; }

        void Awake()
        {
            var session = GameSession.Instance;
            string cls = session.Data.classId;
            ClassPlayer chosen = players.Find(p => p.player != null && p.classId == cls) ?? players.Find(p => p.player != null);
            if (chosen == null) return;
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

            var def = session.Db != null ? session.Db.Class(chosen.classId) : null;
            if (def != null)
            {
                var health = Active.GetComponent<PlayerHealth>();
                if (health != null) health.SetMaxHearts(def.hearts);
                var guard = Active.GetComponent<PlayerGuard>();
                if (guard != null) guard.GoodParries = def.goodParries;
                var combat = Active.GetComponent<SwordCombatController>();
                if (combat != null) combat.DamageMultiplier = def.damage;
            }
            session.PendingSpawn = null;
        }
    }
}
