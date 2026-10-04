using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Server-side thruster state. Active power is drawn per second while it fires.
    public class Thruster
    {
        private const float FiringThrottleThreshold = 0.01f;
        private float m_Throttle;

        public Thruster(ThrusterDef def, ShipSystem system)
        {
            Def = def;
            System = system;
        }

        public ThrusterDef Def { get; }
        public ShipSystem System { get; }
        public bool IsFiring => m_Throttle > FiringThrottleThreshold;
        public float Throttle => m_Throttle;

        public void Step(float deltaTime, float demand, PowerBank power)
        {
            bool canFire = !System.Health.IsDestroyed && demand > 0f && power.TryDraw(Def.ActivePowerDrain * deltaTime);
            float target = canFire ? demand : 0f;
            m_Throttle = ThrusterMath.Ramp(m_Throttle, target, Def.RampUpTime, Def.RampDownTime, deltaTime);
        }

        /// Force is split evenly over the points in use, each pushing along its own +Y.
        public void Apply(Rigidbody2D body)
        {
            if (m_Throttle <= 0f) return;

            int used = Mathf.Min(Def.EmissionPointsUsed, System.Points.Length);
            for (int i = 0; i < used; i++)
            {
                Transform point = System.Points[i];
                body.AddForceAtPosition((Vector2)point.up * (Def.Force * m_Throttle / used), point.position);
            }
        }
    }
}
