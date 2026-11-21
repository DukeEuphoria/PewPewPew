using UnityEngine;

namespace PewPewPew.Ships
{
    public class ShipHudRoot : MonoBehaviour
    {
        private const int SubSystemColumns = 4;
        [SerializeField] private float m_Width = 240f;
        [SerializeField] private float m_BarHeight = 16f;
        [SerializeField] private float m_Gap = 4f;

        public static ShipHudRoot Instance { get; private set; }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void PlaceBar(RectTransform element, int row, int barCount, int subSystemCount)
        {
            int subRows = (subSystemCount + SubSystemColumns - 1) / SubSystemColumns;
            float subSize = (m_Width - m_Gap * (SubSystemColumns - 1)) / SubSystemColumns;
            float bottom = subRows * (subSize + m_Gap) + (barCount - row - 1) * (m_BarHeight + m_Gap);
            Place(element, new Vector2(m_Width * 0.5f, bottom + m_BarHeight * 0.5f),
                new Vector2(m_Width, m_BarHeight));
            HudGauge gauge = element.GetComponent<HudGauge>();
            if (gauge != null && gauge.PlaceIcon(m_Gap))
                element.anchoredPosition += Vector2.right * (m_BarHeight + m_Gap);
        }

        public void PlaceSubSystem(RectTransform element, int index)
        {
            float size = (m_Width - m_Gap * (SubSystemColumns - 1)) / SubSystemColumns;
            int row = index / SubSystemColumns;
            int column = index % SubSystemColumns;
            Place(element, new Vector2(column * (size + m_Gap) + size * 0.5f, row * (size + m_Gap) + size * 0.5f),
                Vector2.one * size);
        }

        private void Place(RectTransform element, Vector2 position, Vector2 size)
        {
            element.SetParent(transform, false);
            element.anchorMin = Vector2.zero;
            element.anchorMax = Vector2.zero;
            element.pivot = Vector2.one * 0.5f;
            element.anchoredPosition = position;
            element.sizeDelta = size;
        }
    }
}
