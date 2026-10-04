using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Spawns the thruster's particle effect at each emission point in use. The effect's authored emission rate and
    /// start speed are the maximums, scaled by the throttle.
    public class ThrusterEffects : MonoBehaviour
    {
        private struct Emitter
        {
            public ParticleSystem Particles;
            public float MaxRate;
            public float MaxSpeed;
        }

        private readonly List<Emitter> m_Emitters = new List<Emitter>();

        public void Build(ThrusterDef def, Transform[] points)
        {
            if (def.Effect == null) return;

            int used = Mathf.Min(def.EmissionPointsUsed, points.Length);
            for (int i = 0; i < used; i++)
            {
                GameObject effect = Instantiate(def.Effect, points[i]);
                effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    m_Emitters.Add(new Emitter
                    {
                        Particles = particles,
                        MaxRate = particles.emission.rateOverTimeMultiplier,
                        MaxSpeed = particles.main.startSpeedMultiplier,
                    });
                }
            }
            SetThrottle(0f);
        }

        public void SetThrottle(float throttle)
        {
            throttle = Mathf.Clamp01(throttle);
            foreach (Emitter emitter in m_Emitters)
            {
                ParticleSystem.EmissionModule emission = emitter.Particles.emission;
                emission.enabled = throttle > 0f;
                emission.rateOverTimeMultiplier = emitter.MaxRate * throttle;

                ParticleSystem.MainModule main = emitter.Particles.main;
                main.startSpeedMultiplier = emitter.MaxSpeed * throttle;
            }
        }
    }
}
