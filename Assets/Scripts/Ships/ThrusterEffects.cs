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

        private readonly List<List<Emitter>> m_EmitterGroups = new List<List<Emitter>>();

        public int Build(ThrusterDef def, Transform[] points)
        {
            var emitters = new List<Emitter>();
            int group = m_EmitterGroups.Count;
            m_EmitterGroups.Add(emitters);
            if (def.Effect == null) return group;

            for (int i = 0; i < points.Length; i++)
            {
                GameObject effect = Instantiate(def.Effect, points[i]);
                effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    emitters.Add(new Emitter
                    {
                        Particles = particles,
                        MaxRate = particles.emission.rateOverTimeMultiplier,
                        MaxSpeed = particles.main.startSpeedMultiplier,
                    });
                }
            }
            SetThrottle(group, 0f);
            return group;
        }

        public void SetThrottle(float throttle)
        {
            if (m_EmitterGroups.Count > 0) SetThrottle(0, throttle);
        }

        public void SetThrottle(int group, float throttle)
        {
            if (group < 0) return;
            if (group >= m_EmitterGroups.Count) throw new System.ArgumentOutOfRangeException(nameof(group));

            throttle = Mathf.Clamp01(throttle);
            foreach (Emitter emitter in m_EmitterGroups[group])
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
