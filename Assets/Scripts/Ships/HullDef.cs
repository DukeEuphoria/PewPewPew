using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/Hull")]
    public class HullDef : ShipComponentDef
    {
        private const float DefaultCargoDropFraction = 0.3f;
        [SerializeField, Tooltip("Root (no parent) of the ship's visual hierarchy, in the XY plane with the nose toward +Y. It must contain a mesh renderer.")] private HullPoints m_HullPoints;
        [SerializeField] private float m_Armour = 10f;
        [SerializeField, Tooltip("HUD bar prefab for armour.")] private HudGauge m_ArmourBar;
        [SerializeField, Tooltip("HUD bar prefab for the ship's power storage.")] private HudGauge m_PowerBar;
        [SerializeField] private float m_ArmourRepairRate = 1f;
        [SerializeField, Tooltip("Armour lost per point of collision damage it blocks. Projectiles use direct armour damage.")] private float m_ArmourWearRatio = 0.1f;
        [SerializeField] private float m_MaxPowerStorage = 100f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of each cargo material dropped when the hull is destroyed.")] private float m_CargoDropFraction = DefaultCargoDropFraction;
        [SerializeField, Tooltip("Flat-space quadratic drag coefficient: drag force = friction * speed^2. Reduced above GameWorld's gravity threshold.")] private float m_Friction = 0.01f;
        [SerializeField] private float m_AngularAcceleration = 360f;
        [SerializeField] private float m_AngularDamping = 2f;

        public HullPoints HullPoints => m_HullPoints;
        public float Armour => m_Armour;
        public HudGauge ArmourBar => m_ArmourBar;
        public HudGauge PowerBar => m_PowerBar;
        public float ArmourRepairRate => m_ArmourRepairRate;
        public float ArmourWearRatio => m_ArmourWearRatio;
        public float MaxPowerStorage => m_MaxPowerStorage;
        public float CargoDropFraction => m_CargoDropFraction;
        public float Friction => m_Friction;
        public float AngularAcceleration => m_AngularAcceleration;
        public float AngularDamping => m_AngularDamping;
        public override ComponentType Type => ComponentType.Hull;
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
