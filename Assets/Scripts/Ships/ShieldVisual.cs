using UnityEngine;

namespace PewPewPew.Ships
{
    /// Brief ellipse flash on the ship when its shield absorbs damage; colour shows remaining shield health.
    public class ShieldVisual : MonoBehaviour
    {
        [SerializeField] private Renderer m_Renderer;
        [SerializeField] private Color m_FullColor = Color.green;
        [SerializeField] private Color m_EmptyColor = Color.red;
        [SerializeField] private float m_FadeTime = 0.3f;

        private float m_Alpha;

        private void Awake() => m_Renderer.enabled = false;

        /// Sizes the unit-sized shield to cover bounds given in its parent's local space.
        public void Fit(Bounds parentLocalBounds)
        {
            transform.localPosition = new Vector3(parentLocalBounds.center.x, parentLocalBounds.center.y, 0f);
            transform.localScale = new Vector3(parentLocalBounds.size.x, parentLocalBounds.size.y, 1f);
        }

        public void Flash(float healthFraction)
        {
            Color color = Color.Lerp(m_EmptyColor, m_FullColor, healthFraction);
            m_Renderer.material.color = color;
            m_Alpha = 1f;
            m_Renderer.enabled = true;
        }

        private void Update()
        {
            if (m_Alpha <= 0f) return;

            m_Alpha -= Time.deltaTime / m_FadeTime;
            Color color = m_Renderer.material.color;
            color.a = Mathf.Max(0f, m_Alpha);
            m_Renderer.material.color = color;
            if (m_Alpha <= 0f) m_Renderer.enabled = false;
        }
    }
}
