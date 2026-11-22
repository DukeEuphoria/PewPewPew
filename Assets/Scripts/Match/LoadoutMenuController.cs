using System;
using System.Linq;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Networking;
using PewPewPew.Ships;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    [Serializable]
    public class LoadoutCategoryView
    {
        public Button Tab;
        public GameObject Grid;
        public LoadoutComponentButton[] Buttons;
    }

    public class LoadoutMenuController : MonoBehaviour
    {
        [Header("Catalog layout")]
        [SerializeField, Min(1), Tooltip("Icons per row in each component category; icon width is derived from the panel width.")]
        private int m_ComponentColumns = 10;
        [SerializeField, Min(1), Tooltip("Swatches per row in each colour category; swatch width is derived from the panel width.")]
        private int m_ColourColumns = 16;
        [SerializeField, Min(0), Tooltip("Gap between neighbouring icons and between icon rows.")]
        private float m_IconGap = 4;
        [SerializeField, Min(0), Tooltip("Space between a category's border and its icons.")]
        private float m_CategoryPadding = 8;
        [SerializeField, Min(0), Tooltip("Vertical space between categories.")]
        private float m_CategoryGap = 12;
        [SerializeField, Min(0), Tooltip("Height of each category's title bar.")]
        private float m_CategoryTitleHeight = 30;
        [Space]
        [SerializeField, Min(0)] private float m_ComponentHoverDelay = 0.4f;
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private CanvasGroup m_EditingGroup;
        [SerializeField] private LoadoutCategoryView[] m_Categories;
        [SerializeField] private Button m_PreviousHull;
        [SerializeField] private Button m_NextHull;
        [SerializeField] private TMP_Text m_HullName;
        [SerializeField] private TMP_Text m_HullDescription;
        [SerializeField] private TMP_Text m_HoverDescription;
        [SerializeField] private Button[] m_Slots;
        [SerializeField] private TMP_Text[] m_SlotLabels;
        [SerializeField] private Button m_Deploy;
        [SerializeField] private TMP_Text m_DeployLabel;
        [SerializeField] private TMP_Text m_Status;
        [SerializeField] private Button m_ColoursToggle;
        [SerializeField] private GameObject m_ColoursPanel;
        [SerializeField] private Button[] m_PrimaryColours;
        [SerializeField] private Button[] m_SecondaryColours;
        [SerializeField] private TMP_Text m_ColourLabel;
        [SerializeField] private LoadoutShipPreview m_Preview;

        private PlayerState m_Player;
        private ShipCatalog m_Catalog;
        private ShipLoadout m_Pending;
        private int m_SelectedPoint = -1;
        private ShipComponentDef m_SelectedComponent;
        private string m_SelectedColour;
        private string m_HoverDetails;
        private int m_HoverPoint = -1;
        private bool m_HoverHull;
        private LoadoutCatalogPanel m_CatalogPanel;
        public bool CanEdit => m_Player == null || (!m_Player.Ready && !m_Player.HasShip);
        public ShipLoadout Pending => m_Pending;

        private void Awake()
        {
            m_Panel.SetActive(false);
            m_PreviousHull.onClick.AddListener(() => CycleHull(-1));
            m_NextHull.onClick.AddListener(() => CycleHull(1));
            m_Deploy.onClick.AddListener(Deploy);
            m_Catalog = ShipCatalog.Instance;
            if (m_Catalog == null) { enabled = false; return; }
            BuildLayout();
            m_Slots[0].onClick.AddListener(ClearSelectedPoint);
            m_Preview.HullHover = inside => { m_HoverHull = inside; ShowDetails(); };
        }

        private void Update()
        {
            PlayerState local = PlayerState.Local;
            MatchManager match = MatchManager.Instance;
            bool visible = local != null && match != null && !local.HasShip &&
                match.State != MatchState.Ended && !MenuScreenFlow.AnyBlocking;
            if (local != m_Player)
            {
                m_Player = local;
                if (local != null)
                {
                    m_Pending = local.Loadout;
                    if (CanEdit)
                    {
                        PlayerColourStore.Current.Load(out m_Pending.Colour0, out m_Pending.Colour1);
                        bool primaryAvailable = PlayerPalette.IsAvailable(m_Pending.Colour0);
                        bool secondaryAvailable = PlayerPalette.IsAvailable(m_Pending.Colour1);
                        if (!primaryAvailable) m_Pending.Colour0 = PlayerPalette.DefaultPrimary;
                        if (!secondaryAvailable) m_Pending.Colour1 = PlayerPalette.DefaultSecondary;
                        if (!primaryAvailable || !secondaryAvailable)
                        {
                            Debug.LogWarning("Saved ship colours included locked or invalid colours; defaults were substituted.");
                            PlayerColourStore.Current.Save(m_Pending.Colour0, m_Pending.Colour1);
                        }
                    }
                    Normalize();
                    if (CanEdit) Submit();
                    Refresh();
                }
            }
            if (m_Panel.activeSelf != visible)
            {
                m_Panel.SetActive(visible);
                if (visible) Refresh();
                else ClearTransientState();
            }
            if (visible)
            {
                m_EditingGroup.interactable = CanEdit;
                m_EditingGroup.alpha = CanEdit ? 1f : 0.65f;
                UpdateDeploy(match);
                HandleInput();
            }
        }

        private ShipComponentDef[] Items(int category)
        {
            ShipComponentDef[] items = m_Catalog.Components;
            switch (category)
            {
                case 0: return items.Where(item => item.Type == ComponentType.Shield).ToArray();
                case 1: return items.Where(item => item.Type == ComponentType.Gun).ToArray();
                case 2: return Array.Empty<ShipComponentDef>();
                case 3: return items.Where(item => item.Type == ComponentType.Thruster).ToArray();
                case 4: return items.Where(item => item.Type == ComponentType.SubSystem).ToArray();
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private HullPoint SelectedPoint
        {
            get
            {
                HullPoint[] points = m_Catalog.Hulls[m_Pending.Hull].HullPoints.Points;
                return m_SelectedPoint >= 0 && m_SelectedPoint < points.Length ? points[m_SelectedPoint] : null;
            }
        }

        private void Normalize()
        {
            if (!m_Catalog.IsValid(m_Pending))
            {
                int hull = LoadoutMath.InRange(m_Pending.Hull, m_Catalog.Hulls.Length, false) ? m_Pending.Hull : 0;
                Color32 primary = m_Pending.Colour0;
                Color32 secondary = m_Pending.Colour1;
                m_Pending = m_Catalog.DefaultLoadout(hull);
                m_Pending.Colour0 = primary;
                m_Pending.Colour1 = secondary;
            }
            if (m_SelectedPoint >= m_Pending.MountComponents.Length) m_SelectedPoint = -1;
        }

        public void SelectComponent(int category, int index)
        {
            ShipComponentDef[] items = Items(category);
            if (index >= 0 && index < items.Length) SelectComponent(items[index]);
        }

        private void SelectComponent(ShipComponentDef component)
        {
            if (!CanEdit) return;
            m_SelectedColour = null;
            if (component.IsLocked)
            {
                m_SelectedComponent = component;
            }
            else if (SelectedPoint != null)
            {
                if (!SelectedPoint.AcceptsComponent(component)) return;
                m_Pending.SetMountComponent(m_SelectedPoint, Array.IndexOf(m_Catalog.Components, component));
                m_SelectedPoint = -1;
                m_SelectedComponent = null;
                Submit();
            }
            else m_SelectedComponent = component;
            Refresh();
        }

        public void CycleHull(int step)
        {
            if (!CanEdit) return;
            int hull = LoadoutMath.Cycle(m_Pending.Hull, m_Catalog.Hulls.Length, step, false);
            Color32 primary = m_Pending.Colour0;
            Color32 secondary = m_Pending.Colour1;
            m_Pending = m_Catalog.DefaultLoadout(hull);
            m_Pending.Colour0 = primary;
            m_Pending.Colour1 = secondary;
            ClearTransientState();
            Submit();
            Refresh();
        }

        private void SelectPoint(int point)
        {
            if (!CanEdit) return;
            HullPoint[] points = m_Catalog.Hulls[m_Pending.Hull].HullPoints.Points;
            if (point < 0 || point >= points.Length) return;
            if (m_SelectedComponent != null)
            {
                if (m_SelectedComponent.IsLocked || !points[point].AcceptsComponent(m_SelectedComponent)) return;
                m_Pending.SetMountComponent(point, Array.IndexOf(m_Catalog.Components, m_SelectedComponent));
                Submit();
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null || (!keyboard.leftShiftKey.isPressed && !keyboard.rightShiftKey.isPressed))
                    m_SelectedComponent = null;
                m_SelectedPoint = -1;
            }
            else
            {
                m_SelectedColour = null;
                m_SelectedPoint = point;
            }
            Refresh();
        }

        private void ClearSelectedPoint()
        {
            if (!CanEdit || SelectedPoint == null) return;
            m_Pending.SetMountComponent(m_SelectedPoint, -1);
            Submit();
            Refresh();
        }

        private void SelectColour(int index, bool secondary)
        {
            if (!CanEdit) return;
            m_SelectedComponent = null;
            m_SelectedColour = $"{PlayerPalette.Description(index)}\nPrice: {PlayerPalette.Price(index)}";
            if (PlayerPalette.Price(index) != 0) { Refresh(); return; }
            if (secondary) m_Pending.Colour1 = PlayerPalette.Colour(index);
            else m_Pending.Colour0 = PlayerPalette.Colour(index);
            PlayerColourStore.Current.Save(m_Pending.Colour0, m_Pending.Colour1);
            Submit();
            Refresh();
        }

        private void Submit()
        {
            if (m_Catalog.IsValid(m_Pending) && m_Catalog.LockedProblem(m_Pending) == null)
                m_Player.CmdSetLoadout(m_Pending);
        }

        private void Deploy()
        {
            UpdateDeploy(MatchManager.Instance);
            if (m_Deploy.interactable) m_Player.CmdDeploy();
        }

        private void Refresh()
        {
            HullDef hull = m_Catalog.Hulls[m_Pending.Hull];
            m_HullName.text = hull.DisplayName + (hull.IsLocked ? $"\nLocked - {hull.Price}" : string.Empty);
            m_PreviousHull.interactable = m_NextHull.interactable = m_Catalog.Hulls.Length > 1;
            HullPoint point = SelectedPoint;
            int selected = point == null ? -1 : m_Pending.MountComponents[m_SelectedPoint];
            m_CatalogPanel.Refresh(point, m_SelectedComponent, m_Pending.Colour0, m_Pending.Colour1);
            m_Slots[0].gameObject.SetActive(point != null);
            m_Slots[0].interactable = point != null && selected >= 0;
            m_Preview.Show(m_Pending, m_SelectedPoint, m_SelectedComponent, SelectPoint, HoverPoint);
            ShowDetails();
        }

        private void BuildLayout()
        {
            var panel = (RectTransform)m_Panel.transform;
            RectTransform left = PaneFor(m_Slots[0].transform, panel);
            RectTransform right = PaneFor(m_Preview.transform, panel);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0.5f, 1);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(-12, 0);
            right.anchorMin = new Vector2(0.5f, 0);
            right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(12, 0);
            right.offsetMax = Vector2.zero;
            foreach (RectTransform pane in new[] { left, right })
            {
                var outline = pane.GetComponent<Outline>() ?? pane.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.3f, 0.55f, 0.62f);
                outline.effectDistance = Vector2.one * 2;
            }

            foreach (Transform child in left) child.gameObject.SetActive(false);
            var catalogRoot = new GameObject("ComponentCatalog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            catalogRoot.transform.SetParent(left, false);
            var catalogRect = (RectTransform)catalogRoot.transform;
            catalogRect.anchorMin = Vector2.zero;
            catalogRect.anchorMax = Vector2.one;
            catalogRect.offsetMin = new Vector2(8, 56);
            catalogRect.offsetMax = new Vector2(-8, -8);
            catalogRoot.GetComponent<Image>().color = new Color(0.035f, 0.065f, 0.1f, 1);
            m_CatalogPanel = catalogRoot.AddComponent<LoadoutCatalogPanel>();
            var layout = new LoadoutCatalogLayout
            {
                ComponentColumns = m_ComponentColumns,
                ColourColumns = m_ColourColumns,
                IconGap = m_IconGap,
                CategoryPadding = m_CategoryPadding,
                CategoryGap = m_CategoryGap,
                TitleHeight = m_CategoryTitleHeight
            };
            m_CatalogPanel.Initialize(m_Catalog, m_HullName.font, layout, m_ComponentHoverDelay,
                SelectComponent, SelectColour, details => { m_HoverDetails = details; ShowDetails(); });

            RectTransform delete = (RectTransform)m_Slots[0].transform;
            delete.anchorMin = delete.anchorMax = new Vector2(1, 0);
            delete.pivot = new Vector2(1, 0);
            delete.anchoredPosition = new Vector2(-12, 8);
            delete.sizeDelta = new Vector2(100, 40);
            m_SlotLabels[0].text = "DELETE";
            m_SlotLabels[0].fontSize = 14;
            m_SlotLabels[0].rectTransform.offsetMin = new Vector2(27, 0);
            var deleteIcon = new GameObject("DeleteIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbySymbol));
            deleteIcon.transform.SetParent(delete, false);
            var deleteRect = (RectTransform)deleteIcon.transform;
            deleteRect.anchorMin = deleteRect.anchorMax = new Vector2(0, 0.5f);
            deleteRect.pivot = new Vector2(0, 0.5f);
            deleteRect.anchoredPosition = new Vector2(5, 0);
            deleteRect.sizeDelta = Vector2.one * 24;
            var symbol = deleteIcon.GetComponent<LobbySymbol>();
            symbol.Kind = LobbySymbolKind.Delete;
            symbol.raycastTarget = false;
            delete.SetAsLastSibling();

            right.Find("HullTitle").gameObject.SetActive(false);
            m_ColoursToggle.gameObject.SetActive(false);
            m_ColoursPanel.SetActive(false);
            m_ColourLabel.gameObject.SetActive(false);
            RectTransform description = (RectTransform)m_HullDescription.transform.parent.parent;
            description.SetParent(right, false);
            Anchor(description, new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.29f));
            CanvasGroup descriptionGroup = description.GetComponent<CanvasGroup>();
            descriptionGroup.alpha = 1;
            descriptionGroup.blocksRaycasts = true;
            m_HullDescription.raycastTarget = true;

            var area = new GameObject("PreviewArea", typeof(RectTransform));
            area.transform.SetParent(right, false);
            Anchor((RectTransform)area.transform, new Vector2(0.025f, 0.34f), new Vector2(0.975f, 0.9f));
            RectTransform image = (RectTransform)m_Preview.transform;
            image.SetParent(area.transform, false);
            Anchor(image, Vector2.zero, Vector2.one);
            var aspect = image.gameObject.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1024f / 744f;
            Transform hullHover = image.Find("HullDescriptionHoverArea");
            if (hullHover != null) Anchor((RectTransform)hullHover, Vector2.zero, Vector2.one);

            RectTransform title = m_HullName.rectTransform;
            title.SetParent(right, false);
            Anchor(title, new Vector2(0.17f, 0.92f), new Vector2(0.83f, 0.99f));
            m_HullName.alignment = TextAlignmentOptions.Center;
            m_HullName.fontStyle = FontStyles.Bold;
            foreach (Button button in new[] { m_PreviousHull, m_NextHull }) button.transform.SetParent(right, false);
            Anchor((RectTransform)m_PreviousHull.transform, new Vector2(0.08f, 0.925f), new Vector2(0.15f, 0.995f));
            Anchor((RectTransform)m_NextHull.transform, new Vector2(0.85f, 0.925f), new Vector2(0.92f, 0.995f));
            Anchor(m_Status.rectTransform, new Vector2(0.025f, 0.29f), new Vector2(0.975f, 0.34f));
            var background = right.gameObject.AddComponent<LoadoutPanelBackground>();
            background.Click = () =>
            {
                if (m_SelectedPoint < 0) return;
                m_SelectedPoint = -1;
                Refresh();
            };
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform PaneFor(Transform child, Transform panel)
        {
            while (child != null && child.parent != panel) child = child.parent;
            if (child == null) throw new InvalidOperationException("Loadout control is not inside the loadout panel.");
            return (RectTransform)child;
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && m_SelectedComponent != null)
            {
                m_SelectedComponent = null;
                Refresh();
            }
            if (!CanEdit) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != null && focused.GetComponent<TMP_InputField>() != null) return;
            if ((keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) && SelectedPoint != null)
                ClearSelectedPoint();
            if (keyboard.pageDownKey.wasPressedThisFrame) m_CatalogPanel.MoveCategory(1);
            if (keyboard.pageUpKey.wasPressedThisFrame) m_CatalogPanel.MoveCategory(-1);
        }

        private void ClearTransientState()
        {
            m_SelectedPoint = -1;
            m_SelectedComponent = null;
            m_SelectedColour = null;
            m_HoverDetails = null;
            m_HoverPoint = -1;
            m_HoverHull = false;
        }

        private void HoverPoint(int index, bool inside)
        {
            if (inside) m_HoverPoint = index;
            else if (m_HoverPoint == index) m_HoverPoint = -1;
            ShowDetails();
        }

        private void ShowDetails()
        {
            if (m_Catalog == null || !m_Catalog.IsValid(m_Pending)) return;
            HullPoint[] points = m_Catalog.Hulls[m_Pending.Hull].HullPoints.Points;
            if (m_HoverDetails != null) m_HullDescription.text = m_HoverDetails;
            else if (m_HoverPoint >= 0 && m_HoverPoint < points.Length)
                m_HullDescription.text = PointDetails(points, m_HoverPoint);
            else if (m_HoverHull) m_HullDescription.text = LoadoutDetails.Hull(m_Catalog, m_Pending);
            else if (m_SelectedComponent != null) m_HullDescription.text = LoadoutDetails.Component(m_SelectedComponent);
            else if (m_SelectedColour != null) m_HullDescription.text = m_SelectedColour;
            else if (m_SelectedPoint >= 0 && m_SelectedPoint < points.Length)
                m_HullDescription.text = PointDetails(points, m_SelectedPoint);
            else m_HullDescription.text = LoadoutDetails.Hull(m_Catalog, m_Pending);
        }

        private string PointDetails(HullPoint[] points, int index)
        {
            int component = m_Pending.MountComponents[index];
            return LoadoutDetails.Slot(points[index], component < 0 ? null : m_Catalog.Components[component]);
        }

        private void UpdateDeploy(MatchManager match)
        {
            double wait = m_Player.RespawnAt - NetworkTime.time;
            string problem = m_Catalog.LaunchProblem(m_Pending);
            bool ready = match.State == MatchState.Lobby && m_Player.Ready;
            m_Status.text = ready ? "Ready - press Stop to change your loadout." : problem ?? "Loadout ready.";
            m_Deploy.interactable = match.State != MatchState.Ended && (ready || (wait <= 0 && problem == null));
            m_DeployLabel.text = wait > 0 ? $"Deploy in {wait:0}s" :
                match.State == MatchState.Lobby ? (ready ? "STOP" : "READY") : "DEPLOY";
        }

        public class LoadoutPanelBackground : MonoBehaviour, IPointerClickHandler
        {
            public Action Click { get; set; }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left) Click?.Invoke();
            }
        }
    }
}
