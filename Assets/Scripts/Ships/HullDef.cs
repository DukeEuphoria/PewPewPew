using System;
using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/Hull")]
    public class HullDef : ShipComponentDef
    {
        [SerializeField, Tooltip("Ship mesh in the XY plane with the nose toward +Y. Left empty, the prefab's placeholder mesh is kept.")] private Mesh m_Mesh;
        [SerializeField, Tooltip("Full width and length of the shield ellipse (assumes a unit-sized shield mesh).")] private Vector2 m_Size = new Vector2(2f, 3f);
        [SerializeField] private float m_Armour = 10f;
        [SerializeField] private float m_ArmourRepairRate = 1f;
        [SerializeField, Tooltip("Armour lost per point of damage it blocks.")] private float m_ArmourWearRatio = 0.1f;
        [SerializeField] private float m_MaxPowerStorage = 100f;
        [SerializeField, Range(0, 4)] private int m_SubSystemSlots = 2;
        [SerializeField] private EmissionPoint[] m_MainWeaponPoints = new EmissionPoint[1];
        [SerializeField] private EmissionPoint[] m_ThrusterPoints = new EmissionPoint[1];
        [SerializeField] private EmissionPoint[] m_SecondaryWeaponPoints = new EmissionPoint[1];
        [SerializeField, Tooltip("Quadratic drag coefficient: drag force = friction * speed^2.")] private float m_Friction = 0.01f;
        [SerializeField] private float m_AngularAcceleration = 360f;
        [SerializeField] private float m_AngularDamping = 2f;

        public Mesh Mesh => m_Mesh;
        public Vector2 Size => m_Size;
        public float Armour => m_Armour;
        public float ArmourRepairRate => m_ArmourRepairRate;
        public float ArmourWearRatio => m_ArmourWearRatio;
        public float MaxPowerStorage => m_MaxPowerStorage;
        public int SubSystemSlots => m_SubSystemSlots;
        public EmissionPoint[] MainWeaponPoints => m_MainWeaponPoints;
        public EmissionPoint[] ThrusterPoints => m_ThrusterPoints;
        public EmissionPoint[] SecondaryWeaponPoints => m_SecondaryWeaponPoints;
        public float Friction => m_Friction;
        public float AngularAcceleration => m_AngularAcceleration;
        public float AngularDamping => m_AngularDamping;
        public override bool TakesFullDamage => true;

        private void OnValidate()
        {
            m_PassivePowerDrain = Mathf.Min(0f, m_PassivePowerDrain);
            m_Size = Vector2.Max(m_Size, new Vector2(0.1f, 0.1f));
            Array.Resize(ref m_MainWeaponPoints, Mathf.Clamp(m_MainWeaponPoints.Length, 1, 4));
            Array.Resize(ref m_ThrusterPoints, Mathf.Clamp(m_ThrusterPoints.Length, 1, 4));
            Array.Resize(ref m_SecondaryWeaponPoints, Mathf.Clamp(m_SecondaryWeaponPoints.Length, 1, 4));
        }
    }
}
