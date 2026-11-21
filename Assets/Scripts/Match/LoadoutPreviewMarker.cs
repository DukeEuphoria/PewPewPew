using System;
using PewPewPew.Ships;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LoadoutPreviewMarker : MonoBehaviour
    {
        [SerializeField] private Image m_Artwork;
        [SerializeField] private TMP_Text m_Label;

        public void Bind(ShipComponentDef definition, Vector2 position, bool selected, Action clicked)
        {
            BindContent(definition != null ? definition.DisplayName : "Empty",
                definition != null ? definition.ButtonArtwork : null, position, selected, clicked);
        }

        public void BindNavigation(string label, Vector2 position, Action clicked)
        {
            BindContent(label, null, position, false, clicked);
        }

        private void BindContent(string label, Sprite artwork, Vector2 position, bool selected, Action clicked)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = position;
            rect.anchoredPosition = Vector2.zero;
            Image background = GetComponent<Image>();
            background.raycastTarget = true;
            background.color = selected ? new Color(0.1f, 0.55f, 0.65f, 0.95f) : new Color(0.025f, 0.07f, 0.1f, 0.85f);
            Button button = GetComponent<Button>();
            if (button == null) button = gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
            m_Artwork.sprite = artwork;
            m_Artwork.enabled = m_Artwork.sprite != null;
            m_Label.text = label;
            m_Label.gameObject.SetActive(m_Artwork.sprite == null);
            gameObject.SetActive(true);
        }
    }
}
