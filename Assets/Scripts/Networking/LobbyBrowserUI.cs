using System.Collections.Generic;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;
// Steamworks.Data also declares Color/Image; disambiguate in favor of Unity's types.
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;

namespace PewPewPew.Networking
{
    /// Builds a minimal runtime UGUI lobby browser (host/refresh/join) with no scene setup required.
    public class LobbyBrowserUI : MonoBehaviour
    {
        private const int MaxPlayers = 8;

        private Text statusText;
        private InputField lobbyNameInput;
        private RectTransform listContent;
        private readonly List<GameObject> listEntries = new List<GameObject>();

        private Font uiFont;

        private void Awake()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            BuildUI();
        }

        private void OnEnable()
        {
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.LobbyListUpdated += OnLobbyListUpdated;
                SteamLobbyManager.Instance.StatusChanged += OnStatusChanged;
            }
        }

        private void OnDisable()
        {
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.LobbyListUpdated -= OnLobbyListUpdated;
                SteamLobbyManager.Instance.StatusChanged -= OnStatusChanged;
            }
        }

        private void BuildUI()
        {
            var canvasObject = new GameObject("LobbyCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvasObject.transform, new Vector2(420, 600), new Color(0f, 0f, 0f, 0.75f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20, -20);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateText(panel.transform, "PewPewPew Lobby", 20, FontStyle.Bold);

            lobbyNameInput = CreateInputField(panel.transform, "Lobby name...");

            var hostButton = CreateButton(panel.transform, "Host Lobby");
            hostButton.onClick.AddListener(() =>
                SteamLobbyManager.Instance?.HostLobby(lobbyNameInput.text, MaxPlayers));

            var refreshButton = CreateButton(panel.transform, "Refresh Lobby List");
            refreshButton.onClick.AddListener(() => SteamLobbyManager.Instance?.RefreshLobbies());

            var scrollArea = CreateScrollArea(panel.transform, out listContent);
            scrollArea.GetComponent<LayoutElement>().flexibleHeight = 1;

            statusText = CreateText(panel.transform, "Ready.", 14, FontStyle.Italic);
        }

        private void OnStatusChanged(string status)
        {
            if (statusText != null) statusText.text = status;
        }

        private void OnLobbyListUpdated(Lobby[] lobbies)
        {
            foreach (var entry in listEntries) Destroy(entry);
            listEntries.Clear();

            foreach (var lobby in lobbies)
            {
                string lobbyName = lobby.GetData("LobbyName");
                if (string.IsNullOrEmpty(lobbyName)) lobbyName = $"{lobby.Owner.Name}'s Game";

                var row = new GameObject("LobbyRow", typeof(RectTransform));
                row.transform.SetParent(listContent, false);
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
                joinButton.onClick.AddListener(() => SteamLobbyManager.Instance?.JoinLobby(capturedLobby));

                listEntries.Add(row);
            }
        }

        private GameObject CreatePanel(Transform parent, Vector2 size, Color color)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private Text CreateText(Transform parent, string content, int fontSize, FontStyle style)
        {
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.text = content;
            textObject.AddComponent<LayoutElement>().minHeight = fontSize + 8;
            return text;
        }

        private InputField CreateInputField(Transform parent, string placeholder)
        {
            var fieldObject = new GameObject("InputField", typeof(RectTransform));
            fieldObject.transform.SetParent(parent, false);
            fieldObject.AddComponent<Image>().color = Color.white;
            var inputField = fieldObject.AddComponent<InputField>();
            fieldObject.AddComponent<LayoutElement>().minHeight = 30;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(fieldObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            StretchToParent(textObject.GetComponent<RectTransform>());

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObject.transform.SetParent(fieldObject.transform, false);
            var placeholderText = placeholderObject.AddComponent<Text>();
            placeholderText.font = uiFont;
            placeholderText.fontSize = 14;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(0f, 0f, 0f, 0.5f);
            placeholderText.text = placeholder;
            StretchToParent(placeholderObject.GetComponent<RectTransform>());

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
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

        private GameObject CreateScrollArea(Transform parent, out RectTransform content)
        {
            var scrollObject = new GameObject("ScrollView", typeof(RectTransform));
            scrollObject.transform.SetParent(parent, false);
            scrollObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            var scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollObject.AddComponent<LayoutElement>().minHeight = 250;
            scrollObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(scrollObject.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            var contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            return scrollObject;
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
