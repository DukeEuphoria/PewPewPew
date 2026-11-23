using Mirror;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;
using Random = System.Random;

namespace PewPewPew.Weapons
{
    /// Server-side firing state for one installed gun. Guns have no ammo or heat yet, so there is nothing to show on the HUD.
    public class Gun : IHudElement
    {
        private readonly Ship m_Shooter;
        private float m_SweepPhase;
        private float m_NextFireTime;

        public Gun(GunDef def, ShipSystem system, Ship shooter)
        {
            Def = def;
            System = system;
            m_Shooter = shooter != null ? shooter : throw new System.ArgumentNullException(nameof(shooter));
        }

        public GunDef Def { get; }
        public ShipSystem System { get; }
        public float RateMultiplier { get; set; } = 1f;

        public void Render() { }

        /// Server only. Advances the sweep even when not firing and returns whether a burst was launched.
        public bool Step(float deltaTime, Transform point, Vector2 velocity, PowerBank power, bool fire, Random random)
        {
            m_SweepPhase += Def.SweepSpeed * deltaTime;
            if (!fire) return false;
            float[] angles = TryFire(Time.time, power, random);
            if (angles == null) return false;

            foreach (float offset in angles)
            {
                float angle = Vector2.SignedAngle(Vector2.up, point.up) + offset;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                Bullet bullet = Object.Instantiate(Def.BulletPrefab, point.position, Quaternion.Euler(0f, 0f, angle));
                bullet.Launch(velocity + direction * Def.LaunchSpeed, m_Shooter);
                NetworkServer.Spawn(bullet.gameObject);
            }
            return true;
        }

        private float[] TryFire(float time, PowerBank power, Random random)
        {
            if (!System.Enabled || System.Health.IsDestroyed || time < m_NextFireTime) return null;
            if (!power.TryDraw(Def.ActivePowerDrain)) return null;

            float[] angles = FireMath.Angles(Def.Pattern, Def.BulletsPerBurst, Def.MinLaunchAngle, Def.MaxLaunchAngle,
                m_SweepPhase, random);

            m_NextFireTime = time + 1f / (Def.FireRate * RateMultiplier);
            return angles;
        }
    }
}
