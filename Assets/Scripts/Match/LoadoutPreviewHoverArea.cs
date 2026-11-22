using UnityEngine;
using UnityEngine.EventSystems;

namespace PewPewPew.Match
{
    public class LoadoutPreviewHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
    {
        [SerializeField] private LoadoutShipPreview m_Preview;

        public void OnPointerEnter(PointerEventData eventData) => m_Preview.SetDescriptionVisible(true);
        public void OnPointerExit(PointerEventData eventData) => m_Preview.SetDescriptionVisible(false);
        public bool IsRaycastLocationValid(Vector2 screenPosition, Camera eventCamera) =>
            m_Preview.ContainsHull(screenPosition, eventCamera);
        private void OnDisable() => m_Preview.SetDescriptionVisible(false);
    }
}
