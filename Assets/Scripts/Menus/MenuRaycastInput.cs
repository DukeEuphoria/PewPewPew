using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace PewPewPew.Networking
{
    public class MenuRaycastInput : MonoBehaviour
    {
        [FormerlySerializedAs("raycastCamera"), SerializeField] private Camera m_RaycastCamera;
        [FormerlySerializedAs("buttonLayers"), SerializeField] private LayerMask m_ButtonLayers = ~0;
        [FormerlySerializedAs("maxDistance"), SerializeField, Min(0f)] private float m_MaxDistance = 100f;

        private bool m_DetectionEnabled = true;

        private void Awake()
        {
            if (m_RaycastCamera == null) Debug.LogWarning("MenuRaycastInput has no camera assigned; falling back to Camera.main, which may not render the menus.", this);
        }

        public void SetDetectionEnabled(bool isEnabled)
        {
            m_DetectionEnabled = isEnabled;
        }

        private void Update()
        {
            if (!m_DetectionEnabled || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Camera activeCamera = m_RaycastCamera != null ? m_RaycastCamera : Camera.main;
            if (activeCamera == null) return;

            Ray ray = activeCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, m_MaxDistance, m_ButtonLayers, QueryTriggerInteraction.Collide)) return;

            MenuRaycastButton button = hit.collider.GetComponent<MenuRaycastButton>();
            if (button == null) button = hit.collider.GetComponentInParent<MenuRaycastButton>();
            button?.Activate();
        }
    }
}