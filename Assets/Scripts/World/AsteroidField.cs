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
            asteroid.GetComponent<Rigidbody2D>().linearVelocity = velocity;
            m_Count++;
            NetworkServer.Spawn(asteroid.gameObject);
        }

        private void SpawnRandom()
        {
            Vector2 position = RandomDenseWorldPosition();
            Spawn(Random.Range(m_MinSize, m_MaxSize + 1), position, Random.insideUnitCircle.normalized * Random.Range(m_MinSpeed, m_MaxSpeed));
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
