using System;
using System.Collections.Generic;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LoadoutShipPreview : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private LobbyHullRenderer m_Renderer;
        [SerializeField] private RawImage m_Image;
        [SerializeField] private CanvasGroup m_Description;
        [SerializeField] private LoadoutPreviewMarker[] m_Markers;
        [SerializeField] private Vector3 m_MarkerSizes = new Vector3(30, 42, 56);
        [SerializeField, Tooltip("Pixels between a slot's edge and its icons, and between quadrant icons.")]
        private float m_SlotIconPadding = 3;
        [SerializeField, Min(1), Tooltip("Maximum magnification relative to the full-ship view.")]
        private float m_MaxZoom = 4;
        [SerializeField, Min(0), Tooltip("Zoom sensitivity per UI scroll unit.")]
        private float m_ZoomSensitivity = 0.03f;

        /// Small, medium and large slot sizes; the catalog scales its icons by the same ratios.
        public Vector3 MarkerSizes => m_MarkerSizes;

        private readonly List<LoadoutPreviewMarker> m_RuntimeMarkers = new List<LoadoutPreviewMarker>();
        private readonly List<Vector2> m_PointPositions = new List<Vector2>();
        private RenderTexture m_Texture;
        private ShipLoadout m_Rendered;
        private Rect m_Frame;
        private bool m_HasRendered;
        private Color32[] m_HullPixels;
        private float m_Zoom = 1;
        private Vector2 m_ViewCentre = new Vector2(0.5f, 0.5f);
        private bool m_Panning;
        private Vector2 m_LastPanPosition;
        public Action<bool> HullHover { get; set; }

        public void Show(ShipLoadout loadout) => Show(loadout, -1, null);

        public void Show(ShipLoadout loadout, int selected, Action<int> select) =>
            Show(loadout, selected, null, select, null);

        public void Show(ShipLoadout loadout, int selected, ShipComponentDef selectedComponent,
            Action<int> select, Action<int, bool> hover)
        {
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !catalog.IsValid(loadout))
                throw new InvalidOperationException("The ship preview requires a valid loadout.");
            if (!m_HasRendered || m_Rendered.Hull != loadout.Hull)
            {
                m_Zoom = 1;
                m_ViewCentre = new Vector2(0.5f, 0.5f);
                m_Panning = false;
                UpdateView();
            }
            if (!m_HasRendered || m_Rendered.Hull != loadout.Hull ||
                !m_Rendered.Colour0.Equals(loadout.Colour0) || !m_Rendered.Colour1.Equals(loadout.Colour1))
            {
                ReleaseTexture();
                m_Texture = m_Renderer.Render(loadout, 1024, 744, out m_Frame, true);
                m_Image.texture = m_Texture;
                ReadHullPixels();
            }
            m_Rendered = loadout;
            m_HasRendered = true;
            if (m_RuntimeMarkers.Count == 0)
            {
                if (m_Markers == null || m_Markers.Length == 0)
                    throw new InvalidOperationException("The ship preview has no marker template.");
                m_RuntimeMarkers.AddRange(m_Markers);
                m_Markers[0].transform.parent.SetAsLastSibling();
            }
            HullPoints hull = catalog.Hulls[loadout.Hull].HullPoints;
            HullPoint[] points = hull.Points;
            int count = points.Length;
            while (m_RuntimeMarkers.Count < count)
                m_RuntimeMarkers.Add(Instantiate(m_Markers[0], m_Markers[0].transform.parent));
            foreach (LoadoutPreviewMarker marker in m_RuntimeMarkers) marker.gameObject.SetActive(false);
            m_PointPositions.Clear();
            ShipComponentDef[] components = catalog.Components;
            for (int i = 0; i < count; i++)
            {
                HullPoint point = points[i];
                Vector3 local = hull.transform.InverseTransformPoint(point.transform.position);
                Vector2 position = Rect.PointToNormalized(m_Frame, local);
                m_PointPositions.Add(position);
                // The preview camera looks along hull-local Z, so the point's local XY up is its on-screen up.
                Vector2 up = hull.transform.InverseTransformDirection(point.transform.up);
                float angle = up == Vector2.zero ? 0 : Vector2.SignedAngle(Vector2.up, up);
                int captured = i;
                int component = loadout.MountComponents[captured];
                m_RuntimeMarkers[i].Bind(point, component < 0 ? null : components[component], ViewPosition(position), angle, m_MarkerSizes,
                    m_SlotIconPadding, captured == selected, selectedComponent != null && !selectedComponent.IsLocked &&
                    point.AcceptsComponent(selectedComponent), () => select?.Invoke(captured),
                    inside => hover?.Invoke(captured, inside));
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!m_HasRendered || eventData.scrollDelta.y == 0) return;
            m_Zoom = Mathf.Clamp(m_Zoom * Mathf.Exp(eventData.scrollDelta.y * m_ZoomSensitivity),
                1, Mathf.Max(1, m_MaxZoom));
            UpdateView();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!m_HasRendered || eventData.button != PointerEventData.InputButton.Right) return;
            m_Panning = RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Image.rectTransform,
                eventData.pressPosition, eventData.pressEventCamera, out m_LastPanPosition);
            Pan(eventData);
        }

        public void OnDrag(PointerEventData eventData) => Pan(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) m_Panning = false;
        }

        private void Pan(PointerEventData eventData)
        {
            if (!m_Panning || eventData.button != PointerEventData.InputButton.Right ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Image.rectTransform,
                    eventData.position, eventData.pressEventCamera, out Vector2 position)) return;
            Vector2 size = m_Image.rectTransform.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            Vector2 delta = position - m_LastPanPosition;
            m_LastPanPosition = position;
            m_ViewCentre -= new Vector2(delta.x / size.x, delta.y / size.y) / m_Zoom;
            UpdateView();
        }

        private void UpdateView()
        {
            float half = 0.5f / m_Zoom;
            m_ViewCentre.x = Mathf.Clamp(m_ViewCentre.x, half, 1 - half);
            m_ViewCentre.y = Mathf.Clamp(m_ViewCentre.y, half, 1 - half);
            m_Image.uvRect = new Rect(m_ViewCentre - Vector2.one * half, Vector2.one / m_Zoom);
            for (int i = 0; i < m_PointPositions.Count; i++)
            {
                var marker = (RectTransform)m_RuntimeMarkers[i].transform;
                marker.anchorMin = marker.anchorMax = ViewPosition(m_PointPositions[i]);
            }
        }

        private Vector2 ViewPosition(Vector2 position) => (position - m_ViewCentre) * m_Zoom + Vector2.one * 0.5f;

        public void SetDescriptionVisible(bool visible)
        {
            m_Description.alpha = 1;
            m_Description.blocksRaycasts = true;
            HullHover?.Invoke(visible);
        }

        public bool ContainsHull(Vector2 screenPosition, Camera eventCamera)
        {
            if (m_HullPixels == null || m_Texture == null) return false;
            RectTransform rect = m_Image.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out Vector2 local))
                return false;
            Vector2 position = Rect.PointToNormalized(rect.rect, local);
            if (position.x < 0 || position.x >= 1 || position.y < 0 || position.y >= 1) return false;
            Vector2 uv = m_Image.uvRect.position + Vector2.Scale(position, m_Image.uvRect.size);
            int x = Mathf.FloorToInt(uv.x * m_Texture.width);
            int y = Mathf.FloorToInt(uv.y * m_Texture.height);
            return m_HullPixels[y * m_Texture.width + x].a != 0;
        }

        private void ReadHullPixels()
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(m_Texture.width, m_Texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = m_Texture;
                image.ReadPixels(new Rect(0, 0, m_Texture.width, m_Texture.height), 0, 0);
                image.Apply();
                m_HullPixels = image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                if (Application.isPlaying) Destroy(image);
                else DestroyImmediate(image);
            }
        }

        private void OnDisable()
        {
            m_Panning = false;
            SetDescriptionVisible(false);
        }
        private void OnDestroy() => ReleaseTexture();

        private void ReleaseTexture()
        {
            m_Image.texture = null;
            m_HullPixels = null;
            if (m_Texture == null) return;
            m_Texture.Release();
            if (Application.isPlaying) Destroy(m_Texture);
            else DestroyImmediate(m_Texture);
            m_Texture = null;
        }
    }
}
