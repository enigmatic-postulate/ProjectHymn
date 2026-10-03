using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Lute Bow shot: flies flat along the ground plane, hits the first damageable thing it touches, then vanishes.</summary>
    public class Projectile : MonoBehaviour
    {
        Vector2 _velocity; // ground plane
        HitData _hit;
        GameObject _owner;
        LayerMask _mask;
        float _life = 1.2f;
        const float Radius = 0.3f;
        readonly Collider[] _hits = new Collider[8];

        public void Launch(Vector2 velocity, HitData hit, GameObject owner, LayerMask mask)
        {
            _velocity = velocity; _hit = hit; _owner = owner; _mask = mask;
            transform.rotation = Ground.Facing(velocity);
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }
            transform.position += Ground.Dir(_velocity * Time.deltaTime);

            int n = Physics.OverlapSphereNonAlloc(transform.position, Radius, _hits, _mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (_owner != null && c.transform.IsChildOf(_owner.transform)) continue;
                if (c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IBeatDamageable>();
                if (target != null)
                {
                    target.TakeHit(_hit);
                    Destroy(gameObject);
                    return;
                }
                if (!c.isTrigger) { Destroy(gameObject); return; } // walls and buildings
            }
        }
    }
}
