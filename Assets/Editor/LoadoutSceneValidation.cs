using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Networking;
using PewPewPew.Ships;
using PewPewPew.Weapons;
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
        private static int s_Checks;

        [MenuItem("PewPewPew/Loadout/Validate static scene UI")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            s_Checks = 0;
            ValidateHullPointCompatibility();
            ValidateAutomaticHullPoints();
            ValidateThrusterDirections();
            ShipCatalog catalog = ShipCatalog.Instance;
            Check(catalog != null && catalog.Hulls.Length > 0, "Catalog and hulls exist");
            ValidateThrusterEffects(catalog);
            ValidateDetails(catalog);
            ValidateArtwork(catalog);
            HullDef[] existingHulls = catalog.Hulls.ToArray();
            ShipComponentDef[] existingComponents = catalog.Components.ToArray();
            ShipCatalogSync.Sync();
            Check(catalog.Hulls.Take(existingHulls.Length).SequenceEqual(existingHulls) &&
                catalog.Components.Take(existingComponents.Length).SequenceEqual(existingComponents),
                "Catalog sync preserves existing network indices");
            ShipComponentDef[] components = catalog.Components;
            Check(components.Length == components.Distinct().Count(), "Catalog components are unique");
            Check(catalog.Hulls.Any(hull => hull != null && hull.HullPoints != null &&
                hull.HullPoints.Points.Count(p => (p.Accepts & AcceptedComponents.SubSystem) != 0) > 4),
                "A hull supports more than four subsystem points");
            foreach (HullDef hull in catalog.Hulls)
            {
                Check(hull != null && hull.HullPoints != null, "Hull has a prefab");
                HullPoint[] points = hull.HullPoints.Points;
                Check(points.Length > 4 && points.Distinct().Count() == points.Length, "Hull points are not restricted to four");
                Check(points.All(p => p != null && p.transform.IsChildOf(hull.HullPoints.transform)), "All points belong to the hull prefab");
                HullPoints instance = Object.Instantiate(hull.HullPoints);
                try
                {
                    typeof(HullPoints).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
                    Check(points.Select(p => p.name).SequenceEqual(instance.Points.Select(p => p.name)),
                        "Prefab and instantiated hull use the same point order");
                }
                finally { Object.DestroyImmediate(instance.gameObject); }
                int hullIndex = Array.IndexOf(catalog.Hulls, hull);
                ShipLoadout loadout = catalog.DefaultLoadout(hullIndex);
                Check(catalog.IsValid(loadout), "Default loadout is valid");
                Check(catalog.LaunchProblem(loadout) == null, "Default loadout can launch");
                Check(points.Count(p => (p.Accepts & AcceptedComponents.Shield) != 0) > 1, "Multiple shield points");
                for (int i = 0; i < points.Length; i++)
                {
                    if (loadout.MountComponents[i] >= 0)
                        Check(points[i].AcceptsComponent(components[loadout.MountComponents[i]]), "Default fits its point");
                }
                ValidateWire(loadout, catalog);
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LoadoutMenuController>(true)).ToArray();
            Check(controllers.Length == 1, "One loadout controller exists");
            ValidateCatalogPresentation(catalog, controllers[0]);
            var controllerFields = new SerializedObject(controllers[0]);
            SerializedProperty iterator = controllerFields.GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    Check(iterator.objectReferenceValue != null, "Missing controller reference: " + iterator.propertyPath);
            Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count() == 1,
                "Exactly one EventSystem exists");
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShipHudRoot>(true)).Single();
            Check(hud.GetComponentInParent<GraphicRaycaster>() != null, "Ship HUD can receive clicks");
            var preview = controllers[0].GetComponentInChildren<LoadoutShipPreview>(true);
            Check(preview != null, "Loadout preview exists");
            ShipLoadout initial = catalog.DefaultLoadout(0);
            int clicked = -1;
            preview.Show(initial, 0, index => clicked = index);
            var field = typeof(LoadoutShipPreview).GetField("m_RuntimeMarkers", BindingFlags.Instance | BindingFlags.NonPublic);
            var markers = (System.Collections.Generic.List<LoadoutPreviewMarker>)field.GetValue(preview);
            int visiblePoints = initial.MountComponents.Length;
            Check(markers.Count >= visiblePoints, "A marker exists for every visible point");
            for (int i = 0; i < visiblePoints; i++)
            {
                Check(markers[i].gameObject.activeSelf, "Every point, including empty points, is visible");
                markers[i].GetComponent<Button>().onClick.Invoke();
                Check(clicked == i, "Point marker selects its own mount");
            }
            ValidateMarkerPresentation(preview, catalog, initial, markers);
            ValidatePreviewNavigation(preview, catalog, initial, markers);
            ValidatePreviewProjection(preview, catalog);
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            Debug.Log($"LOADOUT VALIDATION PASSED: {s_Checks} checks.");
        }

        private static void ValidatePreviewNavigation(LoadoutShipPreview preview, ShipCatalog catalog,
            ShipLoadout loadout, System.Collections.Generic.List<LoadoutPreviewMarker> markers)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var raw = (RawImage)typeof(LoadoutShipPreview).GetField("m_Image", flags).GetValue(preview);
            Rect frame = (Rect)typeof(LoadoutShipPreview).GetField("m_Frame", flags).GetValue(preview);
            HullPoints hull = catalog.Hulls[loadout.Hull].HullPoints;
            var point = hull.Points[0];
            Vector2 original = Rect.PointToNormalized(frame, hull.transform.InverseTransformPoint(point.transform.position));
            var marker = (RectTransform)markers[0].transform;
            Vector2 size = marker.sizeDelta;
            Texture texture = raw.texture;
            EventSystem events = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            var input = new PointerEventData(events) { scrollDelta = Vector2.up };
            Check(ExecuteEvents.GetEventHandler<IScrollHandler>(marker.gameObject) == preview.gameObject &&
                ExecuteEvents.GetEventHandler<IDragHandler>(marker.gameObject) == preview.gameObject,
                "Wheel and drag events on slot buttons reach the preview");
            preview.OnScroll(input);
            Rect view = raw.uvRect;
            Check(view.width < 1 && Mathf.Abs(view.center.x - 0.5f) < 0.0001f &&
                Mathf.Abs(view.center.y - 0.5f) < 0.0001f, "Zoom is centred on the preview");
            Check(Vector2.Distance(marker.anchorMin, Rect.PointToNormalized(view, original)) < 0.0001f &&
                marker.sizeDelta == size && raw.texture == texture, "Zoom moves markers without resizing or rerendering");
            int clicked = -1;
            preview.Show(loadout, -1, index => clicked = index);
            markers[0].GetComponent<Button>().onClick.Invoke();
            Check(clicked == 0 && raw.uvRect == view, "Zoom survives refresh and markers remain clickable");

            Canvas.ForceUpdateCanvases();
            RectTransform rect = raw.rectTransform;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(rect.rect.center + rect.rect.size));
            input.button = PointerEventData.InputButton.Right;
            input.pressPosition = start;
            input.position = end;
            preview.OnBeginDrag(input);
            preview.OnDrag(input);
            preview.OnEndDrag(input);
            view = raw.uvRect;
            Check(Mathf.Abs(view.xMin) < 0.0001f && Mathf.Abs(view.yMin) < 0.0001f,
                "Panning clamps at the full-view boundary");
            Check(Vector2.Distance(marker.anchorMin, Rect.PointToNormalized(view, original)) < 0.0001f &&
                marker.sizeDelta == size, "Panning moves markers with the hull without resizing them");
            input.button = PointerEventData.InputButton.Left;
            input.position = start;
            preview.OnBeginDrag(input);
            preview.OnDrag(input);
            Check(raw.uvRect == view, "Left dragging does not pan the preview");
            input.scrollDelta = Vector2.up;
            preview.OnScroll(input);
            Check(Vector2.Distance(raw.uvRect.center, view.center) < 0.0001f,
                "Zooming after panning preserves the centre of the view");
            view = raw.uvRect;
            Color32[] pixels = (Color32[])typeof(LoadoutShipPreview).GetField("m_HullPixels", flags).GetValue(preview);
            int x = Mathf.Clamp(Mathf.FloorToInt(view.center.x * texture.width), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(view.center.y * texture.height), 0, texture.height - 1);
            Check(preview.ContainsHull(start, null) == (pixels[y * texture.width + x].a != 0),
                "Hull hit testing follows the zoomed and panned texture");
            input.scrollDelta = Vector2.down * 1000;
            preview.OnScroll(input);
            Check(raw.uvRect == new Rect(0, 0, 1, 1) && Vector2.Distance(marker.anchorMin, original) < 0.0001f,
                "Zooming out clamps to the full-ship frame and restores marker positions");
            input.scrollDelta = Vector2.up * 1000;
            preview.OnScroll(input);
            float maxZoom = (float)typeof(LoadoutShipPreview).GetField("m_MaxZoom", flags).GetValue(preview);
            Check(Mathf.Abs(raw.uvRect.width - 1 / maxZoom) < 0.0001f, "Zoom-in respects the configured maximum");
            if (catalog.Hulls.Length > 1)
            {
                preview.Show(catalog.DefaultLoadout(1));
                Check(raw.uvRect == new Rect(0, 0, 1, 1), "Changing hulls restores the full-ship view");
            }
            preview.Show(loadout);
        }

        private static void ValidateDetails(ShipCatalog catalog)
        {
            Check(catalog.Components.All(item => !string.IsNullOrWhiteSpace(item.Description)),
                "Components have authored descriptions");
            Check(catalog.Hulls.All(hull => !string.IsNullOrWhiteSpace(hull.Description) &&
                hull.HullPoints.Points.All(point => !string.IsNullOrWhiteSpace(point.Description))),
                "Hulls and hull points have authored descriptions");
            StatBoosterSubSystemDef storage = catalog.Components.OfType<StatBoosterSubSystemDef>().First(item => item.ExtraPowerStorage > 0);
            int hullIndex = Array.FindIndex(catalog.Hulls, hull => hull.HullPoints.Points.Any(point => point.AcceptsComponent(storage)));
            Check(hullIndex >= 0, "A hull can accept a storage subsystem");
            HullDef hull = catalog.Hulls[hullIndex];
            int mount = Array.FindIndex(hull.HullPoints.Points, point => point.AcceptsComponent(storage));
            ShipLoadout loadout = catalog.DefaultLoadout(hullIndex);
            loadout.SetMountComponent(mount, -1);
            float mass = hull.Mass + loadout.MountComponents.Where(index => index >= 0).Sum(index => catalog.Components[index].Mass);
            float capacity = hull.MaxPowerStorage + loadout.MountComponents.Where(index => index >= 0)
                .Select(index => catalog.Components[index]).OfType<StatBoosterSubSystemDef>().Sum(item => item.ExtraPowerStorage);
            loadout.SetMountComponent(mount, Array.IndexOf(catalog.Components, storage));
            string details = LoadoutDetails.Hull(catalog, loadout);
            Check(details.Contains($"Mass: {mass + storage.Mass:0.##}") &&
                details.Contains($"Storage: {capacity + storage.ExtraPowerStorage:0.##}"),
                "Hull details include fitted component mass and storage");
            Check(LoadoutDetails.Component(storage).Contains($"Extra storage: {storage.ExtraPowerStorage:0.##}"),
                "Storage subsystem details describe its fitted effect");
        }

        private static void ValidateArtwork(ShipCatalog catalog)
        {
            foreach (ShipComponentDef component in catalog.Components)
            {
                Sprite art = component.ButtonArtwork;
                Check(art != null && AssetDatabase.GetAssetPath(art.texture).StartsWith("Assets/Art/Loadout Icons/Components/") &&
                    art.texture.width == art.texture.height, $"{component.name} links to a square component texture");
                Color tint = component.Tint;
                bool armour = component is StatBoosterSubSystemDef subsystem && subsystem.ArmourStrengthMultiplier > 1;
                bool expected = component.Type == ComponentType.Gun ? tint.r > 0.8f && tint.g < 0.4f && tint.b < 0.4f
                    : component.Type == ComponentType.Shield ? tint.r > 0.5f && tint.b > 0.8f && tint.g < 0.5f
                    : armour ? Mathf.Max(tint.r, tint.g, tint.b) - Mathf.Min(tint.r, tint.g, tint.b) < 0.1f && tint.r < 0.9f
                    : tint.r < 0.4f && tint.g > 0.8f && tint.b > 0.8f;
                Check(expected, $"{component.name} uses its category tint");
            }
            foreach (LobbySymbolKind kind in Enum.GetValues(typeof(LobbySymbolKind)))
            {
                Sprite sprite = LobbySymbolLibrary.Get(kind);
                Check(sprite != null && AssetDatabase.GetAssetPath(sprite.texture).StartsWith("Assets/Art/Loadout Icons/Symbols/") &&
                    sprite.texture.width == sprite.texture.height, $"{kind} symbol links to a square texture");
            }
            var symbolObject = new GameObject("Symbol validation", typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbySymbol));
            try
            {
                var symbol = symbolObject.GetComponent<LobbySymbol>();
                ((RectTransform)symbol.transform).sizeDelta = new Vector2(80, 40);
                symbol.Kind = LobbySymbolKind.Lock;
                Check(symbol.mainTexture == LobbySymbolLibrary.Get(LobbySymbolKind.Lock).texture, "Symbols render their library texture");
                var mesh = new Mesh();
                var helper = new VertexHelper();
                typeof(LobbySymbol).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(symbol, new object[] { helper });
                helper.FillMesh(mesh);
                Check(helper.currentVertCount == 4 && Mathf.Approximately(mesh.bounds.size.x, mesh.bounds.size.y),
                    "Symbols draw one unstretched square texture quad");
                helper.Dispose();
                Object.DestroyImmediate(mesh);
            }
            finally
            {
                Object.DestroyImmediate(symbolObject);
            }
        }

        private static void ValidateMarkerPresentation(LoadoutShipPreview preview, ShipCatalog catalog,
            ShipLoadout loadout, System.Collections.Generic.List<LoadoutPreviewMarker> markers)
        {
            HullPoint[] points = catalog.Hulls[loadout.Hull].HullPoints.Points;
            Vector3 sizes = (Vector3)typeof(LoadoutShipPreview)
                .GetField("m_MarkerSizes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
            Check(sizes.x > 0 && sizes.y > sizes.x && sizes.z > sizes.y, "Marker sizes are positive and ordered");
            Func<ComponentSize, float> componentSize = size =>
                size == ComponentSize.Small ? sizes.x : size == ComponentSize.Medium ? sizes.y : sizes.z;
            Color32[] pixels = (Color32[])typeof(LoadoutShipPreview)
                .GetField("m_HullPixels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
            int solid = Array.FindIndex(pixels, pixel => pixel.a > 0);
            int clear = Array.FindIndex(pixels, pixel => pixel.a == 0);
            Check(solid >= 0 && clear >= 0, "Rendered hull has both opaque and transparent pixels");
            var raw = (RawImage)typeof(LoadoutShipPreview)
                .GetField("m_Image", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
            Canvas.ForceUpdateCanvases();
            foreach (int pixel in new[] { solid, clear })
            {
                Rect bounds = raw.rectTransform.rect;
                var local = new Vector3(bounds.xMin + bounds.width * (pixel % raw.texture.width + 0.5f) / raw.texture.width,
                    bounds.yMin + bounds.height * (pixel / raw.texture.width + 0.5f) / raw.texture.height);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, raw.rectTransform.TransformPoint(local));
                Check(preview.ContainsHull(screen, null) == (pixel == solid), "Hull hover follows rendered alpha");
            }
            for (int i = 0; i < points.Length; i++)
            {
                float pointSize = points[i].Size == ComponentSize.Small ? sizes.x :
                    points[i].Size == ComponentSize.Medium ? sizes.y : sizes.z;
                var rect = (RectTransform)markers[i].transform;
                Check(rect.sizeDelta == Vector2.one * pointSize, "Point marker is a size-specific square");
                Check(rect.childCount == 2 + 1 + LoadoutPreviewMarker.SlotTypes.Length,
                    "Point marker uses only its authored prefab children");
                foreach (ComponentSize guideSize in new[] { ComponentSize.Small, ComponentSize.Medium })
                {
                    Image guide = markers[i].SizeGuide(guideSize);
                    bool expected = guideSize < points[i].Size;
                    Check(guide.gameObject.activeSelf == expected, "Only smaller slot sizes are outlined inside a slot");
                    if (!expected) continue;
                    Check(guide.rectTransform.anchoredPosition == Vector2.zero &&
                        guide.rectTransform.sizeDelta == Vector2.one * componentSize(guideSize) &&
                        guide.sprite == LobbySymbolLibrary.Get(LobbySymbolKind.SlotGuide) && guide.type == Image.Type.Sliced &&
                        !guide.raycastTarget && guide.transform.GetSiblingIndex() < markers[i].Artwork.transform.GetSiblingIndex() &&
                        guide.color.a < 0.5f, "Size guide is a faint centred outline behind the icons");
                }
            }
            float padding = (float)typeof(LoadoutShipPreview)
                .GetField("m_SlotIconPadding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
            Check(padding >= 0, "Slot icon padding is not negative");
            ShipLoadout empty = loadout;
            for (int i = 0; i < points.Length; i++) empty.SetMountComponent(i, -1);
            preview.Show(empty, -1, null);
            bool sawSingle = false, sawMultiple = false;
            for (int i = 0; i < points.Length; i++)
            {
                var rect = (RectTransform)markers[i].transform;
                float slot = rect.sizeDelta.x;
                ComponentType[] types = LoadoutPreviewMarker.SlotTypes;
                int accepted = types.Count(type => (points[i].Accepts & (AcceptedComponents)(1 << (int)type)) != 0);
                sawSingle |= accepted == 1;
                sawMultiple |= accepted > 1;
                Check(!markers[i].Artwork.enabled, "Empty point shows no component artwork");
                for (int quadrant = 0; quadrant < types.Length; quadrant++)
                {
                    bool accepts = (points[i].Accepts & (AcceptedComponents)(1 << (int)types[quadrant])) != 0;
                    RectTransform icon = markers[i].TypeIcon(types[quadrant]).rectTransform;
                    Check(icon.gameObject.activeSelf == accepts, "Empty point displays exactly its accepted type icons");
                    if (!accepts) continue;
                    if (accepted == 1)
                        Check(icon.anchoredPosition == Vector2.zero && Mathf.Approximately(icon.sizeDelta.x, slot - 2 * padding),
                            "A single accepted type is centred and sized to the slot");
                    else
                    {
                        float size = (slot - 3 * padding) / 2;
                        Vector2 expected = new Vector2(quadrant % 2 == 0 ? -1 : 1, quadrant < 2 ? 1 : -1) * (size + padding) / 2;
                        Check(Vector2.Distance(icon.anchoredPosition, expected) < 0.001f &&
                            Mathf.Approximately(icon.sizeDelta.x, size) &&
                            Mathf.Abs(Mathf.Abs(icon.anchoredPosition.x) + size / 2 + padding - slot / 2) < 0.001f,
                            "Multiple accepted types fill their own quadrant");
                    }
                }
            }
            Check(sawSingle && sawMultiple, "Validation covers single- and multi-type points");
            preview.Show(loadout, 0, null);
            for (int i = 0; i < points.Length; i++)
            {
                if (loadout.MountComponents[i] < 0) continue;
                var rect = (RectTransform)markers[i].transform;
                ShipComponentDef fitted = catalog.Components[loadout.MountComponents[i]];
                RectTransform icon = fitted.ButtonArtwork != null
                    ? markers[i].Artwork.rectTransform
                    : markers[i].TypeIcon(fitted.Type).rectTransform;
                Check(icon.anchoredPosition == Vector2.zero && icon.sizeDelta == Vector2.one * (componentSize(fitted.Size) - 2 * padding),
                    "Fitted icon is centred and sized for its component's size class");
                Check(icon.GetComponent<Graphic>().color == fitted.Tint, "Fitted icon uses the component tint");
                Check(LoadoutPreviewMarker.SlotTypes.Select(type => markers[i].TypeIcon(type))
                    .Count(child => child.gameObject.activeSelf) == (fitted.ButtonArtwork != null ? 0 : 1),
                    "Fitted point hides empty-slot type icons");
            }
            for (int hull = 0; hull < catalog.Hulls.Length; hull++)
            {
                preview.Show(catalog.DefaultLoadout(hull), -1, null);
                Color32[] hullPixels = (Color32[])typeof(LoadoutShipPreview)
                    .GetField("m_HullPixels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
                int width = raw.texture.width, minX = width, maxX = -1;
                for (int p = 0; p < hullPixels.Length; p++)
                {
                    if (hullPixels[p].a == 0) continue;
                    minX = Mathf.Min(minX, p % width);
                    maxX = Mathf.Max(maxX, p % width);
                }
                Check(maxX >= 0 && Mathf.Abs((minX + maxX + 1) / 2f - width / 2f) <= 2,
                    $"{catalog.Hulls[hull].name} render is horizontally centred ({minX}..{maxX} of {width})");
            }
            preview.Show(loadout, 0, null);
            ShipComponentDef chosen = catalog.Components.First(item => !item.IsLocked);
            preview.Show(loadout, -1, chosen, null, null);
            for (int i = 0; i < points.Length; i++)
            {
                bool highlighted = markers[i].GetComponent<Outline>().effectColor == Color.green;
                Check(highlighted == points[i].AcceptsComponent(chosen), "Only compatible points are highlighted");
            }
        }

        private static void ValidateCatalogPresentation(ShipCatalog catalog, LoadoutMenuController controller)
        {
            ShipCatalog copy = Object.Instantiate(catalog);
            ShipComponentDef locked = Object.Instantiate(catalog.Components.First(item => item.Type == ComponentType.Shield));
            GameObject canvas = new GameObject("Catalog validation canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                var price = new SerializedObject(locked);
                price.FindProperty("m_Price").intValue = 100;
                price.ApplyModifiedPropertiesWithoutUndo();
                ShipComponentDef[] components = catalog.Components.ToArray();
                int index = Array.FindIndex(components, item => item.Type == ComponentType.Shield);
                components[index] = locked;
                typeof(ShipCatalog).GetField("m_Components", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, components);
                var root = new GameObject("Catalog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                root.transform.SetParent(canvas.transform, false);
                ((RectTransform)root.transform).sizeDelta = new Vector2(600, 500);
                var panel = root.AddComponent<LoadoutCatalogPanel>();
                ShipComponentDef selected = null;
                int selectedColour = -1;
                bool selectedSecondary = false;
                string hover = null;
                var controllerFields = new SerializedObject(controller);
                var title = (TMPro.TMP_Text)controllerFields.FindProperty("m_HullName").objectReferenceValue;
                var layout = new LoadoutCatalogLayout
                {
                    ComponentColumns = 3, ColourColumns = 16, IconGap = 4, IconInset = 6, CategoryPadding = 8, CategoryGap = 12, TitleHeight = 30,
                    SizeScales = new Vector3(0.5f, 0.75f, 1f)
                };
                panel.Initialize(copy, title.font, layout, 0, item => selected = item,
                    (colour, secondary) => { selectedColour = colour; selectedSecondary = secondary; }, text => hover = text);
                Canvas.ForceUpdateCanvases();
                panel.Refresh(null, null, PlayerPalette.DefaultPrimary, PlayerPalette.DefaultSecondary);
                var sections = (System.Collections.IList)typeof(LoadoutCatalogPanel)
                    .GetField("m_Sections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                Check(sections.Count == 6, "Catalog contains four component and two colour sections");
                foreach (object section in sections)
                {
                    var sectionRect = (RectTransform)section.GetType().GetField("Rect").GetValue(section);
                    Check(sectionRect.GetComponent<Outline>() != null && sectionRect.Find("Title").GetComponent<Image>() != null,
                        "Each category has a border and a title bar");
                    var entries = (System.Collections.IList)section.GetType().GetField("Entries").GetValue(section);
                    int previousPrice = -1;
                    foreach (object entry in entries)
                    {
                        Type type = entry.GetType();
                        var definition = (ShipComponentDef)type.GetField("Component").GetValue(entry);
                        int colour = (int)type.GetField("Colour").GetValue(entry);
                        int currentPrice = definition != null ? definition.Price : PlayerPalette.Price(colour);
                        Check(currentPrice >= previousPrice, "Category entries are ordered by price");
                        previousPrice = currentPrice;
                        var rect = (RectTransform)type.GetField("Rect").GetValue(entry);
                        Check(rect.gameObject.activeSelf, "Unfiltered catalog entries are visible");
                        Check(Mathf.Approximately(rect.sizeDelta.x, rect.sizeDelta.y), "Catalog icons are square");
                        if (definition != null && !definition.IsLocked)
                        {
                            Image icon = rect.Find("Icon").GetComponent<Image>();
                            Check(icon != null && icon.sprite == definition.ButtonArtwork && icon.color == definition.Tint,
                                "Catalog icon shows the component texture and tint");
                            float scale = definition.Size == ComponentSize.Small ? layout.SizeScales.x :
                                definition.Size == ComponentSize.Medium ? layout.SizeScales.y : layout.SizeScales.z;
                            float expectedIcon = (rect.sizeDelta.x - 2 * layout.IconInset) * scale;
                            Check(icon.rectTransform.anchoredPosition == Vector2.zero &&
                                icon.rectTransform.anchorMin == Vector2.one * 0.5f &&
                                Mathf.Abs(icon.rectTransform.sizeDelta.x - expectedIcon) < 0.01f &&
                                Mathf.Approximately(icon.rectTransform.sizeDelta.x, icon.rectTransform.sizeDelta.y),
                                $"{definition.name} catalog icon is centred and sized for a {definition.Size} component");
                        }
                        if (definition == locked)
                        {
                            Check(rect.Find("Locked") != null && rect.GetComponent<Button>().interactable, "Locked component is inspectable and marked");
                            rect.GetComponent<Button>().onClick.Invoke();
                            Check(selected == locked, "Locked component can be selected for inspection");
                            selected = null;
                            var item = rect.GetComponent<LoadoutCatalogItem>();
                            var hoverFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                            FieldInfo hoverDelay = typeof(LoadoutCatalogPanel).GetField("m_HoverDelay", hoverFlags);
                            FieldInfo hoverChangedAt = typeof(LoadoutCatalogPanel).GetField("m_HoverChangedAt", hoverFlags);
                            FieldInfo pointerPosition = typeof(LoadoutCatalogPanel).GetField("m_PointerPosition", hoverFlags);
                            MethodInfo panelUpdate = typeof(LoadoutCatalogPanel).GetMethod("Update", hoverFlags);
                            Action rest = () =>
                            {
                                if (UnityEngine.InputSystem.Pointer.current != null)
                                    pointerPosition.SetValue(panel, UnityEngine.InputSystem.Pointer.current.position.ReadValue());
                                hoverChangedAt.SetValue(panel, Time.unscaledTime - 1f);
                                panelUpdate.Invoke(panel, null);
                            };
                            hoverDelay.SetValue(panel, 0.4f);
                            item.OnPointerEnter(null);
                            Check(hover == null && selected == null, "Hover does not immediately show or select a component");
                            panelUpdate.Invoke(panel, null);
                            Check(hover == null, "Hover waits for the pointer to rest");
                            rest();
                            Check(hover != null && hover.Contains("Locked"), "Locked component hover explains its price");
                            Check(selected == null, "Delayed hover does not select a component");
                            string shown = hover;
                            item.OnPointerExit(null);
                            panelUpdate.Invoke(panel, null);
                            Check(hover == shown, "Leaving a component does not immediately revert the details");
                            rest();
                            Check(hover == null, "Resting away from components restores the fallback details");
                            hoverDelay.SetValue(panel, 0f);
                        }
                        var positions = entries.Cast<object>().Select(entry =>
                            (RectTransform)entry.GetType().GetField("Rect").GetValue(entry));
                        bool colourSection = definition == null;
                        int expectedColumns = colourSection ? layout.ColourColumns : layout.ComponentColumns;
                        Check(positions.GroupBy(rect => rect.anchoredPosition.y).All(row => row.Count() <= expectedColumns),
                            "Configured row limit is respected");
                        float rowWidth = ((RectTransform)rect.parent).rect.width;
                        float expectedCell = (rowWidth - layout.CategoryPadding * 2 - (expectedColumns - 1) * layout.IconGap) / expectedColumns;
                        Check(Mathf.Abs(rect.sizeDelta.x - expectedCell) < 0.01f,
                            "Icon width is the panel width divided by the category's column count");
                    }
                }
                for (int section = 4; section < 6; section++)
                {
                    object item = sections[section];
                    var entries = (System.Collections.IList)item.GetType().GetField("Entries").GetValue(item);
                    Check(entries.Count == PlayerPalette.Count, "Each colour category contains the complete palette");
                    int lockedColours = entries.Cast<object>().Count(entry =>
                        ((RectTransform)entry.GetType().GetField("Rect").GetValue(entry)).Find("Locked") != null);
                    Check(lockedColours == 48, "Each colour category shows 48 locked swatches");
                    RectTransform[] swatches = entries.Cast<object>()
                        .Select(entry => (RectTransform)entry.GetType().GetField("Rect").GetValue(entry)).ToArray();
                    Check(swatches.GroupBy(rect => rect.anchoredPosition.y).All(row => row.Count() == layout.ColourColumns),
                        "Colour swatches fill rows of the configured column count");
                    RectTransform last = swatches[layout.ColourColumns - 1];
                    float rightEdge = last.anchoredPosition.x + last.sizeDelta.x;
                    Check(Mathf.Abs(rightEdge + layout.CategoryPadding - ((RectTransform)last.parent).rect.width) < 0.01f,
                        "A full row of swatches spans the category width");
                    var colourEntry = entries[0];
                    var colourRect = (RectTransform)colourEntry.GetType().GetField("Rect").GetValue(colourEntry);
                    colourRect.GetComponent<Button>().onClick.Invoke();
                    Check(selectedColour == 0 && selectedSecondary == (section == 5),
                        "Colour selection identifies the swatch and its channel");
                }
                var content = (RectTransform)typeof(LoadoutCatalogPanel)
                    .GetField("m_Content", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                var scrollbar = (GameObject)typeof(LoadoutCatalogPanel)
                    .GetField("m_Scrollbar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                Check(scrollbar.activeSelf, "Overflowing catalog shows its scrollbar");
                Canvas.ForceUpdateCanvases();
                RectTransform handle = scrollbar.GetComponent<Scrollbar>().handleRect;
                RectTransform track = (RectTransform)scrollbar.transform;
                Check(handle.rect.width <= track.rect.width && handle.rect.height <= track.rect.height,
                    "Scrollbar handle stays within its track");
                panel.MoveCategory(1);
                float secondTop = (float)sections[1].GetType().GetField("Top").GetValue(sections[1]);
                Check(Mathf.Approximately(content.anchoredPosition.y, secondTop), "Page down advances to the next category");
                panel.MoveCategory(-1);
                Check(Mathf.Approximately(content.anchoredPosition.y, 0), "Page up returns to the previous category");
                HullPoint point = catalog.Hulls[0].HullPoints.Points.First();
                panel.Refresh(point, null, PlayerPalette.DefaultPrimary, PlayerPalette.DefaultSecondary);
                foreach (object section in sections)
                {
                    var entries = (System.Collections.IList)section.GetType().GetField("Entries").GetValue(section);
                    foreach (object entry in entries)
                    {
                        Type type = entry.GetType();
                        var definition = (ShipComponentDef)type.GetField("Component").GetValue(entry);
                        var rect = (RectTransform)type.GetField("Rect").GetValue(entry);
                        Check(rect.gameObject.activeSelf == (definition != null && point.AcceptsComponent(definition)),
                            "Selected point filters entries by type and size");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(locked);
                Object.DestroyImmediate(copy);
            }
        }

        private static void ValidateAutomaticHullPoints()
        {
            var root = new GameObject("Hull point collection test");
            try
            {
                HullPoints hull = root.AddComponent<HullPoints>();
                var first = new GameObject("First");
                first.transform.SetParent(root.transform, false);
                HullPoint firstPoint = first.AddComponent<HullPoint>();
                var second = new GameObject("Second");
                second.transform.SetParent(first.transform, false);
                HullPoint secondPoint = second.AddComponent<HullPoint>();
                second.SetActive(false);
                Check(hull.Points.SequenceEqual(new[] { firstPoint, secondPoint }), "Prefab points include nested and inactive children");
                typeof(HullPoints).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hull, null);
                Check(hull.Points.SequenceEqual(new[] { firstPoint, secondPoint }), "Awake collects hull points in hierarchy order");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ValidateThrusterDirections()
        {
            var method = typeof(Ship).GetMethod("GetThrusterDirection", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var example in new[]
            {
                (Vector3.down, Vector3.up, Vector3.right, "Forward"),
                (Vector3.up, Vector3.up, Vector3.right, "Reverse"),
                (Vector3.right, Vector3.up, Vector3.right, "Left"),
                (Vector3.left, Vector3.up, Vector3.right, "Right"),
                (new Vector3(-1f, -1f, 0f), Vector3.up, Vector3.right, "Forward"),
                (new Vector3(1f, 1f, 0f), Vector3.up, Vector3.right, "Reverse"),
                (Vector3.down, Vector3.left, Vector3.up, "Right")
            })
                Check(method.Invoke(null, new object[] { example.Item1, example.Item2, example.Item3 }).ToString() == example.Item4,
                    "Thruster direction " + example.Item4);

            bool rejected = false;
            try { method.Invoke(null, new object[] { Vector3.forward, Vector3.up, Vector3.right }); }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected, "Thruster direction perpendicular to the movement plane is rejected");
        }

        private static void ValidateThrusterEffects(ShipCatalog catalog)
        {
            var root = new GameObject("Thruster effect alignment test");
            var point = new GameObject("Rotated hull point");
            try
            {
                point.transform.SetParent(root.transform, false);
                point.transform.localRotation = Quaternion.Euler(0f, 0f, 37f);
                var effects = root.AddComponent<ThrusterEffects>();
                foreach (ThrusterDef def in catalog.Components.OfType<ThrusterDef>())
                {
                    if (def.Effect == null) continue;
                    effects.Build(def, new[] { point.transform });
                    ParticleSystem[] particles = point.GetComponentsInChildren<ParticleSystem>();
                    Check(particles.Length > 0, "Thruster effect has particles");
                    foreach (ParticleSystem emitter in particles)
                    {
                        Vector3 axis = emitter.transform.rotation * Quaternion.Euler(emitter.shape.rotation) * Vector3.forward;
                        Check(Vector3.Dot(axis.normalized, point.transform.up) > 0.999f,
                            "Thruster effect cone follows hull point Up");
                    }
                    while (point.transform.childCount > 0) Object.DestroyImmediate(point.transform.GetChild(0).gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidatePreviewProjection(LoadoutShipPreview preview, ShipCatalog catalog)
        {
            HullDef original = catalog.Hulls[0];
            HullDef hull = Object.Instantiate(original);
            HullPoints points = Object.Instantiate(original.HullPoints);
            try
            {
                typeof(HullDef).GetField("m_HullPoints", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hull, points);
                int extraPoints = 45 - points.Points.Length;
                for (int i = 0; i < extraPoints; i++)
                {
                    var point = new GameObject("Extra preview point");
                    point.transform.SetParent(points.transform, false);
                    point.transform.localRotation = Quaternion.Euler(0, 0, i * 37);
                    point.AddComponent<HullPoint>();
                }
                catalog.Hulls[0] = hull;
                ShipLoadout loadout = catalog.DefaultLoadout(0);
                int clicked = -1;
                preview.Show(loadout, -1, index => clicked = index);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var markers = (System.Collections.Generic.List<LoadoutPreviewMarker>)
                    typeof(LoadoutShipPreview).GetField("m_RuntimeMarkers", flags).GetValue(preview);
                Rect frame = (Rect)typeof(LoadoutShipPreview).GetField("m_Frame", flags).GetValue(preview);
                Check(markers.Count >= 45, "All 45 points have markers");
                var renderer = (LobbyHullRenderer)typeof(LoadoutShipPreview).GetField("m_Renderer", flags).GetValue(preview);
                var camera = (Camera)typeof(LobbyHullRenderer).GetField("m_Camera", flags).GetValue(renderer);
                for (int i = 0; i < loadout.MountComponents.Length; i++)
                {
                    Vector3 local = points.transform.InverseTransformPoint(points.Points[i].transform.position);
                    Vector2 expected = Rect.PointToNormalized(frame, local);
                    RectTransform marker = (RectTransform)markers[i].transform;
                    Check(markers[i].gameObject.activeSelf && Vector2.Distance(marker.anchorMin, expected) < 0.0001f,
                        "Marker is projected onto its authored point");
                    Vector2 viewport = camera.WorldToViewportPoint(renderer.transform.TransformPoint(local));
                    Check(Vector2.Distance(marker.anchorMin, viewport) < 0.001f,
                        $"Marker {i} sits where the render camera drew its point ({marker.anchorMin} vs {viewport})");
                    Vector3 localUp = points.transform.InverseTransformDirection(points.Points[i].transform.up);
                    Vector2 tip = camera.WorldToViewportPoint(renderer.transform.TransformPoint(local + localUp));
                    Vector2 drawnUp = Vector2.Scale(tip - viewport, new Vector2(camera.aspect, 1));
                    if (drawnUp.sqrMagnitude > 0.000001f)
                        Check(Vector2.Angle(drawnUp, marker.localRotation * Vector3.up) < 0.1f,
                            $"Marker {i} up points along its hull point's rendered up vector");
                }
                Check(Mathf.Abs(Mathf.DeltaAngle(markers[44].transform.localEulerAngles.z, (44 - (45 - extraPoints)) * 37f)) < 0.01f,
                    "A rotated hull point rotates its marker by the same angle");
                markers[44].GetComponent<Button>().onClick.Invoke();
                Check(clicked == 44, "Last marker selects its actual hull point");
            }
            finally
            {
                catalog.Hulls[0] = original;
                Object.DestroyImmediate(points.gameObject);
                Object.DestroyImmediate(hull);
            }
        }

        private static void ValidateWire(ShipLoadout loadout, ShipCatalog catalog)
        {
            var writer = new NetworkWriter();
            writer.WriteShipLoadout(loadout);
            var read = new NetworkReader(writer.ToArray()).ReadShipLoadout();
            Check(read.Hull == loadout.Hull && read.Colour0.Equals(loadout.Colour0) &&
                read.Colour1.Equals(loadout.Colour1) && read.MountComponents.SequenceEqual(loadout.MountComponents),
                "Loadout network round trip");
            var malformed = new NetworkWriter();
            malformed.WriteInt(loadout.Hull);
            malformed.WriteColor32(loadout.Colour0);
            malformed.WriteColor32(loadout.Colour1);
            malformed.WriteInt(int.MaxValue);
            bool rejected = false;
            try { new NetworkReader(malformed.ToArray()).ReadShipLoadout(); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Unbounded network mount count rejected before allocation");
            ShipLoadout invalid = loadout;
            invalid.SetMountComponent(0, catalog.Components.Length);
            Check(!catalog.IsValid(invalid), "Out-of-range component rejected");
        }

        public static void ValidateHullPointCompatibility()
        {
            var colourMethod = typeof(HullPoint).GetMethod("GizmoColour", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var example in new[]
            {
                (AcceptedComponents.Gun, Color.red),
                (AcceptedComponents.Thruster, Color.yellow),
                (AcceptedComponents.Shield, Color.cyan),
                (AcceptedComponents.SubSystem, Color.green),
                (AcceptedComponents.None, Color.gray),
                (AcceptedComponents.Gun | AcceptedComponents.Shield, Color.white),
                (AcceptedComponents.Thruster | AcceptedComponents.SubSystem, Color.white)
            })
                Check((Color)colourMethod.Invoke(null, new object[] { example.Item1 }) == example.Item2,
                    "Hull point gizmo colour for " + example.Item1);

            var pointObject = new GameObject("Hull point compatibility test");
            var shield = ScriptableObject.CreateInstance<ShieldDef>();
            var gun = ScriptableObject.CreateInstance<GunDef>();
            var thruster = ScriptableObject.CreateInstance<ThrusterDef>();
            var hull = ScriptableObject.CreateInstance<HullDef>();
            try
            {
                var point = pointObject.AddComponent<HullPoint>();
                var pointData = new SerializedObject(point);
                pointData.FindProperty("m_Accepts").intValue = (int)(AcceptedComponents.Shield | AcceptedComponents.Gun);
                pointData.FindProperty("m_Size").enumValueIndex = (int)ComponentSize.Medium;
                pointData.ApplyModifiedPropertiesWithoutUndo();
                var shieldData = new SerializedObject(shield);
                shieldData.FindProperty("m_Size").enumValueIndex = (int)ComponentSize.Small;
                shieldData.ApplyModifiedPropertiesWithoutUndo();
                Check(point.AcceptsComponent(shield), "Smaller accepted component fits");
                shieldData.FindProperty("m_Size").enumValueIndex = (int)ComponentSize.Medium;
                shieldData.ApplyModifiedPropertiesWithoutUndo();
                Check(point.AcceptsComponent(shield), "Equal-size accepted component fits");
                shieldData.FindProperty("m_Size").enumValueIndex = (int)ComponentSize.Large;
                shieldData.ApplyModifiedPropertiesWithoutUndo();
                Check(!point.AcceptsComponent(shield), "Oversized component does not fit");
                Check(point.AcceptsComponent(gun), "Multiple accepted component types fit");
                Check(!point.AcceptsComponent(hull) && !point.AcceptsComponent(null), "Hull and null are not mountable");
                pointData.FindProperty("m_Accepts").intValue = (int)AcceptedComponents.Gun;
                pointData.ApplyModifiedPropertiesWithoutUndo();
                shieldData.FindProperty("m_Size").enumValueIndex = (int)ComponentSize.Small;
                shieldData.ApplyModifiedPropertiesWithoutUndo();
                Check(!point.AcceptsComponent(shield), "Unaccepted type does not fit");
                pointData.FindProperty("m_Accepts").intValue = (int)(AcceptedComponents.Gun | AcceptedComponents.Thruster);
                pointData.ApplyModifiedPropertiesWithoutUndo();
                Check(point.AcceptsComponent(gun) && point.AcceptsComponent(thruster),
                    "One hull point can accept both a gun and a thruster");
            }
            finally
            {
                Object.DestroyImmediate(pointObject);
                Object.DestroyImmediate(shield);
                Object.DestroyImmediate(gun);
                Object.DestroyImmediate(thruster);
                Object.DestroyImmediate(hull);
            }
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException("Loadout validation failed: " + message);
            s_Checks++;
        }
    }
}
