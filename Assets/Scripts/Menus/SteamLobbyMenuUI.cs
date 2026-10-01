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
        private Lobby? m_PendingInviteLobby;

        private void Awake()
        {
            Debug.Assert(m_MenuScreenFlow != null, "SteamLobbyMenuUI requires a MenuScreenFlow reference.", this);
            Debug.Assert(m_SteamLobbyManager != null, "SteamLobbyMenuUI requires a SteamLobbyManager reference.", this);
            Debug.Assert(m_Canvas != null, "SteamLobbyMenuUI requires a scene Canvas reference.", this);
            if (m_MenuScreenFlow == null || m_SteamLobbyManager == null || m_Canvas == null)
            {
                enabled = false;
                return;
            }

            InitializeFont();
        }

        private void Start()
        {
            BindActions();
            m_MenuScreenFlow.ScreenChanged += OnScreenChanged;
            m_SteamLobbyManager.ActiveLobbyCountChanged += OnActiveLobbyCountChanged;
            m_SteamLobbyManager.LobbyListUpdated += OnLobbyListUpdated;
            m_SteamLobbyManager.StatusChanged += OnStatusChanged;
            m_SteamLobbyManager.GameplayReady += OnGameplayReady;
            m_SteamLobbyManager.PasswordRequired += OnPasswordRequired;

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
            m_SteamLobbyManager.GameplayReady -= OnGameplayReady;
            m_SteamLobbyManager.PasswordRequired -= OnPasswordRequired;
        }

        private void InitializeFont()
        {
            m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (m_Font == null) m_Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

            if (screen == MenuScreenId.Join)
            {
                if (m_PendingInviteLobby.HasValue)
                {
                    Lobby inviteLobby = m_PendingInviteLobby.Value;
                    m_PendingInviteLobby = null;
                    OnLobbyListUpdated(new[] { inviteLobby });
                }
                else
                {
                    SearchLobbies(m_FriendsOnly);
                }
            }
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

        private void OnGameplayReady()
        {
            m_MenuScreenFlow.ShowScreen(MenuScreenId.Gameplay);
        }

        private void OnPasswordRequired(Lobby lobby)
        {
            m_PendingInviteLobby = lobby;
            if (m_JoinPasswordInput != null) m_JoinPasswordInput.text = string.Empty;

            if (m_MenuScreenFlow.CurrentScreen == MenuScreenId.Join)
            {
                m_PendingInviteLobby = null;
                OnLobbyListUpdated(new[] { lobby });
            }
            else
            {
                m_MenuScreenFlow.ShowScreen(MenuScreenId.Join);
            }

            OnStatusChanged("Enter the lobby password and select Join.");
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