using Mirror;
using PewPewPew.Core;
using PewPewPew.GameSystems;
using UnityEngine;
using UnityEngine.Serialization;

namespace PewPewPew.World
{
    /// Server-side asteroid spawner that keeps the field populated. The prefab must be in the NetworkManager's spawnable prefabs.
    public class AsteroidField : NetworkBehaviour
    {
        [SerializeField] private Asteroid m_Prefab;
        [SerializeField] private int m_TargetCount = 100;
        [SerializeField] private int m_MinSize = 1;
        [SerializeField] private int m_MaxSize = 20;

        [SerializeField] 
        [Range(0f, 10f)] private float m_MinSpeed = 1f;

        [FormerlySerializedAs("m_MaxDriftSpeed"), SerializeField] 
        [Range(0f, 10f)] private float m_MaxSpeed = 3f;

        [SerializeField] private float m_TopUpInterval = 2f;
        [SerializeField] private float m_DensityScale = 300f;
        [SerializeField] private float m_DensitySharpness = 2f;
        [SerializeField] private float m_DensitySeed = 17f;
        [SerializeField] private int m_MaxPlacementAttempts = 20;
        [SerializeField, Min(0f), Tooltip("Asteroids spawned within this distance of a gravity source start on a circular orbit around the one pulling hardest. Others drift.")]
        private float m_OrbitDistance = 300f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Random spread around the circular orbit speed, as a fraction.")]
        private float m_OrbitSpeedVariation = 0.05f;
        [SerializeField] private bool m_OrbitClockwise;
        [SerializeField, Range(0f, 1f), Tooltip("0 keeps orbits as circular as possible; 1 allows very elongated ones. Each asteroid picks a random amount up to this.")]
        private float m_OrbitEllipticity;
        [SerializeField, Min(0f), Tooltip("Typical spin in degrees per second for a size 1 asteroid; larger ones spin proportionally slower.")]
        private float m_SpinAtSizeOne = 120f;
        [SerializeField, Range(0f, 1f), Tooltip("Random spread around the typical spin, as a fraction.")]
        private float m_SpinVariation = 0.5f;

        // Eccentricity at full ellipticity; staying below 1 keeps orbits bound.
        private const float MaxEccentricity = 0.85f;

        private int m_Count;
        private float m_NextTopUp;

        public static AsteroidField Instance { get; private set; }

        private void Awake() => Instance = this;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_MaxSpeed = Mathf.Max(m_MinSpeed, m_MaxSpeed);
        }

        public override void OnStartServer()
        {
            for (int i = 0; i < m_TargetCount; i++) SpawnRandom();
        }

        [ServerCallback]
        private void Update()
        {
            if (m_Count >= m_TargetCount || Time.time < m_NextTopUp) return;

            m_NextTopUp = Time.time + m_TopUpInterval;
            SpawnRandom();
        }

        public void Spawn(int size, Vector2 position, Vector2 velocity)
        {
            Asteroid asteroid = Instantiate(m_Prefab, position, Quaternion.identity);
            asteroid.Initialize(size);
            var body = asteroid.GetComponent<Rigidbody2D>();
            body.linearVelocity = velocity;
            float spin = AsteroidMath.SpinSpeed(size, m_SpinAtSizeOne) * Random.Range(1f - m_SpinVariation, 1f + m_SpinVariation);
            body.angularVelocity = Random.value < 0.5f ? -spin : spin;
            m_Count++;
            NetworkServer.Spawn(asteroid.gameObject);
        }

        private void SpawnRandom()
        {
            Vector2 position = RandomDenseWorldPosition();
            Spawn(Random.Range(m_MinSize, m_MaxSize + 1), position, SpawnVelocity(position));
        }

        private Vector2 SpawnVelocity(Vector2 position)
        {
            if (!GameWorld.Instance.TryGetOrbit(position, m_OrbitDistance, out GravitySource source, out float speed))
                return Random.insideUnitCircle.normalized * Random.Range(m_MinSpeed, m_MaxSpeed);

            Vector2 radial = position - (Vector2)source.transform.position;
            Vector2 tangent = (m_OrbitClockwise ? new Vector2(radial.y, -radial.x) : new Vector2(-radial.y, radial.x)).normalized;
            float eccentricity = Random.Range(0f, m_OrbitEllipticity * MaxEccentricity);
            Vector2 orbit = GravityMath.OrbitVelocity(speed, eccentricity, Random.Range(0f, 2f * Mathf.PI));
            float variation = Random.Range(1f - m_OrbitSpeedVariation, 1f + m_OrbitSpeedVariation);
            // A moving body carries its orbiting asteroids with it.
            Vector2 frame = source.TryGetComponent(out OrbitalBody body) ? body.VelocityAt(NetworkTime.time) : Vector2.zero;
            return frame + (radial.normalized * orbit.x + tangent * orbit.y) * variation;
        }

        /// Rejection sampling against the density map; falls back to the last candidate if none is accepted.
        private Vector2 RandomDenseWorldPosition()
        {
            float radius = GameWorld.Instance.WorldRadius;
            Vector2 center = GameWorld.Instance.transform.position;
            Vector2 offset = Vector2.zero;
            for (int i = 0; i < m_MaxPlacementAttempts; i++)
            {
                offset = Random.insideUnitCircle * radius;
                // Shifted by the radius so noise coordinates are never negative.
                float density = AsteroidMath.Density(offset + Vector2.one * radius, m_DensityScale, m_DensitySeed, m_DensitySharpness);
                if (Random.value < density) break;
            }
            return center + offset;
        }

        public void Forget() => m_Count--;
    }
}
