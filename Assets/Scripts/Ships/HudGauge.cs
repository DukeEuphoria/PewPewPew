using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Ships
{
    /// Root of a HUD bar or radial prefab; the ship drives the fill amount and colour of its filled Image.
    public class HudGauge : MonoBehaviour
    {
        [SerializeField] private Image m_Fill;

        public RectTransform Rect => (RectTransform)transform;

        public void SetFill(float fraction) => m_Fill.fillAmount = Mathf.Clamp01(fraction);

        public void SetColor(Color color) => m_Fill.color = color;
    }
}
