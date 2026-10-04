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
