using System;
using System.Linq;
using System.Reflection;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Networking;
using PewPewPew.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PewPewPew.Editor
{
    [InitializeOnLoad]
    public static class SoloMenuPlayModeValidation
    {
        private const string ActiveKey = "PewPewPew.SoloMenuValidation.Active";
        private const string ReloadKey = "PewPewPew.SoloMenuValidation.Reload";
        private const string ResultKey = "PewPewPew.SoloMenuValidation.Result";
        private static int s_Stage;
        private static int s_HullIndex;
        private static int s_MarkerIndex;
        private static double s_Next;
        private static double s_Deadline;
        private static IPlayerColourStore s_OriginalColourStore;

        static SoloMenuPlayModeValidation()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            if (SessionState.GetBool(ReloadKey, false)) EditorApplication.delayCall += StartAfterReload;
        }

        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before running solo validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/SplashScreen.unity");
            foreach (HullDef hull in ShipCatalog.Instance.Hulls)
                if (hull.HullPoints.Points.Length == 0)
                    throw new InvalidOperationException($"{hull.name} has no hull points before script reload.");
            SessionState.SetBool(ReloadKey, true);
            EditorUtility.RequestScriptReload();
        }

        private static void StartAfterReload()
        {
            SessionState.SetBool(ReloadKey, false);
            try
            {
                foreach (HullDef hull in ShipCatalog.Instance.Hulls)
                {
                    HullPoints points = hull.HullPoints;
                    if (!points.Points.SequenceEqual(points.GetComponentsInChildren<HullPoint>(true)) ||
                        points.Points.Length == 0)
                        throw new InvalidOperationException($"{hull.name} lost its hull points after script reload.");
                }
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(ResultKey, 1);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_Stage = 0;
                s_HullIndex = 0;
                s_MarkerIndex = 0;
                s_Next = EditorApplication.timeSinceStartup + 1;
                s_Deadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                if (s_OriginalColourStore != null) PlayerColourStore.Current = s_OriginalColourStore;
                s_OriginalColourStore = null;
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(ResultKey, 1));
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.timeSinceStartup < s_Next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > s_Deadline)
                    throw new TimeoutException("Solo menu validation timed out at stage " + s_Stage);
                Step();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(false);
            }
        }

        private static void Step()
        {
            switch (s_Stage)
            {
                case 0:
                    s_OriginalColourStore = PlayerColourStore.Current;
                    PlayerColourStore.Current = new LobbyPlayModeValidation.ValidationColourStore();
                    MenuScreenFlow flow = UnityEngine.Object.FindObjectsByType<MenuScreenFlow>()
                        .Single(item => item.CurrentScreen == MenuScreenId.Title);
                    flow.ShowScreen(MenuScreenId.Main);
                    Next(1.2);
                    break;
                case 1:
                    var button = GameObject.Find("Play Solo");
                    if (button == null) throw new InvalidOperationException("Solo button is not visible on the main menu.");
                    var input = UnityEngine.Object.FindAnyObjectByType<MenuRaycastInput>();
                    Camera camera = Get<Camera>(input, "m_RaycastCamera");
                    BoxCollider collider = button.GetComponent<BoxCollider>();
                    Vector3 screen = camera.WorldToScreenPoint(collider.bounds.center);
                    Ray ray = camera.ScreenPointToRay(screen);
                    int layers = Get<LayerMask>(input, "m_ButtonLayers").value;
                    if (!Physics.Raycast(ray, out RaycastHit hit, Get<float>(input, "m_MaxDistance"), layers, QueryTriggerInteraction.Collide) ||
                        hit.collider.GetComponentInParent<MenuRaycastButton>() != button.GetComponent<MenuRaycastButton>())
                        throw new InvalidOperationException("Solo button cannot be reached by the menu raycast.");
                    hit.collider.GetComponentInParent<MenuRaycastButton>().Activate();
                    Next(1);
                    break;
                case 2:
                    if (SceneManager.GetActiveScene().name != "GameScene" || PlayerState.Local == null) return;
                    if (!(Mirror.NetworkManager.singleton is GameNetworkManager manager) || !manager.IsOfflineGame)
                        throw new InvalidOperationException("Solo button did not start an offline host.");
                    LoadoutMenuController menu = UnityEngine.Object.FindAnyObjectByType<LoadoutMenuController>();
                    Button deploy = Get<Button>(menu, "m_Deploy");
                    if (!deploy.IsActive() || !deploy.interactable) return;
                    Next(0.5);
                    break;
                case 3:
                    menu = UnityEngine.Object.FindAnyObjectByType<LoadoutMenuController>();
                    LoadoutShipPreview preview = Get<LoadoutShipPreview>(menu, "m_Preview");
                    var markers = Get<System.Collections.Generic.List<LoadoutPreviewMarker>>(preview, "m_RuntimeMarkers");
                    int pointCount = ShipCatalog.Instance.Hulls[menu.Pending.Hull].HullPoints.Points.Length;
                    int visibleCount = pointCount;
                    if (visibleCount == 0 || markers.Count(marker => marker.gameObject.activeInHierarchy) != visibleCount)
                        throw new InvalidOperationException($"Expected {visibleCount} visible hull point markers; found {markers.Count(marker => marker.gameObject.activeInHierarchy)}.");
                    LoadoutPreviewMarker target = markers[s_MarkerIndex];
                    var canvas = target.GetComponentInParent<Canvas>();
                    Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, target.transform.position);
                    var pointer = new PointerEventData(EventSystem.current) { position = screenPosition, button = PointerEventData.InputButton.Left };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    if (target.GetComponent<CanvasRenderer>().cull || hits.Count == 0 ||
                        hits[0].gameObject.GetComponentInParent<LoadoutPreviewMarker>() == null)
                        throw new InvalidOperationException($"Hull {menu.Pending.Hull} marker {s_MarkerIndex} is not visible or reachable by UI raycast; first hit: {(hits.Count == 0 ? "none" : hits[0].gameObject.name)}.");
                    if (hits[0].gameObject.GetComponentInParent<LoadoutPreviewMarker>() == target)
                        ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    else target.GetComponent<Button>().onClick.Invoke();
                    if (Get<int>(menu, "m_SelectedPoint") != s_MarkerIndex)
                        throw new InvalidOperationException($"Clicking hull point {s_MarkerIndex} did not select it.");
                    if (++s_MarkerIndex < visibleCount) return;
                    Debug.Log($"SOLO LOADOUT VALIDATION: hull {menu.Pending.Hull}, {visibleCount} visible and selectable hull points.");
                    s_MarkerIndex = 0;
                    if (++s_HullIndex < ShipCatalog.Instance.Hulls.Length)
                    {
                        menu.CycleHull(1);
                        return;
                    }
                    Get<Button>(menu, "m_Deploy").onClick.Invoke();
                    Next(1);
                    break;
                case 4:
                    if (MatchManager.Instance == null || MatchManager.Instance.State != MatchState.Running ||
                        PlayerState.Local == null || !PlayerState.Local.HasShip) return;
                    Debug.Log("SOLO MENU PLAY MODE VALIDATION PASSED: raycast hit, button activation, scene change, ready and ship spawn.");
                    Finish(true);
                    break;
            }
        }

        private static void Next(double wait)
        {
            s_Stage++;
            s_Next = EditorApplication.timeSinceStartup + wait;
        }

        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (Mirror.NetworkManager.singleton is GameNetworkManager manager && manager.IsOfflineGame)
            {
                manager.offlineScene = string.Empty;
                manager.StopOfflineGame();
            }
            if (s_OriginalColourStore != null) PlayerColourStore.Current = s_OriginalColourStore;
            EditorApplication.ExitPlaymode();
        }

        private static T Get<T>(object instance, string name) =>
            (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
    }
}
