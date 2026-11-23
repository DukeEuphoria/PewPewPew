using System;
using PewPewPew.Core;
using PewPewPew.Match;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PewPewPew.Ships
{
    /// Attach beneath the existing HUD Canvas/EventSystem. Size is the square panel width in Canvas units;
    /// Initialize sizes an owned, centred child Rect, leaving the host's position and size untouched.
    /// Only fitted component icons participate in UI raycasts.
    public sealed class ShipHudPaperdoll : MonoBehaviour
    {
        private const int TextureWidth = 1024;
        private const int TextureHeight = 744;
        private RectTransform m_Panel;
        private RawImage m_HullImage;
        private RenderTexture m_Texture;
        private GameObject m_RenderRoot;
        private Camera m_Camera;
        private Image[] m_Icons = Array.Empty<Image>();
        private Button[] m_Buttons = Array.Empty<Button>();
        private HudGauge[] m_Gauges = Array.Empty<HudGauge>();
        private Color[] m_Tints = Array.Empty<Color>();
        private Action<int> m_OnMountClick;

        public RectTransform Rect => m_Panel;

        /// Reinitializing replaces all owned UI and render resources. Darkness is a 0..1 hull-only multiplier.
        public void Initialize(ShipLoadout loadout, Action<int> onMountClick, float size, float darkness, float markerScale = 1f)
        {
            if (!float.IsFinite(size) || size <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            if (!float.IsFinite(darkness)) throw new ArgumentOutOfRangeException(nameof(darkness));
            if (!float.IsFinite(markerScale) || markerScale <= 0f) throw new ArgumentOutOfRangeException(nameof(markerScale));
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !catalog.IsValid(loadout))
                throw new InvalidOperationException("The HUD paperdoll requires a valid loadout.");
            if (GetComponentInParent<Canvas>() == null)
                throw new InvalidOperationException("The HUD paperdoll must be attached beneath a Canvas.");
            int previewLayer = LayerMask.NameToLayer(LobbyHullRenderer.PreviewLayer);
            if (previewLayer < 0) throw new InvalidOperationException("Missing lobby preview layer.");

            Dispose();
            try
            {
                m_OnMountClick = onMountClick;
                m_Panel = CreateRect("Paperdoll", transform);
                m_Panel.sizeDelta = Vector2.one * size;
                var imageRect = CreateRect("Hull", m_Panel);
                imageRect.sizeDelta = new Vector2(size, size * TextureHeight / TextureWidth);
                m_HullImage = imageRect.gameObject.AddComponent<RawImage>();
                m_HullImage.raycastTarget = false;
                float brightness = 1f - Mathf.Clamp01(darkness);
                m_HullImage.color = new Color(brightness, brightness, brightness, 1f);
                HullPoints hull = catalog.Hulls[loadout.Hull].HullPoints;
                Rect frame = RenderHull(loadout, previewLayer);
                m_HullImage.texture = m_Texture;

                HullPoint[] points = hull.Points;
                m_Icons = new Image[points.Length];
                m_Buttons = new Button[points.Length];
                m_Gauges = new HudGauge[points.Length];
                m_Tints = new Color[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    int component = loadout.MountComponents[i];
                    if (component < 0) continue;
                    ShipComponentDef definition = catalog.Components[component];
                    HullPoint point = points[i];
                    Vector3 local = hull.transform.InverseTransformPoint(point.transform.position);
                    Vector2 position = UnityEngine.Rect.PointToNormalized(frame, local);
                    Vector2 up = hull.transform.InverseTransformDirection(point.transform.up);
                    float angle = up == Vector2.zero ? 0f : Vector2.SignedAngle(Vector2.up, up);
                    RectTransform marker = CreateRect("Mount " + i + ": " + definition.DisplayName, imageRect);
                    marker.anchorMin = marker.anchorMax = position;
                    marker.localRotation = Quaternion.Euler(0f, 0f, LobbySymbol.ComponentRotation(definition, angle));
                    // Keep the loadout's size classes and padding, but readable at the HUD's 240-unit scale.
                    float artworkSize = definition.Size == ComponentSize.Small ? 30f :
                        definition.Size == ComponentSize.Medium ? 42f : 56f;
                    marker.sizeDelta = Vector2.one * ((artworkSize - 6f) * size / 240f * markerScale);
                    Image icon = marker.gameObject.AddComponent<Image>();
                    if (definition is SubSystemDef subSystem && subSystem.Gauge != null)
                    {
                        HudGauge gauge = Instantiate(subSystem.Gauge, marker, false);
                        RectTransform gaugeRect = gauge.Rect;
                        gaugeRect.anchorMin = gaugeRect.anchorMax = gaugeRect.pivot = Vector2.one * 0.5f;
                        gaugeRect.anchoredPosition = Vector2.zero;
                        gaugeRect.localRotation = Quaternion.identity;
                        Vector2 gaugeSize = gaugeRect.rect.size;
                        if (gaugeSize.x <= 0f || gaugeSize.y <= 0f)
                            throw new InvalidOperationException($"{subSystem.name} has a HUD gauge with a non-positive size.");
                        gaugeRect.localScale = new Vector3(marker.rect.width / gaugeSize.x, marker.rect.height / gaugeSize.y, 1f);
                        foreach (Graphic graphic in gauge.GetComponentsInChildren<Graphic>(true))
                            graphic.raycastTarget = false;
                        m_Gauges[i] = gauge;
                        icon.color = Color.clear;
                    }
                    else
                    {
                        if (definition is SubSystemDef)
                            Debug.LogWarning($"{definition.name} has no HUD gauge; using its component artwork.", definition);
                        icon.sprite = definition.ButtonArtwork != null ? definition.ButtonArtwork :
                            LobbySymbolLibrary.Get(LobbySymbol.ForComponent(definition.Type));
                        icon.preserveAspect = true;
                        icon.color = definition.Tint;
                    }
                    icon.raycastTarget = true;
                    Button button = marker.gameObject.AddComponent<Button>();
                    button.targetGraphic = icon;
                    button.transition = Selectable.Transition.None;
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                    int captured = i;
                    button.onClick.AddListener(() => m_OnMountClick?.Invoke(captured));
                    m_Icons[i] = icon;
                    m_Buttons[i] = button;
                    m_Tints[i] = definition.Tint;
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// Empty mounts are ignored. Disabled icons remain clickable; destroyed icons cannot activate.
        public void SetMountState(int mountIndex, bool enabled, bool destroyed)
        {
            if (mountIndex < 0 || mountIndex >= m_Icons.Length)
                throw new ArgumentOutOfRangeException(nameof(mountIndex));
            Image icon = m_Icons[mountIndex];
            if (icon == null) return;
            HudGauge gauge = m_Gauges[mountIndex];
            if (gauge != null) gauge.SetDimmed(!enabled || destroyed);
            else
            {
                Color tint = m_Tints[mountIndex];
                icon.color = !enabled || destroyed ? new Color(0.4f, 0.4f, 0.4f, tint.a) : tint;
            }
            m_Buttons[mountIndex].interactable = !destroyed;
        }

        public HudGauge GetMountGauge(int mountIndex)
        {
            if (mountIndex < 0 || mountIndex >= m_Gauges.Length)
                throw new ArgumentOutOfRangeException(nameof(mountIndex));
            return m_Gauges[mountIndex];
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private Rect RenderHull(ShipLoadout loadout, int layer)
        {
            m_RenderRoot = new GameObject("HUD paperdoll renderer");
            m_RenderRoot.layer = layer;
            // Isolate the preview from the lobby's point light even if both panels are present.
            m_RenderRoot.transform.position = new Vector3(0f, 0f, 10000f);
            var cameraObject = new GameObject("HUD paperdoll camera", typeof(Camera));
            cameraObject.layer = layer;
            cameraObject.transform.SetParent(m_RenderRoot.transform, false);
            m_Camera = cameraObject.GetComponent<Camera>();
            m_Camera.enabled = false;
            m_Camera.orthographic = true;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Color.clear;
            m_Camera.cullingMask = 1 << layer;
            m_Camera.nearClipPlane = 0.01f;
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = false;
            UniversalAdditionalCameraData data = m_Camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            var lightObject = new GameObject("HUD paperdoll light", typeof(Light));
            lightObject.layer = layer;
            lightObject.transform.SetParent(m_RenderRoot.transform, false);
            lightObject.transform.localPosition = new Vector3(-2f, 3f, -5f);
            Light light = lightObject.GetComponent<Light>();
            // Match the authored HullThumbnailLight without depending on the loadout menu being active.
            light.type = LightType.Point;
            light.color = Color.white;
            light.intensity = 5f;
            light.range = 30f;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << layer;
            light.GetUniversalAdditionalLightData();

            LobbyHullRenderer renderer = m_RenderRoot.AddComponent<LobbyHullRenderer>();
            renderer.SetCamera(m_Camera);
            try
            {
                m_Texture = renderer.Render(loadout, TextureWidth, TextureHeight, out Rect frame, true);
                return frame;
            }
            finally
            {
                m_RenderRoot.SetActive(false);
            }
        }

        public void Dispose()
        {
            m_OnMountClick = null;
            foreach (Button button in m_Buttons)
                if (button != null) button.onClick.RemoveAllListeners();
            m_Icons = Array.Empty<Image>();
            m_Buttons = Array.Empty<Button>();
            m_Gauges = Array.Empty<HudGauge>();
            m_Tints = Array.Empty<Color>();
            if (m_HullImage != null) m_HullImage.texture = null;
            if (m_Camera != null) m_Camera.targetTexture = null;
            if (m_Texture != null)
            {
                m_Texture.Release();
                DestroyOwned(m_Texture);
                m_Texture = null;
            }
            if (m_RenderRoot != null)
            {
                m_RenderRoot.SetActive(false);
                DestroyOwned(m_RenderRoot);
                m_RenderRoot = null;
            }
            if (m_Panel != null)
            {
                m_Panel.gameObject.SetActive(false);
                DestroyOwned(m_Panel.gameObject);
                m_Panel = null;
            }
            m_Camera = null;
            m_HullImage = null;
        }

        private void OnDestroy() => Dispose();

        private static void DestroyOwned(UnityEngine.Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
