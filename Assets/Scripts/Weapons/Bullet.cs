using Mirror;
using PewPewPew.Match;
using PewPewPew.Ships;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.Weapons
{
    /// Flies straight and ignores gravity so clients can simulate it from the launch velocity alone. Hits are resolved on the server.
    public class Bullet : SpaceObject
    {
        [SerializeField] private float m_ImpactDamage = 10f;
        [SerializeField] private float m_Lifetime = 3f;
        [SerializeField, Tooltip("Keeps the object alive briefly after a hit so clients receive the explosion.")] private float m_DespawnDelay = 0.2f;
        [SerializeField] private GameObject m_DamageExplosion;
        [SerializeField] private GameObject m_NoDamageExplosion;
        [SerializeField] private GameObject m_ShieldExplosion;

        [SyncVar] private Vector2 m_LaunchVelocity;

        private Collider2D m_Collider;
        private PlayerState m_Attacker;
        private bool m_Spent;

        protected override void Awake()
        {
            base.Awake();
            m_Collider = GetComponent<Collider2D>();
            m_Collider.isTrigger = true;
        }

        /// Server only, before the bullet is spawned. Bullets never hit the ship that fired them.
        public void Launch(Vector2 velocity, Ship shooter)
        {
            m_LaunchVelocity = velocity;
            m_Attacker = shooter.Owner;
            m_Body.linearVelocity = velocity;
            foreach (Collider2D collider in shooter.GetComponentsInChildren<Collider2D>())
            {
                Physics2D.IgnoreCollision(m_Collider, collider);
            }
        }

        public override void OnStartServer() => Invoke(nameof(Despawn), m_Lifetime);

        public override void OnStartClient()
        {
            if (isServer) return;

            m_Body.bodyType = RigidbodyType2D.Kinematic;
            m_Body.linearVelocity = m_LaunchVelocity;
        }

        protected override void OnPhysicsStep() { }

        [ServerCallback]
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Spent || other.GetComponentInParent<Bullet>() != null) return;

            Vector2 point = other.ClosestPoint(transform.position);
            IDamageable target = other.GetComponentInParent<IDamageable>();
            HitResult result = target != null ? target.TakeDamage(m_ImpactDamage, point, m_Attacker) : HitResult.NoDamage;

            m_Spent = true;
            m_Collider.enabled = false;
            m_Body.linearVelocity = Vector2.zero;
            RpcExplode(point, result);
            CancelInvoke(nameof(Despawn));
            Invoke(nameof(Despawn), m_DespawnDelay);
        }

        [ClientRpc]
        private void RpcExplode(Vector2 point, HitResult result)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;

            GameObject explosion = result switch
            {
                HitResult.Damaged => m_DamageExplosion,
                HitResult.ShieldAbsorbed => m_ShieldExplosion,
                _ => m_NoDamageExplosion,
            };
            if (explosion != null) Instantiate(explosion, point, Quaternion.identity);
        }

        private void Despawn() => NetworkServer.Destroy(gameObject);
    }
}
