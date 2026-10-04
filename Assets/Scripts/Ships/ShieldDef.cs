using UnityEngine;

namespace PewPewPew.Ships
{
    /// The component's own Max Health is the generator's. The emitted shield has its own capacity, absorbs damage and
    /// recharges; it is considered active (drawing active power) while recharging.
    [CreateAssetMenu(menuName = "PewPewPew/Shield")]
    public class ShieldDef : ShipComponentDef
    {
        [SerializeField] private float m_ShieldCapacity = 100f;
        [SerializeField] private float m_MaxSingleImpact = 50f;
        [SerializeField] private float m_RechargeRate = 10f;

        public float ShieldCapacity => m_ShieldCapacity;
        public float MaxSingleImpact => m_MaxSingleImpact;
        public float RechargeRate => m_RechargeRate;
        public override bool TakesFullDamage => true;
    }
}
