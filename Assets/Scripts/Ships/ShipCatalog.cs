using System;
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
        [SerializeField] private ShipComponentDef[] m_Components;

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
        public ShipComponentDef[] Components => m_Components;

        public ShipLoadout DefaultLoadout(int hullIndex)
        {
            if (m_Hulls == null || !LoadoutMath.InRange(hullIndex, m_Hulls.Length, false) || m_Hulls[hullIndex] == null)
                throw new ArgumentOutOfRangeException(nameof(hullIndex));
            HullPoints hull = m_Hulls[hullIndex].HullPoints;
            if (hull == null)
                throw new InvalidOperationException($"{m_Hulls[hullIndex].name} has no hull points.");
            HullPoint[] points = hull.Points;

            ShipComponentDef[] components = Components;
            var loadout = ShipLoadout.Empty;
            loadout.Hull = hullIndex;
            loadout.MountComponents = new int[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                HullPoint point = points[i];
                if (point == null) throw new InvalidOperationException($"{hull.name} has a missing hull point at index {i}.");
                if (point.DefaultComponent == null)
                {
                    loadout.MountComponents[i] = -1;
                    continue;
                }
                int component = Array.IndexOf(components, point.DefaultComponent);
                if (component < 0 || !point.AcceptsComponent(point.DefaultComponent))
                    throw new InvalidOperationException($"{hull.name}: invalid default component at {point.name}.");
                loadout.MountComponents[i] = component;
            }
            return loadout;
        }

        public bool IsMountLoadoutValid(ShipLoadout loadout)
        {
            if (m_Hulls == null || m_Components == null ||
                !LoadoutMath.InRange(loadout.Hull, m_Hulls.Length, false) || m_Hulls[loadout.Hull] == null) return false;
            HullPoints hull = m_Hulls[loadout.Hull].HullPoints;
            if (hull == null || loadout.MountComponents == null) return false;
            HullPoint[] points = hull.Points;
            if (loadout.MountComponents.Length != points.Length) return false;
            ShipComponentDef[] components = Components;
            for (int i = 0; i < points.Length; i++)
            {
                int index = loadout.MountComponents[i];
                if (!LoadoutMath.InRange(index, components.Length, true) || points[i] == null) return false;
                if (index >= 0 && (components[index] == null || !points[i].AcceptsComponent(components[index]))) return false;
            }
            return true;
        }

        public bool IsValid(ShipLoadout loadout) => IsMountLoadoutValid(loadout);

        /// Requires an in-range loadout. Purchasing can later replace the definition's initial lock rule.
        public string LockedProblem(ShipLoadout loadout)
        {
            if (!PlayerPalette.IsAvailable(loadout.Colour0)) return "Main colour is locked or invalid.";
            if (!PlayerPalette.IsAvailable(loadout.Colour1)) return "Accent colour is locked or invalid.";
            HullDef hull = m_Hulls[loadout.Hull];
            if (hull.IsLocked) return $"{hull.DisplayName} is locked (price: {hull.Price}).";
            ShipComponentDef[] components = Components;
            foreach (int index in loadout.MountComponents)
            {
                if (index >= 0 && components[index].IsLocked)
                    return $"{components[index].DisplayName} is locked (price: {components[index].Price}).";
            }
            return null;
        }

        /// Null if the loadout can launch; otherwise why not.
        public string LaunchProblem(ShipLoadout loadout)
        {
            if (!IsValid(loadout)) return "The loadout is out of range.";
            string locked = LockedProblem(loadout);
            if (locked != null) return locked;
            ShipComponentDef[] components = Components;
            foreach (int index in loadout.MountComponents)
            {
                if (index < 0) continue;
                ShipComponentDef component = components[index];
                if (component is ShieldDef shield && shield.Visual == null)
                    return $"{shield.name} has no shield visual.";
                if (component is TractorSubSystemDef tractor && tractor.TractorRange > 0f && tractor.Visual == null)
                    return $"{tractor.name} has no tractor visual.";
                if (component is GunDef gun && gun.BulletPrefab == null)
                    return $"{gun.name} has no bullet prefab.";
            }
            return null;
        }
    }
}
