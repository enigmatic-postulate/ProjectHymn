using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Lute Bow shot: flies straight, hits the first damageable thing it touches, then vanishes.</summary>
    public class Projectile : MonoBehaviour
    {
        Vector2 _velocity;
        HitData _hit;
        GameObject _owner;
        LayerMask _mask;
        float _life = 1.2f;
        const float Radius = 0.25f;
        readonly List<Collider2D> _hits = new List<Collider2D>();

        public void Launch(Vector2 velocity, HitData hit, GameObject owner, LayerMask mask)
        {
            _velocity = velocity; _hit = hit; _owner = owner; _mask = mask;
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }
            transform.position += (Vector3)(_velocity * Time.deltaTime);

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _mask, useTriggers = true };
            int n = Physics2D.OverlapCircle(transform.position, Radius, filter, _hits);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == _owner) continue;
                if (c.TryGetComponent<IBeatDamageable>(out var target))
                {
                    target.TakeHit(_hit);
                    Destroy(gameObject);
                    return;
                }
                if (!c.isTrigger) { Destroy(gameObject); return; } // walls
            }
        }
    }
}
