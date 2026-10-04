using UnityEngine;

namespace PewPewPew.Ships
{
    /// On the hull prefab's root: which of its own child transforms are weapon and thruster emission points.
    /// Each transform's position and local +Y give the point and its direction.
    public class HullPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] m_MainWeapon;
        [SerializeField] private Transform[] m_SecondaryWeapon;
        [SerializeField] private Transform[] m_Thruster;

        public Transform[] MainWeapon => m_MainWeapon;
        public Transform[] SecondaryWeapon => m_SecondaryWeapon;
        public Transform[] Thruster => m_Thruster;
    }
}
