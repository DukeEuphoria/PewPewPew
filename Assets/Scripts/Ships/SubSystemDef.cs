using PewPewPew.Core;
using PewPewPew.Presentation;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Optional hull extras. Active power drain is per second while active; Triggered systems run for Duration then cool down.
    [CreateAssetMenu(menuName = "PewPewPew/SubSystem")]
    public class SubSystemDef : ShipComponentDef
    {
        [SerializeField] private ActivationMode m_Mode = ActivationMode.Toggle;
        [SerializeField] private float m_Duration = 5f;
        [SerializeField] private float m_Cooldown = 10f;
        [SerializeField, Tooltip("Added to the ship's power storage.")] private float m_ExtraPowerStorage;
        [SerializeField, Tooltip("Hull health repaired per second while active.")] private float m_HullRepairRate;
        [SerializeField, Tooltip("Multiplies every gun's fire rate while active.")] private float m_FireRateMultiplier = 1f;
        [SerializeField, Min(1f), Tooltip("Multiplies engine thrust force while active.")] private float m_ThrustForceMultiplier = 1f;
        [SerializeField, Min(1f), Tooltip("Multiplies shield health capacity while active, preserving its filled fraction.")] private float m_ShieldHealthMultiplier = 1f;
        [SerializeField, Min(1f), Tooltip("Multiplies armour blocking strength while active.")] private float m_ArmourStrengthMultiplier = 1f;
        [SerializeField, Tooltip("HUD radial gauge prefab.")] private HudGauge m_Gauge;
        [SerializeField] private RadarEmitterDef m_Radar;

        public ActivationMode Mode => m_Mode;
        public float Duration => m_Duration;
        public float Cooldown => m_Cooldown;
        public float ExtraPowerStorage => m_ExtraPowerStorage;
        public float HullRepairRate => m_HullRepairRate;
        public float FireRateMultiplier => m_FireRateMultiplier;
        public float ThrustForceMultiplier => Mathf.Max(1f, m_ThrustForceMultiplier);
        public float ShieldHealthMultiplier => Mathf.Max(1f, m_ShieldHealthMultiplier);
        public float ArmourStrengthMultiplier => Mathf.Max(1f, m_ArmourStrengthMultiplier);
        public HudGauge Gauge => m_Gauge;
        public RadarEmitterDef Radar => m_Radar;
    }
}
