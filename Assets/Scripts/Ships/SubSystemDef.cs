using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Optional hull extras. Active power drain is per second while active; Triggered systems run for Duration then cool down.
    public abstract class SubSystemDef : ShipComponentDef
    {
        [SerializeField] private ActivationMode m_Mode = ActivationMode.Toggle;
        [SerializeField] private float m_Duration = 5f;
        [SerializeField] private float m_Cooldown = 10f;
        [SerializeField, Tooltip("HUD radial gauge prefab.")] private HudGauge m_Gauge;

        public ActivationMode Mode => m_Mode;
        public float Duration => m_Duration;
        public float Cooldown => m_Cooldown;
        public HudGauge Gauge => m_Gauge;
        public override ComponentType Type => ComponentType.SubSystem;
        public abstract SubSystem Create(Transform mount, Transform space);
    }
}
