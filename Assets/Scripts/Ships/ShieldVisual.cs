using UnityEngine;

namespace PewPewPew.Ships
{
    public enum ShieldMeshType { Grid, Geodesic }

    /// Brief ellipse flash on the ship when its shield absorbs damage; colour shows remaining shield health.
    public class ShieldVisual : MonoBehaviour
    {
        [SerializeField] private Renderer m_Renderer;
        [SerializeField] private Color m_FullColor = Color.green;
        [SerializeField] private Color m_EmptyColor = Color.red;
        [SerializeField] private float m_FadeTime = 0.3f;
        [SerializeField] private float m_ShieldBloat = 1.2f;
        [SerializeField, Range(0.1f, 90f), Tooltip("Latitude from the ship's equator where the shield becomes transparent.")] private float m_LatitudeFadeAngle = 45f;
        [SerializeField] private ShieldMeshType m_MeshType = ShieldMeshType.Grid;
        [SerializeField, Range(3, 64), Tooltip("Latitude segments; longitude density scales proportionally.")] private int m_GridResolution = 24;
        [SerializeField, Range(0, 5), Tooltip("Number of icosahedron subdivision steps.")] private int m_GeodesicResolution = 2;
        [SerializeField, Range(0f, 1f)] private float m_MaxAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float m_EdgeAlpha = 0f;
        [SerializeField, Range(0f, 0.999f), Tooltip("Proportion of the shield half angle at which the edge fade begins.")] private float m_FadeStartProportion = 0.75f;

        private float m_Alpha;
        private bool m_Held;
        private float m_HeldHealthFraction;
        private float m_AppliedLatitudeFadeAngle = -1f;
        private Mesh m_ArcMesh;
        private Mesh m_OriginalMesh;

        private int GridResolution => Mathf.Clamp(m_GridResolution, 3, 64);
        private int GeodesicResolution => Mathf.Clamp(m_GeodesicResolution, 0, 5);
        private float MaxAlpha => Mathf.Clamp01(m_MaxAlpha);
        private float EdgeAlpha => Mathf.Clamp(m_EdgeAlpha, 0f, MaxAlpha);
        private float FadeStartProportion => Mathf.Clamp(m_FadeStartProportion, 0f, 0.999f);

        private void Awake()
        {
            Flash(1f);
        }

        private void OnValidate()
        {
            m_LatitudeFadeAngle = Mathf.Clamp(m_LatitudeFadeAngle, 0.1f, 90f);
            m_GridResolution = GridResolution;
            m_GeodesicResolution = GeodesicResolution;
            m_MaxAlpha = MaxAlpha;
            m_EdgeAlpha = EdgeAlpha;
            m_FadeStartProportion = FadeStartProportion;
        }

        private void ApplyLatitudeFade()
        {
            var properties = new MaterialPropertyBlock();
            m_Renderer.GetPropertyBlock(properties);
            m_AppliedLatitudeFadeAngle = Mathf.Clamp(m_LatitudeFadeAngle, 0.1f, 90f);
            properties.SetFloat("_LatitudeFadeAngle", m_AppliedLatitudeFadeAngle * Mathf.Deg2Rad);
            m_Renderer.SetPropertyBlock(properties);
        }

        /// Sizes the unit-sized shield to cover bounds given in its parent's local space.
        public void Fit(Bounds parentLocalBounds, Transform mount, ShieldDef definition)
        {
            transform.localPosition = new Vector3(parentLocalBounds.center.x, parentLocalBounds.center.y, 0f);
            transform.localScale = new Vector3(parentLocalBounds.size.x, parentLocalBounds.size.y, 1f) * m_ShieldBloat;
            bool originalMesh = m_MeshType == ShieldMeshType.Grid && definition.HalfAngle >= 180f;
            var properties = new MaterialPropertyBlock();
            m_Renderer.GetPropertyBlock(properties);
            properties.SetFloat("_UseVertexColors", originalMesh ? 0f : 1f);
            properties.SetFloat("_ShieldAlpha", originalMesh ? MaxAlpha : 1f);
            m_AppliedLatitudeFadeAngle = Mathf.Clamp(m_LatitudeFadeAngle, 0.1f, 90f);
            properties.SetFloat("_LatitudeFadeAngle", m_AppliedLatitudeFadeAngle * Mathf.Deg2Rad);
            m_Renderer.SetPropertyBlock(properties);
            if (originalMesh && m_ArcMesh == null) return;
            if (m_Renderer.transform != transform || !m_Renderer.TryGetComponent(out MeshFilter filter))
                throw new System.InvalidOperationException("Directional shields require a mesh renderer and mesh filter on the shield visual root.");
            if (m_ArcMesh != null)
            {
                filter.sharedMesh = m_OriginalMesh;
                ReleaseArcMesh();
                m_ArcMesh = null;
            }
            if (originalMesh) return;

            Vector2 forward = transform.parent.InverseTransformDirection(mount.up);
            Vector3 scale = transform.localScale;
            if (forward.sqrMagnitude < 1e-10f || scale.x <= 0f || scale.y <= 0f)
                throw new System.InvalidOperationException("Directional shields require a planar mount Up direction and nonzero hull dimensions.");

            m_OriginalMesh = filter.sharedMesh;
            float fadeStartAngle = definition.HalfAngle * FadeStartProportion;
            switch (m_MeshType)
            {
                case ShieldMeshType.Grid:
                    m_ArcMesh = ShieldMeshGenerator.CreateArcMesh(forward, scale, definition.HalfAngle,
                        GridResolution, fadeStartAngle, MaxAlpha, EdgeAlpha);
                    break;
                case ShieldMeshType.Geodesic:
                    m_ArcMesh = ShieldMeshGenerator.CreateGeodesicArcMesh(forward, scale, definition.HalfAngle,
                        GeodesicResolution, fadeStartAngle, MaxAlpha, EdgeAlpha);
                    break;
                default:
                    throw new System.InvalidOperationException($"Unknown shield mesh type {m_MeshType}.");
            }
            filter.sharedMesh = m_ArcMesh;
        }

        private void OnDestroy()
        {
            if (m_ArcMesh != null) ReleaseArcMesh();
        }

        private void ReleaseArcMesh()
        {
            if (Application.isPlaying) Destroy(m_ArcMesh);
            else DestroyImmediate(m_ArcMesh);
        }

        public void Flash(float healthFraction)
        {
            Color color = Color.Lerp(m_EmptyColor, m_FullColor, healthFraction);
            m_Renderer.material.color = color;
            m_Alpha = 1f;
            m_Renderer.enabled = true;
        }

        public void SetHeld(bool held, float healthFraction)
        {
            if (held && (!m_Held || healthFraction != m_HeldHealthFraction))
            {
                m_HeldHealthFraction = healthFraction;
                Flash(healthFraction);
            }
            m_Held = held;
        }

        private void Update()
        {
            if (Mathf.Clamp(m_LatitudeFadeAngle, 0.1f, 90f) != m_AppliedLatitudeFadeAngle) ApplyLatitudeFade();
            if (m_Held || m_Alpha <= 0f) return;

            m_Alpha -= Time.deltaTime / m_FadeTime;
            Color color = m_Renderer.material.color;
            color.a = Mathf.Max(0f, m_Alpha);
            m_Renderer.material.color = color;
            if (m_Alpha <= 0f) m_Renderer.enabled = false;
        }
    }
}
