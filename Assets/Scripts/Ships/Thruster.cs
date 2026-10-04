using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Server-side thruster state. Active power is drawn per second while it fires.
    public class Thruster
    {
        private float m_Throttle;

        public Thruster(ThrusterDef def, ShipSystem system)
        {
            Def = def;
            System = system;
        }

        public ThrusterDef Def { get; }
        public ShipSystem System { get; }
        public bool IsFiring => m_Throttle > 0.01f;

        public void Step(float deltaTime, float demand, PowerBank power)
        {
            bool canFire = !System.Health.IsDestroyed && demand > 0f && power.TryDraw(Def.ActivePowerDrain * deltaTime);
            float target = canFire ? demand : 0f;
            m_Throttle = ThrusterMath.Ramp(m_Throttle, target, Def.RampUpTime, Def.RampDownTime, deltaTime);
        }

        /// Force is split evenly over the points in use, each pushing along its own angle (ship forward is +Y).
        public void Apply(Rigidbody2D body)
        {
            if (m_Throttle <= 0f) return;

            int used = Mathf.Min(Def.EmissionPointsUsed, System.Points.Length);
            for (int i = 0; i < used; i++)
            {
                EmissionPoint point = System.Points[i];
                Vector2 direction = Quaternion.Euler(0f, 0f, body.rotation + point.Angle) * Vector2.up;
                Vector2 worldPoint = body.transform.TransformPoint(point.Position);
                body.AddForceAtPosition(direction * (Def.Force * m_Throttle / used), worldPoint);
            }
        }
    }
}
