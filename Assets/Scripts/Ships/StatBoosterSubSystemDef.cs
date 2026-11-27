using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/SubSystems/Stat Booster")]
    public sealed class StatBoosterSubSystemDef : SubSystemDef
    {
        [Header("Stat Booster Settings")]
        [SerializeField, Tooltip("Added to the ship's power storage.")] private float m_ExtraPowerStorage;
        [SerializeField, Tooltip("Hull health repaired per second while active.")] private float m_HullRepairRate;
        [SerializeField, Tooltip("Multiplies every gun's fire rate while active.")] private float m_FireRateMultiplier = 1f;
        [SerializeField, Min(1f), Tooltip("Multiplies engine thrust force while active.")] private float m_ThrustForceMultiplier = 1f;
        [SerializeField, Min(1f), Tooltip("Multiplies shield health capacity while active, preserving its filled fraction.")] private float m_ShieldHealthMultiplier = 1f;
        [SerializeField, Min(1f), Tooltip("Multiplies armour blocking strength while active.")] private float m_ArmourStrengthMultiplier = 1f;

        public float ExtraPowerStorage => m_ExtraPowerStorage;
        public float HullRepairRate => m_HullRepairRate;
        public float FireRateMultiplier => m_FireRateMultiplier;
        public float ThrustForceMultiplier => Mathf.Max(1f, m_ThrustForceMultiplier);
        public float ShieldHealthMultiplier => Mathf.Max(1f, m_ShieldHealthMultiplier);
        public float ArmourStrengthMultiplier => Mathf.Max(1f, m_ArmourStrengthMultiplier);
        public override SubSystem Create(Transform mount, Transform space) => new StatBoosterSubSystem(this, mount, space);
    }
}
