using UnityEngine;

namespace PewPewPew.Ships
{
    /// Shared ratings for every ship component. Negative passive drain means the component generates power.
    public abstract class ShipComponentDef : ScriptableObject
    {
        [SerializeField] private float m_Mass = 1f;
        [SerializeField] protected float m_PassivePowerDrain;
        [SerializeField] private float m_ActivePowerDrain;
        [SerializeField] private float m_MaxHealth = 100f;
        [SerializeField] private float m_AutoRepairRate;

        public float Mass => m_Mass;
        public float PassivePowerDrain => m_PassivePowerDrain;
        public float ActivePowerDrain => m_ActivePowerDrain;
        public float MaxHealth => m_MaxHealth;
        public float AutoRepairRate => m_AutoRepairRate;

        /// True for components that take the full damage that gets past shield and armour, rather than a proximity share.
        public virtual bool TakesFullDamage => false;
    }
}
