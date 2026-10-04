using Mirror;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.World
{
    /// Never accelerated: fixed in place, or moving along an ellipse around its parent. Clients compute the
    /// position from the shared network time, so nothing needs to be synced.
    public class OrbitalBody : SpaceObject
    {
        [SerializeField] private OrbitalBody m_Parent;
        [SerializeField] private float m_MinAltitude = 100f;
        [SerializeField] private float m_MaxAltitude = 100f;
        [SerializeField] private float m_Period = 60f;
        [SerializeField] private float m_PhaseAngle;
        [SerializeField] private float m_PrecessionRate;

        private Vector2 m_FixedPosition;

        protected override void Awake()
        {
            base.Awake();
            m_Body.bodyType = RigidbodyType2D.Kinematic;
            m_FixedPosition = transform.position;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            m_MinAltitude = Mathf.Max(0.01f, m_MinAltitude);
            m_MaxAltitude = Mathf.Max(m_MinAltitude, m_MaxAltitude);
            m_Period = Mathf.Max(0.01f, m_Period);
        }

        protected override void OnPhysicsStep()
        {
            if (m_Parent != null) m_Body.MovePosition(PositionAt(NetworkTime.time));
        }

        private Vector2 PositionAt(double time)
        {
            if (m_Parent == null) return m_FixedPosition;

            Vector2 offset = OrbitMath.Position(m_MinAltitude, m_MaxAltitude, m_PhaseAngle, m_PrecessionRate, m_Period, (float)time);
            return m_Parent.PositionAt(time) + offset;
        }
    }
}
