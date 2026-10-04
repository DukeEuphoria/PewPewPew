using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Scales a world-space particle system's emission with the followed ship's speed.
    [RequireComponent(typeof(ParticleSystem))]
    public class SpaceDust : MonoBehaviour
    {
        [SerializeField] private float m_RateAtRest = 2f;
        [SerializeField] private float m_RateAtFullSpeed = 60f;

        private ParticleSystem m_Particles;

        private void Awake() => m_Particles = GetComponent<ParticleSystem>();

        private void Update()
        {
            if (ShipCamera.Instance == null) return;

            ParticleSystem.EmissionModule emission = m_Particles.emission;
            emission.rateOverTime = SpeedMath.Blend(ShipCamera.Instance.Speed, m_RateAtRest, m_RateAtFullSpeed, ShipCamera.Instance.SpeedForMaxSize);
        }
    }
}
