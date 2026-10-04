using UnityEngine;

namespace PewPewPew.Ships
{
    /// Scene anchor for the local ship's HUD, placed in the lower-left of a Canvas. Rows run top to bottom: three bars,
    /// then weapons, then sub systems as squares one quarter of the row wide.
    public class ShipHudRoot : MonoBehaviour
    {
        public const int SubSystemRow = 4;

        [SerializeField] private float m_Width = 240f;
        [SerializeField] private float m_BarHeight = 16f;
        [SerializeField] private float m_Gap = 4f;

        public static ShipHudRoot Instance { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Sets the element's parent, anchors and size so it fills one cell of the given row.
        public void Place(RectTransform element, int row, int column, int columns)
        {
            float cellWidth = (m_Width - m_Gap * (columns - 1)) / columns;
            float subSystemSize = (m_Width - m_Gap * 3f) / 4f;
            float height = row == SubSystemRow ? subSystemSize : m_BarHeight;
            float bottom = row == SubSystemRow ? 0f : subSystemSize + m_Gap + (SubSystemRow - 1 - row) * (m_BarHeight + m_Gap);

            element.SetParent(transform, false);
            element.anchorMin = Vector2.zero;
            element.anchorMax = Vector2.zero;
            element.pivot = Vector2.zero;
            element.anchoredPosition = new Vector2(column * (cellWidth + m_Gap), bottom);
            element.sizeDelta = new Vector2(cellWidth, height);
        }
    }
}
