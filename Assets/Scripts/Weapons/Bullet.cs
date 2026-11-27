using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Ships;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.Weapons
{
    /// Flies straight and ignores gravity so clients can simulate it from the launch velocity alone. Hits are resolved on the server.
    public class Bullet : SpaceObject
    {
        [SerializeField, Min(0f), Tooltip("Damage against asteroid solidness and strength.")] private float m_AsteroidDamage = 10f;
        [SerializeField, Range(0f, 1f), Tooltip("Probability of attempting an asteroid crack after a damaging hit.")] private float m_AsteroidCrackChance = 0.01f;
        [SerializeField, Min(0f), Tooltip("World-space damage radius. Up to 0.5 is a point impact; larger values are explosions.")] private float m_DamageRadius = 0.5f;
        [SerializeField, Min(0f), Tooltip("Shield-rated damage. Zero bypasses shields.")] private float m_ShieldDamage = 10f;
        [SerializeField, Min(0f), Tooltip("Direct armour damage. Zero bypasses armour.")] private float m_ArmourDamage = 1f;
        [SerializeField, Min(0f), Tooltip("Damage to hull and nearby components after defensive layers.")] private float m_HullComponentDamage = 10f;
        [SerializeField] private float m_Lifetime = 3f;
        [SerializeField, Tooltip("Keeps the object alive briefly after a hit so clients receive the explosion.")] private float m_DespawnDelay = 0.2f;
        [SerializeField] private GameObject m_DamageExplosion;
        [SerializeField] private GameObject m_NoDamageExplosion;
        [SerializeField] private GameObject m_ShieldExplosion;

        [SyncVar] private Vector2 m_LaunchVelocity;

        private Collider2D m_Collider;
        private PlayerState m_Attacker;
        private bool m_Spent;

        public ProjectileDamage Damage => new ProjectileDamage(m_AsteroidDamage, m_ShieldDamage,
            m_ArmourDamage, m_HullComponentDamage);
        public float AsteroidCrackChance => m_AsteroidCrackChance;
        public float DamageRadius => m_DamageRadius;

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
            HitResult result = HitResult.NoDamage;
            ProjectileDamage damage = Damage;
            if (!damage.IsValid)
                Debug.LogError("Projectile damage ratings must be finite and nonnegative.", this);
            else if (target is Ship ship)
                result = ship.TakeProjectileDamage(damage, point, m_Attacker);
            else if (target is Asteroid asteroid)
                result = asteroid.TakeProjectileDamage(damage.Asteroid, point, m_Body.linearVelocity,
                    DamageRadius, AsteroidCrackChance, m_Attacker);
            else if (target != null)
                result = target.TakeDamage(damage.HullComponents, point, m_Attacker);

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
            if (explosion != null) Instantiate(explosion, point, transform.rotation);
        }

        private void Despawn() => NetworkServer.Destroy(gameObject);
    }
}
