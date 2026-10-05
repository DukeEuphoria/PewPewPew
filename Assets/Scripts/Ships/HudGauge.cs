using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Ships
{
    /// Root of a HUD bar or radial prefab; the ship drives the fill amount and colour of its filled Image.
    public class HudGauge : MonoBehaviour
    {
        [SerializeField] private Image m_Fill;
        [SerializeField] private RawImage m_Icon;

        public RectTransform Rect => (RectTransform)transform;

        public bool PlaceIcon(float gap)
        {
            if (m_Icon == null) return false;

            m_Icon.rectTransform.anchorMin = m_Icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            m_Icon.rectTransform.pivot = new Vector2(1f, 0.5f);
            m_Icon.rectTransform.anchoredPosition = new Vector2(-gap, 0f);
            m_Icon.rectTransform.sizeDelta = Vector2.one * Rect.rect.height;
            m_Icon.rectTransform.localScale = Vector3.one;
            return true;
        }

        public void SetFill(float fraction)
        {
            if (m_Fill != null) m_Fill.fillAmount = Mathf.Clamp01(fraction);
        }

        public void SetColor(Color color) => m_Fill.color = color;
    }
}
