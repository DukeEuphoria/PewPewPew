using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Place on a large object to request a minimum ship-camera half-height when visible or nearby.
    [DisallowMultipleComponent]
    public class CameraSizeHint : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Minimum camera orthographic size (half-height in world units) when this object intersects the view.")]
        private float m_MinimumSize = 30f;
        [SerializeField, Min(0f), Tooltip("Circular footprint radius in world units, centered on this transform. Does not scale with the transform.")]
        private float m_Radius = 10f;

        private static readonly HashSet<CameraSizeHint> m_ActiveHints = new HashSet<CameraSizeHint>();

        public static IReadOnlyCollection<CameraSizeHint> ActiveHints => m_ActiveHints;
        public float MinimumSize => m_MinimumSize;
        public float Radius => m_Radius;

        private void OnEnable() => m_ActiveHints.Add(this);

        private void OnDisable() => m_ActiveHints.Remove(this);
    }
}
