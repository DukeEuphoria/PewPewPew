using System.Collections.Generic;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;
using Lobby = Steamworks.Data.Lobby;

namespace PewPewPew.Networking
{
    public class SteamLobbyMenuUI : MonoBehaviour
    {
        [FormerlySerializedAs("menuScreenFlow"), SerializeField] private MenuScreenFlow m_MenuScreenFlow;
        [FormerlySerializedAs("steamLobbyManager"), SerializeField] private SteamLobbyManager m_SteamLobbyManager;
        [SerializeField] private Canvas m_Canvas;
        [SerializeField] private GameObject m_TitlePanel;
        [SerializeField] private GameObject m_HostPanel;
        [SerializeField] private GameObject m_JoinPanel;
        [SerializeField] private Text m_ActiveGamesText;
        [SerializeField] private Text m_HostStatusText;
        [SerializeField] private Text m_JoinStatusText;
        [SerializeField] private InputField m_LobbyNameInput;
        [SerializeField] private InputField m_HostPasswordInput;
        [SerializeField] private InputField m_JoinPasswordInput;
        [SerializeField] private Text m_PlayerCountText;
        [SerializeField] private RectTransform m_LobbyListContent;
        [SerializeField] private Button m_FreeForAllButton;
        [SerializeField] private Button m_TeamDeathmatchButton;
        [SerializeField] private Button m_DecrementButton;
        [SerializeField] private Button m_IncrementButton;
        [SerializeField] private Button m_CreateLobbyButton;
        [SerializeField] private Button m_PublicSearchButton;
        [SerializeField] private Button m_FriendsSearchButton;
        [SerializeField] private Button m_RefreshButton;

        private readonly List<GameObject> m_LobbyRows = new List<GameObject>();

        private Font m_Font;
        private GameMode m_SelectedGameMode;
        private int m_MaxPlayers = 8;
        private bool m_FriendsOnly;

        private void Awake()
        {
            Debug.Assert(m_MenuScreenFlow != null, "SteamLobbyMenuUI requires a MenuScreenFlow reference.", this);
            Debug.Assert(m_SteamLobbyManager != null, "SteamLobbyMenuUI requires a SteamLobbyManager reference.", this);
            if (m_MenuScreenFlow == null || m_SteamLobbyManager == null)
            {
                enabled = false;
                return;
            }

            InitializeFont();
            if (m_Canvas == null) BuildUI();
        }

        public bool HasSceneObjects => m_Canvas != null;

        public void BuildSceneObjects()
        {
            if (m_Canvas != null) return;
            InitializeFont();
            BuildUI();
        }

        private void Start()
        {
            BindActions();
            m_MenuScreenFlow.ScreenChanged += OnScreenChanged;
            m_SteamLobbyManager.ActiveLobbyCountChanged += OnActiveLobbyCountChanged;
            m_SteamLobbyManager.LobbyListUpdated += OnLobbyListUpdated;
            m_SteamLobbyManager.StatusChanged += OnStatusChanged;

            if (m_MenuScreenFlow.CurrentScreen.HasValue)
            {
                OnScreenChanged(m_MenuScreenFlow.CurrentScreen.Value);
            }

            m_SteamLobbyManager.RefreshActiveLobbyCount();
        }

