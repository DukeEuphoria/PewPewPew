using System;
using System.Linq;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Networking;
using PewPewPew.Ships;
using TMPro;
using UnityEngine;
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
        private int m_Category;
        private int m_SelectedPoint;
        public bool CanEdit => m_Player == null || (!m_Player.Ready && !m_Player.HasShip);
        public ShipLoadout Pending => m_Pending;

        private void Awake()
        {
            m_Panel.SetActive(false);
            m_PreviousHull.onClick.AddListener(() => CycleHull(-1));
            m_NextHull.onClick.AddListener(() => CycleHull(1));
            m_Deploy.onClick.AddListener(Deploy);
            m_ColoursToggle.onClick.AddListener(() =>
            {
                if (CanEdit) m_ColoursPanel.SetActive(!m_ColoursPanel.activeSelf);
            });
            m_Catalog = ShipCatalog.Instance;
            if (m_Catalog == null) { enabled = false; return; }
            for (int category = 0; category < m_Categories.Length; category++)
            {
                int capturedCategory = category;
                var view = m_Categories[category];
                view.Tab.onClick.AddListener(() => ShowCategory(capturedCategory));
                int required = Items(category).Length;
                if (required > view.Buttons.Length)
                {
                    if (view.Buttons.Length == 0) throw new InvalidOperationException("The loadout category has no button template.");
                    int oldLength = view.Buttons.Length;
                    Array.Resize(ref view.Buttons, required);
                    for (int index = oldLength; index < required; index++)
                        view.Buttons[index] = Instantiate(view.Buttons[0], view.Grid.transform);
                }
                for (int index = 0; index < view.Buttons.Length; index++)
                {
                    int capturedIndex = index;
                    view.Buttons[index].Initialize(() => SelectComponent(capturedCategory, capturedIndex), ShowHover);
                }
            }
            m_Categories[2].Tab.gameObject.SetActive(false);
            TMP_Text gunTab = m_Categories[1].Tab.GetComponentInChildren<TMP_Text>();
            if (gunTab != null) gunTab.text = "GUNS";
            for (int slot = 0; slot < m_Slots.Length; slot++)
                m_Slots[slot].gameObject.SetActive(slot == 0);
            m_Slots[0].onClick.AddListener(ClearSelectedPoint);
            for (int index = 0; index < PlayerPalette.Count; index++)
            {
                int captured = index;
                m_PrimaryColours[index].onClick.AddListener(() => SelectColour(captured, false));
                m_SecondaryColours[index].onClick.AddListener(() => SelectColour(captured, true));
            }
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
                    if (CanEdit) PlayerColourStore.Current.Load(out m_Pending.Colour0, out m_Pending.Colour1);
                    Normalize();
                    if (CanEdit) Submit();
                    Refresh();
                }
            }
            if (m_Panel.activeSelf != visible)
            {
                m_Panel.SetActive(visible);
                if (visible) Refresh();
                else m_ColoursPanel.SetActive(false);
            }
            if (visible)
            {
                m_EditingGroup.interactable = CanEdit;
                m_EditingGroup.alpha = CanEdit ? 1f : 0.65f;
                if (!CanEdit) m_ColoursPanel.SetActive(false);
                UpdateDeploy(match);
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

        private string Unavailable(ShipComponentDef definition, HullPoint point)
        {
            if (definition.IsLocked) return $"Locked - price: {definition.Price}";
            if (point == null) return "Select a hull point.";
            if (!point.AcceptsComponent(definition))
                return $"This point does not accept a {definition.Size} {definition.Type}.";
            return null;
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
            if (m_SelectedPoint >= m_Pending.MountComponents.Length) m_SelectedPoint = 0;
        }

        public void SelectComponent(int category, int index)
        {
            if (!CanEdit) return;
            ShipComponentDef[] items = Items(category);
            if (index < 0 || index >= items.Length) return;
            string unavailable = Unavailable(items[index], SelectedPoint);
            if (unavailable != null) { ShowHover(unavailable); return; }
            m_Pending.SetMountComponent(m_SelectedPoint, Array.IndexOf(m_Catalog.Components, items[index]));
            Submit();
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
            m_SelectedPoint = 0;
            Submit();
            Refresh();
        }

        private void SelectPoint(int point)
        {
            if (!CanEdit) return;
            m_SelectedPoint = point;
            HullPoint selectedPoint = SelectedPoint;
            if (!Items(m_Category).Any(item => selectedPoint.AcceptsComponent(item)))
            {
                foreach (int category in new[] { 0, 1, 3, 4 })
                    if (Items(category).Any(item => selectedPoint.AcceptsComponent(item))) { m_Category = category; break; }
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

        private void ShowCategory(int category)
        {
            if (!CanEdit) return;
            m_Category = category;
            Refresh();
        }

        private void ShowHover(string description) =>
            m_HoverDescription.text = description ?? "Select a hull point, then choose a compatible component.";

        private void Refresh()
        {
            HullDef hull = m_Catalog.Hulls[m_Pending.Hull];
            m_HullName.text = hull.DisplayName + (hull.IsLocked ? $"\nLocked - {hull.Price}" : string.Empty);
            m_HullDescription.text = hull.Description;
            if (hull.IsLocked) m_HullDescription.text += $"\nLocked - price: {hull.Price}";
            m_PreviousHull.interactable = m_NextHull.interactable = m_Catalog.Hulls.Length > 1;
            ShipComponentDef[] all = m_Catalog.Components;
            HullPoint point = SelectedPoint;
            int selected = point == null ? -1 : m_Pending.MountComponents[m_SelectedPoint];
            for (int category = 0; category < m_Categories.Length; category++)
            {
                LoadoutCategoryView view = m_Categories[category];
                view.Grid.SetActive(category == m_Category);
                view.Tab.interactable = category != m_Category;
                ShipComponentDef[] items = Items(category);
                for (int index = 0; index < view.Buttons.Length; index++)
                    view.Buttons[index].Bind(index < items.Length ? items[index] : null,
                        index < items.Length && selected >= 0 && all[selected] == items[index],
                        index < items.Length ? Unavailable(items[index], point) : null);
            }
            m_Slots[0].interactable = point != null && selected >= 0;
            m_SlotLabels[0].text = point == null ? "No point" :
                $"{point.name} ({point.Size})\n{(selected < 0 ? "Empty" : "Click to clear")}";
            m_ColourLabel.text = $"Primary: {PlayerPalette.IndexOf(m_Pending.Colour0) + 1}   Secondary: {PlayerPalette.IndexOf(m_Pending.Colour1) + 1}";
            m_Preview.Show(m_Pending, m_SelectedPoint, SelectPoint);
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
    }
}
