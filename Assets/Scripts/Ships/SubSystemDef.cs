using PewPewPew.Core;
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
        [SerializeField, Tooltip("HUD radial gauge prefab.")] private HudGauge m_Gauge;

        public ActivationMode Mode => m_Mode;
        public float Duration => m_Duration;
        public float Cooldown => m_Cooldown;
        public float ExtraPowerStorage => m_ExtraPowerStorage;
        public float HullRepairRate => m_HullRepairRate;
        public float FireRateMultiplier => m_FireRateMultiplier;
        public HudGauge Gauge => m_Gauge;
    }
}
