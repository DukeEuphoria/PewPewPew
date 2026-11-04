using System;
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

        private RenderTexture m_Texture;
        private ShipLoadout m_Rendered;
        private Rect m_Frame;
        private bool m_HasRendered;

        public void Show(ShipLoadout loadout)
        {
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !catalog.IsValid(loadout))
                throw new InvalidOperationException("The ship preview requires an in-range loadout.");
            if (!m_HasRendered || m_Rendered.Hull != loadout.Hull ||
                !m_Rendered.Colour0.Equals(loadout.Colour0) || !m_Rendered.Colour1.Equals(loadout.Colour1))
            {
                ReleaseTexture();
                m_Texture = m_Renderer.Render(loadout, 1024, 744, out m_Frame);
                m_Image.texture = m_Texture;
            }
            if (m_HasRendered && m_Rendered.Equals(loadout)) return;
            m_Rendered = loadout;
            m_HasRendered = true;
            foreach (LoadoutPreviewMarker marker in m_Markers) marker.gameObject.SetActive(false);
            HullDef hull = catalog.Hulls[loadout.Hull];
            int next = 0;
            // These are the same selected firing/mount points used by Ship.Initialize.
            AddPoints(hull, catalog.MainGuns[loadout.MainGun],
                hull.SelectPoints(hull.MainWeaponPoints, catalog.MainGuns[loadout.MainGun].MaxEmissionPointsConsidered), ref next);
            AddPoints(hull, catalog.SecondaryGuns[loadout.SecondaryGun],
                hull.SelectPoints(hull.SecondaryWeaponPoints, catalog.SecondaryGuns[loadout.SecondaryGun].MaxEmissionPointsConsidered), ref next);
            AddPoints(hull, catalog.Thrusters[loadout.Thruster],
                hull.SelectPoints(hull.ThrusterPoints, catalog.Thrusters[loadout.Thruster].EmissionPointsUsed), ref next);
            Transform[] mounts = hull.HullPoints.SubSystems;
            for (int slot = 0; slot < hull.SubSystemSlots; slot++)
            {
                int component = loadout.GetSubSystem(slot);
                if (component < 0) continue;
                if (mounts == null || slot >= mounts.Length || mounts[slot] == null)
                {
                    Debug.LogWarning($"{hull.DisplayName}: no preview attachment point for fitted subsystem slot {slot + 1}.", this);
                    continue;
                }
                AddPoints(hull, catalog.SubSystems[component], new[] { mounts[slot] }, ref next);
            }
            // Shields surround the hull rather than occupying an emission point.
            AddMarker(catalog.Shields[loadout.Shield], Vector3.zero, ref next);
        }

        private void AddPoints(HullDef hull, ShipComponentDef definition, Transform[] points, ref int next)
        {
            if (points == null) return; // Incompatible selections are explained by the deploy status.
            foreach (Transform point in points)
            {
                Vector3 local = hull.HullPoints.transform.InverseTransformPoint(point.position);
                AddMarker(definition, local, ref next);
            }
        }

        private void AddMarker(ShipComponentDef definition, Vector3 local, ref int next)
        {
            if (next >= m_Markers.Length) throw new InvalidOperationException("The static ship preview marker capacity is insufficient.");
            Vector2 position = Rect.PointToNormalized(m_Frame, local);
            m_Markers[next++].Bind(definition, position);
        }

        public void SetDescriptionVisible(bool visible)
        {
            m_Description.alpha = visible ? 1 : 0;
            // The description must not intercept the pointer or expand its separate hover region.
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
