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
        private const int PointsPerPage = 20;
        private const float NavigationHeight = 40f;
        [SerializeField] private LobbyHullRenderer m_Renderer;
        [SerializeField] private RawImage m_Image;
        [SerializeField] private CanvasGroup m_Description;
        [SerializeField] private LoadoutPreviewMarker[] m_Markers;

        private readonly List<LoadoutPreviewMarker> m_RuntimeMarkers = new List<LoadoutPreviewMarker>();
        private RenderTexture m_Texture;
        private ShipLoadout m_Rendered;
        private Rect m_Frame;
        private bool m_HasRendered;
        private int m_Page;
        private LoadoutPreviewMarker m_PreviousPage;
        private LoadoutPreviewMarker m_NextPage;

        public void Show(ShipLoadout loadout) => Show(loadout, -1, null);

        public void Show(ShipLoadout loadout, int selected, Action<int> select)
        {
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !catalog.IsValid(loadout))
                throw new InvalidOperationException("The ship preview requires a valid loadout.");
            if (!m_HasRendered || m_Rendered.Hull != loadout.Hull ||
                !m_Rendered.Colour0.Equals(loadout.Colour0) || !m_Rendered.Colour1.Equals(loadout.Colour1))
            {
                if (!m_HasRendered || m_Rendered.Hull != loadout.Hull) m_Page = 0;
                ReleaseTexture();
                m_Texture = m_Renderer.Render(loadout, 1024, 744, out m_Frame);
                m_Image.texture = m_Texture;
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
            int pageCount = Mathf.Max(1, (points.Length + PointsPerPage - 1) / PointsPerPage);
            m_Page = Mathf.Clamp(m_Page, 0, pageCount - 1);
            int first = m_Page * PointsPerPage;
            int count = Mathf.Min(PointsPerPage, points.Length - first);
            while (m_RuntimeMarkers.Count < count)
                m_RuntimeMarkers.Add(Instantiate(m_Markers[0], m_Markers[0].transform.parent));
            foreach (LoadoutPreviewMarker marker in m_RuntimeMarkers) marker.gameObject.SetActive(false);
            if (m_PreviousPage != null) m_PreviousPage.gameObject.SetActive(false);
            if (m_NextPage != null) m_NextPage.gameObject.SetActive(false);
            ShipComponentDef[] components = catalog.Components;
            var positions = new List<Vector2>();
            Vector2 size = m_Image.rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = new Vector2(592f, 430f);
            Vector2 available = pageCount > 1 ? new Vector2(size.x, size.y - NavigationHeight) : size;
            bool useGrid = false;
            for (int i = 0; i < count; i++)
            {
                HullPoint point = points[first + i];
                Vector3 local = hull.transform.InverseTransformPoint(point.transform.position);
                if (!TrySeparate(Rect.PointToNormalized(m_Frame, local), positions, available, out Vector2 position))
                {
                    useGrid = true;
                    break;
                }
                positions.Add(position);
            }
            if (useGrid)
            {
                positions.Clear();
                for (int i = 0; i < count; i++)
                    positions.Add(new Vector2((i % 4 + 0.5f) / 4f, (i / 4 + 0.5f) / 5f));
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 position = positions[i];
                if (pageCount > 1) position.y = (position.y * available.y + NavigationHeight) / size.y;
                int captured = first + i;
                int component = loadout.MountComponents[captured];
                m_RuntimeMarkers[i].Bind(component < 0 ? null : components[component], position, captured == selected, () => select?.Invoke(captured));
            }
            if (pageCount > 1)
            {
                if (m_PreviousPage == null) m_PreviousPage = Instantiate(m_Markers[0], m_Markers[0].transform.parent);
                if (m_NextPage == null) m_NextPage = Instantiate(m_Markers[0], m_Markers[0].transform.parent);
                m_PreviousPage.gameObject.SetActive(false);
                m_NextPage.gameObject.SetActive(false);
                float y = NavigationHeight * 0.5f / size.y;
                if (m_Page > 0) m_PreviousPage.BindNavigation($"< {m_Page}/{pageCount}", new Vector2(0.35f, y),
                    () => { m_Page--; Show(m_Rendered, selected, select); });
                if (m_Page + 1 < pageCount) m_NextPage.BindNavigation($"{m_Page + 2}/{pageCount} >", new Vector2(0.65f, y),
                    () => { m_Page++; Show(m_Rendered, selected, select); });
            }
        }

        private static bool TrySeparate(Vector2 origin, List<Vector2> occupied, Vector2 size, out Vector2 position)
        {
            Vector2 margin = new Vector2(36f / size.x, 19f / size.y);
            for (int ring = 0; ring < 20; ring++)
            {
                int count = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? 0f : i * Mathf.PI * 2f / count;
                    Vector2 candidate = origin + new Vector2(Mathf.Cos(angle) * ring * 38f / size.x, Mathf.Sin(angle) * ring * 22f / size.y);
                    candidate.x = Mathf.Clamp(candidate.x, margin.x, 1f - margin.x);
                    candidate.y = Mathf.Clamp(candidate.y, margin.y, 1f - margin.y);
                    bool overlaps = occupied.Exists(other => Mathf.Abs(other.x - candidate.x) * size.x < 70f &&
                        Mathf.Abs(other.y - candidate.y) * size.y < 36f);
                    if (!overlaps) { position = candidate; return true; }
                }
            }
            position = default;
            return false;
        }

        public void SetDescriptionVisible(bool visible)
        {
            m_Description.alpha = visible ? 1 : 0;
            m_Description.blocksRaycasts = false;
        }

        private void OnDisable() => SetDescriptionVisible(false);
        private void OnDestroy() => ReleaseTexture();

        private void ReleaseTexture()
        {
            m_Image.texture = null;
            if (m_Texture == null) return;
            m_Texture.Release();
            if (Application.isPlaying) Destroy(m_Texture);
            else DestroyImmediate(m_Texture);
            m_Texture = null;
        }
    }
}
