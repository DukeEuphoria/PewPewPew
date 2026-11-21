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
                Check(loadout.MountComponents.Any(i => i >= 0 && components[i] is ShieldDef), "Default has a shield");
                Check(loadout.MountComponents.Any(i => i >= 0 && components[i] is GunDef), "Default has a gun");
                Check(loadout.MountComponents.Any(i => i >= 0 && components[i] is ThrusterDef), "Default has a thruster");
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
            int visiblePoints = Math.Min(20, initial.MountComponents.Length);
            Check(markers.Count >= visiblePoints, "A marker exists for every visible point");
            for (int i = 0; i < visiblePoints; i++)
            {
                Check(markers[i].gameObject.activeSelf, "Every point, including empty points, is visible");
                markers[i].GetComponent<Button>().onClick.Invoke();
                Check(clicked == i, "Point marker selects its own mount");
            }
            ValidatePreviewPaging(preview, catalog);
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            Debug.Log($"LOADOUT VALIDATION PASSED: {s_Checks} checks.");
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

        private static void ValidatePreviewPaging(LoadoutShipPreview preview, ShipCatalog catalog)
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
                    point.AddComponent<HullPoint>();
                }
                catalog.Hulls[0] = hull;
                ShipLoadout loadout = catalog.DefaultLoadout(0);
                int clicked = -1;
                preview.Show(loadout, -1, index => clicked = index);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var markers = (System.Collections.Generic.List<LoadoutPreviewMarker>)
                    typeof(LoadoutShipPreview).GetField("m_RuntimeMarkers", flags).GetValue(preview);
                var next = (LoadoutPreviewMarker)typeof(LoadoutShipPreview).GetField("m_NextPage", flags).GetValue(preview);
                var previous = (LoadoutPreviewMarker)typeof(LoadoutShipPreview).GetField("m_PreviousPage", flags).GetValue(preview);
                Check(next.gameObject.activeSelf && !previous.gameObject.activeSelf, "First page has next navigation");
                next.GetComponent<Button>().onClick.Invoke();
                Check(previous.gameObject.activeSelf && next.gameObject.activeSelf, "Middle page has both navigation buttons");
                next.GetComponent<Button>().onClick.Invoke();
                Check(previous.gameObject.activeSelf && !next.gameObject.activeSelf, "Last page has previous navigation");
                markers[4].GetComponent<Button>().onClick.Invoke();
                Check(clicked == 44, "Last page selects its actual hull point");
                previous.GetComponent<Button>().onClick.Invoke();
                markers[0].GetComponent<Button>().onClick.Invoke();
                Check(clicked == 20, "Previous page selects its actual hull point");
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
