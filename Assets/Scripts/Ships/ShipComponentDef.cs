using UnityEngine;

namespace PewPewPew.Ships
{
    /// Shared ratings for every ship component. Negative passive drain means the component generates power.
    public abstract class ShipComponentDef : ScriptableObject
    {
        [Header("Loadout menu")]
        [SerializeField] private string m_DisplayName;
        [SerializeField, TextArea(3, 8)] private string m_Description;
        [SerializeField] private Sprite m_ButtonArtwork;
        [SerializeField, Min(0), Tooltip("Components with a price above zero remain locked until purchasing is implemented.")] private int m_Price;

        [SerializeField] private float m_Mass = 1f;
        [SerializeField] protected float m_PassivePowerDrain;
        [SerializeField] private float m_ActivePowerDrain;
        [SerializeField] private float m_MaxHealth = 100f;
        [SerializeField] private float m_AutoRepairRate;

        public string DisplayName => string.IsNullOrWhiteSpace(m_DisplayName) ? name : m_DisplayName;
        public string Description => m_Description;
        public Sprite ButtonArtwork => m_ButtonArtwork;
        public int Price => Mathf.Max(0, m_Price);
        public bool IsLocked => Price > 0;

        public float Mass => m_Mass;
        public float PassivePowerDrain => m_PassivePowerDrain;
        public float ActivePowerDrain => m_ActivePowerDrain;
        public float MaxHealth => m_MaxHealth;
        public float AutoRepairRate => m_AutoRepairRate;

        /// True for components that take the full damage that gets past shield and armour, rather than a proximity share.
        public virtual bool TakesFullDamage => false;
    }
}
