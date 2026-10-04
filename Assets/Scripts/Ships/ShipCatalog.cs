using PewPewPew.Core;
using PewPewPew.Weapons;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Every component a player can choose from. Loadouts refer to entries by index, so the order must match on server and clients.
    [CreateAssetMenu(menuName = "PewPewPew/Ship Catalog")]
    public class ShipCatalog : ScriptableObject
    {
        private static ShipCatalog s_Instance;

        [SerializeField] private HullDef[] m_Hulls;
        [SerializeField] private ShieldDef[] m_Shields;
        [SerializeField] private GunDef[] m_MainGuns;
        [SerializeField] private GunDef[] m_SecondaryGuns;
        [SerializeField] private ThrusterDef[] m_Thrusters;
        [SerializeField] private SubSystemDef[] m_SubSystems;

        /// The first ShipCatalog asset found in any Resources folder.
        public static ShipCatalog Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    ShipCatalog[] found = Resources.LoadAll<ShipCatalog>(string.Empty);
                    if (found.Length == 0) Debug.LogError("No ShipCatalog asset found in a Resources folder.");
                    else s_Instance = found[0];
                }
                return s_Instance;
            }
        }

        public HullDef[] Hulls => m_Hulls;
        public ShieldDef[] Shields => m_Shields;
        public GunDef[] MainGuns => m_MainGuns;
        public GunDef[] SecondaryGuns => m_SecondaryGuns;
        public ThrusterDef[] Thrusters => m_Thrusters;
        public SubSystemDef[] SubSystems => m_SubSystems;

        /// Indices in range, and no more sub systems than the hull has slots.
        public bool IsValid(ShipLoadout loadout)
        {
            if (!LoadoutMath.InRange(loadout.Hull, m_Hulls.Length, false) ||
                !LoadoutMath.InRange(loadout.Shield, m_Shields.Length, false) ||
                !LoadoutMath.InRange(loadout.MainGun, m_MainGuns.Length, false) ||
                !LoadoutMath.InRange(loadout.SecondaryGun, m_SecondaryGuns.Length, false) ||
                !LoadoutMath.InRange(loadout.Thruster, m_Thrusters.Length, false))
            {
                return false;
            }

            int slots = m_Hulls[loadout.Hull].SubSystemSlots;
            for (int slot = 0; slot < ShipLoadout.SubSystemSlots; slot++)
            {
                int index = loadout.GetSubSystem(slot);
                if (!LoadoutMath.InRange(index, m_SubSystems.Length, true)) return false;
                if (index >= 0 && slot >= slots) return false;
            }
            return true;
        }

        public bool IsCompatible(HullDef hull, GunDef gun, bool secondary)
        {
            Transform[] points = secondary ? hull.SecondaryWeaponPoints : hull.MainWeaponPoints;
            return hull.SelectPoints(points, gun.MaxEmissionPointsConsidered) != null;
        }

        public bool IsCompatible(HullDef hull, ThrusterDef thruster)
        {
            return hull.SelectPoints(hull.ThrusterPoints, thruster.EmissionPointsUsed) != null;
        }

        /// Null if the loadout can launch; otherwise why not. The chosen guns and thruster need enough emission points on the hull.
        public string LaunchProblem(ShipLoadout loadout)
        {
            if (!IsValid(loadout)) return "The loadout is out of range.";

            HullDef hull = m_Hulls[loadout.Hull];
            if (hull.HullPoints == null) return $"{hull.name} has no hull prefab.";
            if (m_Shields[loadout.Shield].Visual == null) return $"{m_Shields[loadout.Shield].name} has no shield visual.";

            GunDef main = m_MainGuns[loadout.MainGun];
            if (!IsCompatible(hull, main, false))
                return $"{hull.name} has too few main weapon points for {main.name} (needs {main.MaxEmissionPointsConsidered}).";

            GunDef secondary = m_SecondaryGuns[loadout.SecondaryGun];
            if (!IsCompatible(hull, secondary, true))
                return $"{hull.name} has too few secondary weapon points for {secondary.name} (needs {secondary.MaxEmissionPointsConsidered}).";

            ThrusterDef thruster = m_Thrusters[loadout.Thruster];
            if (!IsCompatible(hull, thruster))
                return $"{hull.name} has too few thruster points for {thruster.name} (needs {thruster.EmissionPointsUsed}).";

            return null;
        }
    }
}
