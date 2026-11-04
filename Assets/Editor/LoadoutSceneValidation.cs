using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Ships;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PewPewPew.Editor
{
    public static class LoadoutSceneValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int s_Checks;

        [MenuItem("PewPewPew/Loadout/Validate static scene UI")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            s_Checks = 0;
            ShipCatalog source = ShipCatalog.Instance;
            ShipComponentDef[][] before =
            {
                source.Hulls.ToArray(), source.Shields.ToArray(), source.MainGuns.ToArray(),
                source.SecondaryGuns.ToArray(), source.Thrusters.ToArray(), source.SubSystems.ToArray()
            };
            ShipCatalogSync.Sync();
            ShipComponentDef[][] after =
            {
                source.Hulls, source.Shields, source.MainGuns, source.SecondaryGuns, source.Thrusters, source.SubSystems
            };
            for (int category = 0; category < before.Length; category++)
                Check(after[category].Take(before[category].Length).SequenceEqual(before[category]), "Catalog sync preserves existing network indices " + category);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LoadoutMenuController>(true)).Single();
            var serialized = new SerializedObject(controller);
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    Check(iterator.objectReferenceValue != null, "Missing controller reference: " + iterator.propertyPath);

            var categories = Get<LoadoutCategoryView[]>(controller, "m_Categories");
            Check(categories.Length == 5, "Five categories");
            Check(controller.GetComponentsInChildren<LoadoutComponentButton>(true).Length == 128, "Exactly 128 statically saved component buttons");
            Check(controller.GetComponent<GraphicRaycaster>() != null, "Canvas accepts UI input");
            Check(controller.GetComponent<CanvasScaler>().referenceResolution == new Vector2(1600, 900), "Scalable reference layout");
            Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count() == 1, "Exactly one EventSystem");
            for (int category = 0; category < 5; category++)
            {
                Check(categories[category].Buttons.Length == (category == 4 ? 64 : 16), "Static category capacity " + category);
                Check(categories[category].Grid.GetComponent<GridLayoutGroup>().constraintCount == (category == 4 ? 8 : 4), "Grid columns " + category);
                foreach (LoadoutComponentButton button in categories[category].Buttons)
                {
                    var fields = new SerializedObject(button);
                    foreach (string field in new[] { "m_Button", "m_Artwork", "m_NameLabel", "m_Price", "m_Selected" })
                        Check(fields.FindProperty(field).objectReferenceValue != null, "Complete component button " + button.name);
                }
            }

            ShipCatalog catalog = Object.Instantiate(ShipCatalog.Instance);
            var definition = Object.Instantiate(catalog.Shields[0]);
            GameObject buttonObject = Object.Instantiate(categories[0].Buttons[0].gameObject);
            var buttonView = buttonObject.GetComponent<LoadoutComponentButton>();
            try
            {
                var def = new SerializedObject(definition);
                def.FindProperty("m_DisplayName").stringValue = "Test shield";
                def.FindProperty("m_Description").stringValue = "Hover description";
                def.FindProperty("m_Price").intValue = 500;
                def.ApplyModifiedPropertiesWithoutUndo();
                Check(definition.IsLocked && definition.Price == 500, "Price establishes initial lock");
                buttonView.Bind(definition, true, "Locked - price: 500");
                Check(!buttonObject.GetComponent<Button>().interactable, "Locked button cannot fit");
                Check(Get<TMP_Text>(buttonView, "m_Price").text.Contains("500"), "Locked price displayed");
                Check(Get<GameObject>(buttonView, "m_Selected").activeSelf, "Selected state visible");
                Check(!Get<Image>(buttonView, "m_Artwork").gameObject.activeSelf, "Missing artwork leaves name-only button");
                string hover = null;
                bool clicked = false;
                buttonView.Initialize(() => clicked = true, text => hover = text);
                buttonView.OnPointerEnter(null);
                Check(hover.Contains("Hover description") && hover.Contains("500"), "Locked button still explains itself on hover");
                buttonView.Bind(definition, true, "Availability changed");
                Check(hover.Contains("Availability changed"), "Hovered description refreshes after fitting changes");
                buttonView.OnPointerExit(null);
                Check(hover == null, "Hover exit clears description");
                buttonView.Bind(null, false, null);
                Check(!buttonObject.activeSelf, "Unused scene button disabled");
                def.FindProperty("m_Price").intValue = 0;
                def.ApplyModifiedPropertiesWithoutUndo();
                buttonView.Bind(definition, false, null);
                Check(buttonObject.activeSelf && buttonObject.GetComponent<Button>().interactable, "Free component can fit");
                buttonObject.GetComponent<Button>().onClick.Invoke();
                Check(clicked, "Selection callback wired");
                Check(Get<TMP_Text>(buttonView, "m_Price").text == "", "Available component has no price label");
                Check(!Get<GameObject>(buttonView, "m_Selected").activeSelf, "Selection clears when rebound");
                buttonView.Bind(definition, false, "Incompatible");
                Check(!buttonObject.GetComponent<Button>().interactable, "Incompatible component disabled");

                Set(controller, "m_Catalog", catalog);
                Set(controller, "m_Pending", ShipLoadout.Empty);
                Call(controller, "Normalize");
                Check(catalog.IsValid(controller.Pending), "Default loadout normalized");
                Check(catalog.LaunchProblem(controller.Pending) == null, "Default loadout can launch");

                // Invoke normal UI initialization in edit mode; no network commands are sent by these checks.
                Call(controller, "Awake");
                for (int category = 0; category < categories.Length; category++)
                {
                    categories[category].Tab.onClick.Invoke();
                    Check(categories.Count(view => view.Grid.activeSelf) == 1 && categories[category].Grid.activeSelf, "Tab switches grid " + category);
                    Check(!categories[category].Tab.interactable, "Selected tab highlighted " + category);
                }
                Check(Get<TMP_Text>(controller, "m_HullName").text == catalog.Hulls[0].DisplayName, "Hull name populated");
                Check(Get<TMP_Text>(controller, "m_HullDescription").text == catalog.Hulls[0].Description, "Hull description populated");
                var pending = controller.Pending;
                for (int slot = 0; slot < ShipLoadout.SubSystemSlots; slot++) pending.SetSubSystem(slot, 0);
                Set(controller, "m_Pending", pending);
                Call(controller, "Normalize");
                int slots = catalog.Hulls[pending.Hull].SubSystemSlots;
                for (int slot = 0; slot < ShipLoadout.SubSystemSlots; slot++)
                    Check(controller.Pending.GetSubSystem(slot) == (slot < slots ? 0 : -1), "Hull trims unavailable subsystem slot " + slot);
                Check((int)Call(controller, "FirstEmptySlot") == -1, "Full hull has no empty slot");
                Check((string)Call(controller, "Unavailable", 4, 0) != null, "Full slots prevent another subsystem fit");
                pending = controller.Pending;
                if (slots > 0)
                {
                    pending.SetSubSystem(0, -1);
                    Set(controller, "m_Pending", pending);
                    Check((int)Call(controller, "FirstEmptySlot") == 0, "Subsystem assignment uses first empty slot");
                }
                pending.SubSystem0 = -1;
                pending.SubSystem1 = -1;
                pending.SubSystem2 = -1;
                pending.SubSystem3 = -1;

                var catalogFields = new SerializedObject(catalog);
                catalogFields.FindProperty("m_Shields").GetArrayElementAtIndex(0).objectReferenceValue = definition;
                catalogFields.ApplyModifiedPropertiesWithoutUndo();
                def.FindProperty("m_Price").intValue = 500;
                def.ApplyModifiedPropertiesWithoutUndo();
                pending.Shield = 0;
                Check(catalog.LockedProblem(pending).Contains("500"), "Server loadout lock validation");
                Check(catalog.LaunchProblem(pending).Contains("500"), "Deploy rejects locked components");
                Check((string)Call(controller, "Unavailable", 0, 0) != null, "UI uses the same lock rule");
                def.FindProperty("m_Price").intValue = 0;
                def.ApplyModifiedPropertiesWithoutUndo();

                var normalized = ShipLoadout.Empty;
                normalized.Hull = catalog.Hulls.Length - 1;
                normalized.MainGun = catalog.MainGuns.Length - 1;
                normalized.Thruster = catalog.Thrusters.Length - 1;
                Set(controller, "m_Pending", normalized);
                Call(controller, "Normalize");
                Check(catalog.LaunchProblem(controller.Pending) == null, "Hull change repairs incompatible selections");
                CheckClickFlow(controller, catalog, categories);
                Set(controller, "m_Pending", ShipLoadout.Empty);
                Call(controller, "Normalize");
                Call(controller, "ShowCategory", 3);
                Get<GameObject>(controller, "m_Panel").SetActive(true);
                ValidateShipPreview(controller);
                ValidateLobby(controller);
                RenderPreview(controller);
            }
            finally
            {
                Object.DestroyImmediate(buttonObject);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                // Discard validation-only bindings and states; never save these into the scene.
                EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            }
            Debug.Log($"LOADOUT VALIDATION PASSED: {s_Checks} checks.");
        }

        private static void ValidateShipPreview(LoadoutMenuController controller)
        {
            var preview = Get<LoadoutShipPreview>(controller, "m_Preview");
            var fields = new SerializedObject(preview);
            SerializedProperty iterator = fields.GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    Check(iterator.objectReferenceValue != null, "Ship preview reference: " + iterator.propertyPath);
            var image = Get<RawImage>(preview, "m_Image");
            var texture = (RenderTexture)image.texture;
            Check(texture != null && texture.width == 1024 && texture.height == 744, "High-resolution non-square ship pane texture");
            ValidateAlpha(texture);
            preview.Show(controller.Pending);
            Check(image.texture == texture, "Unchanged loadout reuses rendered hull");
            var description = Get<CanvasGroup>(preview, "m_Description");
            var hover = preview.GetComponentInChildren<LoadoutPreviewHoverArea>(true);
            Check(hover != null && Get<LoadoutShipPreview>(hover, "m_Preview") == preview, "Separate hover rectangle is wired");
            Check(hover.GetComponent<Image>().raycastTarget && hover.GetComponent<Image>().color.a == 0, "Hover rectangle is transparent and raycastable");
            var hoverRect = (RectTransform)hover.transform;
            Check(hoverRect.anchorMin.x > 0 && hoverRect.anchorMax.x < 1 &&
                hoverRect.anchorMin.y > 0 && hoverRect.anchorMax.y < 1, "Hover rectangle is smaller than the image");
            hover.OnPointerExit(null);
            Check(description.alpha == 0 && !description.blocksRaycasts, "Hull description hidden when not hovered");
            hover.OnPointerEnter(null);
            Check(description.alpha == 1 && !description.blocksRaycasts, "Hull description visible without intercepting hover raycasts");
            hover.OnPointerExit(null);
            Check(Get<TMP_Text>(controller, "m_HullName").transform.IsChildOf(preview.transform), "Hull name overlays image");
            Check(Get<TMP_Text>(controller, "m_HullDescription").transform.IsChildOf(preview.transform), "Hull description overlays image");
            foreach (Button button in new[] { Get<Button>(controller, "m_PreviousHull"), Get<Button>(controller, "m_NextHull") })
            {
                var rect = (RectTransform)button.transform;
                Check(rect.sizeDelta == new Vector2(36, 36) && rect.IsChildOf(preview.transform), "Small square hull navigation on image");
                Check(button.GetComponentsInChildren<TMP_Text>(true).Length == 0, "Hull navigation has no words");
                Check(button.GetComponentInChildren<LobbySymbol>().Kind == LobbySymbolKind.Play, "Hull navigation uses a triangle");
            }
            Check(Get<Button>(controller, "m_PreviousHull").GetComponentInChildren<LobbySymbol>().transform.localEulerAngles.z == 180, "Previous triangle points backwards");
            Check(((RectTransform)preview.transform).sizeDelta == new Vector2(592, 430), "Hull image fills pane above unchanged colour controls");
            Check(Get<Button>(controller, "m_ColoursToggle").GetComponent<RectTransform>().anchoredPosition == new Vector2(20, -498), "Ship colours position preserved");
            var markers = Get<LoadoutPreviewMarker[]>(preview, "m_Markers");
            Check(markers.Length == 17, "Static capacity for all major mounts, four subsystems and shield");
            ShipCatalog catalog = ShipCatalog.Instance;
            var loadout = controller.Pending;
            HullDef hull = catalog.Hulls[loadout.Hull];
            int count = hull.SelectPoints(hull.MainWeaponPoints, catalog.MainGuns[loadout.MainGun].MaxEmissionPointsConsidered).Length +
                hull.SelectPoints(hull.SecondaryWeaponPoints, catalog.SecondaryGuns[loadout.SecondaryGun].MaxEmissionPointsConsidered).Length +
                hull.SelectPoints(hull.ThrusterPoints, catalog.Thrusters[loadout.Thruster].EmissionPointsUsed).Length + 1;
            Check(markers.Count(marker => marker.gameObject.activeSelf) == count, "Only fitted mount overlays shown");
            Rect frame = Get<Rect>(preview, "m_Frame");
            Transform first = hull.SelectPoints(hull.MainWeaponPoints, catalog.MainGuns[loadout.MainGun].MaxEmissionPointsConsidered)[0];
            Vector3 local = hull.HullPoints.transform.InverseTransformPoint(first.position);
            Check(Vector2.Distance(((RectTransform)markers[0].transform).anchorMin, Rect.PointToNormalized(frame, local)) < 0.0001f, "Mount overlay projects hull-local position into render frame");
            if (hull.SubSystemSlots > 0)
            {
                loadout.SetSubSystem(0, 0);
                preview.Show(loadout);
                Check(image.texture == texture, "Component change does not rerender unchanged hull");
                Check(markers.Count(marker => marker.gameObject.activeSelf) == count + 1, "Selecting subsystem adds its overlay");
                Transform mount = hull.HullPoints.SubSystems[0];
                Check(Vector2.Distance(((RectTransform)markers[count - 1].transform).anchorMin,
                    Rect.PointToNormalized(frame, hull.HullPoints.transform.InverseTransformPoint(mount.position))) < 0.0001f,
                    "Subsystem overlay uses the selected hull slot");
                loadout.SetSubSystem(0, -1);
                preview.Show(loadout);
                Check(markers.Count(marker => marker.gameObject.activeSelf) == count, "Clearing subsystem hides its overlay");
            }
            ShipComponentDef definition = Object.Instantiate(catalog.Shields[0]);
            Texture2D artwork = new Texture2D(8, 8);
            Sprite sprite = Sprite.Create(artwork, new Rect(0, 0, 8, 8), Vector2.one / 2);
            try
            {
                var def = new SerializedObject(definition);
                def.FindProperty("m_ButtonArtwork").objectReferenceValue = sprite;
                def.ApplyModifiedPropertiesWithoutUndo();
                markers[0].Bind(definition, Vector2.one / 2);
                Check(Get<Image>(markers[0], "m_Artwork").enabled && Get<Image>(markers[0], "m_Artwork").sprite == sprite, "Assigned component artwork is displayed");
                Check(!Get<TMP_Text>(markers[0], "m_Label").gameObject.activeSelf, "Artwork replaces name fallback");
                def.FindProperty("m_ButtonArtwork").objectReferenceValue = null;
                def.ApplyModifiedPropertiesWithoutUndo();
                markers[0].Bind(definition, Vector2.one / 2);
                Check(!Get<Image>(markers[0], "m_Artwork").enabled && Get<TMP_Text>(markers[0], "m_Label").gameObject.activeSelf, "Missing artwork uses named marker");
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(artwork);
            }
            var changed = loadout;
            changed.Hull = (changed.Hull + 1) % catalog.Hulls.Length;
            // Choose compatible components through the existing normalization path.
            Set(controller, "m_Pending", changed);
            Call(controller, "Normalize");
            Call(controller, "Refresh");
            Check(image.texture != texture, "Hull selection refreshes rendered image");
            Set(controller, "m_Pending", loadout);
            Call(controller, "Refresh");
            var colours = loadout;
            colours.Colour0 = PlayerPalette.Colour(10);
            texture = (RenderTexture)image.texture;
            preview.Show(colours);
            Check(image.texture != texture, "Existing colour choices update hull rendering");
            preview.Show(loadout);
        }

        private static void ValidateLobby(LoadoutMenuController controller)
        {
            LobbyPanel lobby = controller.GetComponent<LobbyPanel>();
            Check(lobby != null, "Bottom lobby controller exists");
            var fields = new SerializedObject(lobby);
            SerializedProperty iterator = fields.GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    Check(iterator.objectReferenceValue != null, "Lobby reference: " + iterator.propertyPath);
            LobbyPlayerCard[] cards = Get<LobbyPlayerCard[]>(lobby, "m_Cards");
            Check(cards.Length == 16, "Exactly 16 statically saved player cards");
            foreach (LobbySymbol graphic in lobby.GetComponentsInChildren<LobbySymbol>(true))
                Check(graphic.GetComponent<CanvasRenderer>() != null, "Symbol has a canvas renderer: " + graphic.name);
            var panel = (RectTransform)Get<GameObject>(controller, "m_Panel").transform;
            var bottom = (RectTransform)Get<GameObject>(lobby, "m_Panel").transform;
            Check(panel.anchorMin == new Vector2(0.5f, 1) && bottom.anchorMin == new Vector2(0.5f, 0), "Loadout panes anchor to top and lobby to bottom");
            Check(controller.GetComponent<CanvasScaler>().screenMatchMode == CanvasScaler.ScreenMatchMode.Expand, "Panels fit narrower and wider aspect ratios");
            Check(Get<Button>(controller, "m_Deploy").transform.IsChildOf(bottom), "Ready button is in the bottom lobby");
            Check(!Get<Button>(controller, "m_Deploy").transform.IsChildOf(panel), "Ready button is outside the editing lock");
            Check(Get<CanvasGroup>(controller, "m_EditingGroup").gameObject == panel.gameObject, "Loadout editing lock is wired");
            var hulls = Get<LobbyHullRenderer>(lobby, "m_Hulls");
            Camera camera = Get<Camera>(hulls, "m_Camera");
            Check(!camera.enabled, "Hull camera renders only on demand");
            Check(camera.cullingMask == 1 << LayerMask.NameToLayer(LobbyHullRenderer.PreviewLayer), "Hull rendering is isolated");
            Get<GameObject>(lobby, "m_Panel").SetActive(true);
            Get<TMP_Text>(controller, "m_DeployLabel").text = "READY";
            var players = new PlayerState[16];
            try
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    var player = new GameObject("Validation player " + i, typeof(NetworkIdentity), typeof(PlayerState)).GetComponent<PlayerState>();
                    players[i] = player;
                    typeof(NetworkBehaviour).GetProperty("netIdentity").SetValue(player, player.GetComponent<NetworkIdentity>());
                    typeof(NetworkIdentity).GetProperty("netId").SetValue(player.GetComponent<NetworkIdentity>(), (uint)(i + 1));
                    Set(player, "m_Name", "Player " + (i + 1));
                    Set(player, "m_Ready", i < 6);
                    Set(player, "m_Kills", i);
                    Set(player, "m_Deaths", i / 2);
                    var loadout = ShipLoadout.Empty;
                    loadout.Hull = i % ShipCatalog.Instance.Hulls.Length;
                    loadout.Colour0 = PlayerPalette.Colour(i);
                    Set(player, "m_Loadout", loadout);
                    cards[i].Bind(player, hulls);
                    Check(cards[i].gameObject.activeSelf, "Player card enabled " + i);
                    Check(Get<TMP_Text>(cards[i], "m_Kills").text == i.ToString(), "Kill counter " + i);
                    Check(Get<TMP_Text>(cards[i], "m_Deaths").text == (i / 2).ToString(), "Death counter " + i);
                    Check(Get<Image>(cards[i], "m_ReadyOutline").color.g > 0.9f == (i < 6), "Ready outline " + i);
                    var texture = (RenderTexture)Get<RawImage>(cards[i], "m_Hull").texture;
                    Check(texture != null && texture.IsCreated(), "Rendered hull texture exists " + i);
                    ValidateAlpha(texture);
                    int symbolCount = cards[i].GetComponentsInChildren<LobbySymbol>().Length;
                    Check(symbolCount == 2, "Skull and crosshair indicators " + i);
                }
                Set(controller, "m_Player", players[0]);
                var previous = controller.Pending;
                controller.CycleHull(1);
                controller.ClearSlot(0);
                controller.SelectComponent(3, 0);
                Check(controller.Pending.Hull == previous.Hull && controller.Pending.Thruster == previous.Thruster, "Ready player cannot edit loadout");
                Check(!controller.CanEdit, "Readiness locks editing");
                Set(players[0], "m_Ready", false);
                Check(controller.CanEdit, "Stop/unready re-enables editing");
                Set(controller, "m_Player", null);
                var firstTexture = Get<RawImage>(cards[0], "m_Hull").texture;
                ShipLoadout changed = players[0].Loadout;
                changed.Hull = (changed.Hull + 1) % ShipCatalog.Instance.Hulls.Length;
                Set(players[0], "m_Loadout", changed);
                cards[0].Bind(players[0], hulls);
                Check(Get<RawImage>(cards[0], "m_Hull").texture != firstTexture, "Changing hull refreshes the thumbnail");
                var symbol = Get<LobbySymbol>(lobby, "m_ReadySymbol");
                symbol.Kind = LobbySymbolKind.Stop;
                Check(symbol.Kind == LobbySymbolKind.Stop, "Ready control can show stop square");
                symbol.Kind = LobbySymbolKind.Play;
                Get<TMP_Text>(lobby, "m_ReadyCount").text = "6/16";
                cards[15].Bind(null, hulls);
                Check(!cards[15].gameObject.activeSelf && Get<RawImage>(cards[15], "m_Hull").texture == null, "Departed player clears card and texture");
                cards[15].Bind(players[15], hulls);
            }
            finally
            {
                Set(controller, "m_Player", null);
                foreach (PlayerState player in players)
                    if (player != null) Object.DestroyImmediate(player.gameObject);
            }
        }

        private static void ValidateAlpha(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                pixels.Apply();
                Color32[] data = pixels.GetPixels32();
                int visible = data.Count(pixel => pixel.a > 128);
                Check(visible > 200 && visible < data.Length * 0.9f, "Hull image has visible geometry and transparent avatar background");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
            }
        }

        private static void CheckClickFlow(LoadoutMenuController controller, ShipCatalog catalog, LoadoutCategoryView[] categories)
        {
            HullDef original = catalog.Hulls[0];
            HullDef lockedHull = Object.Instantiate(original);
            var hull = new SerializedObject(lockedHull);
            hull.FindProperty("m_Price").intValue = 750;
            hull.ApplyModifiedPropertiesWithoutUndo();
            var fields = new SerializedObject(catalog);
            fields.FindProperty("m_Hulls").GetArrayElementAtIndex(0).objectReferenceValue = lockedHull;
            fields.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                var pending = ShipLoadout.Empty;
                pending.Hull = catalog.Hulls.Length - 1;
                Set(controller, "m_Pending", pending);
                Call(controller, "Normalize");
                Call(controller, "Refresh");
                Check(Get<Button>(controller, "m_NextHull").GetComponentsInChildren<TMP_Text>().Length == 0, "Hull navigation remains wordless for locked targets");
                controller.CycleHull(1);
                Check(controller.Pending.Hull == 0, "Previous/next can browse locked hulls");
                Check(Get<TMP_Text>(controller, "m_HullName").text.Contains("750"), "Browsed hull shows price");
                Check(catalog.LaunchProblem(controller.Pending).Contains("750"), "Browsed locked hull cannot deploy");

                // Browsing a locked hull exercises actual selection callbacks without sending a network command.
                for (int category = 0; category < 4; category++)
                {
                    var buttons = categories[category].Buttons;
                    int available = Array.FindLastIndex(buttons, view => view.gameObject.activeSelf && view.GetComponent<Button>().interactable);
                    Check(available >= 0, "A compatible free component is available in category " + category);
                    buttons[available].GetComponent<Button>().onClick.Invoke();
                    Check((int)Call(controller, "Selected", category) == available, "Direct component click selects category " + category);
                }
                for (int slot = 0; slot < lockedHull.SubSystemSlots; slot++)
                {
                    categories[4].Buttons[0].GetComponent<Button>().onClick.Invoke();
                    Check(controller.Pending.GetSubSystem(slot) == 0, "Click fills the first empty slot " + slot);
                }
                categories[4].Buttons[1].GetComponent<Button>().onClick.Invoke();
                Check(controller.Pending.GetSubSystem(0) == 0, "Full slots do not overwrite existing subsystems");
                Get<Button[]>(controller, "m_Slots")[0].onClick.Invoke();
                Check(controller.Pending.GetSubSystem(0) == -1, "Slot click clears fitted subsystem");
                categories[4].Buttons[1].GetComponent<Button>().onClick.Invoke();
                Check(controller.Pending.GetSubSystem(0) == 1, "Next subsystem click refills first cleared slot");
                categories[3].Tab.onClick.Invoke();
                var hoveredButton = categories[3].Buttons[controller.Pending.Thruster];
                hoveredButton.OnPointerEnter(null);
                hoveredButton.GetComponent<Button>().onClick.Invoke();
                Check(Get<TMP_Text>(controller, "m_HoverDescription").text.Contains(catalog.Thrusters[controller.Pending.Thruster].DisplayName),
                    "Clicking a hovered component preserves its description");
            }
            finally
            {
                fields.FindProperty("m_Hulls").GetArrayElementAtIndex(0).objectReferenceValue = original;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(lockedHull);
            }
        }

        private static void RenderPreview(LoadoutMenuController controller)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(arguments, "-loadoutPreview");
            if (argument < 0 || argument + 1 >= arguments.Length) return;
            string path = arguments[argument + 1];
            var texture = new RenderTexture(1600, 900, 24);
            var cameraObject = new GameObject("Loadout preview camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.045f);
            camera.orthographic = true;
            camera.cullingMask = ~0;
            camera.targetTexture = texture;
            Canvas canvas = controller.GetComponent<Canvas>();
            Canvas[] otherCanvases = Object.FindObjectsByType<Canvas>()
                .Where(other => other != canvas && other.enabled).ToArray();
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                foreach (Canvas other in otherCanvases) other.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                ValidateShipHoverRaycasts(controller, camera);
                foreach (TMP_Text text in controller.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                ValidatePanelBounds(controller, camera);
                foreach (LobbySymbol graphic in controller.GetComponentsInChildren<LobbySymbol>())
                {
                    Mesh mesh = graphic.canvasRenderer.GetMesh();
                    Check(mesh != null && mesh.vertexCount > 0, "Visible vector symbol has rendered geometry: " + graphic.name);
                }
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 1024), new Vector2Int(2560, 1080) })
                {
                    camera.targetTexture = null;
                    texture.Release();
                    texture.width = size.x;
                    texture.height = size.y;
                    texture.Create();
                    camera.targetTexture = texture;
                    typeof(CanvasScaler).GetMethod("Handle", PrivateInstance).Invoke(controller.GetComponent<CanvasScaler>(), null);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    ValidatePanelBounds(controller, camera);
                }
            }
            finally
            {
                foreach (Canvas other in otherCanvases) other.enabled = true;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(image);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateShipHoverRaycasts(LoadoutMenuController controller, Camera camera)
        {
            LoadoutShipPreview preview = Get<LoadoutShipPreview>(controller, "m_Preview");
            LoadoutPreviewHoverArea hover = preview.GetComponentInChildren<LoadoutPreviewHoverArea>();
            var rect = (RectTransform)hover.transform;
            var imageRect = (RectTransform)preview.transform;
            var events = Object.FindAnyObjectByType<EventSystem>();
            var results = new System.Collections.Generic.List<RaycastResult>();
            var pointer = new PointerEventData(events);
            Vector3 centre = rect.TransformPoint(rect.rect.center);
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera, centre);
            controller.GetComponent<GraphicRaycaster>().Raycast(pointer, results);
            Check(results.Count > 0 && results[0].gameObject == hover.gameObject, "Inside independent rectangle raycasts to hover target");
            ExecuteEvents.Execute(hover.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Check(Get<CanvasGroup>(preview, "m_Description").alpha == 1, "Independent rectangle reveals description");
            results.Clear();
            controller.GetComponent<GraphicRaycaster>().Raycast(pointer, results);
            Check(results.Count > 0 && results[0].gameObject == hover.gameObject, "Visible tooltip does not steal hover target");
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera,
                imageRect.TransformPoint(new Vector3(imageRect.rect.xMin + 8, imageRect.rect.center.y)));
            results.Clear();
            controller.GetComponent<GraphicRaycaster>().Raycast(pointer, results);
            Check(results.Count > 0 && results[0].gameObject != hover.gameObject &&
                results[0].gameObject.transform.IsChildOf(preview.transform), "Outside hover rectangle but inside image does not trigger description");
            Check(!(preview is IPointerEnterHandler), "Entire ship image is no longer a hover handler");
            ExecuteEvents.Execute(hover.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Check(Get<CanvasGroup>(preview, "m_Description").alpha == 0, "Leaving rectangle hides description");
            hover.OnPointerEnter(pointer);
            hover.gameObject.SetActive(false);
            Call(hover, "OnDisable");
            Check(Get<CanvasGroup>(preview, "m_Description").alpha == 0, "Disabling hover rectangle hides description");
            hover.gameObject.SetActive(true);
        }

        private static void ValidatePanelBounds(LoadoutMenuController controller, Camera camera)
        {
            var corners = new Vector3[4];
            var top = (RectTransform)Get<GameObject>(controller, "m_Panel").transform;
            var bottom = (RectTransform)Get<GameObject>(controller.GetComponent<LobbyPanel>(), "m_Panel").transform;
            float loadoutBottom = 0;
            foreach (RectTransform rect in new[] { top, bottom })
            {
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    Check(viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1,
                        "Panel fits rendered viewport " + camera.pixelWidth + "x" + camera.pixelHeight);
                }
                if (rect == top) loadoutBottom = camera.WorldToViewportPoint(corners[0]).y;
                else Check(loadoutBottom > camera.WorldToViewportPoint(corners[1]).y, "Top loadout and bottom lobby do not overlap");
            }
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException("Loadout validation failed: " + message);
            s_Checks++;
        }

        private static T Get<T>(object instance, string field) =>
            (T)instance.GetType().GetField(field, PrivateInstance).GetValue(instance);
        private static void Set(object instance, string field, object value) =>
            instance.GetType().GetField(field, PrivateInstance).SetValue(instance, value);
        private static object Call(object instance, string method, params object[] args) =>
            instance.GetType().GetMethod(method, PrivateInstance).Invoke(instance, args);
    }
}
