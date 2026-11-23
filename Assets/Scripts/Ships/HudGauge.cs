using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Ships
{
    /// Root of a HUD bar or radial prefab; the ship drives the fill amount and colour of its filled Image.
    public class HudGauge : MonoBehaviour
    {
        [SerializeField] private Image m_Fill;
        [SerializeField] private RawImage m_Icon;
        private Graphic[] m_Graphics;
        private Color[] m_GraphicColors;

        public RectTransform Rect => (RectTransform)transform;
        public Color FillColor => m_Fill != null ? m_Fill.color : Color.white;

        public bool PlaceIcon(float gap)
        {
            if (m_Icon == null) return false;

            m_Icon.rectTransform.anchorMin = m_Icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            m_Icon.rectTransform.pivot = new Vector2(1f, 0.5f);
            m_Icon.rectTransform.anchoredPosition = new Vector2(-gap, 0f);
            Vector2 size = m_Icon.rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                Debug.LogError("HUD gauge icon must have a positive width and height.", this);
                return false;
            }

            float height = Rect.rect.height;
            m_Icon.rectTransform.localScale = new Vector3(height / size.x, height / size.y, 1f);
            return true;
        }

        public void SetFill(float fraction)
        {
            if (m_Fill != null) m_Fill.fillAmount = Mathf.Clamp01(fraction);
        }

        public void SetColor(Color color)
        {
            if (m_Fill != null) m_Fill.color = color;
        }

        public void SetDimmed(bool dimmed)
        {
            if (m_Graphics == null)
            {
                m_Graphics = GetComponentsInChildren<Graphic>(true);
                m_GraphicColors = new Color[m_Graphics.Length];
                for (int i = 0; i < m_Graphics.Length; i++)
                    m_GraphicColors[i] = m_Graphics[i].color;
            }

            for (int i = 0; i < m_Graphics.Length; i++)
            {
                Graphic graphic = m_Graphics[i];
                if (graphic == m_Fill) continue;
                Color color = m_GraphicColors[i];
                graphic.color = dimmed ? new Color(0.4f, 0.4f, 0.4f, color.a) : color;
            }
        }
    }
}
