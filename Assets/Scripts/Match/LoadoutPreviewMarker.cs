using System;
using PewPewPew.Ships;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    /// One clickable hull-point slot. Every visual part is authored in the LoadoutSlotMarker prefab;
    /// Bind only positions, sizes, shows and tints those parts.
    public class LoadoutPreviewMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Outline m_Outline;
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_Artwork;
        [SerializeField, Tooltip("Empty-slot type symbols in SlotTypes order: shield, gun, thruster, subsystem.")]
        private LobbySymbol[] m_TypeIcons;
        [SerializeField, Tooltip("Faint outlines of the small and medium slot sizes, in that order.")]
        private Image[] m_SizeGuides;
        [SerializeField] private Color m_IdleOutline = new Color(0.4f, 0.55f, 0.6f);
        [SerializeField] private Color m_SelectedOutline = Color.cyan;
        [SerializeField] private Color m_CompatibleOutline = Color.green;
        private Action<bool> m_Hover;
        private bool m_Hovered;

        /// Empty-slot type icons use fixed quadrants: shield top-left, gun top-right,
        /// thruster bottom-left, subsystem bottom-right.
        public static readonly ComponentType[] SlotTypes =
            { ComponentType.Shield, ComponentType.Gun, ComponentType.Thruster, ComponentType.SubSystem };

        public Image Artwork => m_Artwork;
        public LobbySymbol TypeIcon(ComponentType type) => m_TypeIcons[Array.IndexOf(SlotTypes, type)];
        public Image SizeGuide(ComponentSize size) => m_SizeGuides[(int)size];

        private static float Size(ComponentSize size, Vector3 sizes)
        {
            switch (size)
            {
                case ComponentSize.Small: return sizes.x;
                case ComponentSize.Medium: return sizes.y;
                case ComponentSize.Large: return sizes.z;
                default: throw new ArgumentOutOfRangeException(nameof(size));
            }
        }

        public void Bind(HullPoint point, ShipComponentDef definition, Vector2 position, float angle, Vector3 sizes, float padding,
            bool selected, bool compatible, Action clicked, Action<bool> hover)
        {
            if (m_TypeIcons == null || m_TypeIcons.Length != SlotTypes.Length || m_SizeGuides == null || m_SizeGuides.Length != 2)
                throw new InvalidOperationException($"{name} is missing its authored type icons or size guides.");
            m_Hover = hover;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = position;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            float slot = Size(point.Size, sizes);
            rect.sizeDelta = Vector2.one * slot;
            m_Outline.effectColor = selected ? m_SelectedOutline : compatible ? m_CompatibleOutline : m_IdleOutline;
            m_Button.onClick.RemoveAllListeners();
            m_Button.onClick.AddListener(() => clicked?.Invoke());

            bool fitted = definition != null;
            float fill = Mathf.Max(0, slot - 2 * padding);
            // Fitted components are drawn at their own size class, so a small component stays small in a large slot.
            float fittedSize = fitted ? Mathf.Max(0, Size(definition.Size, sizes) - 2 * padding) : 0;
            Quaternion iconRotation = fitted
                ? Quaternion.Euler(0f, 0f, LobbySymbol.ComponentRotation(definition, angle) - angle)
                : Quaternion.identity;
            m_Artwork.sprite = fitted ? definition.ButtonArtwork : null;
            m_Artwork.enabled = m_Artwork.sprite != null;
            m_Artwork.color = fitted ? definition.Tint : Color.white;
            Place(m_Artwork.rectTransform, Vector2.zero, fittedSize);
            m_Artwork.rectTransform.localRotation = iconRotation;

            for (int i = 0; i < m_SizeGuides.Length; i++)
            {
                var guideSize = (ComponentSize)i;
                m_SizeGuides[i].gameObject.SetActive(guideSize < point.Size);
                Place(m_SizeGuides[i].rectTransform, Vector2.zero, Size(guideSize, sizes));
            }

            int acceptedTypes = 0;
            foreach (ComponentType type in SlotTypes) if (Accepts(point, type)) acceptedTypes++;
            float quadrant = Mathf.Max(0, (slot - 3 * padding) / 2);
            float offset = (quadrant + padding) / 2;
            for (int i = 0; i < SlotTypes.Length; i++)
            {
                LobbySymbol symbol = m_TypeIcons[i];
                symbol.color = fitted ? definition.Tint : Color.white;
                symbol.gameObject.SetActive(fitted
                    ? definition.ButtonArtwork == null && definition.Type == SlotTypes[i]
                    : Accepts(point, SlotTypes[i]));
                symbol.rectTransform.localRotation = iconRotation;
                if (fitted) Place(symbol.rectTransform, Vector2.zero, fittedSize);
                else if (acceptedTypes == 1) Place(symbol.rectTransform, Vector2.zero, fill);
                else Place(symbol.rectTransform, new Vector2(i % 2 == 0 ? -offset : offset, i < 2 ? offset : -offset), quadrant);
            }
            gameObject.SetActive(true);
        }

        private static bool Accepts(HullPoint point, ComponentType type) =>
            (point.Accepts & (AcceptedComponents)(1 << (int)type)) != 0;

        private static void Place(RectTransform rect, Vector2 centre, float size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = Vector2.one * size;
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Hovered = true;
            m_Hover?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData) => ClearHover();
        private void OnDisable() => ClearHover();

        private void ClearHover()
        {
            if (m_Hovered) m_Hover?.Invoke(false);
            m_Hovered = false;
        }
    }
}
