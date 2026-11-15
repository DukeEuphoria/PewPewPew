using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Follows the local ship and zooms out with speed and nearby large-object size hints.
    [RequireComponent(typeof(Camera))]
    public class ShipCamera : MonoBehaviour
    {
        [SerializeField] private float m_MinSize = 10f;
        [SerializeField] private float m_MaxSize = 30f;
        [SerializeField] private float m_SpeedForMaxSize = 100f;
        [SerializeField] private float m_ZoomSmoothTime = 0.5f;
        [SerializeField] private float m_SpeedSmoothing = 5f;
        [SerializeField, Min(0f), Tooltip("World-unit margin outside the speed-based view over which large-object size hints blend in.")]
        private float m_SizeHintMargin = 50f;

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
            float baselineSize = SpeedMath.Blend(Speed, m_MinSize, m_MaxSize, m_SpeedForMaxSize);
            float targetSize = baselineSize;
            foreach (CameraSizeHint hint in CameraSizeHint.ActiveHints)
            {
                if ((m_Camera.cullingMask & (1 << hint.gameObject.layer)) == 0) continue;
                Vector3 offset = hint.transform.position - transform.position;
                float depth = Vector3.Dot(offset, transform.forward);
                if (depth + hint.Radius < m_Camera.nearClipPlane || depth - hint.Radius > m_Camera.farClipPlane) continue;

                Vector2 viewOffset = new Vector2(Vector3.Dot(offset, transform.right), Vector3.Dot(offset, transform.up));
                targetSize = Mathf.Max(targetSize, CameraZoomMath.TargetSize(
                    baselineSize, m_Camera.aspect, viewOffset, hint.Radius, hint.MinimumSize, m_SizeHintMargin));
            }
            m_Camera.orthographicSize = Mathf.SmoothDamp(m_Camera.orthographicSize, targetSize, ref m_ZoomVelocity, m_ZoomSmoothTime);
        }
    }
}
