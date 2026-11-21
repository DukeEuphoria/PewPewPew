using System;
using UnityEngine;

namespace PewPewPew.Ships
{
    [Flags]
    public enum AcceptedComponents
    {
        None = 0,
        Shield = 1 << (int)ComponentType.Shield,
        Gun = 1 << (int)ComponentType.Gun,
        Thruster = 1 << (int)ComponentType.Thruster,
        SubSystem = 1 << (int)ComponentType.SubSystem
    }

    public enum GunControlRole { Main, Secondary }

    public class HullPoint : MonoBehaviour
    {
        [SerializeField] private AcceptedComponents m_Accepts;
        [SerializeField] private ComponentSize m_Size;
        [SerializeField] private GunControlRole m_GunRole;
        [SerializeField] private ShipComponentDef m_DefaultComponent;

        public AcceptedComponents Accepts => m_Accepts;
        public ComponentSize Size => m_Size;
        public GunControlRole GunRole => m_GunRole;
        public ShipComponentDef DefaultComponent => m_DefaultComponent;

        public bool AcceptsComponent(ShipComponentDef component)
        {
            if (component == null || component.Type == ComponentType.Hull) return false;
            return (m_Accepts & (AcceptedComponents)(1 << (int)component.Type)) != 0
                && (int)component.Size <= (int)m_Size;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.identity;
            Color colour = GizmoColour(m_Accepts);
            Vector3 position = transform.position;
            Vector3 up = transform.up;
            Vector3 tip = position + up * 0.8f;
            Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.5f);
            if ((m_Accepts & AcceptedComponents.SubSystem) != 0)
            {
                Gizmos.matrix = Matrix4x4.TRS(position, transform.rotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, Vector3.one * 0.4f);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else Gizmos.DrawSphere(position, 0.2f);

            if ((m_Accepts & AcceptedComponents.Gun) != 0)
            {
#if UNITY_EDITOR
                Color previousHandleColour = UnityEditor.Handles.color;
                Matrix4x4 previousHandleMatrix = UnityEditor.Handles.matrix;
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.matrix = Matrix4x4.identity;
                UnityEditor.Handles.DrawDottedLine(position, tip, 4f);
                UnityEditor.Handles.color = previousHandleColour;
                UnityEditor.Handles.matrix = previousHandleMatrix;
#endif
            }
            else
            {
                Gizmos.color = colour;
                Gizmos.DrawLine(position, tip);
            }

            if ((m_Accepts & AcceptedComponents.Thruster) != 0)
            {
                Gizmos.color = Color.yellow;
                Vector3 exhaust = up * (0.8f * Mathf.Cos(30f * Mathf.Deg2Rad));
                Vector3 spread = transform.right * (0.8f * Mathf.Sin(30f * Mathf.Deg2Rad));
                Gizmos.DrawLine(position, position + exhaust + spread);
                Gizmos.DrawLine(position, position + exhaust - spread);
            }
        }

        private static Color GizmoColour(AcceptedComponents accepts)
        {
            switch (accepts)
            {
                case AcceptedComponents.Gun: return Color.red;
                case AcceptedComponents.Thruster: return Color.yellow;
                case AcceptedComponents.Shield: return Color.cyan;
                case AcceptedComponents.SubSystem: return Color.green;
                case AcceptedComponents.None: return Color.gray;
                default: return Color.white;
            }
        }
    }
}
