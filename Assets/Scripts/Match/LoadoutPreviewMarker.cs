using System;
using PewPewPew.Ships;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LoadoutPreviewMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image m_Artwork;
        [SerializeField] private TMP_Text m_Label;
        private Action<bool> m_Hover;
        private bool m_Hovered;

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

        public void Bind(HullPoint point, ShipComponentDef definition, Vector2 position, Vector3 sizes, bool selected,
            bool compatible, Action clicked, Action<bool> hover)
        {
            m_Hover = hover;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = position;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * Size(point.Size, sizes);
            Image background = GetComponent<Image>();
            background.raycastTarget = true;
            background.color = new Color(0.025f, 0.07f, 0.1f, 0.85f);
            Outline outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectDistance = Vector2.one * 2f;
            outline.effectColor = selected ? Color.cyan : compatible ? Color.green : new Color(0.4f, 0.55f, 0.6f);
            Button button = GetComponent<Button>();
            if (button == null) button = gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
            m_Artwork.sprite = definition != null ? definition.ButtonArtwork : null;
            m_Artwork.enabled = m_Artwork.sprite != null;
            m_Artwork.color = definition != null ? definition.Tint : Color.white;
            m_Artwork.preserveAspect = true;
            m_Artwork.rectTransform.anchorMin = m_Artwork.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            m_Artwork.rectTransform.anchoredPosition = Vector2.zero;
            m_Artwork.rectTransform.sizeDelta = Vector2.one * (definition != null ? Size(definition.Size, sizes) * 0.8f : 0f);
            m_Label.gameObject.SetActive(false);
            ComponentType[] types = { ComponentType.Shield, ComponentType.Gun, ComponentType.Thruster, ComponentType.SubSystem };
            for (int i = 0; i < types.Length; i++)
            {
                string name = "TypeIcon" + i;
                Transform child = transform.Find(name);
                LobbySymbol symbol;
                if (child == null)
                {
                    var icon = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbySymbol));
                    icon.transform.SetParent(transform, false);
                    symbol = icon.GetComponent<LobbySymbol>();
                    symbol.raycastTarget = false;
                }
                else symbol = child.GetComponent<LobbySymbol>();
                symbol.Kind = LobbySymbol.ForComponent(types[i]);
                symbol.color = definition != null ? definition.Tint : Color.white;
                bool show = definition == null
                    ? (point.Accepts & (AcceptedComponents)(1 << (int)types[i])) != 0
                    : definition.ButtonArtwork == null && definition.Type == types[i];
                symbol.gameObject.SetActive(show);
                RectTransform iconRect = symbol.rectTransform;
                if (definition == null)
                {
                    float x = i % 2 == 0 ? 0.25f : 0.75f;
                    float y = i < 2 ? 0.75f : 0.25f;
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(x, y);
                    iconRect.sizeDelta = Vector2.one * Size(point.Size, sizes) * 0.4f;
                }
                else
                {
                    iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = Vector2.one * Size(definition.Size, sizes) * 0.8f;
                }
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
            }
            gameObject.SetActive(true);
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
