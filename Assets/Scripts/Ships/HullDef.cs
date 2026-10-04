using System;
using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/Hull")]
    public class HullDef : ShipComponentDef
    {
        [SerializeField, Tooltip("Root (no parent) of the ship's visual hierarchy, in the XY plane with the nose toward +Y. Emission points must be this object or its descendants.")] private MeshRenderer m_Mesh;
        [SerializeField] private float m_Armour = 10f;
        [SerializeField, Tooltip("HUD bar prefab for armour.")] private HudGauge m_ArmourBar;
        [SerializeField, Tooltip("HUD bar prefab for the ship's power storage.")] private HudGauge m_PowerBar;
        [SerializeField] private float m_ArmourRepairRate = 1f;
        [SerializeField, Tooltip("Armour lost per point of damage it blocks.")] private float m_ArmourWearRatio = 0.1f;
        [SerializeField] private float m_MaxPowerStorage = 100f;
        [SerializeField, Range(0, 4)] private int m_SubSystemSlots = 2;
        [SerializeField, Tooltip("Each transform's position and local +Y give the emission point and its direction.")] private Transform[] m_MainWeaponPoints = new Transform[1];
        [SerializeField] private Transform[] m_ThrusterPoints = new Transform[1];
        [SerializeField] private Transform[] m_SecondaryWeaponPoints = new Transform[1];
        [SerializeField, Tooltip("Quadratic drag coefficient: drag force = friction * speed^2.")] private float m_Friction = 0.01f;
        [SerializeField] private float m_AngularAcceleration = 360f;
        [SerializeField] private float m_AngularDamping = 2f;

        public MeshRenderer Mesh => m_Mesh;
        public float Armour => m_Armour;
        public HudGauge ArmourBar => m_ArmourBar;
        public HudGauge PowerBar => m_PowerBar;
        public float ArmourRepairRate => m_ArmourRepairRate;
        public float ArmourWearRatio => m_ArmourWearRatio;
        public float MaxPowerStorage => m_MaxPowerStorage;
        public int SubSystemSlots => m_SubSystemSlots;
        public Transform[] MainWeaponPoints => m_MainWeaponPoints;
        public Transform[] ThrusterPoints => m_ThrusterPoints;
        public Transform[] SecondaryWeaponPoints => m_SecondaryWeaponPoints;

        /// How many of each point list actually resolve to a transform inside the Mesh prefab.
        public int MainPointCount => ResolvedCount(m_MainWeaponPoints);
        public int SecondaryPointCount => ResolvedCount(m_SecondaryWeaponPoints);
        public int ThrusterPointCount => ResolvedCount(m_ThrusterPoints);

        private int ResolvedCount(Transform[] points) => m_Mesh == null ? 0 : Bind(points, m_Mesh.transform).Length;

        /// Finds the counterpart of each prefab point inside an instance of the Mesh prefab; unresolvable points are dropped.
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
            if (prefabPoint == null || m_Mesh == null) return null;

            var path = new Stack<int>();
            for (Transform t = prefabPoint; t != m_Mesh.transform; t = t.parent)
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
            Array.Resize(ref m_MainWeaponPoints, Mathf.Clamp(m_MainWeaponPoints.Length, 1, 4));
            Array.Resize(ref m_ThrusterPoints, Mathf.Clamp(m_ThrusterPoints.Length, 1, 4));
            Array.Resize(ref m_SecondaryWeaponPoints, Mathf.Clamp(m_SecondaryWeaponPoints.Length, 1, 4));
        }
    }
}
