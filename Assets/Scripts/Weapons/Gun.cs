using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;
using Random = System.Random;

namespace PewPewPew.Weapons
{
    /// Server-side firing state for one installed gun.
    public class Gun
    {
        private int m_BurstIndex;
        private float m_SweepPhase;
        private float m_NextFireTime;

        public Gun(GunDef def, ShipSystem system)
        {
            Def = def;
            System = system;
        }

        public GunDef Def { get; }
        public ShipSystem System { get; }
        public float RateMultiplier { get; set; } = 1f;

        public void Tick(float deltaTime) => m_SweepPhase += Def.SweepSpeed * deltaTime;

        /// Returns the burst, or null if the gun is destroyed, still cooling down or the burst's power is unavailable.
        public Shot[] TryFire(float time, PowerBank power, Random random)
        {
            if (System.Health.IsDestroyed || time < m_NextFireTime) return null;
            if (!power.TryDraw(Def.ActivePowerDrain)) return null;

            int considered = Mathf.Min(Def.MaxEmissionPointsConsidered, System.Points.Length);
            int used = Mathf.Min(Def.EmissionPointsUsed, considered);
            Shot[] shots = FireMath.Burst(Def.Pattern, Def.BulletsPerBurst, Def.MinLaunchAngle, Def.MaxLaunchAngle,
                m_SweepPhase, m_BurstIndex, used, considered, random);

            m_BurstIndex++;
            m_NextFireTime = time + 1f / (Def.FireRate * RateMultiplier);
            return shots;
        }
    }
}
