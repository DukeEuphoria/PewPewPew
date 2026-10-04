using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using UnityEngine;

namespace PewPewPew.World
{
    /// Outcome of a hit, used to pick the impact explosion.
    public enum HitResult { NoDamage, Damaged, ShieldAbsorbed }

    public interface IDamageable
    {
        /// attacker is the player responsible, or null for environmental damage.
        HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker);
    }

    /// Floating rock. Size drives scale (linear), mass (cubic) and health; it splits into smaller rocks when destroyed.
    public class Asteroid : SpaceObject, IDamageable
    {
        [SerializeField] private float m_ScalePerSize = 0.5f;
        [SerializeField] private float m_MassPerCubicSize = 1f;
        [SerializeField] private float m_HealthPerSize = 20f;
        [SerializeField] private float m_FragmentScatterSpeed = 2f;
        [SerializeField] private float m_CollisionDamageMultiplier = 1f;
        [SerializeField] private float m_OrbitalBodyDamageMultiplier = 10f;

        [SyncVar(hook = nameof(OnSizeChanged))] private int m_Size = 1;

        private float m_Health;
        private bool m_Broken;

        public int Size => m_Size;

        /// Server only, before the object is spawned.
        public void Initialize(int size)
        {
            m_Size = size;
            m_Health = size * m_HealthPerSize;
            ApplySize();
        }

        public override void OnStartClient() => ApplySize();

        public override void OnStopServer()
        {
            if (AsteroidField.Instance != null) AsteroidField.Instance.Forget();
        }

        public HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker)
        {
            if (!isServer || m_Broken) return HitResult.NoDamage;

            m_Health -= amount;
            if (m_Health <= 0f) Break();
            return HitResult.Damaged;
        }

        [ServerCallback]
        private void OnCollisionEnter2D(Collision2D collision)
        {
            bool isOrbitalBody = collision.collider.GetComponentInParent<OrbitalBody>() != null;
            float multiplier = isOrbitalBody ? m_OrbitalBodyDamageMultiplier : m_CollisionDamageMultiplier;
            ContactPoint2D contact = collision.GetContact(0);
            TakeDamage(DamageMath.Impact(collision.relativeVelocity, contact.normal, multiplier), contact.point, null);
        }

        private void Break()
        {
            m_Broken = true;
            var random = new System.Random();
            foreach (int pieceSize in AsteroidMath.Split(m_Size, random))
            {
                Vector2 offset = Random.insideUnitCircle * (m_Size * m_ScalePerSize * 0.5f);
                Vector2 velocity = m_Body.linearVelocity + Random.insideUnitCircle * m_FragmentScatterSpeed;
                AsteroidField.Instance.Spawn(pieceSize, m_Body.position + offset, velocity);
            }
            NetworkServer.Destroy(gameObject);
        }

        private void OnSizeChanged(int oldSize, int newSize) => ApplySize();

        private void ApplySize()
        {
            transform.localScale = Vector3.one * (m_Size * m_ScalePerSize);
            m_Body.mass = m_Size * m_Size * m_Size * m_MassPerCubicSize;
        }
    }
}
