using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Spawns the thruster's particle effect at each emission point in use and switches it on and off.
    public class ThrusterEffects : MonoBehaviour
    {
        private readonly List<ParticleSystem> m_Particles = new List<ParticleSystem>();

        public void Build(ThrusterDef def, Transform[] points)
        {
            if (def.Effect == null) return;

            int used = Mathf.Min(def.EmissionPointsUsed, points.Length);
            for (int i = 0; i < used; i++)
            {
                GameObject effect = Instantiate(def.Effect, points[i]);
                effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                m_Particles.AddRange(effect.GetComponentsInChildren<ParticleSystem>());
            }
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            foreach (ParticleSystem particles in m_Particles)
            {
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = active;
            }
        }
    }
}
