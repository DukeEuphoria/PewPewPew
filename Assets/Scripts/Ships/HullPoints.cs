using UnityEngine;

namespace PewPewPew.Ships
{
    /// On the hull prefab's root: which of its own child transforms are weapon and thruster emission points.
    /// Each transform's position and local +Y give the point and its direction.
    public class HullPoints : MonoBehaviour
    {
        public const int MaxEmissionPoints = 4;
        [SerializeField] private Transform[] m_MainWeapon;
        [SerializeField] private Transform[] m_SecondaryWeapon;
        [SerializeField] private Transform[] m_Thruster;
        [SerializeField] private Transform[] m_SubSystems = new Transform[4];

        public Transform[] MainWeapon => m_MainWeapon;
        public Transform[] SecondaryWeapon => m_SecondaryWeapon;
        public Transform[] Thruster => m_Thruster;
        public Transform[] SubSystems => m_SubSystems;
    }
}
