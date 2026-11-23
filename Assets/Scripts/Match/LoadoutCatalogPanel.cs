using System;
using System.Collections.Generic;
using System.Linq;
using PewPewPew.Core;
using PewPewPew.Ships;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    /// Reports pointer enter/exit immediately; LoadoutCatalogPanel decides when the details text follows.
    public class LoadoutCatalogItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action m_Click;
        private Action<bool> m_Hover;
        private bool m_Inside;

        public void Initialize(Action click, Action<bool> hover)
        {
            m_Click = click;
            m_Hover = hover;
            GetComponent<Button>().onClick.AddListener(() => m_Click?.Invoke());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Inside = true;
            m_Hover?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData) => ClearHover();
        private void OnDisable() => ClearHover();

        private void ClearHover()
        {
            if (m_Inside) m_Hover?.Invoke(false);
            m_Inside = false;
        }
    }
    /// Catalog grid settings, authored on LoadoutMenuController. Icon width is derived from the panel width.
    [Serializable]
    public struct LoadoutCatalogLayout
    {
        public int ComponentColumns;
        public int ColourColumns;
        public float IconGap;
        [Tooltip("Space between a catalog cell's edge and a large component's icon.")]
        public float IconInset;
        public float CategoryPadding;
        public float CategoryGap;
        public float TitleHeight;
        /// Component icon scale for small, medium and large components, relative to a large component's icon.
        public Vector3 SizeScales;
    }

    public class LoadoutCatalogPanel : MonoBehaviour
    {
        private sealed class Entry
        {
            public RectTransform Rect;
            public RectTransform Icon;
            public Outline Outline;
            public ShipComponentDef Component;
            public int Colour = -1;
            public bool Secondary;
        }

        private sealed class Section
        {
            public RectTransform Rect;
            public readonly List<Entry> Entries = new List<Entry>();
            public float Top;
            public bool Colours;
        }

        private readonly List<Section> m_Sections = new List<Section>();
        private ScrollRect m_Scroll;
        private RectTransform m_Content;
        private RectTransform m_Viewport;
        private GameObject m_Scrollbar;
        private HullPoint m_Point;
        private ShipComponentDef m_Selected;
        private int m_PrimaryColour;
        private int m_SecondaryColour;
        private LoadoutCatalogLayout m_Layout;
        private float m_Width;
        private float m_Height;
        private Action<string> m_Hover;
        private float m_HoverDelay;
        private string m_HoverTarget;
        private string m_HoverShown;
        private float m_HoverChangedAt;
        private Vector2 m_PointerPosition;

        public void Initialize(ShipCatalog catalog, TMP_FontAsset font, LoadoutCatalogLayout layout, float hoverDelay,
            Action<ShipComponentDef> selectComponent, Action<int, bool> selectColour, Action<string> hover)
        {
            layout.ComponentColumns = Mathf.Max(1, layout.ComponentColumns);
            layout.ColourColumns = Mathf.Max(1, layout.ColourColumns);
            m_Layout = layout;
            m_Hover = hover;
            m_HoverDelay = hoverDelay;
            RectTransform root = (RectTransform)transform;
            m_Viewport = Rect("Viewport", root);
            Stretch(m_Viewport, new Vector2(8, 8), new Vector2(-30, -8));
            m_Viewport.gameObject.AddComponent<RectMask2D>();
            m_Content = Rect("Content", m_Viewport);
            m_Content.anchorMin = new Vector2(0, 1);
            m_Content.anchorMax = Vector2.one;
            m_Content.pivot = new Vector2(0.5f, 1);
            m_Content.anchoredPosition = Vector2.zero;
            m_Content.sizeDelta = Vector2.zero;
            m_Scroll = gameObject.AddComponent<ScrollRect>();
            m_Scroll.viewport = m_Viewport;
            m_Scroll.content = m_Content;
            m_Scroll.horizontal = false;
            m_Scroll.scrollSensitivity = 36;
            m_Scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform bar = Rect("Scrollbar", root);
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1, 0.5f);
            bar.offsetMin = new Vector2(-22, 8);
            bar.offsetMax = new Vector2(-6, -8);
            Image track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(0.12f, 0.2f, 0.26f);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            RectTransform sliding = Rect("SlidingArea", bar);
            Stretch(sliding, new Vector2(2, 8), new Vector2(-2, -8));
            RectTransform handle = Rect("Handle", sliding);
            Stretch(handle, Vector2.zero, Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.35f, 0.8f, 0.9f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            m_Scroll.verticalScrollbar = scrollbar;
            m_Scrollbar = bar.gameObject;

            foreach (ComponentType type in new[] { ComponentType.Shield, ComponentType.Gun, ComponentType.Thruster, ComponentType.SubSystem })
            {
                ShipComponentDef[] definitions = catalog.Components.Where(item => item.Type == type)
                    .OrderBy(item => item.Price).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
                Section section = NewSection(type == ComponentType.SubSystem ? "Subsystems" : type + "s", font);
                foreach (ShipComponentDef definition in definitions)
                {
                    ShipComponentDef captured = definition;
                    Entry entry = NewEntry(section, definition.ButtonArtwork, LobbySymbol.ForComponent(type), definition.Tint, null, definition.IsLocked);
                    entry.Component = definition;
                    entry.Rect.gameObject.GetComponent<LoadoutCatalogItem>().Initialize(
                        () => selectComponent(captured),
                        inside => SetHover(LoadoutDetails.Component(captured), inside));
                }
            }
            foreach (bool secondary in new[] { false, true })
            {
                bool capturedChannel = secondary;
                Section section = NewSection(secondary ? "Secondary colours" : "Primary colours", font);
                section.Colours = true;
                for (int i = 0; i < PlayerPalette.Count; i++)
                {
                    int index = i;
                    Entry entry = NewEntry(section, null, LobbySymbolKind.Stop, Color.white, PlayerPalette.Colour(i), PlayerPalette.Price(i) > 0);
                    entry.Colour = i;
                    entry.Secondary = secondary;
                    entry.Rect.gameObject.GetComponent<LoadoutCatalogItem>().Initialize(
                        () => selectColour(index, capturedChannel),
                        inside => SetHover($"{PlayerPalette.Description(index)}\nPrice: {PlayerPalette.Price(index)}", inside));
                }
            }
        }

        public void Refresh(HullPoint point, ShipComponentDef selected, Color32 primary, Color32 secondary)
        {
            m_Point = point;
            m_Selected = selected;
            m_PrimaryColour = PlayerPalette.IndexOf(primary);
            m_SecondaryColour = PlayerPalette.IndexOf(secondary);
            Layout();
        }

        public void MoveCategory(int direction)
        {
            if (m_Content.rect.height <= m_Viewport.rect.height) return;
            float current = m_Content.anchoredPosition.y;
            float[] positions = m_Sections.Where(section => section.Rect.gameObject.activeSelf).Select(section => section.Top).ToArray();
            float target = direction > 0
                ? positions.FirstOrDefault(value => value > current + 1f)
                : positions.Reverse().FirstOrDefault(value => value < current - 1f);
            if (direction > 0 && target <= current) target = m_Content.rect.height - m_Viewport.rect.height;
            m_Content.anchoredPosition = new Vector2(0, Mathf.Clamp(target, 0, m_Content.rect.height - m_Viewport.rect.height));
        }

        private void Update()
        {
            if (Mathf.Abs(m_Width - m_Viewport.rect.width) > 0.5f || Mathf.Abs(m_Height - m_Viewport.rect.height) > 0.5f)
                Layout();
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                Vector2 position = pointer.position.ReadValue();
                if (position != m_PointerPosition) m_HoverChangedAt = Time.unscaledTime;
                m_PointerPosition = position;
            }
            if (m_HoverTarget != m_HoverShown && Time.unscaledTime - m_HoverChangedAt >= m_HoverDelay) ShowHover();
        }

        private void OnDisable()
        {
            m_HoverTarget = null;
            if (m_HoverShown != null) ShowHover();
        }

        /// The details text follows catalog hover only once the pointer has rested for the hover delay,
        /// so sweeping across icons or gaps never flashes the fallback text in between.
        private void SetHover(string details, bool inside)
        {
            if (inside) m_HoverTarget = details;
            else if (m_HoverTarget == details) m_HoverTarget = null;
            m_HoverChangedAt = Time.unscaledTime;
            if (m_HoverDelay <= 0) ShowHover();
        }

        private void ShowHover()
        {
            m_HoverShown = m_HoverTarget;
            m_Hover?.Invoke(m_HoverShown);
        }

        private void Layout()
        {
            if (m_Viewport == null) return;
            m_Width = m_Viewport.rect.width;
            m_Height = m_Viewport.rect.height;
            float width = m_Width;
            float gap = m_Layout.IconGap;
            float padding = m_Layout.CategoryPadding;
            float title = m_Layout.TitleHeight;
            float top = 0;
            foreach (Section section in m_Sections)
            {
                int columns = section.Colours ? m_Layout.ColourColumns : m_Layout.ComponentColumns;
                float cell = Mathf.Max(0, (width - padding * 2 - (columns - 1) * gap) / columns);
                List<Entry> shown = section.Entries.Where(entry => entry.Component != null
                    ? m_Point == null || m_Point.AcceptsComponent(entry.Component) : m_Point == null).ToList();
                section.Rect.gameObject.SetActive(shown.Count > 0);
                foreach (Entry entry in section.Entries)
                {
                    bool visible = shown.Contains(entry);
                    entry.Rect.gameObject.SetActive(visible);
                    bool selected = entry.Component != null ? entry.Component == m_Selected :
                        entry.Colour == (entry.Secondary ? m_SecondaryColour : m_PrimaryColour);
                    entry.Outline.effectColor = selected
                        ? Color.cyan : new Color(0.3f, 0.45f, 0.5f);
                }
                if (shown.Count == 0) continue;
                int rows = (shown.Count + columns - 1) / columns;
                float height = title + padding + rows * cell + (rows - 1) * gap + padding;
                section.Top = top;
                Place(section.Rect, 0, top, width, height);
                for (int i = 0; i < shown.Count; i++)
                {
                    Place(shown[i].Rect, padding + (i % columns) * (cell + gap),
                        title + padding + (i / columns) * (cell + gap), cell, cell);
                    if (shown[i].Icon != null && shown[i].Component != null)
                        shown[i].Icon.sizeDelta = Vector2.one * Mathf.Max(0, cell - 2 * m_Layout.IconInset) *
                            SizeScale(shown[i].Component.Size);
                }
                top += height + m_Layout.CategoryGap;
            }
            m_Content.sizeDelta = new Vector2(0, top);
            m_Scrollbar.SetActive(top > m_Height + 1);
            float offset = Mathf.Clamp(m_Content.anchoredPosition.y, 0, Mathf.Max(0, top - m_Height));
            m_Content.anchoredPosition = new Vector2(0, offset);
        }

        private Section NewSection(string title, TMP_FontAsset font)
        {
            RectTransform rect = Rect(title, m_Content);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.09f, 0.13f, 1);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.45f, 0.5f);
            outline.effectDistance = Vector2.one;
            RectTransform heading = Rect("Title", rect);
            heading.anchorMin = new Vector2(0, 1);
            heading.anchorMax = Vector2.one;
            heading.offsetMin = new Vector2(1, -m_Layout.TitleHeight);
            heading.offsetMax = new Vector2(-1, -1);
            Image titleBar = heading.gameObject.AddComponent<Image>();
            titleBar.color = new Color(0.1f, 0.18f, 0.23f);
            titleBar.raycastTarget = false;
            RectTransform text = Rect("Label", heading);
            Stretch(text, new Vector2(8, 0), new Vector2(-8, 0));
            var label = text.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 18;
            label.color = Color.white;
            label.text = title;
            label.raycastTarget = false;
            var section = new Section { Rect = rect };
            m_Sections.Add(section);
            return section;
        }

        private Entry NewEntry(Section section, Sprite artwork, LobbySymbolKind kind, Color tint, Color32? swatch, bool locked)
        {
            RectTransform rect = Rect("Item", section.Rect);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = swatch.HasValue ? (Color)swatch.Value : new Color(0.1f, 0.18f, 0.25f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectDistance = Vector2.one;
            rect.gameObject.AddComponent<LoadoutCatalogItem>();
            RectTransform icon = null;
            if (!swatch.HasValue)
            {
                Color iconColour = locked ? Color.white * (0.35f + tint.grayscale * 0.25f) : tint;
                iconColour.a = 1;
                icon = Rect("Icon", rect);
                // Centred; Layout() sizes it from the cell and the component's size class.
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = Vector2.zero;
                if (artwork != null)
                {
                    Image image = icon.gameObject.AddComponent<Image>();
                    image.sprite = artwork;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    image.color = iconColour;
                }
                else
                {
                    LobbySymbol symbol = icon.gameObject.AddComponent<LobbySymbol>();
                    symbol.Kind = kind;
                    symbol.color = iconColour;
                    symbol.raycastTarget = false;
                }
            }
            if (locked)
            {
                RectTransform overlay = Rect("Locked", rect);
                Stretch(overlay, Vector2.zero, Vector2.zero);
                Image dim = overlay.gameObject.AddComponent<Image>();
                dim.color = new Color(0.15f, 0.15f, 0.15f, 0.55f);
                dim.raycastTarget = false;
                RectTransform lockRect = Rect("LockIcon", overlay);
                lockRect.anchorMin = Vector2.one * 0.22f;
                lockRect.anchorMax = Vector2.one * 0.78f;
                lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
                LobbySymbol symbol = lockRect.gameObject.AddComponent<LobbySymbol>();
                symbol.Kind = LobbySymbolKind.Lock;
                symbol.color = Color.white;
                symbol.raycastTarget = false;
            }
            var entry = new Entry { Rect = rect, Icon = icon, Outline = outline };
            section.Entries.Add(entry);
            return entry;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private float SizeScale(ComponentSize size)
        {
            switch (size)
            {
                case ComponentSize.Small: return m_Layout.SizeScales.x;
                case ComponentSize.Medium: return m_Layout.SizeScales.y;
                case ComponentSize.Large: return m_Layout.SizeScales.z;
                default: throw new ArgumentOutOfRangeException(nameof(size));
            }
        }
    }
}
