using System;
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
            for (int category = 0; category < m_Categories.Length; category++)
            {
                int capturedCategory = category;
                m_Categories[category].Tab.onClick.AddListener(() => ShowCategory(capturedCategory));
                for (int index = 0; index < m_Categories[category].Buttons.Length; index++)
                {
                    int capturedIndex = index;
                    m_Categories[category].Buttons[index].Initialize(
                        () => SelectComponent(capturedCategory, capturedIndex), ShowHover);
                }
            }
            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                int captured = slot;
                m_Slots[slot].onClick.AddListener(() => ClearSlot(captured));
            }
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
                    m_Catalog = ShipCatalog.Instance;
                    if (!ValidateCatalog())
                    {
                        m_Panel.SetActive(false);
                        enabled = false;
                        return;
                    }
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

        private bool ValidateCatalog()
        {
            if (m_Catalog == null) return false; // ShipCatalog logs the missing asset.
            if (m_Catalog.Hulls == null || m_Catalog.Hulls.Length == 0 || Array.Exists(m_Catalog.Hulls, h => h == null))
            {
                Debug.LogError("Loadout menu requires a non-empty hull catalog with no missing definitions.", this);
                return false;
            }
            for (int category = 0; category < m_Categories.Length; category++)
            {
                ShipComponentDef[] items = Items(category);
                if (items == null || (category != 4 && items.Length == 0) || Array.Exists(items, item => item == null) ||
                    items.Length > m_Categories[category].Buttons.Length)
                {
                    Debug.LogError($"Loadout category {category} is empty, contains missing definitions, or exceeds its static button capacity.", this);
                    return false;
                }
            }
            return true;
        }

        private ShipComponentDef[] Items(int category)
        {
            switch (category)
            {
                case 0: return m_Catalog.Shields;
                case 1: return m_Catalog.MainGuns;
                case 2: return m_Catalog.SecondaryGuns;
                case 3: return m_Catalog.Thrusters;
                case 4: return m_Catalog.SubSystems;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private int Selected(int category)
        {
            switch (category)
            {
                case 0: return m_Pending.Shield;
                case 1: return m_Pending.MainGun;
                case 2: return m_Pending.SecondaryGun;
                case 3: return m_Pending.Thruster;
                default: return -1;
            }
        }

        private string Unavailable(int category, int index)
        {
            ShipComponentDef definition = Items(category)[index];
            if (definition.IsLocked) return $"Locked - price: {definition.Price}";
            HullDef hull = m_Catalog.Hulls[m_Pending.Hull];
            bool compatible = category == 1 ? m_Catalog.IsCompatible(hull, m_Catalog.MainGuns[index], false) :
                category == 2 ? m_Catalog.IsCompatible(hull, m_Catalog.SecondaryGuns[index], true) :
                category == 3 ? m_Catalog.IsCompatible(hull, m_Catalog.Thrusters[index]) : true;
            if (!compatible) return "This hull does not have enough attachment points.";
            if (category == 4 && FirstEmptySlot() < 0) return "All subsystem slots are full. Clear a slot to fit this component.";
            return null;
        }

        private int FirstEmptySlot()
        {
            int count = m_Catalog.Hulls[m_Pending.Hull].SubSystemSlots;
            for (int slot = 0; slot < count; slot++)
                if (m_Pending.GetSubSystem(slot) < 0) return slot;
            return -1;
        }

        private void Normalize()
        {
            if (!LoadoutMath.InRange(m_Pending.Hull, m_Catalog.Hulls.Length, false)) m_Pending.Hull = 0;
            for (int category = 0; category < 4; category++)
            {
                int index = Selected(category);
                if (LoadoutMath.InRange(index, Items(category).Length, false) && Unavailable(category, index) == null) continue;
                int replacement = Array.FindIndex(Items(category), item => !item.IsLocked);
                for (int candidate = 0; candidate < Items(category).Length; candidate++)
                    if (Unavailable(category, candidate) == null) { replacement = candidate; break; }
                SetMajor(category, replacement >= 0 ? replacement : 0);
            }
            for (int slot = 0; slot < ShipLoadout.SubSystemSlots; slot++)
            {
                int index = m_Pending.GetSubSystem(slot);
                if (slot >= m_Catalog.Hulls[m_Pending.Hull].SubSystemSlots ||
                    !LoadoutMath.InRange(index, m_Catalog.SubSystems.Length, true) ||
                    (index >= 0 && m_Catalog.SubSystems[index].IsLocked))
                    m_Pending.SetSubSystem(slot, -1);
            }
        }

        private void SetMajor(int category, int index)
        {
            switch (category)
            {
                case 0: m_Pending.Shield = index; break;
                case 1: m_Pending.MainGun = index; break;
                case 2: m_Pending.SecondaryGun = index; break;
                case 3: m_Pending.Thruster = index; break;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public void SelectComponent(int category, int index)
        {
            if (!CanEdit) return;
            string unavailable = Unavailable(category, index);
            if (unavailable != null) { ShowHover(unavailable); return; }
            if (category == 4) m_Pending.SetSubSystem(FirstEmptySlot(), index);
            else SetMajor(category, index);
            Submit();
            Refresh();
        }

        public void CycleHull(int step)
        {
            if (!CanEdit) return;
            m_Pending.Hull = LoadoutMath.Cycle(m_Pending.Hull, m_Catalog.Hulls.Length, step, false);
            Normalize();
            Submit();
            Refresh();
        }

        public void ClearSlot(int slot)
        {
            if (!CanEdit) return;
            m_Pending.SetSubSystem(slot, -1);
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

        private void ShowHover(string description)
        {
            m_HoverDescription.text = description ?? "Hover over a component to see its description and availability.";
        }

        private void Refresh()
        {
            HullDef hull = m_Catalog.Hulls[m_Pending.Hull];
            m_HullName.text = hull.DisplayName + (hull.IsLocked ? $"\nLocked - {hull.Price}" : string.Empty);
            m_HullDescription.text = hull.Description;
            if (hull.IsLocked) m_HullDescription.text += $"\nLocked - price: {hull.Price}";
            m_PreviousHull.interactable = m_NextHull.interactable = m_Catalog.Hulls.Length > 1;
            for (int category = 0; category < m_Categories.Length; category++)
            {
                LoadoutCategoryView view = m_Categories[category];
                view.Grid.SetActive(category == m_Category);
                view.Tab.interactable = category != m_Category;
                ShipComponentDef[] items = Items(category);
                for (int index = 0; index < view.Buttons.Length; index++)
                {
                    bool selected = Selected(category) == index;
                    if (category == 4)
                        for (int slot = 0; slot < hull.SubSystemSlots; slot++)
                            selected |= m_Pending.GetSubSystem(slot) == index;
                    view.Buttons[index].Bind(index < items.Length ? items[index] : null, selected,
                        index < items.Length ? Unavailable(category, index) : null);
                }
            }
            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                int index = m_Pending.GetSubSystem(slot);
                bool available = slot < hull.SubSystemSlots;
                m_Slots[slot].interactable = available && index >= 0;
                m_SlotLabels[slot].text = !available ? "Unavailable" : index < 0 ? "Empty" :
                    m_Catalog.SubSystems[index].DisplayName + "\nClick to clear";
            }
            m_ColourLabel.text = $"Primary: {PlayerPalette.IndexOf(m_Pending.Colour0) + 1}   Secondary: {PlayerPalette.IndexOf(m_Pending.Colour1) + 1}";
            m_Preview.Show(m_Pending);
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
