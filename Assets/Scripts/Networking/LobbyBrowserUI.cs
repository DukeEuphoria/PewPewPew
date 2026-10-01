using System.Collections.Generic;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
// Steamworks.Data also declares Color/Image; disambiguate in favor of Unity's types.
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;

namespace PewPewPew.Networking
{
    /// Connects scene-authored host and join controls to SteamLobbyManager.
    public class LobbyBrowserUI : MonoBehaviour
    {
        [FormerlySerializedAs("lobbyManager"), SerializeField] private SteamLobbyManager m_LobbyManager;
        [FormerlySerializedAs("hostButton"), SerializeField] private Button m_HostButton;
        [FormerlySerializedAs("lobbyNameInput"), SerializeField] private InputField m_LobbyNameInput;
        [FormerlySerializedAs("refreshButton"), SerializeField] private Button m_RefreshButton;
        [FormerlySerializedAs("listContent"), SerializeField] private RectTransform m_ListContent;
        [FormerlySerializedAs("statusText"), SerializeField] private Text m_StatusText;
        [FormerlySerializedAs("maxPlayers"), SerializeField, Min(1)] private int m_MaxPlayers = 8;

        private readonly List<GameObject> m_ListEntries = new List<GameObject>();
        private Font m_UiFont;
        private bool m_Initialized;

        private void Start()
        {
            if (m_LobbyManager == null) m_LobbyManager = SteamLobbyManager.Instance;
            if (m_LobbyManager == null)
            {
                Debug.LogError("LobbyBrowserUI requires a scene SteamLobbyManager.", this);
                enabled = false;
                return;
            }

            m_UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (m_UiFont == null)
            {
                m_UiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            m_Initialized = true;
            Subscribe();
            BindButtons();
            OnStatusChanged("Ready.");
        }

        private void OnEnable()
        {
            if (!m_Initialized) return;
            Subscribe();
            BindButtons();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnbindButtons();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnbindButtons();
        }

        private void Subscribe()
        {
            if (m_LobbyManager == null) return;
            m_LobbyManager.LobbyListUpdated -= OnLobbyListUpdated;
            m_LobbyManager.LobbyListUpdated += OnLobbyListUpdated;
            m_LobbyManager.StatusChanged -= OnStatusChanged;
            m_LobbyManager.StatusChanged += OnStatusChanged;
        }

        private void Unsubscribe()
        {
            if (m_LobbyManager == null) return;
            m_LobbyManager.LobbyListUpdated -= OnLobbyListUpdated;
            m_LobbyManager.StatusChanged -= OnStatusChanged;
        }

        private void BindButtons()
        {
            if (m_HostButton != null)
            {
                m_HostButton.onClick.RemoveListener(HostLobby);
                m_HostButton.onClick.AddListener(HostLobby);
            }
            if (m_RefreshButton != null)
            {
                m_RefreshButton.onClick.RemoveListener(RefreshLobbies);
                m_RefreshButton.onClick.AddListener(RefreshLobbies);
            }
        }

        private void UnbindButtons()
        {
            m_HostButton?.onClick.RemoveListener(HostLobby);
            m_RefreshButton?.onClick.RemoveListener(RefreshLobbies);
        }

        private void HostLobby()
        {
            m_LobbyManager?.HostLobby(m_LobbyNameInput != null ? m_LobbyNameInput.text : string.Empty, m_MaxPlayers);
        }

        private void RefreshLobbies()
        {
            m_LobbyManager?.RefreshLobbies();
        }

        private void OnStatusChanged(string status)
        {
            if (m_StatusText != null) m_StatusText.text = status;
        }

        private void OnLobbyListUpdated(Lobby[] lobbies)
        {
            foreach (GameObject entry in m_ListEntries) Destroy(entry);
            m_ListEntries.Clear();
            if (m_ListContent == null || lobbies == null) return;

            foreach (var lobby in lobbies)
            {
                string lobbyName = lobby.GetData("LobbyName");
                if (string.IsNullOrEmpty(lobbyName)) lobbyName = $"{lobby.Owner.Name}'s Game";

                var row = new GameObject("LobbyRow", typeof(RectTransform));
                row.transform.SetParent(m_ListContent, false);
                var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.spacing = 8;
                row.AddComponent<LayoutElement>().minHeight = 28;

                Text nameText = CreateText(row.transform, $"{lobbyName} ({lobby.MemberCount}/{lobby.MaxMembers})", 14, FontStyle.Normal);
                nameText.alignment = TextAnchor.MiddleLeft;
                nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
                nameText.GetComponent<LayoutElement>().flexibleWidth = 1; // takes remaining space so the Join button doesn't squeeze it left

                var joinButton = CreateButton(row.transform, "Join");
                var joinLayout = joinButton.GetComponent<LayoutElement>();
                joinLayout.flexibleWidth = 0;
                joinLayout.preferredWidth = 70;
                Lobby capturedLobby = lobby;
                joinButton.onClick.AddListener(() => m_LobbyManager?.JoinLobby(capturedLobby));

                m_ListEntries.Add(row);
            }
        }

        private Text CreateText(Transform parent, string content, int fontSize, FontStyle style)
        {
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = m_UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.text = content;
            textObject.AddComponent<LayoutElement>().minHeight = fontSize + 8;
            return text;
        }

        private Button CreateButton(Transform parent, string label)
        {
            var buttonObject = new GameObject("Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f);
            var button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<LayoutElement>().minHeight = 32;

            CreateText(buttonObject.transform, label, 14, FontStyle.Bold).alignment = TextAnchor.MiddleCenter;
            StretchToParent(buttonObject.transform.GetChild(0).GetComponent<RectTransform>());

            return button;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6, 2);
            rect.offsetMax = new Vector2(-6, -2);
        }
    }
}
