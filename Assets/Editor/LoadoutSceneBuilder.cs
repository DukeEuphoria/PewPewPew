using System;
using System.IO;
using System.Linq;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Ships;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PewPewPew.Editor
{
    public static class LoadoutSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private static readonly Color PanelColour = new Color(0.035f, 0.065f, 0.10f, 0.97f);
        private static readonly Color ButtonColour = new Color(0.10f, 0.18f, 0.25f, 1f);
        private static readonly Color Accent = new Color(0.25f, 0.85f, 0.95f, 1f);
        private static TMP_FontAsset s_Font;

        [MenuItem("PewPewPew/Loadout/Add separate hull hover area")]
        public static void UpgradeShipHoverArea()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadoutShipPreview preview = All(scene).Select(go => go.GetComponent<LoadoutShipPreview>()).Single(item => item != null);
            AddShipHoverArea(preview);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save hull hover area.");
            Debug.Log("Separate hull hover area saved.");
        }

        private static void AddShipHoverArea(LoadoutShipPreview preview)
        {
            if (preview.GetComponentInChildren<LoadoutPreviewHoverArea>(true) != null) return;
            RectTransform area = Rect("HullDescriptionHoverArea", preview.transform, 148, 86, 296, 258);
            area.anchorMin = new Vector2(0.25f, 0.2f);
            area.anchorMax = new Vector2(0.75f, 0.8f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var image = area.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            var hover = area.gameObject.AddComponent<LoadoutPreviewHoverArea>();
            var fields = new SerializedObject(hover);
            Set(fields, "m_Preview", preview);
            fields.ApplyModifiedPropertiesWithoutUndo();
            // Keep navigation above the transparent hit area.
            preview.transform.Find("PreviousHull").SetAsLastSibling();
            preview.transform.Find("NextHull").SetAsLastSibling();
        }

        [MenuItem("PewPewPew/Loadout/Upgrade rendered ship pane")]
        public static void UpgradeShipPane()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadoutMenuController controller = All(scene).Select(go => go.GetComponent<LoadoutMenuController>()).Single(item => item != null);
            if (controller.GetComponentInChildren<LoadoutShipPreview>(true) != null)
                throw new InvalidOperationException("The rendered ship pane already exists; edit its saved controls instead.");
            s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Transform right = controller.transform.Find("LoadoutPanel/RightPanel");
            var image = (RectTransform)right.Find("ShipImage");
            Place(image, right, 20, 56, 592, 430);
            image.GetComponent<Image>().raycastTarget = true;
            Object.DestroyImmediate(image.Find("PreviewPlaceholder").gameObject);
            var render = Raw("HullRender", image, 0, 0, 592, 430);
            Stretch(render.rectTransform, Vector2.zero, Vector2.one);
            RectTransform markersRoot = Rect("FittedComponents", image, 0, 0, 592, 430);
            Stretch(markersRoot, Vector2.zero, Vector2.one);
            markersRoot.gameObject.AddComponent<RectMask2D>();
            var markers = new LoadoutPreviewMarker[17];
            for (int i = 0; i < markers.Length; i++)
            {
                RectTransform marker = Surface("ComponentMarker" + i.ToString("00"), markersRoot, 0, 0, 68, 34);
                marker.pivot = new Vector2(0.5f, 0.5f);
                marker.GetComponent<Image>().raycastTarget = false;
                marker.GetComponent<Image>().color = new Color(0.025f, 0.07f, 0.1f, 0.78f);
                var art = Rect("Artwork", marker, 0, 0, 68, 34).gameObject.AddComponent<Image>();
                Stretch(art.rectTransform, Vector2.zero, Vector2.one);
                art.preserveAspect = true;
                art.raycastTarget = false;
                TMP_Text label = Label("NameFallback", marker, "", 3, 2, 62, 30, 11, Accent);
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 8;
                label.fontSizeMax = 11;
                markers[i] = marker.gameObject.AddComponent<LoadoutPreviewMarker>();
                var markerFields = new SerializedObject(markers[i]);
                Set(markerFields, "m_Artwork", art);
                Set(markerFields, "m_Label", label);
                markerFields.ApplyModifiedPropertiesWithoutUndo();
                marker.gameObject.SetActive(false);
            }
            TMP_Text hullName = right.Find("HullName").GetComponent<TMP_Text>();
            Place(hullName.rectTransform, image, 12, 8, 210, 48);
            hullName.alignment = TextAlignmentOptions.TopLeft;
            hullName.enableAutoSizing = true;
            hullName.fontSizeMin = 15;
            hullName.fontSizeMax = 23;
            RectTransform description = (RectTransform)right.Find("HullDescriptionPanel");
            Place(description, image, 48, 300, 496, 122);
            var viewport = (RectTransform)description.Find("Viewport");
            Stretch(viewport, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(12, 8);
            viewport.offsetMax = new Vector2(-12, -8);
            var descriptionGroup = description.gameObject.AddComponent<CanvasGroup>();
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
            foreach (string buttonName in new[] { "PreviousHull", "NextHull" })
            {
                Button button = right.Find(buttonName).GetComponent<Button>();
                Place((RectTransform)button.transform, image, buttonName == "PreviousHull" ? 8 : 548, 386, 36, 36);
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) Object.DestroyImmediate(label.gameObject);
                LobbySymbol arrow = Symbol("Arrow", button.transform, LobbySymbolKind.Play, 6, 6, 24, 24, Color.white);
                if (buttonName == "PreviousHull")
                {
                    var rect = (RectTransform)arrow.transform;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(18, -18);
                    rect.localRotation = Quaternion.Euler(0, 0, 180);
                }
                button.transform.SetAsLastSibling();
            }
            var preview = image.gameObject.AddComponent<LoadoutShipPreview>();
            var previewFields = new SerializedObject(preview);
            Set(previewFields, "m_Renderer", All(scene).Select(go => go.GetComponent<LobbyHullRenderer>()).Single(item => item != null));
            Set(previewFields, "m_Image", render);
            Set(previewFields, "m_Description", descriptionGroup);
            SetArray(previewFields.FindProperty("m_Markers"), markers);
            previewFields.ApplyModifiedPropertiesWithoutUndo();
            var controllerFields = new SerializedObject(controller);
            Set(controllerFields, "m_Preview", preview);
            controllerFields.ApplyModifiedPropertiesWithoutUndo();
            AddShipHoverArea(preview);
            preview.Show(ShipLoadout.Empty);
            // Render textures are runtime resources; do not persist a transient texture reference.
            render.texture = null;
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save rendered ship pane.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("Rendered ship pane saved: hull image, 17 static attachment overlays, triangle controls and hover description. Ship colours unchanged.");
        }

        [MenuItem("PewPewPew/Loadout/Upgrade bottom lobby UI")]
        public static void UpgradeLobby()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadoutMenuController controller = All(scene).Select(go => go.GetComponent<LoadoutMenuController>()).Single(item => item != null);
            if (controller.GetComponent<LobbyPanel>() != null)
            {
                foreach (LobbySymbol graphic in controller.GetComponentsInChildren<LobbySymbol>(true))
                    if (graphic.GetComponent<CanvasRenderer>() == null) graphic.gameObject.AddComponent<CanvasRenderer>();
                foreach (Image image in controller.GetComponentsInChildren<Image>(true).Where(image => image.name == "PortraitBackground"))
                    image.color = new Color(PanelColour.r, PanelColour.g, PanelColour.b, 1);
                AnchorLobbyPanels(controller);
                EditorSceneManager.SaveScene(scene);
                return;
            }
            s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Transform menu = controller.transform;
            RectTransform panel = (RectTransform)menu.Find("LoadoutPanel");
            Place(panel, menu, 24, 16, 1552, 642);
            RectTransform left = (RectTransform)panel.Find("LeftPanel");
            RectTransform right = (RectTransform)panel.Find("RightPanel");
            left.sizeDelta = new Vector2(900, 642);
            right.sizeDelta = new Vector2(632, 642);
            foreach (string gridName in new[] { "ShieldsGrid", "MainGunsGrid", "SecondaryGunsGrid", "ThrustersGrid", "SubSystemsGrid" })
            {
                var grid = (RectTransform)left.Find(gridName);
                grid.sizeDelta = new Vector2(860, 332);
                GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
                int columns = layout.constraintCount;
                layout.cellSize = new Vector2(layout.cellSize.x, (332 - (columns - 1) * 8) / columns);
            }
            Move(left, "SlotsTitle", 20, 454, 860, 24);
            for (int slot = 0; slot < 4; slot++) Move(left, "Slot" + (slot + 1), 20 + slot * 217, 484, 209, 54);
            Move(left, "HoverDescriptionPanel", 20, 550, 860, 76);
            Move(right, "ShipImage", 20, 56, 592, 240);
            Move(right.Find("ShipImage"), "PreviewPlaceholder", 20, 70, 552, 90);
            Move(right, "HullName", 150, 306, 332, 48);
            Move(right, "PreviousHull", 20, 306, 120, 48);
            Move(right, "NextHull", 492, 306, 120, 48);
            Move(right, "HullDescriptionPanel", 20, 366, 592, 120);
            Move(right, "ColoursToggle", 20, 498, 190, 38);
            Move(right, "ColourSelection", 224, 498, 388, 38);
            Move(right, "DeployStatus", 20, 548, 592, 70);
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            RectTransform lobby = Surface("LobbyPanel", menu, 24, 674, 1552, 210);
            TMP_Text lobbyStatus = Label("LobbyStatus", lobby, "LOBBY - all players ready to start", 16, 8, 1000, 30, 21, Accent);
            Button returnButton = MakeButton("ReturnToLobby", lobby, "Return to lobby", 1050, 4, 240, 32);
            returnButton.gameObject.SetActive(false);
            var cards = new LobbyPlayerCard[16];
            for (int i = 0; i < cards.Length; i++)
            {
                RectTransform card = Rect("PlayerCard" + (i + 1).ToString("00"), lobby, 16 + i * 81, 46, 78, 154);
                TMP_Text nameLabel = Label("PlayerName", card, "Player " + (i + 1), 0, 0, 78, 20, 12, Color.white);
                nameLabel.alignment = TextAlignmentOptions.Center;
                RectTransform outline = Surface("ReadyOutline", card, 0, 24, 78, 78);
                outline.GetComponent<Image>().color = new Color(0.16f, 0.25f, 0.32f);
                RectTransform background = Surface("PortraitBackground", outline, 3, 3, 72, 72);
                background.GetComponent<Image>().color = new Color(PanelColour.r, PanelColour.g, PanelColour.b, 1);
                RawImage avatar = Raw("SteamAvatar", background, 0, 0, 72, 72);
                RawImage hull = Raw("SelectedHull", background, 0, 0, 72, 72);
                avatar.enabled = hull.enabled = false;
                Symbol("KillsIcon", card, LobbySymbolKind.Crosshair, 2, 112, 18, 18, Color.white);
                TMP_Text kills = Label("Kills", card, "0", 23, 110, 17, 22, 14, Color.white);
                Symbol("DeathsIcon", card, LobbySymbolKind.Skull, 42, 112, 18, 18, Color.white);
                TMP_Text deaths = Label("Deaths", card, "0", 63, 110, 17, 22, 14, Color.white);
                cards[i] = card.gameObject.AddComponent<LobbyPlayerCard>();
                var cardFields = new SerializedObject(cards[i]);
                Set(cardFields, "m_Avatar", avatar);
                Set(cardFields, "m_Hull", hull);
                Set(cardFields, "m_ReadyOutline", outline.GetComponent<Image>());
                Set(cardFields, "m_NameLabel", nameLabel);
                Set(cardFields, "m_Kills", kills);
                Set(cardFields, "m_Deaths", deaths);
                cardFields.ApplyModifiedPropertiesWithoutUndo();
                card.gameObject.SetActive(false);
            }

            Button ready = right.Find("DeployButton").GetComponent<Button>();
            Place((RectTransform)ready.transform, lobby, 1344, 28, 192, 166);
            ready.name = "ReadyPlayStop";
            ready.GetComponent<Image>().color = ButtonColour;
            LobbySymbol symbol = Symbol("PlayStopSymbol", ready.transform, LobbySymbolKind.Play, 30, 8, 132, 132,
                new Color(0.12f, 0.8f, 0.3f));
            TMP_Text readyLabel = ready.GetComponentInChildren<TMP_Text>();
            Place(readyLabel.rectTransform, ready.transform, 0, 140, 192, 24);
            readyLabel.fontSize = 17;
            TMP_Text count = Label("ReadyCount", ready.transform, "0/0", 0, 48, 192, 48, 29, Color.white);
            count.alignment = TextAlignmentOptions.Center;
            count.fontStyle = FontStyles.Bold;
            count.transform.SetAsLastSibling();

            int layer = EnsurePreviewLayer();
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                camera.cullingMask &= ~(1 << layer);
            var preview = new GameObject("LobbyHullRenderer");
            SceneManager.MoveGameObjectToScene(preview, scene);
            preview.transform.position = new Vector3(10000, 10000, 0);
            var cameraObject = new GameObject("HullThumbnailCamera", typeof(Camera));
            cameraObject.transform.SetParent(preview.transform, false);
            Camera previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << layer;
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.GetUniversalAdditionalCameraData().renderShadows = false;
            var lightObject = new GameObject("HullThumbnailLight", typeof(Light));
            lightObject.transform.SetParent(preview.transform, false);
            lightObject.transform.localPosition = new Vector3(-2, 3, -5);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 5;
            light.range = 30;
            light.cullingMask = 1 << layer;
            var hullRenderer = preview.AddComponent<LobbyHullRenderer>();
            var rendererFields = new SerializedObject(hullRenderer);
            Set(rendererFields, "m_Camera", previewCamera);
            rendererFields.ApplyModifiedPropertiesWithoutUndo();

            var lobbyController = controller.gameObject.AddComponent<LobbyPanel>();
            var lobbyFields = new SerializedObject(lobbyController);
            Set(lobbyFields, "m_Panel", lobby.gameObject);
            SetArray(lobbyFields.FindProperty("m_Cards"), cards);
            Set(lobbyFields, "m_Hulls", hullRenderer);
            Set(lobbyFields, "m_ReadySymbol", symbol);
            Set(lobbyFields, "m_ReadyCount", count);
            Set(lobbyFields, "m_Status", lobbyStatus);
            Set(lobbyFields, "m_ReturnToLobby", returnButton);
            Set(lobbyFields, "m_ReadyButton", ready);
            lobbyFields.ApplyModifiedPropertiesWithoutUndo();
            var controllerFields = new SerializedObject(controller);
            Set(controllerFields, "m_EditingGroup", group);
            controllerFields.ApplyModifiedPropertiesWithoutUndo();
            AnchorLobbyPanels(controller);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Bottom lobby saved: 16 static player cards, play/stop ready control and isolated hull thumbnail rig.");
        }

        private static void AnchorLobbyPanels(LoadoutMenuController controller)
        {
            controller.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var top = (RectTransform)controller.transform.Find("LoadoutPanel");
            top.anchorMin = top.anchorMax = top.pivot = new Vector2(0.5f, 1);
            top.anchoredPosition = new Vector2(0, -16);
            var bottom = (RectTransform)controller.transform.Find("LobbyPanel");
            bottom.anchorMin = bottom.anchorMax = bottom.pivot = new Vector2(0.5f, 0);
            bottom.anchoredPosition = new Vector2(0, 16);
            foreach (TMP_Text text in bottom.GetComponentsInChildren<TMP_Text>(true).Where(text => text.name == "Kills" || text.name == "Deaths"))
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 9;
                text.fontSizeMax = 14;
            }
        }

        private static int EnsurePreviewLayer()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == LobbyHullRenderer.PreviewLayer) return i;
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = LobbyHullRenderer.PreviewLayer;
                tags.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("No free layer for isolated lobby hull thumbnails.");
        }

        private static void Move(Transform parent, string name, float x, float y, float width, float height) =>
            Place((RectTransform)parent.Find(name), parent, x, y, width, height);

        private static RawImage Raw(string name, Transform parent, float x, float y, float width, float height)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static LobbySymbol Symbol(string name, Transform parent, LobbySymbolKind kind, float x, float y, float width, float height, Color colour)
        {
            var symbol = Rect(name, parent, x, y, width, height).gameObject.AddComponent<LobbySymbol>();
            symbol.Kind = kind;
            symbol.color = colour;
            symbol.raycastTarget = false;
            return symbol;
        }

        [MenuItem("PewPewPew/Loadout/Build static scene UI")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ShipCatalogSync.Sync();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject menu = All(scene).Single(go => go.name == "ShipLoadoutMenu");
            if (menu.GetComponent<LoadoutMenuController>() != null)
                throw new InvalidOperationException("The scene already has a loadout UI. Edit the existing objects instead of rebuilding over custom layout changes.");

            s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (s_Font == null) throw new InvalidOperationException("Missing loadout UI font.");
            var canvas = menu.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menu.transform.localScale = Vector3.one;
            var scaler = menu.GetComponent<CanvasScaler>() ?? menu.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            if (menu.GetComponent<GraphicRaycaster>() == null) menu.AddComponent<GraphicRaycaster>();

            GameObject image = menu.GetComponentsInChildren<Image>(true).Single(item => item.name == "ShipImage").gameObject;
            TMP_Text hullName = menu.GetComponentsInChildren<TMP_Text>(true).Single(item => item.name == "HullSelectText");
            Button[] hullButtons = hullName.GetComponentsInChildren<Button>(true);
            if (hullButtons.Length != 2) throw new InvalidOperationException("Expected the two existing hull stub buttons.");
            hullButtons = hullButtons.OrderBy(button => ((RectTransform)button.transform).anchoredPosition.x).ToArray();

            RectTransform panel = Rect("LoadoutPanel", menu.transform, 24, 150, 1552, 726);
            RectTransform left = Surface("LeftPanel", panel, 0, 0, 900, 726);
            RectTransform right = Surface("RightPanel", panel, 920, 0, 632, 726);
            Label("ComponentsTitle", left, "SHIP COMPONENTS", 20, 12, 860, 35, 25, Accent);
            string[] titles = { "Shields", "Main guns", "Secondary", "Engines", "Subsystems" };
            string[] names = { "Shields", "MainGuns", "SecondaryGuns", "Thrusters", "SubSystems" };
            var categories = new LoadoutCategoryView[5];
            var catalog = ShipCatalog.Instance;
            ShipComponentDef[][] definitions = { catalog.Shields, catalog.MainGuns, catalog.SecondaryGuns, catalog.Thrusters, catalog.SubSystems };
            for (int category = 0; category < 5; category++)
            {
                Button tab = MakeButton(names[category] + "Tab", left, titles[category], 20 + category * 174, 56, 166, 42);
                RectTransform grid = Rect(names[category] + "Grid", left, 20, 112, 860, 416);
                var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
                int columns = category == 4 ? 8 : 4;
                int count = category == 4 ? 64 : 16;
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = columns;
                layout.spacing = new Vector2(8, 8);
                layout.cellSize = new Vector2((860 - (columns - 1) * 8) / columns, (416 - (columns - 1) * 8) / columns);
                var buttons = new LoadoutComponentButton[count];
                for (int index = 0; index < count; index++)
                {
                    buttons[index] = ComponentButton(grid, names[category] + "_" + index.ToString("00"), category == 4);
                    buttons[index].Bind(index < definitions[category].Length ? definitions[category][index] : null, index == 0 && category != 4,
                        index < definitions[category].Length && definitions[category][index].IsLocked ? "Locked" : null);
                }
                categories[category] = new LoadoutCategoryView { Tab = tab, Grid = grid.gameObject, Buttons = buttons };
                grid.gameObject.SetActive(category == 0);
                tab.interactable = category != 0;
            }

            Label("SlotsTitle", left, "SUBSYSTEM SLOTS - click a fitted component to clear it", 20, 538, 860, 24, 17, Accent);
            var slots = new Button[4];
            var slotLabels = new TMP_Text[4];
            for (int slot = 0; slot < slots.Length; slot++)
            {
                slots[slot] = MakeButton("Slot" + (slot + 1), left, slot < catalog.Hulls[0].SubSystemSlots ? "Empty" : "Unavailable",
                    20 + slot * 217, 568, 209, 54);
                slots[slot].interactable = false;
                slotLabels[slot] = slots[slot].GetComponentInChildren<TMP_Text>();
            }
            RectTransform hoverPanel = Surface("HoverDescriptionPanel", left, 20, 634, 860, 76);
            TMP_Text hover = Label("HoverDescription", hoverPanel, "Hover over a component to see its description and availability.",
                12, 8, 836, 60, 16, Color.white);
            AddScroll(hoverPanel, hover);

            Label("HullTitle", right, "SELECTED HULL", 20, 12, 592, 35, 25, Accent);
            Place((RectTransform)image.transform, right, 20, 56, 592, 290);
            image.GetComponent<Image>().color = new Color(0.055f, 0.11f, 0.16f, 1f);
            image.GetComponent<Image>().raycastTarget = false;
            Label("PreviewPlaceholder", image.transform, "SHIP PREVIEW\n\nImage placeholder - 3D preview to follow", 20, 100, 552, 90, 22, new Color(0.5f, 0.65f, 0.72f));
            Place((RectTransform)hullName.transform, right, 150, 356, 332, 48);
            hullName.name = "HullName";
            hullName.text = catalog.Hulls[0].DisplayName;
            hullName.fontSize = 23;
            hullName.alignment = TextAlignmentOptions.Center;
            hullName.raycastTarget = false;
            Place((RectTransform)hullButtons[0].transform, right, 20, 356, 120, 48);
            Place((RectTransform)hullButtons[1].transform, right, 492, 356, 120, 48);
            StyleExistingButton(hullButtons[0], "PreviousHull", "Previous");
            StyleExistingButton(hullButtons[1], "NextHull", "Next");
            RectTransform descriptionPanel = Surface("HullDescriptionPanel", right, 20, 416, 592, 120);
            TMP_Text description = Label("HullDescription", descriptionPanel, catalog.Hulls[0].Description, 12, 8, 568, 104, 18, Color.white);
            AddScroll(descriptionPanel, description);
            Button coloursToggle = MakeButton("ColoursToggle", right, "Ship colours", 20, 548, 190, 38);
            TMP_Text colourLabel = Label("ColourSelection", right, "Primary / Secondary", 224, 548, 388, 38, 17, Accent);
            TMP_Text status = Label("DeployStatus", right, "Loadout ready.", 20, 596, 592, 46, 17, Color.white);
            Button deploy = MakeButton("DeployButton", right, "Ready up", 20, 656, 592, 54);

            RectTransform colours = Surface("ColoursPanel", right, 20, 56, 592, 480);
            Label("ColourTitle", colours, "SHIP COLOURS", 16, 10, 560, 32, 23, Accent);
            Label("PrimaryTitle", colours, "Primary", 20, 54, 260, 30, 20, Color.white);
            Label("SecondaryTitle", colours, "Secondary", 312, 54, 260, 30, 20, Color.white);
            Button[] primary = Swatches(colours, "Primary", 20);
            Button[] secondary = Swatches(colours, "Secondary", 312);
            Label("ColourHelp", colours, "Choose two colours. Click Ship colours again to return to the hull.", 20, 390, 552, 70, 18, Color.white);
            colours.gameObject.SetActive(false);

            var controller = menu.AddComponent<LoadoutMenuController>();
            var serialized = new SerializedObject(controller);
            Set(serialized, "m_Panel", panel.gameObject);
            SerializedProperty views = serialized.FindProperty("m_Categories");
            views.arraySize = categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                SerializedProperty view = views.GetArrayElementAtIndex(i);
                view.FindPropertyRelative("Tab").objectReferenceValue = categories[i].Tab;
                view.FindPropertyRelative("Grid").objectReferenceValue = categories[i].Grid;
                SetArray(view.FindPropertyRelative("Buttons"), categories[i].Buttons);
            }
            Set(serialized, "m_PreviousHull", hullButtons[0]);
            Set(serialized, "m_NextHull", hullButtons[1]);
            Set(serialized, "m_HullName", hullName);
            Set(serialized, "m_HullDescription", description);
            Set(serialized, "m_HoverDescription", hover);
            SetArray(serialized.FindProperty("m_Slots"), slots);
            SetArray(serialized.FindProperty("m_SlotLabels"), slotLabels);
            Set(serialized, "m_Deploy", deploy);
            Set(serialized, "m_DeployLabel", deploy.GetComponentInChildren<TMP_Text>());
            Set(serialized, "m_Status", status);
            Set(serialized, "m_ColoursToggle", coloursToggle);
            Set(serialized, "m_ColoursPanel", colours.gameObject);
            Set(serialized, "m_ColourLabel", colourLabel);
            SetArray(serialized.FindProperty("m_PrimaryColours"), primary);
            SetArray(serialized.FindProperty("m_SecondaryColours"), secondary);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save loadout scene.");
            Debug.Log("Static loadout UI saved: 64 major-component buttons, 64 subsystem buttons, hull previous/next, descriptions and colour controls.");
            UpgradeLobby();
            UpgradeShipPane();
        }

        private static LoadoutComponentButton ComponentButton(Transform parent, string name, bool compact)
        {
            Button button = MakeButton(name, parent, "", 0, 0, 100, 100);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.name = "ComponentName";
            label.fontSize = compact ? 11 : 18;
            label.enableAutoSizing = true;
            label.fontSizeMin = compact ? 9 : 13;
            label.fontSizeMax = compact ? 11 : 18;
            label.rectTransform.anchorMin = new Vector2(0, 0.20f);
            label.rectTransform.anchorMax = new Vector2(1, 0.70f);
            label.rectTransform.offsetMin = new Vector2(4, 0);
            label.rectTransform.offsetMax = new Vector2(-4, 0);
            var artwork = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            artwork.transform.SetParent(button.transform, false);
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;
            Stretch(artwork.rectTransform, new Vector2(0.38f, 0.72f), new Vector2(0.62f, 0.96f));
            TMP_Text price = Label("Price", button.transform, "", 0, 0, 100, 20, compact ? 9 : 13, new Color(1f, 0.75f, 0.35f));
            Stretch(price.rectTransform, new Vector2(0, 0), new Vector2(1, 0.22f));
            price.alignment = TextAlignmentOptions.Center;
            RectTransform selected = Rect("Selected", button.transform, 0, 0, 100, 4);
            Stretch(selected, new Vector2(0, 0.96f), Vector2.one);
            var highlight = selected.gameObject.AddComponent<Image>();
            highlight.color = Accent;
            highlight.raycastTarget = false;
            var view = button.gameObject.AddComponent<LoadoutComponentButton>();
            var serialized = new SerializedObject(view);
            Set(serialized, "m_Button", button);
            Set(serialized, "m_Artwork", artwork);
            Set(serialized, "m_NameLabel", label);
            Set(serialized, "m_Price", price);
            Set(serialized, "m_Selected", selected.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button[] Swatches(Transform parent, string name, float x)
        {
            RectTransform grid = Rect(name + "Palette", parent, x, 96, 260, 260);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 8;
            layout.cellSize = new Vector2(29, 29);
            layout.spacing = new Vector2(4, 4);
            var buttons = new Button[PlayerPalette.Count];
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = Surface(name + i.ToString("00"), grid, 0, 0, 29, 29);
                Image image = rect.GetComponent<Image>();
                image.color = PlayerPalette.Colour(i);
                buttons[i] = rect.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = image;
            }
            return buttons;
        }

        private static void StyleExistingButton(Button button, string name, string text)
        {
            button.name = name;
            button.GetComponent<Image>().color = ButtonColour;
            button.onClick = new Button.ButtonClickedEvent();
            Label("Label", button.transform, text, 0, 0, 120, 48, 19, Color.white).alignment = TextAlignmentOptions.Center;
        }

        private static Button MakeButton(string name, Transform parent, string text, float x, float y, float width, float height)
        {
            RectTransform rect = Surface(name, parent, x, y, width, height);
            Image image = rect.GetComponent<Image>();
            image.color = ButtonColour;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.4f, 1.4f, 1.4f);
            colours.disabledColor = new Color(0.55f, 0.55f, 0.55f);
            button.colors = colours;
            TMP_Text label = Label("Label", rect, text, 4, 2, width - 8, height - 4, 19, Color.white);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(4, 2);
            label.rectTransform.offsetMax = new Vector2(-4, -2);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void AddScroll(RectTransform panel, TMP_Text text)
        {
            // Descriptions remain readable even when an author supplies more than a few lines.
            var viewport = Rect("Viewport", panel, 8, 6, panel.sizeDelta.x - 16, panel.sizeDelta.y - 12);
            viewport.gameObject.AddComponent<RectMask2D>();
            text.rectTransform.SetParent(viewport, false);
            Stretch(text.rectTransform, new Vector2(0, 1), Vector2.one);
            text.rectTransform.pivot = new Vector2(0.5f, 1);
            text.rectTransform.sizeDelta = new Vector2(0, viewport.sizeDelta.y);
            text.overflowMode = TextOverflowModes.Overflow;
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = text.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            text.raycastTarget = true;
        }

        private static RectTransform Surface(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            rect.gameObject.AddComponent<Image>().color = PanelColour;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            Place(rect, parent, x, y, width, height);
            return rect;
        }

        private static void Place(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static TMP_Text Label(string name, Transform parent, string text, float x, float y, float width, float height, float size, Color colour)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = s_Font;
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject serialized, string property, Object value) =>
            serialized.FindProperty(property).objectReferenceValue = value;

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static GameObject[] All(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
    }
}
