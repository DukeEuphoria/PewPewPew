using System.Collections.Generic;
using PewPewPew.Core;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.GameSystems
{
    /// Scene-level physics settings and gravity source registry (match flow comes later). One per game scene.
    [DefaultExecutionOrder(-100)] // Must exist before GravitySource.OnEnable registers.
    public class GameWorld : MonoBehaviour
    {
        [SerializeField] private float m_GravityConstant = 1f;
        [SerializeField] private float m_Softening = 1f;
        [SerializeField] private float m_MaxGravityDistance = 1000f;
        [SerializeField] private float m_WorldRadius = 1000f;
        [SerializeField] private float m_EdgeStrength = 50f;

        private readonly List<GravitySource> m_Sources = new List<GravitySource>();

        public static GameWorld Instance { get; private set; }

        public float WorldRadius => m_WorldRadius;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(GravitySource source) => m_Sources.Add(source);

        public void Unregister(GravitySource source) => m_Sources.Remove(source);

        /// The source pulling hardest on position within maxDistance, and the speed of a circular orbit around it.
        public bool TryGetOrbit(Vector2 position, float maxDistance, out GravitySource dominant, out float speed)
        {
            dominant = null;
            speed = 0f;
            float range = Mathf.Min(maxDistance, m_MaxGravityDistance);
            float strongest = 0f;
            foreach (GravitySource source in m_Sources)
            {
                Vector2 toSource = (Vector2)source.transform.position - position;
                if (toSource.sqrMagnitude > range * range) continue;

                float pull = GravityMath.Force(toSource, source.Mass, 1f, m_GravityConstant, m_Softening, m_MaxGravityDistance).sqrMagnitude;
                if (pull <= strongest) continue;
                strongest = pull;
                dominant = source;
            }
            if (dominant == null) return false;

            float distance = Vector2.Distance(position, dominant.transform.position);
            speed = GravityMath.CircularSpeed(dominant.Mass, m_GravityConstant, m_Softening, distance);
            return true;
        }

        /// Total gravitational force, including the edge fence, on a body of the given mass.
        public Vector2 ForceOn(Vector2 position, float mass)
        {
            Vector2 force = GravityMath.EdgeForce(position - (Vector2)transform.position, m_WorldRadius, m_EdgeStrength, mass);
            foreach (GravitySource source in m_Sources)
            {
                Vector2 toSource = (Vector2)source.transform.position - position;
                force += GravityMath.Force(toSource, source.Mass, mass, m_GravityConstant, m_Softening, m_MaxGravityDistance);
            }
            return force;
        }
    }
}