        private void OnDestroy()
        {
            UnbindActions();
            if (m_MenuScreenFlow != null) m_MenuScreenFlow.ScreenChanged -= OnScreenChanged;
            if (m_SteamLobbyManager == null) return;

            m_SteamLobbyManager.ActiveLobbyCountChanged -= OnActiveLobbyCountChanged;
            m_SteamLobbyManager.LobbyListUpdated -= OnLobbyListUpdated;
            m_SteamLobbyManager.StatusChanged -= OnStatusChanged;
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("SteamLobbyCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localScale = Vector3.one;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            StretchToParent(canvasRect, 0f, 0f, 0f, 0f);
            m_Canvas = canvasObject.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = -1;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            m_TitlePanel = new GameObject("TitleLobbyCount", typeof(RectTransform));
            m_TitlePanel.transform.SetParent(m_Canvas.transform, false);
            RectTransform titleRect = m_TitlePanel.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(1f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(1f, 1f);
            titleRect.anchoredPosition = new Vector2(-24f, -24f);
            titleRect.sizeDelta = new Vector2(340f, 48f);
            m_ActiveGamesText = CreateText(m_TitlePanel.transform, "Games in progress: ...", 22, FontStyle.Bold);
            StretchToParent(m_ActiveGamesText.rectTransform, 0f, 0f, 0f, 0f);
            m_ActiveGamesText.alignment = TextAnchor.MiddleRight;
            m_ActiveGamesText.raycastTarget = false;

            m_HostPanel = CreatePanel("HostLobbyPanel", new Vector2(500f, 520f));
            BuildHostPanel(m_HostPanel.transform);
            m_HostPanel.SetActive(false);

            m_JoinPanel = CreatePanel("JoinLobbyPanel", new Vector2(620f, 600f));
            BuildJoinPanel(m_JoinPanel.transform);
            m_JoinPanel.SetActive(false);

            m_TitlePanel.SetActive(false);
        }

        private void InitializeFont()
        {
            m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (m_Font == null) m_Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void BuildHostPanel(Transform parent)
        {
            AddSectionTitle(parent, "Host Game");
            AddLabel(parent, "Lobby name");
            m_LobbyNameInput = CreateInputField(parent, "Enter a lobby name");
            AddLabel(parent, "Password (optional)");
            m_HostPasswordInput = CreateInputField(parent, "Leave blank for no password", InputField.ContentType.Standard);

            AddLabel(parent, "Game mode");
            GameObject modeRow = CreateRow(parent, "GameModeButtons");
            m_FreeForAllButton = CreateButton(modeRow.transform, "Free-for-All");
            m_TeamDeathmatchButton = CreateButton(modeRow.transform, "Team Deathmatch");
            AddLabel(parent, "Maximum players");
            GameObject playerCountRow = CreateRow(parent, "PlayerCountStepper");
            m_DecrementButton = CreateButton(playerCountRow.transform, "-");
            m_PlayerCountText = CreateText(playerCountRow.transform, m_MaxPlayers.ToString(), 18, FontStyle.Bold);
            m_PlayerCountText.alignment = TextAnchor.MiddleCenter;
            LayoutElement countLayout = m_PlayerCountText.gameObject.AddComponent<LayoutElement>();
            countLayout.preferredWidth = 90f;
            countLayout.minHeight = 26f;
            countLayout.preferredHeight = 26f;
            m_IncrementButton = CreateButton(playerCountRow.transform, "+");

            m_CreateLobbyButton = CreateButton(parent, "Create Lobby");
            m_HostStatusText = AddStatusText(parent);

            SetGameMode(GameMode.FreeForAll);
        }

        private void BuildJoinPanel(Transform parent)
        {
            AddSectionTitle(parent, "Join Game");
            GameObject searchRow = CreateRow(parent, "LobbySearchTabs");
            m_PublicSearchButton = CreateButton(searchRow.transform, "Public Games");
            m_FriendsSearchButton = CreateButton(searchRow.transform, "Friends' Games");
            m_RefreshButton = CreateButton(searchRow.transform, "Refresh");

            m_JoinPasswordInput = CreateInputField(parent, "Password (if required)", InputField.ContentType.Standard);
            m_JoinStatusText = AddStatusText(parent);
            CreateLobbyScrollArea(parent, out m_LobbyListContent);
        }

        private void BindActions()
        {
            BindButton(m_FreeForAllButton, SelectFreeForAll);
            BindButton(m_TeamDeathmatchButton, SelectTeamDeathmatch);
            BindButton(m_DecrementButton, DecreaseMaxPlayers);
            BindButton(m_IncrementButton, IncreaseMaxPlayers);
            BindButton(m_CreateLobbyButton, HostLobby);
            BindButton(m_PublicSearchButton, SearchPublicLobbies);
            BindButton(m_FriendsSearchButton, SearchFriendsLobbies);
            BindButton(m_RefreshButton, RefreshCurrentSearch);
        }

        private void UnbindActions()
        {
            UnbindButton(m_FreeForAllButton, SelectFreeForAll);
            UnbindButton(m_TeamDeathmatchButton, SelectTeamDeathmatch);
            UnbindButton(m_DecrementButton, DecreaseMaxPlayers);
            UnbindButton(m_IncrementButton, IncreaseMaxPlayers);
            UnbindButton(m_CreateLobbyButton, HostLobby);
            UnbindButton(m_PublicSearchButton, SearchPublicLobbies);
            UnbindButton(m_FriendsSearchButton, SearchFriendsLobbies);
            UnbindButton(m_RefreshButton, RefreshCurrentSearch);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button == null) return;
            button.onClick.RemoveListener(listener);
            button.onClick.AddListener(listener);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button != null) button.onClick.RemoveListener(listener);
        }

        private GameObject CreatePanel(string objectName, Vector2 size)
        {
            GameObject panel = new GameObject(objectName, typeof(RectTransform));
            panel.transform.SetParent(m_Canvas.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            panel.AddComponent<Image>().color = new Color(0.035f, 0.06f, 0.09f, 0.94f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private void AddSectionTitle(Transform parent, string value)
        {
            Text title = CreateText(parent, value, 28, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleCenter;
            LayoutElement layout = title.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 44f;
            layout.preferredHeight = 44f;
        }

        private void AddLabel(Transform parent, string value)
        {
            Text label = CreateText(parent, value, 15, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleLeft;
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 22f;
            layout.preferredHeight = 22f;
        }

        private Text AddStatusText(Transform parent)
        {
            Text statusText = CreateText(parent, "Ready.", 14, FontStyle.Normal);
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            LayoutElement layout = statusText.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 24f;
            layout.preferredHeight = 24f;
            return statusText;
        }

        private InputField CreateInputField(Transform parent, string placeholder, InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            GameObject fieldObject = new GameObject("InputField", typeof(RectTransform));
            fieldObject.transform.SetParent(parent, false);
            fieldObject.AddComponent<Image>().color = new Color(0.92f, 0.95f, 0.97f, 1f);
            InputField inputField = fieldObject.AddComponent<InputField>();
            inputField.contentType = contentType;
            LayoutElement fieldLayout = fieldObject.AddComponent<LayoutElement>();
            fieldLayout.minHeight = 38f;
            fieldLayout.preferredHeight = 38f;

            Text text = CreateText(fieldObject.transform, string.Empty, 16, FontStyle.Normal);
            text.color = new Color(0.05f, 0.08f, 0.1f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            StretchToParent(text.rectTransform, 8f, 4f, -8f, -4f);

            Text placeholderText = CreateText(fieldObject.transform, placeholder, 15, FontStyle.Italic);
            placeholderText.color = new Color(0.35f, 0.4f, 0.43f, 1f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            StretchToParent(placeholderText.rectTransform, 8f, 4f, -8f, -4f);
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
        }

        private Button CreateButton(Transform parent, string label)
        {
            GameObject buttonObject = new GameObject("Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.37f, 0.42f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
            buttonLayout.minHeight = 38f;
            buttonLayout.preferredHeight = 38f;

            Text buttonText = CreateText(buttonObject.transform, label, 15, FontStyle.Bold);
            buttonText.alignment = TextAnchor.MiddleCenter;
            StretchToParent(buttonText.rectTransform, 6f, 2f, -6f, -2f);
            return button;
        }

        private GameObject CreateRow(Transform parent, string objectName)
        {
            GameObject row = new GameObject(objectName, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            LayoutElement rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 38f;
            rowLayoutElement.preferredHeight = 38f;
            return row;
        }

        private void CreateLobbyScrollArea(Transform parent, out RectTransform content)
        {
            GameObject scrollObject = new GameObject("LobbyList", typeof(RectTransform));
            scrollObject.transform.SetParent(parent, false);
            scrollObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            StretchToParent(viewport, 0f, 0f, 0f, 0f);
            viewportObject.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(6, 6, 6, 6);
            contentLayout.spacing = 5f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }

        private Text CreateText(Transform parent, string value, int fontSize, FontStyle style)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = m_Font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void SetGameMode(GameMode gameMode)
        {
            m_SelectedGameMode = gameMode;
            if (m_FreeForAllButton != null)
            {
                m_FreeForAllButton.targetGraphic.color = gameMode == GameMode.FreeForAll
                    ? new Color(0.12f, 0.58f, 0.52f, 1f)
                    : new Color(0.13f, 0.37f, 0.42f, 1f);
            }
            if (m_TeamDeathmatchButton != null)
            {
                m_TeamDeathmatchButton.targetGraphic.color = gameMode == GameMode.TeamDeathmatch
                    ? new Color(0.12f, 0.58f, 0.52f, 1f)
                    : new Color(0.13f, 0.37f, 0.42f, 1f);
            }
        }

        private void SelectFreeForAll()
        {
            SetGameMode(GameMode.FreeForAll);
        }

        private void SelectTeamDeathmatch()
        {
            SetGameMode(GameMode.TeamDeathmatch);
        }

        private void DecreaseMaxPlayers()
        {
            SetMaxPlayers(m_MaxPlayers - 1);
        }

        private void IncreaseMaxPlayers()
        {
            SetMaxPlayers(m_MaxPlayers + 1);
        }

        private void SetMaxPlayers(int maxPlayers)
        {
            m_MaxPlayers = Mathf.Clamp(maxPlayers, 2, 16);
            if (m_PlayerCountText != null) m_PlayerCountText.text = m_MaxPlayers.ToString();
        }

        private void HostLobby()
        {
            m_SteamLobbyManager.HostLobby(
                m_LobbyNameInput.text,
                m_HostPasswordInput.text,
                m_SelectedGameMode,
                m_MaxPlayers);
        }

        private void SearchLobbies(bool friendsOnly)
        {
            m_FriendsOnly = friendsOnly;
            m_SteamLobbyManager.RefreshLobbies(friendsOnly);
        }

        private void SearchPublicLobbies()
        {
            SearchLobbies(false);
        }

        private void SearchFriendsLobbies()
        {
            SearchLobbies(true);
        }

        private void RefreshCurrentSearch()
        {
            SearchLobbies(m_FriendsOnly);
        }

        private void OnScreenChanged(MenuScreenId screen)
        {
            m_TitlePanel.SetActive(screen == MenuScreenId.Title);
            m_HostPanel.SetActive(screen == MenuScreenId.Host);
            m_JoinPanel.SetActive(screen == MenuScreenId.Join);

            if (screen == MenuScreenId.Join) SearchLobbies(m_FriendsOnly);
        }

        private void OnActiveLobbyCountChanged(int count)
        {
            m_ActiveGamesText.text = $"Games in progress: {count}";
        }

        private void OnStatusChanged(string status)
        {
            if (m_HostStatusText != null) m_HostStatusText.text = status;
            if (m_JoinStatusText != null) m_JoinStatusText.text = status;
        }

        private void OnLobbyListUpdated(Lobby[] lobbies)
        {
            foreach (GameObject row in m_LobbyRows) Destroy(row);
            m_LobbyRows.Clear();
            if (lobbies == null) return;

            foreach (Lobby lobby in lobbies)
            {
                string lobbyName = lobby.GetData("LobbyName");
                if (string.IsNullOrWhiteSpace(lobbyName)) lobbyName = $"{lobby.Owner.Name}'s Game";
                string gameMode = lobby.GetData("GameMode");
                if (string.IsNullOrWhiteSpace(gameMode)) gameMode = "Unknown mode";
                string passwordLabel = lobby.GetData("PasswordRequired") == "true" ? "  [Password]" : string.Empty;

                GameObject row = CreateRow(m_LobbyListContent, "LobbyRow");
                Text lobbyText = CreateText(row.transform,
                    $"{lobbyName}{passwordLabel}\n{gameMode}  |  {lobby.MemberCount}/{lobby.MaxMembers}  |  {lobby.Owner.Name}",
                    14,
                    FontStyle.Normal);
                lobbyText.alignment = TextAnchor.MiddleLeft;
                lobbyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                lobbyText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                Button joinButton = CreateButton(row.transform, "Join");
                joinButton.GetComponent<LayoutElement>().preferredWidth = 90f;
                Lobby capturedLobby = lobby;
                joinButton.onClick.AddListener(() => m_SteamLobbyManager.JoinLobby(capturedLobby, m_JoinPasswordInput.text));
                m_LobbyRows.Add(row);
            }

            if (lobbies.Length == 0) AddEmptyLobbyMessage();
        }

        private void AddEmptyLobbyMessage()
        {
            Text emptyText = CreateText(m_LobbyListContent, m_FriendsOnly ? "No friends are hosting a game." : "No public games found.", 15, FontStyle.Italic);
            emptyText.alignment = TextAnchor.MiddleCenter;
            emptyText.gameObject.AddComponent<LayoutElement>().minHeight = 48f;
            m_LobbyRows.Add(emptyText.gameObject);
        }

        private static void StretchToParent(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}