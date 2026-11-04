using System;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PewPewPew.Match
{
    public class LobbyHullRenderer : MonoBehaviour
    {
        public const string PreviewLayer = "LobbyHullPreview";
        [SerializeField] private Camera m_Camera;

        public RenderTexture Render(ShipLoadout loadout) => Render(loadout, 256, 256, out _);

        /// Frame is in hull-root local XY coordinates, shared by the rendered model and UI attachment overlays.
        public RenderTexture Render(ShipLoadout loadout, int width, int height, out Rect frame)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Preview dimensions must be positive.");
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !LoadoutMath.InRange(loadout.Hull, catalog.Hulls.Length, false))
                throw new InvalidOperationException("Cannot render a lobby hull outside the ship catalog.");
            HullDef hull = catalog.Hulls[loadout.Hull];
            if (hull.HullPoints == null) throw new InvalidOperationException($"{hull.name} has no hull visual.");
            int layer = LayerMask.NameToLayer(PreviewLayer);
            if (layer < 0) throw new InvalidOperationException("Missing lobby preview layer.");

            var root = new GameObject("LobbyHullVisual");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            RenderTexture texture = null;
            try
            {
                // Copy renderers only: previewing must never execute prefab gameplay scripts or physics.
                foreach (MeshFilter source in hull.HullPoints.GetComponentsInChildren<MeshFilter>())
                {
                    MeshRenderer renderer = source.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || source.sharedMesh == null) continue;
                    var visual = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                    visual.layer = layer;
                    visual.transform.SetParent(root.transform, false);
                    Matrix4x4 relative = hull.HullPoints.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                    visual.transform.localPosition = relative.GetColumn(3);
                    visual.transform.localRotation = relative.rotation;
                    visual.transform.localScale = relative.lossyScale;
                    visual.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    visual.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                }
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException($"{hull.name} has no renderable hull meshes.");
                ShipColours.Apply(root, loadout);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                m_Camera.transform.position = bounds.center + Vector3.back * (bounds.extents.z + 10);
                m_Camera.transform.rotation = Quaternion.identity;
                float aspect = width / (float)height;
                m_Camera.aspect = aspect;
                m_Camera.orthographicSize = Mathf.Max(0.1f, Mathf.Max(bounds.extents.x / aspect, bounds.extents.y) * 1.12f);
                Vector3 centre = bounds.center - root.transform.position;
                Vector2 size = new Vector2(m_Camera.orthographicSize * 2 * aspect, m_Camera.orthographicSize * 2);
                frame = new Rect((Vector2)centre - size / 2, size);
                m_Camera.farClipPlane = bounds.size.z + 30;
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Lobby hull: " + hull.DisplayName
                };
                texture.Create();
                m_Camera.targetTexture = texture;
                m_Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                m_Camera.Render();
                return texture;
            }
            catch
            {
                if (texture != null)
                {
                    texture.Release();
                    if (Application.isPlaying) Destroy(texture);
                    else DestroyImmediate(texture);
                }
                throw;
            }
            finally
            {
                m_Camera.targetTexture = null;
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
            }
        }
    }
}
