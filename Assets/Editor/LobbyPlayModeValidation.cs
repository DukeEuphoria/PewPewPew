using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kcp2k;
using Mirror;
using PewPewPew.Match;
using PewPewPew.Core;
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
        private static int s_ValidatedHulls;
        private static int s_ThrusterDirection;
        private static Ship s_TestShip;
        private static Mesh s_TestShieldMesh;
        private static bool s_TestingShieldHold;
        private static Rigidbody2D s_ForceBody;
        private static Vector2 s_ExhaustDirection;

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
                s_ValidatedHulls = 0;
                s_ThrusterDirection = 0;
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
                    StartThrusterForceCheck();
                    Next();
                    break;
                case 1:
                    if (local == null) return;
                    if (s_ForceBody != null)
                    {
                        Vector2 velocity = s_ForceBody.linearVelocity;
                        Check(velocity.sqrMagnitude > 0f && Vector2.Dot(velocity.normalized, s_ExhaustDirection) < -0.999f,
                            "Thruster accelerates opposite a rotated point's Up direction");
                        Object.Destroy(s_ForceBody.gameObject);
                        s_ForceBody = null;
                    }
                    var preview = Get<LoadoutShipPreview>(s_Menu, "m_Preview");
                    if (Get<RawImage>(preview, "m_Image").texture == null) return;
                    Check(s_Manager.maxConnections == 16 && NetworkServer.maxConnections == 16, "Server capacity is capped at 16");
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
                    if (s_ValidatedHulls < ShipCatalog.Instance.Hulls.Length)
                    {
                        ValidatePreviewHitTargets(s_Menu, local.Loadout);
                        ValidateHullFitting(s_Menu, local);
                        s_Menu.CycleHull(1);
                        s_ValidatedHulls++;
                        return;
                    }
                    int testHull = Array.FindIndex(ShipCatalog.Instance.Hulls, hull => hull.name == "Slab Hull");
                    Check(testHull >= 0, "Slab Hull exists for the comprehensive fitting test");
                    if (local.Loadout.Hull != testHull)
                    {
                        s_Menu.CycleHull(1);
                        return;
                    }
                    FitTestLoadout(local);
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
                    s_TestShip = local.Ship;
                    ValidateMountedSystems(s_TestShip);
                    ValidatePowerStorageDisplay(s_TestShip);
                    s_TestShip.GetComponent<ShipInput>().enabled = false;
                    s_TestShip.CmdToggleShield(0);
                    s_TestShip.CmdSetControls(new ShipControls { AimPoint = s_TestShip.transform.position, HoldShields = true });
                    IList mountedShields = Get<IList>(s_TestShip, "m_Shields");
                    ShieldVisual heldVisual = GetPublic<ShieldVisual>(mountedShields[0], "Visual");
                    ComponentHealth heldCharge = GetPublic<ComponentHealth>(mountedShields[0], "Charge");
                    heldCharge.SetFraction(0f);
                    typeof(Ship).GetMethod("PublishHeldShieldLevels", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s_TestShip, null);
                    Check(Get<bool>(heldVisual, "m_Held") &&
                        heldVisual.GetComponent<Renderer>().material.color == Get<Color>(heldVisual, "m_EmptyColor"),
                        "A fully depleted shield remains visible while held");
                    heldVisual.Flash(0f);
                    Check(Get<bool>(heldVisual, "m_Held"), "An impact flash does not cancel the held state");
                    ShipSystem heldGenerator = GetPublic<ShipSystem>(mountedShields[0], "Generator");
                    heldGenerator.Health.Damage(heldGenerator.Health.Current);
                    typeof(Ship).GetMethod("PublishHeldShieldLevels", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s_TestShip, null);
                    Check(!Get<bool>(heldVisual, "m_Held") &&
                        Get<bool>(GetPublic<ShieldVisual>(mountedShields[1], "Visual"), "m_Held"),
                        "A shield destroyed during a hold stops being held visible");
                    heldGenerator.Health.Repair(heldGenerator.Health.Max);
                    heldCharge.SetFraction(0.5f);
                    s_TestingShieldHold = true;
                    Next(1.3);
                    break;
                case 5:
                    if (s_TestingShieldHold)
                    {
                        var heldShields = Get<IList>(s_TestShip, "m_Shields");
                        foreach (object mounted in heldShields)
                        {
                            ShieldVisual visual = GetPublic<ShieldVisual>(mounted, "Visual");
                            Check(Get<bool>(visual, "m_Held") && visual.GetComponent<Renderer>().enabled &&
                                Mathf.Approximately(Get<float>(visual, "m_Alpha"), 1f),
                                "Holding shields keeps each working shield visible beyond its flash duration");
                        }
                        ShieldVisual firstVisual = GetPublic<ShieldVisual>(heldShields[0], "Visual");
                        byte firstLevel = Get<byte[]>(s_TestShip, "m_HeldShieldLevels")[0];
                        Color expected = Color.Lerp(Get<Color>(firstVisual, "m_EmptyColor"),
                            Get<Color>(firstVisual, "m_FullColor"), (firstLevel - 1) / 254f);
                        Check(firstLevel > 1 && firstLevel < 255 &&
                            firstVisual.GetComponent<Renderer>().material.color == expected,
                            "Held shield colour tracks changing charge");
                        Check(!GetPublic<bool>(heldShields[0], "Toggled"), "Holding shields leaves individual toggles unchanged");
                        SetThrusterControl(s_TestShip, 0);
                        s_TestingShieldHold = false;
                        s_Next = EditorApplication.timeSinceStartup + 0.4;
                        return;
                    }
                    ValidateThrusterControl(s_TestShip, s_ThrusterDirection);
                    if (s_ThrusterDirection == 3)
                        foreach (object mounted in Get<IList>(s_TestShip, "m_Shields"))
                        {
                            ShieldVisual visual = GetPublic<ShieldVisual>(mounted, "Visual");
                            Check(!Get<bool>(visual, "m_Held") && !visual.GetComponent<Renderer>().enabled,
                                "Released shields fade out normally");
                        }
                    s_ThrusterDirection++;
                    if (s_ThrusterDirection < 4)
                    {
                        SetThrusterControl(s_TestShip, s_ThrusterDirection);
                        s_Next = EditorApplication.timeSinceStartup + 0.4;
                        return;
                    }
                    Check(s_TestShip.SubSystems[4].Activation.IsActive, "Fifth subsystem HUD button activates the system");
                    Ship ship = local.Ship;
                    object directionalShield = Get<IList>(ship, "m_Shields").Cast<object>()
                        .First(shield => GetPublic<ShieldDef>(shield, "Def").HalfAngle < 180f);
                    s_TestShieldMesh = GetPublic<ShieldVisual>(directionalShield, "Visual").GetComponent<MeshFilter>().sharedMesh;
                    Set(match, "m_RespawnDelay", 0.7f);
                    Call(match, "HandleShipDestroyed", ship);
                    NetworkServer.Destroy(ship.gameObject);
                    Check(!local.Ready && !local.HasShip && local.Deaths == 1, "Death clears ready and updates score");
                    local.CmdDeploy();
                    Next(0.25);
                    break;
                case 6:
                    Check(s_TestShieldMesh == null, "Destroying the ship releases its generated shield mesh");
                    Check(!local.HasShip, "Respawn delay blocks early deployment");
                    Next(0.8);
                    break;
                case 7:
                    local.CmdDeploy();
                    Next();
                    break;
                case 8:
                    Check(match.State == MatchState.Running && local.HasShip, "Individual redeploy works during a running match");
                    Call(match, "EndMatch");
                    Check(!local.Ready && !s_Peer.Ready, "Match end clears readiness");
                    match.CmdReturnToLobby();
                    NetworkServer.DestroyPlayerForConnection(s_Connection);
                    NetworkServer.connections.Remove(s_Connection.connectionId);
                    local.CmdDeploy();
                    Next();
                    break;
                case 9:
                    Check(match.State == MatchState.Running && local.HasShip, "Solo all-ready starts after returning to lobby");
                    Debug.Log("LOBBY PLAY MODE VALIDATION PASSED: host/peer ready, unready, fitting lock, automatic start, respawn delay, redeploy and solo start.");
                    Finish(true);
                    break;
            }
        }

        private static void StartThrusterForceCheck()
        {
            ThrusterDef def = ShipCatalog.Instance.Components.OfType<ThrusterDef>().First(item => item.Force > 0f);
            var root = new GameObject("Thruster force validation");
            root.transform.position = new Vector3(10000f, 10000f, 0f);
            s_ForceBody = root.AddComponent<Rigidbody2D>();
            s_ForceBody.gravityScale = 0f;
            var point = new GameObject("Rotated hull point").transform;
            point.SetParent(root.transform, false);
            point.localRotation = Quaternion.Euler(0f, 0f, 37f);
            s_ExhaustDirection = point.up;
            var thruster = new Thruster(def, new ShipSystem(def, new[] { point }, root.transform));
            thruster.Step(1f, 1f, new PowerBank(100000f));
            thruster.Apply(s_ForceBody);
        }

        private static void SetThrusterControl(Ship ship, int direction)
        {
            ship.CmdSetControls(new ShipControls
            {
                AimPoint = (Vector2)ship.transform.position + (Vector2)ship.transform.up * 100f,
                Thrust = direction == 0 ? 1f : 0f,
                ReverseThrust = direction == 1 ? 1f : 0f,
                SideThrust = direction == 2 ? -1f : direction == 3 ? 1f : 0f
            });
        }

        private static void ValidateThrusterControl(Ship ship, int direction)
        {
            string[] names = { "Forward", "Reverse", "Left", "Right" };
            var thrusters = Get<IList>(ship, "m_Thrusters");
            int active = 0;
            foreach (object mounted in thrusters)
            {
                string group = GetPublic<object>(mounted, "Direction").ToString();
                var thruster = GetPublic<Thruster>(mounted, "Thruster");
                bool expected = group == names[direction];
                Check(expected ? thruster.Throttle > 0f : Mathf.Approximately(thruster.Throttle, 0f),
                    names[direction] + " control fires only its direction group");
                if (expected) active++;
            }
            Check(active > 0, names[direction] + " control has a fitted thruster");
        }

            private static void FitTestLoadout(PlayerState player)
            {
                ShipCatalog catalog = ShipCatalog.Instance;
                ShipComponentDef[] components = catalog.Components;
                ShipLoadout loadout = player.Loadout;
                HullPoint[] points = catalog.Hulls[loadout.Hull].HullPoints.Points;
                int shield = Array.FindIndex(components, component => component is ShieldDef);
                int directionalShield = Array.FindIndex(components, component => component is ShieldDef def && def.HalfAngle == 45f);
                int thruster = Array.FindIndex(components, component => component is ThrusterDef);
                int subsystem = Array.FindIndex(components, component => component is SubSystemDef def && def.Mode == ActivationMode.Toggle);
                int storage = Array.FindIndex(components, component => component is SubSystemDef def && def.ExtraPowerStorage > 0f);
                Check(shield >= 0 && directionalShield >= 0 && thruster >= 0 && subsystem >= 0 && storage >= 0, "Test components exist");
                string[] directionPoints = { "ReverseThrusterPoint", "SideThrusterPositivePoint", "SideThrusterNegativePoint" };
                int firstSubsystem = Array.FindIndex(points, point => point.AcceptsComponent(components[subsystem]) &&
                    (point.Accepts & AcceptedComponents.Shield) == 0 &&
                    !directionPoints.Contains(point.name) &&
                    loadout.MountComponents[Array.IndexOf(points, point)] < 0);
                Check(firstSubsystem >= 0, "An empty subsystem point is available");
                var preview = Get<LoadoutShipPreview>(s_Menu, "m_Preview");
                var markers = Get<List<LoadoutPreviewMarker>>(preview, "m_RuntimeMarkers");
                Button pointButton = markers[firstSubsystem].GetComponent<Button>();
                pointButton.onClick.Invoke();
                var categories = Get<LoadoutCategoryView[]>(s_Menu, "m_Categories");
                categories[4].Tab.onClick.Invoke();
                var subItems = components.Where(component => component.Type == ComponentType.SubSystem).ToArray();
                int subButtonIndex = Array.IndexOf(subItems, components[subsystem]);
                Button fitButton = Get<Button>(categories[4].Buttons[subButtonIndex], "m_Button");
                Check(fitButton.interactable, "Compatible component button is enabled");
                fitButton.onClick.Invoke();
                Check(player.Loadout.MountComponents[firstSubsystem] == subsystem, "Point selection and component button fit the selected mount");
                loadout = player.Loadout;
                foreach (string name in directionPoints)
                {
                    int index = Array.FindIndex(points, point => point.name == name);
                    Check(index >= 0 && points[index].AcceptsComponent(components[thruster]), name + " accepts a test thruster");
                    if (loadout.MountComponents[index] < 0) loadout.SetMountComponent(index, thruster);
                }
                int extraShield = Array.FindIndex(points, point => point.AcceptsComponent(components[shield]) &&
                    loadout.MountComponents[Array.IndexOf(points, point)] < 0);
                Check(extraShield >= 0, "An empty shield point is available");
                loadout.SetMountComponent(extraShield, directionalShield);
                int extraThruster = Array.FindIndex(points, point =>
                    Vector3.Dot(point.transform.up, catalog.Hulls[loadout.Hull].HullPoints.transform.up) < -0.99f &&
                    point.AcceptsComponent(components[thruster]) && loadout.MountComponents[Array.IndexOf(points, point)] < 0);
                Check(extraThruster >= 0, "A second forward thruster point is available");
                loadout.SetMountComponent(extraThruster, thruster);
                int fitted = 1;
                for (int i = 0; i < points.Length && fitted < 5; i++)
                {
                    if (loadout.MountComponents[i] >= 0 || !points[i].AcceptsComponent(components[subsystem])) continue;
                    loadout.SetMountComponent(i, subsystem);
                    fitted++;
                }
                Check(fitted == 5 && catalog.IsValid(loadout), "Five subsystems and multiple thrusters and shields fit");
                int stores = 0;
                for (int i = 0; i < points.Length && stores < 2; i++)
                {
                    if (loadout.MountComponents[i] != subsystem || !points[i].AcceptsComponent(components[storage])) continue;
                    loadout.SetMountComponent(i, storage);
                    stores++;
                }
                Check(stores == 2 && catalog.IsValid(loadout), "Two power storage subsystems fit");
                player.CmdSetLoadout(loadout);
                Check(player.Loadout.MountComponents.SequenceEqual(loadout.MountComponents), "Server accepted mount assignments");
            }

            private static void ValidatePowerStorageDisplay(Ship ship)
            {
                var stores = ship.SubSystems.Where(system => system.Def.ExtraPowerStorage > 0f).ToArray();
                Check(stores.Length == 2 && stores.All(system => system.Gauge != null), "Two power storage gauges exist");
                PowerBank bank = Get<PowerBank>(ship, "m_Power");
                HullDef hull = Get<HullDef>(ship, "m_HullDef");
                var elements = Get<List<IHudElement>>(ship, "m_HudElements");
                HudGauge hullGauge = Get<HudGauge>(elements[ship.GetComponent<ShipHudSync>().Vitals.Shields.Length + 1], "m_Gauge");
                float firstCapacity = stores[0].Def.ExtraPowerStorage;
                float secondCapacity = stores[1].Def.ExtraPowerStorage;
                foreach (var example in new[]
                {
                    (bank.Max, 1f, 1f, 1f),
                    (hull.MaxPowerStorage + secondCapacity + firstCapacity * 0.5f, 1f, 0.5f, 1f),
                    (hull.MaxPowerStorage + secondCapacity * 0.5f, 1f, 0f, 0.5f),
                    (hull.MaxPowerStorage * 0.5f, 0.5f, 0f, 0f),
                    (hull.MaxPowerStorage + secondCapacity * 0.5f, 1f, 0f, 0.5f)
                })
                {
                    bank.SetFraction(example.Item1 / bank.Max);
                    Call(ship, "PublishVitals");
                    foreach (IHudElement element in elements) element.Render();
                    ShipVitals vitals = ship.GetComponent<ShipHudSync>().Vitals;
                    Check(Mathf.Abs(Get<Image>(hullGauge, "m_Fill").fillAmount - example.Item2) < 0.001f &&
                        Mathf.Abs(Get<Image>(stores[0].Gauge, "m_Fill").fillAmount - example.Item3) < 0.001f &&
                        Mathf.Abs(Get<Image>(stores[1].Gauge, "m_Fill").fillAmount - example.Item4) < 0.001f,
                        "Power gauges show the ordered storage segments");
                    int firstIndex = ship.SubSystems.ToList().IndexOf(stores[0]);
                    int secondIndex = ship.SubSystems.ToList().IndexOf(stores[1]);
                    Check(vitals.HullPower == VitalsMath.ToByte(example.Item2) &&
                        vitals.SubSystemLevels[firstIndex] == VitalsMath.ToByte(example.Item3) &&
                        vitals.SubSystemLevels[secondIndex] == VitalsMath.ToByte(example.Item4),
                        "Ordered power gauge levels are published to the owner");
                }
                bank.SetFraction(1f);
                Call(ship, "PublishVitals");
            }

            private static void ValidateHullFitting(LoadoutMenuController menu, PlayerState player)
            {
                ShipCatalog catalog = ShipCatalog.Instance;
                ShipLoadout loadout = player.Loadout;
                int point = Array.FindIndex(loadout.MountComponents,
                    index => index >= 0 && catalog.Components[index] is ShieldDef);
                if (point < 0)
                {
                    ShieldDef shield = catalog.Components.OfType<ShieldDef>().First();
                    HullPoint[] points = catalog.Hulls[loadout.Hull].HullPoints.Points;
                    point = Array.FindIndex(points, candidate => candidate.AcceptsComponent(shield) &&
                        loadout.MountComponents[Array.IndexOf(points, candidate)] < 0);
                }
                Check(point >= 0, "Hull has a shield-compatible point to test");
                int component = loadout.MountComponents[point];
                var preview = Get<LoadoutShipPreview>(menu, "m_Preview");
                var markers = Get<List<LoadoutPreviewMarker>>(preview, "m_RuntimeMarkers");
                Button marker = markers[point].GetComponent<Button>();
                ExecuteEvents.Execute(marker.gameObject, CheckHudHitTarget(marker, "Hull point"), ExecuteEvents.pointerClickHandler);
                if (component >= 0)
                {
                    Get<Button[]>(menu, "m_Slots")[0].onClick.Invoke();
                    Check(player.Loadout.MountComponents[point] == -1, "Hull " + loadout.Hull + " point can be cleared");
                }
                var categories = Get<LoadoutCategoryView[]>(menu, "m_Categories");
                categories[0].Tab.onClick.Invoke();
                var shields = catalog.Components.Where(item => item.Type == ComponentType.Shield).ToArray();
                foreach (float angle in new[] { 90f, 45f, 22.5f })
                {
                    int variantIndex = Array.FindIndex(shields, item => item is ShieldDef def && def.HalfAngle == angle);
                    Check(variantIndex >= 0, "Directional shield " + angle + " exists in the catalog");
                    Button variant = Get<Button>(categories[0].Buttons[variantIndex], "m_Button");
                    Check(variant.interactable, "Directional shield " + angle + " is compatible with the selected point");
                    variant.onClick.Invoke();
                    Check(player.Loadout.MountComponents[point] == Array.IndexOf(catalog.Components, shields[variantIndex]),
                        "Directional shield " + angle + " can be selected in the loadout");
                }
                if (component >= 0)
                {
                    int indexInCategory = Array.IndexOf(shields, catalog.Components[component]);
                    Button fit = Get<Button>(categories[0].Buttons[indexInCategory], "m_Button");
                    Check(fit.interactable, "Original shield is compatible with its point");
                    fit.onClick.Invoke();
                }
                else Get<Button[]>(menu, "m_Slots")[0].onClick.Invoke();
                Check(player.Loadout.MountComponents[point] == component, "Hull " + loadout.Hull + " point is restored");
            }

            private static void ValidatePreviewHitTargets(LoadoutMenuController menu, ShipLoadout loadout)
            {
                Canvas.ForceUpdateCanvases();
                var preview = Get<LoadoutShipPreview>(menu, "m_Preview");
                var markers = Get<List<LoadoutPreviewMarker>>(preview, "m_RuntimeMarkers");
                CheckHudHitTarget(Get<Button>(menu, "m_PreviousHull"), "Previous hull");
                CheckHudHitTarget(Get<Button>(menu, "m_NextHull"), "Next hull");
                int count = ShipCatalog.Instance.Hulls[loadout.Hull].HullPoints.Points.Length;
                for (int i = 0; i < count; i++)
                {
                    var target = markers[i].GetComponent<Button>();
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position)
                    };
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    Check(hits.Count > 0 && hits[0].gameObject == target.gameObject,
                        "Hull " + loadout.Hull + " point marker " + i + " is reachable by UI raycast at " +
                        pointer.position + " (first hit: " + (hits.Count > 0 ? hits[0].gameObject.name : "none") +
                        ", active: " + target.gameObject.activeInHierarchy + ", culled: " + target.GetComponent<CanvasRenderer>().cull +
                        ", graphic enabled: " + target.GetComponent<Image>().enabled +
                        ", raycast: " + target.GetComponent<Image>().raycastTarget +
                        ", depth: " + target.GetComponent<Image>().depth +
                        ", contains: " + RectTransformUtility.RectangleContainsScreenPoint((RectTransform)target.transform, pointer.position) +
                        ", parent contains: " + RectTransformUtility.RectangleContainsScreenPoint((RectTransform)target.transform.parent, pointer.position) +
                        ", bounds: " + ((RectTransform)target.transform).rect + ", parent: " + target.transform.parent.name + ")");
                }
            }

            private static void ValidateMountedSystems(Ship ship)
            {
                var shields = Get<IList>(ship, "m_Shields");
                var thrusters = Get<IList>(ship, "m_Thrusters");
                Check(shields.Count == 2 && thrusters.Count >= 5 && ship.SubSystems.Count == 5,
                    "Ship constructs every mounted system (shields: " + shields.Count + ", thrusters: " + thrusters.Count +
                    ", subsystems: " + ship.SubSystems.Count + ")");
                Check(ship.SubSystems[4].Gauge != null, "Fifth subsystem has a HUD gauge");
                Button subsystemButton = ship.SubSystems[4].Gauge.GetComponentInChildren<Button>();
                Check(subsystemButton != null, "Fifth subsystem has a clickable HUD button");
                ExecuteEvents.Execute(subsystemButton.gameObject, CheckHudHitTarget(subsystemButton, "Fifth subsystem"),
                    ExecuteEvents.pointerClickHandler);
                ComponentHealth first = GetPublic<ComponentHealth>(shields[0], "Charge");
                ComponentHealth second = GetPublic<ComponentHealth>(shields[1], "Charge");
                Vector2 centre = ship.transform.TransformPoint(Get<Vector2>(ship, "m_ShieldCenter"));
                Check(first.Current == first.Max && second.Current == second.Max, "Shields start charged");
                var shieldGauges = Get<List<HudGauge>>(ship, "m_ShieldGauges");
                Button shieldButton = shieldGauges[0].GetComponentInChildren<Button>();
                Check(shieldButton != null, "Shield has a clickable HUD button");
                ExecuteEvents.Execute(shieldButton.gameObject, CheckHudHitTarget(shieldButton, "Shield"),
                    ExecuteEvents.pointerClickHandler);
                ship.TakeDamage(25f, centre, null);
                Check(Mathf.Approximately(first.Current, first.Max) && Mathf.Approximately(second.Current, second.Max - 25f),
                    "Inactive shield retains charge while active shield absorbs");
                ship.CmdSetControls(new ShipControls { AimPoint = ship.transform.position, HoldShields = true });
                ship.TakeDamage(25f, centre, null);
                Check(Mathf.Approximately(first.Current, first.Max - 25f) && Mathf.Approximately(second.Current, second.Max - 25f),
                    "Held shield control temporarily activates the first shield");
                ship.CmdSetControls(new ShipControls { AimPoint = ship.transform.position });
                Check(!GetPublic<bool>(shields[0], "Toggled") && GetPublic<bool>(shields[1], "Toggled"),
                    "Releasing shield control preserves individual toggles");
                ship.CmdToggleShields();
                Check(GetPublic<bool>(shields[0], "Toggled") && GetPublic<bool>(shields[1], "Toggled"), "Mixed shields toggle all on");
                float firstBefore = first.Current;
                float secondBefore = second.Current;
                ship.TakeDamage(70f, centre, null);
                Check(Mathf.Approximately(first.Current, firstBefore - 50f) && Mathf.Approximately(second.Current, secondBefore - 20f),
                    "Active shields absorb in mount order subject to impact limits");
                ship.CmdToggleShields();
                Check(!GetPublic<bool>(shields[0], "Toggled") && !GetPublic<bool>(shields[1], "Toggled"), "All-on shields toggle off");
                ship.CmdToggleShields();
                Check(GetPublic<bool>(shields[0], "Toggled") && GetPublic<bool>(shields[1], "Toggled"), "All-off shields toggle on");
                ValidateDirectionalShields(ship, shields, centre);
            }

            private static void ValidateDirectionalShields(Ship ship, IList shields, Vector2 centre)
            {
                var definitions = ShipCatalog.Instance.Components.OfType<ShieldDef>().ToArray();
                ShieldDef originalFirst = GetPublic<ShieldDef>(shields[0], "Def");
                ShieldDef originalSecond = GetPublic<ShieldDef>(shields[1], "Def");
                ShipSystem firstGenerator = GetPublic<ShipSystem>(shields[0], "Generator");
                ShipSystem secondGenerator = GetPublic<ShipSystem>(shields[1], "Generator");
                Quaternion rotation = ship.transform.rotation;
                Quaternion firstRotation = firstGenerator.Points[0].localRotation;
                Quaternion secondRotation = secondGenerator.Points[0].localRotation;
                ComponentHealth first = GetPublic<ComponentHealth>(shields[0], "Charge");
                ComponentHealth second = GetPublic<ComponentHealth>(shields[1], "Charge");
                try
                {
                    shields[0].GetType().GetField("Def").SetValue(shields[0], definitions.Single(def => def.HalfAngle == 90f));
                    shields[1].GetType().GetField("Def").SetValue(shields[1], definitions.Single(def => def.HalfAngle == 45f));
                    ship.transform.rotation = Quaternion.Euler(0f, 0f, 73f);
                    firstGenerator.Points[0].rotation = ship.transform.rotation;
                    secondGenerator.Points[0].rotation = ship.transform.rotation * Quaternion.Euler(0f, 0f, 180f);
                    centre = ship.transform.TransformPoint(Get<Vector2>(ship, "m_ShieldCenter"));
                    Vector2 forward = ship.transform.up;
                    first.SetFraction(1f);
                    second.SetFraction(1f);
                    ship.TakeDamage(20f, centre + forward * 20f, null);
                    Check(Mathf.Approximately(first.Current, first.Max - 20f) && Mathf.Approximately(second.Current, second.Max),
                        "A forward hit uses only the forward-facing shield on a rotated ship");
                    ship.TakeDamage(20f, centre - forward * 20f, null);
                    Check(Mathf.Approximately(first.Current, first.Max - 20f) && Mathf.Approximately(second.Current, second.Max - 20f),
                        "A rear hit uses only the rear-facing shield");
                    Vector2 gap = Quaternion.Euler(0f, 0f, 100f) * forward;
                    ComponentHealth armour = Get<ComponentHealth>(ship, "m_Armour");
                    float armourBefore = armour.Current;
                    ship.TakeDamage(10f, centre + gap * 20f, null);
                    Check(Mathf.Approximately(first.Current, first.Max - 20f) && Mathf.Approximately(second.Current, second.Max - 20f) &&
                        armour.Current < armourBefore, "An uncovered impact bypasses both shields and reaches armour");
                    secondGenerator.Points[0].rotation = ship.transform.rotation;
                    first.SetFraction(1f);
                    second.SetFraction(1f);
                    ship.TakeDamage(70f, centre + forward * 20f, null);
                    Check(Mathf.Approximately(first.Current, first.Max - 50f) && Mathf.Approximately(second.Current, second.Max - 20f),
                        "Overlapping directional shields absorb in mount order");
                    firstGenerator.Health.Damage(firstGenerator.Health.Current);
                    first.SetFraction(1f);
                    second.SetFraction(1f);
                    ship.CmdSetControls(new ShipControls { AimPoint = ship.transform.position, HoldShields = true });
                    Check(!Get<bool>(GetPublic<ShieldVisual>(shields[0], "Visual"), "m_Held") &&
                        Get<bool>(GetPublic<ShieldVisual>(shields[1], "Visual"), "m_Held"),
                        "A destroyed generator does not show a held shield");
                    ship.TakeDamage(20f, centre + forward * 20f, null);
                    Check(Mathf.Approximately(first.Current, first.Max) && Mathf.Approximately(second.Current, second.Max - 20f),
                        "A destroyed shield generator is skipped");
                }
                finally
                {
                    shields[0].GetType().GetField("Def").SetValue(shields[0], originalFirst);
                    shields[1].GetType().GetField("Def").SetValue(shields[1], originalSecond);
                    firstGenerator.Health.Repair(firstGenerator.Health.Max);
                    ship.CmdSetControls(new ShipControls { AimPoint = ship.transform.position });
                    ship.transform.rotation = rotation;
                    firstGenerator.Points[0].localRotation = firstRotation;
                    secondGenerator.Points[0].localRotation = secondRotation;
                }
            }

            private static PointerEventData CheckHudHitTarget(Button button, string name)
            {
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Check(hits.Count > 0 && hits[0].gameObject == button.gameObject,
                    name + " HUD button is reachable by UI raycast (first hit: " +
                    (hits.Count > 0 ? hits[0].gameObject.name : "none") + ")");
                return pointer;
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
        private static T GetPublic<T>(object instance, string field) =>
            (T)instance.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
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
