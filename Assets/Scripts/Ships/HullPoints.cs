using UnityEngine;

namespace PewPewPew.Ships
{
    /// Hierarchy order determines mount indices in network loadouts.
    public class HullPoints : MonoBehaviour
    {
        // Unity's script reload otherwise restores an uninitialized prefab cache as an empty array.
        [System.NonSerialized] private HullPoint[] m_Points;
        public HullPoint[] Points => m_Points ?? GetComponentsInChildren<HullPoint>(true);

        private void Awake() => m_Points = GetComponentsInChildren<HullPoint>(true);
    }
}
