using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/SubSystems/Tractor")]
    public sealed class TractorSubSystemDef : SubSystemDef
    {
        [Header("Tractor Settings")]
        
        [SerializeField, Min(0f), Tooltip("Automatic tractor range; zero disables the tractor.")] private float m_TractorRange;
        [SerializeField, Range(0f, 180f), Tooltip("Half of the tractor's forward cone angle.")] private float m_TractorHalfAngle;
        [SerializeField, Min(0f), Tooltip("Force applied to an attracted pickup.")] private float m_TractorForce;
        [SerializeField, Tooltip("Beam visual prefab shown while pulling a pickup.")] private TractorVisual m_Visual;

        public float TractorRange => m_TractorRange;
        public float TractorHalfAngle => m_TractorHalfAngle;
        public float TractorForce => m_TractorForce;
        public TractorVisual Visual => m_Visual;
        public override SubSystem Create(Transform mount, Transform space) => new TractorSubSystem(this, mount, space);
    }
}
