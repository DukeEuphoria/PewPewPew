using System;
using PewPewPew.Ships;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LoadoutComponentButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_Artwork;
        [SerializeField] private TMP_Text m_NameLabel;
        [SerializeField] private TMP_Text m_Price;
        [SerializeField] private GameObject m_Selected;

        private Action m_Click;
        private Action<string> m_Hover;
        private string m_Details;
        private bool m_Hovered;

        public void Initialize(Action click, Action<string> hover)
        {
            m_Click = click;
            m_Hover = hover;
            m_Button.onClick.AddListener(Click);
        }

        public void Bind(ShipComponentDef definition, bool selected, string unavailable)
        {
            gameObject.SetActive(definition != null);
            if (definition == null) return;
            m_NameLabel.text = definition.DisplayName;
            m_Artwork.sprite = definition.ButtonArtwork;
            m_Artwork.gameObject.SetActive(definition.ButtonArtwork != null);
            m_Price.text = definition.IsLocked ? $"LOCKED - {definition.Price}" : string.Empty;
            m_Selected.SetActive(selected);
            m_Button.interactable = unavailable == null;
            m_Details = definition.DisplayName + "\n" + definition.Description;
            if (unavailable != null) m_Details += "\n\n" + unavailable;
            if (m_Hovered) m_Hover?.Invoke(m_Details);
        }

        private void Click() => m_Click?.Invoke();
        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Hovered = true;
            m_Hover?.Invoke(m_Details);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Hovered = false;
            m_Hover?.Invoke(null);
        }

        private void OnDisable()
        {
            if (m_Hovered) m_Hover?.Invoke(null);
            m_Hovered = false;
        }

        public void OnSelect(BaseEventData eventData) => m_Hover?.Invoke(m_Details);
        public void OnDeselect(BaseEventData eventData) => m_Hover?.Invoke(null);
    }
}
