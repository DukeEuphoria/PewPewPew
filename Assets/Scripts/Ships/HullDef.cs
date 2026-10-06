using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/Hull")]
    public class HullDef : ShipComponentDef
    {
        private const float CentreTolerance = 0.001f;
        [SerializeField, Tooltip("Root (no parent) of the ship's visual hierarchy, in the XY plane with the nose toward +Y. It must contain a mesh renderer.")] private HullPoints m_HullPoints;
        [SerializeField] private float m_Armour = 10f;
        [SerializeField, Tooltip("HUD bar prefab for armour.")] private HudGauge m_ArmourBar;
        [SerializeField, Tooltip("HUD bar prefab for the ship's power storage.")] private HudGauge m_PowerBar;
        [SerializeField] private float m_ArmourRepairRate = 1f;
        [SerializeField, Tooltip("Armour lost per point of damage it blocks.")] private float m_ArmourWearRatio = 0.1f;
        [SerializeField] private float m_MaxPowerStorage = 100f;
        [SerializeField, Range(0, 4)] private int m_SubSystemSlots = 2;
        [SerializeField, Tooltip("Quadratic drag coefficient: drag force = friction * speed^2.")] private float m_Friction = 0.01f;
        [SerializeField] private float m_AngularAcceleration = 360f;
        [SerializeField] private float m_AngularDamping = 2f;

        public HullPoints HullPoints => m_HullPoints;
        public float Armour => m_Armour;
        public HudGauge ArmourBar => m_ArmourBar;
        public HudGauge PowerBar => m_PowerBar;
        public float ArmourRepairRate => m_ArmourRepairRate;
        public float ArmourWearRatio => m_ArmourWearRatio;
        public float MaxPowerStorage => m_MaxPowerStorage;
        public int SubSystemSlots => m_SubSystemSlots;
        public Transform[] MainWeaponPoints => m_HullPoints != null ? m_HullPoints.MainWeapon : Array.Empty<Transform>();
        public Transform[] ThrusterPoints => m_HullPoints != null ? m_HullPoints.Thruster : Array.Empty<Transform>();
        public Transform[] SecondaryWeaponPoints => m_HullPoints != null ? m_HullPoints.SecondaryWeapon : Array.Empty<Transform>();

        /// The `needed` points a component uses, in order, or null if the hull has too few. When the hull has more than needed,
        /// an even count skips centre-line points and an odd count takes them first, so firing and thrust stay symmetrical.
        public Transform[] SelectPoints(Transform[] prefabPoints, int needed)
        {
            if (m_HullPoints == null) return null;

            List<Transform> points = new List<Transform>(Bind(prefabPoints, m_HullPoints.transform));
            if (points.Count < needed) return null;

            if (points.Count > needed)
            {
                bool isCentre(Transform point) => Mathf.Abs(point.localPosition.x) <= CentreTolerance;
                if (needed % 2 == 0)
                {
                    List<Transform> offCentre = points.FindAll(point => !isCentre(point));
                    if (offCentre.Count >= needed) points = offCentre;
                }
                else
                {
                    points = points.FindAll(isCentre).Concat(points.FindAll(point => !isCentre(point))).ToList();
                }
            }
            return points.GetRange(0, needed).ToArray();
        }

        /// Finds the counterpart of each prefab point inside an instance of the hull prefab; unresolvable points are dropped.
        public Transform[] Bind(Transform[] prefabPoints, Transform instanceRoot)
        {
            var bound = new List<Transform>();
            foreach (Transform point in prefabPoints)
            {
                Transform found = Find(point, instanceRoot);
                if (found != null) bound.Add(found);
            }
            return bound.ToArray();
        }

        private Transform Find(Transform prefabPoint, Transform instanceRoot)
        {
            if (prefabPoint == null || m_HullPoints == null) return null;

            var path = new Stack<int>();
            for (Transform t = prefabPoint; t != m_HullPoints.transform; t = t.parent)
            {
                if (t == null) return null;
                path.Push(t.GetSiblingIndex());
            }

            Transform result = instanceRoot;
            while (path.Count > 0) result = result.GetChild(path.Pop());
            return result;
        }
        public float Friction => m_Friction;
        public float AngularAcceleration => m_AngularAcceleration;
        public float AngularDamping => m_AngularDamping;
        public override bool TakesFullDamage => true;

        private void OnValidate()
        {
            m_PassivePowerDrain = Mathf.Min(0f, m_PassivePowerDrain);
            if (m_HullPoints != null && m_HullPoints.GetComponentInChildren<MeshRenderer>() == null)
            {
                Debug.LogError($"{name}: the hull prefab '{m_HullPoints.name}' has no MeshRenderer in its hierarchy.", this);
            }
        }
    }
}
