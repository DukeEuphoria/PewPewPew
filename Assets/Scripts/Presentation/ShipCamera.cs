using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Follows the local ship and zooms out as it speeds up.
    [RequireComponent(typeof(Camera))]
    public class ShipCamera : MonoBehaviour
    {
        [SerializeField] private float m_MinSize = 10f;
        [SerializeField] private float m_MaxSize = 30f;
        [SerializeField] private float m_SpeedForMaxSize = 100f;
        [SerializeField] private float m_ZoomSmoothTime = 0.5f;
        [SerializeField] private float m_SpeedSmoothing = 5f;

        private Camera m_Camera;
        private Transform m_Target;
        private Vector3 m_LastTargetPosition;
        private float m_ZoomVelocity;

        public static ShipCamera Instance { get; private set; }

        /// Smoothed speed of the followed ship, from its movement on screen so it works whatever drives the ship.
        public float Speed { get; private set; }

        /// Smoothed velocity of the followed ship, in world units per second.
        public Vector2 Velocity { get; private set; }

        public float SpeedForMaxSize => m_SpeedForMaxSize;

        private void Awake()
        {
            Instance = this;
            m_Camera = GetComponent<Camera>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Follow(Transform target)
        {
            m_Target = target;
            m_LastTargetPosition = target.position;
        }

        private void LateUpdate()
        {
            if (m_Target == null) return;

            Vector2 rawVelocity = (m_Target.position - m_LastTargetPosition) / Time.deltaTime;
            m_LastTargetPosition = m_Target.position;
            float smoothing = 1f - Mathf.Exp(-m_SpeedSmoothing * Time.deltaTime);
            Speed = Mathf.Lerp(Speed, rawVelocity.magnitude, smoothing);
            Velocity = Vector2.Lerp(Velocity, rawVelocity, smoothing);

            transform.position = new Vector3(m_Target.position.x, m_Target.position.y, transform.position.z);
            float targetSize = SpeedMath.Blend(Speed, m_MinSize, m_MaxSize, m_SpeedForMaxSize);
            m_Camera.orthographicSize = Mathf.SmoothDamp(m_Camera.orthographicSize, targetSize, ref m_ZoomVelocity, m_ZoomSmoothTime);
        }
    }
}
