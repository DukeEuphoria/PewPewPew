using UnityEngine;

namespace PewPewPew.Ships
{
    public class ShipHudRoot : MonoBehaviour
    {
        private const int SubSystemColumns = 4;
        private const float DefaultPaperdollSize = 360f;
        [SerializeField] private float m_Width = 240f;
        [SerializeField] private float m_BarHeight = 16f;
        [SerializeField] private float m_Gap = 4f;
        [SerializeField, Min(1f)] private float m_PaperdollSize = DefaultPaperdollSize;
        [SerializeField, Range(0f, 1f)] private float m_PaperdollBrightness = 0.35f;
        [SerializeField, Min(0.01f)] private float m_PaperdollMarkerScale = 1f;

        public static ShipHudRoot Instance { get; private set; }
        public float PaperdollSize => Mathf.Max(1f, m_PaperdollSize);
        public float PaperdollBrightness => Mathf.Clamp01(m_PaperdollBrightness);
        public float PaperdollMarkerScale => Mathf.Max(0.01f, m_PaperdollMarkerScale);

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void PlaceBar(RectTransform element, int row, int barCount)
        {
            float bottom = (barCount - row - 1) * (m_BarHeight + m_Gap);
            Place(element, new Vector2(PaperdollSize + m_Gap + m_Width * 0.5f, bottom + m_BarHeight * 0.5f),
                new Vector2(m_Width, m_BarHeight));
            HudGauge gauge = element.GetComponent<HudGauge>();
            if (gauge != null && gauge.PlaceIcon(m_Gap))
                element.anchoredPosition += Vector2.right * (m_BarHeight + m_Gap);
        }

        public void PlacePaperdoll(RectTransform element)
        {
            float size = PaperdollSize;
            Place(element, Vector2.one * (size * 0.5f), Vector2.one * size);
        }

        public void PlaceSubSystem(RectTransform element, int index)
        {
            float size = (m_Width - m_Gap * (SubSystemColumns - 1)) / SubSystemColumns;
            int row = index / SubSystemColumns;
            int column = index % SubSystemColumns;
            Place(element, new Vector2(column * (size + m_Gap) + size * 0.5f, row * (size + m_Gap) + size * 0.5f));
            Vector2 elementSize = element.rect.size;
            if (elementSize.x <= 0f || elementSize.y <= 0f)
            {
                Debug.LogError("HUD subsystem must have a positive width and height.", element);
                return;
            }

            element.localScale = new Vector3(size / elementSize.x, size / elementSize.y, 1f);
        }

        private void Place(RectTransform element, Vector2 position, Vector2 size)
        {
            Place(element, position);
            element.sizeDelta = size;
        }

        private void Place(RectTransform element, Vector2 position)
        {
            element.SetParent(transform, false);
            element.anchorMin = Vector2.zero;
            element.anchorMax = Vector2.zero;
            element.pivot = Vector2.one * 0.5f;
            element.anchoredPosition = position;
        }
    }
}
