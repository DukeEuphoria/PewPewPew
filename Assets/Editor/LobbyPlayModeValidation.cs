using System;
using System.Linq;
using System.Reflection;
using kcp2k;
using Mirror;
using PewPewPew.Match;
using PewPewPew.Networking;
using PewPewPew.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace PewPewPew.Editor
{
    /// Local host plus a server-side peer exercises Mirror commands without needing a second Steam account.
    [InitializeOnLoad]
    public static class LobbyPlayModeValidation
    {
        private const string ActiveKey = "PewPewPew.LobbySmoke.Active";
        private const string ResultKey = "PewPewPew.LobbySmoke.Result";
        private static GameNetworkManager s_Manager;
        private static PlayerState s_Peer;
        private static NetworkConnectionToClient s_Connection;
        private static LoadoutMenuController s_Menu;
        private static int s_Stage;
        private static double s_Next;
        private static double s_Deadline;
        private static int s_OriginalHull;

        static LobbyPlayModeValidation()
        {
            EditorApplication.playModeStateChanged += StateChanged;
        }

        [MenuItem("PewPewPew/Loadout/Validate lobby in play mode")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before running lobby validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ResultKey, 1);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_Stage = 0;
                s_Next = EditorApplication.timeSinceStartup + 1;
                s_Deadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(ResultKey, 1));
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (EditorApplication.timeSinceStartup < s_Next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > s_Deadline) throw new TimeoutException("Lobby play mode validation timed out at stage " + s_Stage);
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
            MatchManager match = MatchManager.Instance;
            PlayerState local = PlayerState.Local;
            switch (s_Stage)
            {
                case 0:
                    foreach (MenuScreenFlow flow in Object.FindObjectsByType<MenuScreenFlow>()) flow.enabled = false;
                    s_Menu = Object.FindAnyObjectByType<LoadoutMenuController>();
                    Check(s_Menu != null && match != null, "Scene controllers exist");
                    var managerObject = new GameObject("Lobby validation host");
                    KcpTransport transport = managerObject.AddComponent<KcpTransport>();
                    transport.port = 0;
                    s_Manager = managerObject.AddComponent<GameNetworkManager>();
                    s_Manager.transport = transport;
                    s_Manager.playerPrefab = Resources.Load<GameObject>("SpawnablePrefabs/Player/PlayerPrefab");
                    s_Manager.onlineScene = s_Manager.offlineScene = string.Empty;
                    s_Manager.maxConnections = 100;
                    s_Manager.spawnPrefabs.AddRange(Resources.LoadAll<GameObject>("SpawnablePrefabs").Where(prefab => prefab != s_Manager.playerPrefab));
                    s_Manager.StartHost();
                    Next();
                    break;
                case 1:
                    if (local == null) return;
                    Check(s_Manager.maxConnections == 16 && NetworkServer.maxConnections == 16, "Server capacity is capped at 16");
                    var preview = Get<LoadoutShipPreview>(s_Menu, "m_Preview");
                    Check(Get<RawImage>(preview, "m_Image").texture != null, "Main ship pane renders during play");
                    var description = Get<CanvasGroup>(preview, "m_Description");
                    Check(description.alpha == 0, "Hull description starts hidden");
                    var hover = preview.GetComponentInChildren<LoadoutPreviewHoverArea>();
                    ExecuteEvents.Execute(hover.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                    Check(description.alpha == 1, "Separate hull hover rectangle reveals description");
                    ExecuteEvents.Execute(hover.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                    Check(description.alpha == 0, "Leaving hull hover rectangle hides description");
                    ExecuteEvents.Execute(hover.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                    hover.gameObject.SetActive(false);
                    Check(description.alpha == 0, "Disabling hover rectangle hides description in play mode");
                    hover.gameObject.SetActive(true);
                    Check(match.State == MatchState.Lobby && !local.Ready, "Unready solo player does not auto-start");
                    s_Connection = new ValidationConnection(1) { isAuthenticated = true };
                    NetworkServer.connections.Add(1, s_Connection);
                    GameObject peer = Object.Instantiate(s_Manager.playerPrefab);
                    NetworkServer.AddPlayerForConnection(s_Connection, peer);
                    s_Peer = peer.GetComponent<PlayerState>();
                    s_OriginalHull = local.Loadout.Hull;
                    match.CmdStartMatch();
                    local.CmdDeploy();
                    Next();
                    break;
                case 2:
                    Check(match.State == MatchState.Lobby && local.Ready && !s_Peer.Ready, "One ready player cannot start a two-player lobby");
                    Check(!s_Menu.CanEdit, "Ready locks editing");
                    var editing = Get<CanvasGroup>(s_Menu, "m_EditingGroup");
                    Check(!editing.interactable, "Ready locks canvas controls");
                    Check(Get<LobbySymbol>(s_Menu.GetComponent<LobbyPanel>(), "m_ReadySymbol").Kind == LobbySymbolKind.Stop, "Ready control displays a stop square");
                    var changed = local.Loadout;
                    changed.Hull = (changed.Hull + 1) % ShipCatalog.Instance.Hulls.Length;
                    local.CmdSetLoadout(changed);
                    local.CmdDeploy();
                    Next();
                    break;
                case 3:
                    Check(local.Loadout.Hull == s_OriginalHull, "Server rejects fitting while ready");
                    Check(!local.Ready && s_Menu.CanEdit, "Stop clears readiness and restores editing");
                    Check(Get<LobbySymbol>(s_Menu.GetComponent<LobbyPanel>(), "m_ReadySymbol").Kind == LobbySymbolKind.Play, "Unready control displays play triangle");
                    s_Peer.ServerSetReady(true);
                    local.CmdDeploy();
                    Next();
                    break;
                case 4:
                    Check(match.State == MatchState.Running && local.HasShip && s_Peer.HasShip, "All-ready automatically starts and deploys both ships");
                    Ship ship = local.Ship;
                    Set(match, "m_RespawnDelay", 0.7f);
                    Call(match, "HandleShipDestroyed", ship);
                    NetworkServer.Destroy(ship.gameObject);
                    Check(!local.Ready && !local.HasShip && local.Deaths == 1, "Death clears ready and updates score");
                    local.CmdDeploy();
                    Next(0.25);
                    break;
                case 5:
                    Check(!local.HasShip, "Respawn delay blocks early deployment");
                    Next(0.8);
                    break;
                case 6:
                    local.CmdDeploy();
                    Next();
                    break;
                case 7:
                    Check(match.State == MatchState.Running && local.HasShip, "Individual redeploy works during a running match");
                    Call(match, "EndMatch");
                    Check(!local.Ready && !s_Peer.Ready, "Match end clears readiness");
                    match.CmdReturnToLobby();
                    NetworkServer.DestroyPlayerForConnection(s_Connection);
                    NetworkServer.connections.Remove(s_Connection.connectionId);
                    local.CmdDeploy();
                    Next();
                    break;
                case 8:
                    Check(match.State == MatchState.Running && local.HasShip, "Solo all-ready starts after returning to lobby");
                    Debug.Log("LOBBY PLAY MODE VALIDATION PASSED: host/peer ready, unready, fitting lock, automatic start, respawn delay, redeploy and solo start.");
                    Finish(true);
                    break;
            }
        }

        private static void Next(double wait = 0.4)
        {
            s_Stage++;
            s_Next = EditorApplication.timeSinceStartup + wait;
        }

        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (s_Manager != null) s_Manager.StopHost();
            EditorApplication.ExitPlaymode();
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException("Lobby play mode validation failed: " + message);
            Debug.Log("Lobby play mode check: " + message);
        }

        private static T Get<T>(object instance, string field) =>
            (T)instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        private static void Set(object instance, string field, object value) =>
            instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
        private static void Call(object instance, string method, params object[] args) =>
            instance.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, args);

        private sealed class ValidationConnection : NetworkConnectionToClient
        {
            public ValidationConnection(int id) : base(id) { }
            protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable) { }
        }
    }
}
