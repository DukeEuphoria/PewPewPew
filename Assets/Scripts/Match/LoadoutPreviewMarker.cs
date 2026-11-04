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

        public void Bind(ShipComponentDef definition, Vector2 position)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = position;
            rect.anchoredPosition = Vector2.zero;
            m_Artwork.sprite = definition.ButtonArtwork;
            m_Artwork.enabled = definition.ButtonArtwork != null;
            m_Label.text = definition.DisplayName;
            m_Label.gameObject.SetActive(definition.ButtonArtwork == null);
            gameObject.SetActive(true);
        }
    }
}
