using System;
using System.Collections.Generic;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LoadoutShipPreview : MonoBehaviour
    {
        [SerializeField] private LobbyHullRenderer m_Renderer;
        [SerializeField] private RawImage m_Image;
        [SerializeField] private CanvasGroup m_Description;
        [SerializeField] private LoadoutPreviewMarker[] m_Markers;
        [SerializeField] private Vector3 m_MarkerSizes = new Vector3(30, 42, 56);
        [SerializeField, Tooltip("Pixels between a slot's edge and its icons, and between quadrant icons.")]
        private float m_SlotIconPadding = 3;

        /// Small, medium and large slot sizes; the catalog scales its icons by the same ratios.
        public Vector3 MarkerSizes => m_MarkerSizes;

        private readonly List<LoadoutPreviewMarker> m_RuntimeMarkers = new List<LoadoutPreviewMarker>();
        private RenderTexture m_Texture;
        private ShipLoadout m_Rendered;
        private Rect m_Frame;
        private bool m_HasRendered;
        private Color32[] m_HullPixels;
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
            ShipComponentDef[] components = catalog.Components;
            for (int i = 0; i < count; i++)
            {
                HullPoint point = points[i];
                Vector3 local = hull.transform.InverseTransformPoint(point.transform.position);
                Vector2 position = Rect.PointToNormalized(m_Frame, local);
                // The preview camera looks along hull-local Z, so the point's local XY up is its on-screen up.
                Vector2 up = hull.transform.InverseTransformDirection(point.transform.up);
                float angle = up == Vector2.zero ? 0 : Vector2.SignedAngle(Vector2.up, up);
                int captured = i;
                int component = loadout.MountComponents[captured];
                m_RuntimeMarkers[i].Bind(point, component < 0 ? null : components[component], position, angle, m_MarkerSizes,
                    m_SlotIconPadding, captured == selected, selectedComponent != null && !selectedComponent.IsLocked &&
                    point.AcceptsComponent(selectedComponent), () => select?.Invoke(captured),
                    inside => hover?.Invoke(captured, inside));
            }
        }

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
            Vector2 uv = Rect.PointToNormalized(rect.rect, local);
            if (uv.x < 0 || uv.x >= 1 || uv.y < 0 || uv.y >= 1) return false;
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

        private void OnDisable() => SetDescriptionVisible(false);
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
