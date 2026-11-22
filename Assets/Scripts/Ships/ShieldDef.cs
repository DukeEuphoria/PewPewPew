using UnityEngine;

namespace PewPewPew.Ships
{
    public enum ShieldMeshType { Grid, Geodesic }

    /// The component's own Max Health is the generator's. The emitted shield has its own capacity, absorbs damage and
    /// recharges; it is considered active (drawing active power) while recharging.
    [CreateAssetMenu(menuName = "PewPewPew/Shield")]
    public class ShieldDef : ShipComponentDef
    {
        [SerializeField, Tooltip("Prefab root: ShieldVisual, a unit-sized mesh renderer and the ship's collider. Scaled to fit the hull.")] private ShieldVisual m_Visual;
        [SerializeField, Tooltip("HUD bar prefab for shield health.")] private HudGauge m_Bar;
        [SerializeField] private float m_ShieldCapacity = 100f;
        [SerializeField] private float m_MaxSingleImpact = 50f;
        [SerializeField] private float m_RechargeRate = 10f;
        [SerializeField, Range(1f, 180f), Tooltip("Coverage to either side of the hull point's Up direction; 180 covers all directions.")] private float m_HalfAngle = 180f;
        [SerializeField] private ShieldMeshType m_MeshType = ShieldMeshType.Grid;
        [SerializeField, Range(3, 64), Tooltip("Latitude segments; longitude density scales proportionally.")] private int m_GridResolution = 24;
        [SerializeField, Range(0, 5), Tooltip("Number of icosahedron subdivision steps.")] private int m_GeodesicResolution = 2;
        [SerializeField, Range(0f, 1f)] private float m_MaxAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float m_EdgeAlpha = 0f;
        [SerializeField, Range(0f, 179.999f), Tooltip("Angle from the mount's Up direction at which the edge fade begins.")] private float m_FadeStartAngle = 135f;

        public ShieldVisual Visual => m_Visual;
        public HudGauge Bar => m_Bar;
        public float ShieldCapacity => m_ShieldCapacity;
        public float MaxSingleImpact => m_MaxSingleImpact;
        public float RechargeRate => m_RechargeRate;
        public float HalfAngle => Mathf.Clamp(m_HalfAngle, 1f, 180f);
        public ShieldMeshType MeshType => m_MeshType;
        public int GridResolution => Mathf.Clamp(m_GridResolution, 3, 64);
        public int GeodesicResolution => Mathf.Clamp(m_GeodesicResolution, 0, 5);
        public float MaxAlpha => Mathf.Clamp01(m_MaxAlpha);
        public float EdgeAlpha => Mathf.Clamp(m_EdgeAlpha, 0f, MaxAlpha);
        public float FadeStartAngle => Mathf.Clamp(m_FadeStartAngle, 0f, HalfAngle - 0.001f);
        public override ComponentType Type => ComponentType.Shield;
        public override bool TakesFullDamage => true;

        private void OnValidate()
        {
            m_GridResolution = GridResolution;
            m_GeodesicResolution = GeodesicResolution;
            m_MaxAlpha = MaxAlpha;
            m_EdgeAlpha = EdgeAlpha;
            m_FadeStartAngle = FadeStartAngle;
        }
    }
}
