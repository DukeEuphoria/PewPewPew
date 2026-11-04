using UnityEngine;
using UnityEngine.EventSystems;

namespace PewPewPew.Match
{
    public class LoadoutPreviewHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private LoadoutShipPreview m_Preview;

        public void OnPointerEnter(PointerEventData eventData) => m_Preview.SetDescriptionVisible(true);
        public void OnPointerExit(PointerEventData eventData) => m_Preview.SetDescriptionVisible(false);
        private void OnDisable() => m_Preview.SetDescriptionVisible(false);
    }
}
