#if PEW_MOUNT_REMOTE_VALIDATION
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using kcp2k;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Ships;
using PewPewPew.Weapons;
using UnityEngine;

namespace PewPewPew.Networking
{
    public class MountRemoteValidation : MonoBehaviour
    {
        private GameNetworkManager m_Manager;
        private bool m_Host;
        private int m_Phase;
        private float m_Deadline;
        private float m_ReadyAt;
        private ShipLoadout m_Fitted;
        private bool m_RemoteHoldSeen;
        private bool m_RemoteReleaseSeen;
        private bool m_HostReleased;
        private bool m_RemoteDisableSeen;
        private int m_DisabledThrusterMount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartValidation()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-mountHost") && !args.Contains("-mountClient")) return;
            new GameObject("Mount remote validation").AddComponent<MountRemoteValidation>();
        }

        private void Start()
        {
            m_Host = Environment.GetCommandLineArgs().Contains("-mountHost");
            string[] args = Environment.GetCommandLineArgs();
            int portIndex = Array.IndexOf(args, "-mountPort");
            if (portIndex < 0 || portIndex + 1 >= args.Length || !ushort.TryParse(args[portIndex + 1], out ushort port))
            {
                Fail("Missing validation port.");
                return;
            }
            foreach (MenuScreenFlow flow in FindObjectsByType<MenuScreenFlow>()) flow.enabled = false;
            var managerObject = new GameObject("Remote validation network manager");
            KcpTransport transport = managerObject.AddComponent<KcpTransport>();
            transport.port = port;
            m_Manager = managerObject.AddComponent<GameNetworkManager>();
            m_Manager.transport = transport;
            m_Manager.playerPrefab = Resources.Load<GameObject>("SpawnablePrefabs/Player/PlayerPrefab");
            m_Manager.onlineScene = m_Manager.offlineScene = string.Empty;
            m_Manager.networkAddress = "127.0.0.1";
            m_Manager.spawnPrefabs.AddRange(Resources.LoadAll<GameObject>("SpawnablePrefabs")
                .Where(prefab => prefab != m_Manager.playerPrefab));
            m_Deadline = Time.realtimeSinceStartup + 90f;
            if (m_Host)
            {
                m_Manager.StartHost();
                Debug.Log("MOUNT_REMOTE_HOST_LISTENING");
            }
            else m_Manager.StartClient();
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup > m_Deadline)
            {
                Fail($"Timed out in phase {m_Phase} (remote hold {m_RemoteHoldSeen}, release {m_RemoteReleaseSeen}, host release {m_HostReleased}).");
                return;
            }
            if (!NetworkClient.active || PlayerState.Local == null || MatchManager.Instance == null) return;
            if (m_Host) StepHost();
            else StepClient();
        }

        private void StepHost()
        {
            if (m_Phase == 0)
            {
                PlayerState remote = PlayerState.All.FirstOrDefault(player => player != PlayerState.Local);
                if (remote == null || !HasExpectedComponents(remote.Loadout)) return;
                PlayerState.Local.CmdDeploy();
                m_Phase = 1;
            }
            else if (m_Phase == 1 && MatchManager.Instance.State == MatchState.Running)
            {
                PlayerState remote = PlayerState.All.FirstOrDefault(player => player != PlayerState.Local);
                if (remote == null || !remote.HasShip || remote.Ship == null) return;
                if (remote.Ship.SubSystems.Count != 5) { Fail("Server did not construct all remote subsystems."); return; }
                Ship localShip = PlayerState.Local.Ship;
                if (localShip == null) return;
                localShip.GetComponent<Collider2D>().enabled = false;
                remote.Ship.GetComponent<Collider2D>().enabled = false;
                remote.Ship.netIdentity.visibility = Visibility.ForceShown;
                NetworkServer.RebuildObservers(remote.Ship.netIdentity, true);
                localShip.GetComponent<ShipInput>().enabled = false;
                localShip.netIdentity.visibility = Visibility.ForceHidden;
                NetworkServer.RebuildObservers(localShip.netIdentity, false);
                if (localShip.netIdentity.observers.ContainsKey(remote.connectionToClient.connectionId))
                {
                    Fail("Remote client remained an observer before the shield hold.");
                    return;
                }
                localShip.CmdSetControls(new ShipControls { AimPoint = localShip.transform.position, HoldShields = true });
                localShip.netIdentity.visibility = Visibility.ForceShown;
                NetworkServer.RebuildObservers(localShip.netIdentity, true);
                Debug.Log("MOUNT_REMOTE_HOST_PASSED");
                m_Phase = 2;
                m_Deadline = Time.realtimeSinceStartup + 25f;
            }
            else if (m_Phase == 2 && PlayerState.All.Count == 1)
            {
                if (!m_RemoteHoldSeen || !m_RemoteReleaseSeen || !m_RemoteDisableSeen)
                {
                    Fail("Host did not observe remote shield hold, release, and component disable.");
                    return;
                }
                m_Manager.StopHost();
                Application.Quit(0);
            }
            else if (m_Phase == 2)
            {
                if (!m_HostReleased && m_RemoteReleaseSeen)
                {
                    Ship localShip = PlayerState.Local.Ship;
                    localShip.CmdSetControls(new ShipControls { AimPoint = localShip.transform.position });
                    m_HostReleased = true;
                }
                PlayerState remote = PlayerState.All.FirstOrDefault(player => player != PlayerState.Local);
                if (remote != null && remote.Ship != null)
                {
                    Ship ship = remote.Ship;
                    ShipSystem[] mounts = (ShipSystem[])typeof(Ship).GetField("m_MountSystems",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ship);
                    int disabledMount = FindThrusterMount(remote.Loadout);
                    if (disabledMount >= 0 && !mounts[disabledMount].Enabled) m_RemoteDisableSeen = true;
                    if (HeldShieldCount(ship) > 0) m_RemoteHoldSeen = true;
                    else if (m_RemoteHoldSeen) m_RemoteReleaseSeen = true;
                    var bank = (PowerBank)typeof(Ship).GetField("m_Power", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ship);
                    HullDef hull = ShipCatalog.Instance.Hulls[remote.Loadout.Hull];
                    SubSystem[] stores = ship.SubSystems.Where(system => system.Def.ExtraPowerStorage > 0f).ToArray();
                    if (stores.Length != 2) { Fail("Remote ship does not have two power stores."); return; }
                    float stored = hull.MaxPowerStorage + stores[1].Def.ExtraPowerStorage + stores[0].Def.ExtraPowerStorage * 0.5f;
                    bank.SetFraction(stored / bank.Max);
                }
            }
        }

        private void StepClient()
        {
            PlayerState player = PlayerState.Local;
            if (m_Phase == 0)
            {
                if (m_ReadyAt == 0f) { m_ReadyAt = Time.realtimeSinceStartup + 1f; return; }
                if (Time.realtimeSinceStartup < m_ReadyAt) return;
                m_Fitted = MakeLoadout(player.Loadout);
                player.CmdSetLoadout(m_Fitted);
                m_Phase = 1;
            }
            else if (m_Phase == 1)
            {
                if (player.Loadout.MountComponents == null ||
                    !player.Loadout.MountComponents.SequenceEqual(m_Fitted.MountComponents)) return;
                player.CmdDeploy();
                m_Phase = 2;
            }
            else if (m_Phase == 2)
            {
                Ship ship = FindObjectsByType<Ship>().FirstOrDefault(candidate => candidate.isOwned);
                if (ship == null || ship.SubSystems.Count != 5 || MatchManager.Instance.State != MatchState.Running) return;
                ShipInput input = ship.GetComponent<ShipInput>();
                if (input != null) input.enabled = false;
                m_DisabledThrusterMount = FindThrusterMount(m_Fitted);
                if (m_DisabledThrusterMount < 0) { Fail("Fitted loadout has no thruster."); return; }
                ship.CmdToggleComponent(m_DisabledThrusterMount);
                ship.CmdToggleShield(0);
                ship.CmdSetControls(new ShipControls
                {
                    AimPoint = (Vector2)ship.transform.position + Vector2.up * 100f,
                    Thrust = 1f,
                    FireMain = true,
                    HoldShields = true
                });
                m_ReadyAt = Time.realtimeSinceStartup + 1.4f;
                m_Phase = 3;
            }
            else if (m_Phase == 3)
            {
                Ship ship = FindObjectsByType<Ship>().FirstOrDefault(candidate => candidate.isOwned);
                if (ship == null) { Fail("Owned ship disappeared."); return; }
                ShipVitals vitals = ship.GetComponent<ShipHudSync>().Vitals;
                SubSystem[] stores = ship.SubSystems.Where(system => system.Def.ExtraPowerStorage > 0f).ToArray();
                if (stores.Length != 2) { Fail("Owned ship does not have two power stores."); return; }
                float hullPower = (float)typeof(Ship).GetField("m_HullPowerLevel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ship);
                var field = typeof(Ship).GetField("m_Throttles", BindingFlags.NonPublic | BindingFlags.Instance);
                var throttles = (byte[])field.GetValue(ship);
                Ship other = FindObjectsByType<Ship>().FirstOrDefault(candidate => !candidate.isOwned);
                if (Time.realtimeSinceStartup < m_ReadyAt || other == null || HeldShieldCount(other) == 0 ||
                    HeldShieldCount(ship) != 2 ||
                    vitals.ShieldStates == null || vitals.ShieldStates.Length != 2 || vitals.ShieldStates[0] != 2 ||
                    vitals.ComponentStates == null || vitals.ComponentStates.Length <= m_DisabledThrusterMount ||
                    (vitals.ComponentStates[m_DisabledThrusterMount] & 1) != 0 ||
                    vitals.SubSystemLevels == null || vitals.SubSystemLevels.Length != 5 ||
                    vitals.HullPower != 255 || hullPower < 0.99f ||
                    stores[0].HudLevel < 0.48f || stores[0].HudLevel > 0.52f || stores[1].HudLevel < 0.99f ||
                    throttles == null || !throttles.Any(value => value > 0) ||
                    FindObjectsByType<Bullet>().Length == 0) return;
                ship.CmdSetControls(new ShipControls
                {
                    AimPoint = (Vector2)ship.transform.position + Vector2.up * 100f,
                    Thrust = 1f,
                    FireMain = true
                });
                m_ReadyAt = Time.realtimeSinceStartup + 1.4f;
                m_Phase = 4;
            }
            else if (m_Phase == 4)
            {
                Ship ship = FindObjectsByType<Ship>().FirstOrDefault(candidate => candidate.isOwned);
                if (ship == null) { Fail("Owned ship disappeared."); return; }
                Ship other = FindObjectsByType<Ship>().FirstOrDefault(candidate => !candidate.isOwned);
                if (Time.realtimeSinceStartup < m_ReadyAt || HeldShieldCount(ship) != 0 ||
                    VisibleShieldCount(ship) != 0 || other == null || HeldShieldCount(other) != 0 ||
                    VisibleShieldCount(other) != 0 || ship.GetComponent<ShipHudSync>().Vitals.ShieldStates[0] != 0) return;
                Debug.Log("MOUNT_REMOTE_CLIENT_PASSED");
                m_Manager.StopClient();
                Application.Quit(0);
            }
        }

        private static int FindThrusterMount(ShipLoadout loadout)
        {
            ShipComponentDef[] components = ShipCatalog.Instance.Components;
            return Array.FindIndex(loadout.MountComponents,
                index => index >= 0 && components[index] is ThrusterDef);
        }

        private static int HeldShieldCount(Ship ship) => ShieldVisuals(ship).Count(visual =>
            (bool)typeof(ShieldVisual).GetField("m_Held", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(visual) &&
            visual.GetComponent<Renderer>().enabled);

        private static int VisibleShieldCount(Ship ship) => ShieldVisuals(ship).Count(visual => visual.GetComponent<Renderer>().enabled);

        private static ShieldVisual[] ShieldVisuals(Ship ship)
        {
            var shields = (IList)typeof(Ship).GetField("m_Shields", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ship);
            return shields.Cast<object>().Select(shield =>
                (ShieldVisual)shield.GetType().GetField("Visual").GetValue(shield)).ToArray();
        }

        private static ShipLoadout MakeLoadout(ShipLoadout loadout)
        {
            ShipCatalog catalog = ShipCatalog.Instance;
            int hullIndex = Array.FindIndex(catalog.Hulls, hull => hull.name == "Slab Hull");
            if (hullIndex < 0) throw new InvalidOperationException("Slab Hull is required for the remote fitting test.");
            loadout = catalog.DefaultLoadout(hullIndex);
            ShipComponentDef[] components = catalog.Components;
            HullPoint[] points = catalog.Hulls[loadout.Hull].HullPoints.Points;
            int shield = Array.FindIndex(components, component => component is ShieldDef);
            int thruster = Array.FindIndex(components, component => component is ThrusterDef);
            int subsystem = Array.FindIndex(components, component => component is SubSystemDef def && def.Mode == ActivationMode.Toggle);
            int storage = Array.FindIndex(components, component => component is SubSystemDef def && def.ExtraPowerStorage > 0f);
            if (shield < 0 || thruster < 0 || subsystem < 0 || storage < 0)
                throw new InvalidOperationException("Remote test components are missing.");
            int shields = 0, thrusters = 0, subsystems = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (loadout.MountComponents[i] >= 0) continue;
                if (shields == 0 && points[i].AcceptsComponent(components[shield]))
                {
                    loadout.SetMountComponent(i, shield);
                    shields++;
                }
                else if (thrusters == 0 &&
                    Vector3.Dot(points[i].transform.up, catalog.Hulls[loadout.Hull].HullPoints.transform.up) < -0.99f &&
                    points[i].AcceptsComponent(components[thruster]))
                {
                    loadout.SetMountComponent(i, thruster);
                    thrusters++;
                }
            }
            for (int i = 0; i < points.Length && subsystems < 5; i++)
                if (loadout.MountComponents[i] < 0 && points[i].AcceptsComponent(components[subsystem]))
                {
                    loadout.SetMountComponent(i, subsystem);
                    subsystems++;
                }
            if (shields != 1 || thrusters != 1 || subsystems != 5 || !catalog.IsValid(loadout))
                throw new InvalidOperationException("Could not construct the remote test loadout.");
            int stores = 0;
            for (int i = 0; i < points.Length && stores < 2; i++)
                if (loadout.MountComponents[i] == subsystem && points[i].AcceptsComponent(components[storage]))
                {
                    loadout.SetMountComponent(i, storage);
                    stores++;
                }
            if (stores != 2 || !catalog.IsValid(loadout))
                throw new InvalidOperationException("Could not fit remote power storage subsystems.");
            return loadout;
        }

        private static bool HasExpectedComponents(ShipLoadout loadout)
        {
            if (!ShipCatalog.Instance.IsValid(loadout)) return false;
            ShipComponentDef[] components = ShipCatalog.Instance.Components;
            return loadout.MountComponents.Count(i => i >= 0 && components[i] is ShieldDef) == 2 &&
                loadout.MountComponents.Count(i => i >= 0 && components[i] is SubSystemDef) == 5;
        }

        private void Fail(string message)
        {
            Debug.LogError("MOUNT_REMOTE_FAILED: " + message);
            if (m_Manager != null)
            {
                if (m_Host) m_Manager.StopHost();
                else m_Manager.StopClient();
            }
            Application.Quit(1);
            enabled = false;
        }
    }
}
#endif
