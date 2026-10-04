using System;
using Mirror;
using PewPewPew.Ships;
using UnityEngine;

namespace PewPewPew.Match
{
    public enum MatchState : byte { Lobby, Running, Ended }

    /// Timed free-for-all. The host starts the match from the lobby; players can deploy at any time while it runs, including late joiners.
    public class MatchManager : NetworkBehaviour
    {
        [SerializeField] private Ship m_ShipPrefab;
        [SerializeField] private float m_MatchDuration = 600f;
        [SerializeField] private float m_RespawnDelay = 3f;
        [SerializeField, Tooltip("A kill is credited to whoever last hit the ship within this time.")] private float m_KillCreditSeconds = 10f;

        [SyncVar] private MatchState m_State;
        [SyncVar] private double m_EndTime;

        public static MatchManager Instance { get; private set; }

        public MatchState State => m_State;

        public double TimeRemaining => m_State == MatchState.Running ? Math.Max(0.0, m_EndTime - NetworkTime.time) : 0.0;

        private void Awake()
        {
            Instance = this;
            GetComponent<NetworkIdentity>().visibility = Visibility.ForceShown;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [ServerCallback]
        private void Update()
        {
            if (m_State == MatchState.Running && NetworkTime.time >= m_EndTime) EndMatch();
        }

        [Command(requiresAuthority = false)]
        public void CmdStartMatch(NetworkConnectionToClient sender = null)
        {
            if (sender != NetworkServer.localConnection || m_State != MatchState.Lobby) return;

            m_State = MatchState.Running;
            m_EndTime = NetworkTime.time + m_MatchDuration;
            foreach (PlayerState player in PlayerState.All)
            {
                player.ServerResetScore();
                if (player.Ready) SpawnShip(player);
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdReturnToLobby(NetworkConnectionToClient sender = null)
        {
            if (sender != NetworkServer.localConnection || m_State != MatchState.Ended) return;

            m_State = MatchState.Lobby;
            foreach (PlayerState player in PlayerState.All) player.ServerSetReady(false);
        }

        /// Server only. In the lobby this queues the player for the start; while running it launches them now.
        [Server]
        public void ServerDeploy(PlayerState player)
        {
            if (m_State == MatchState.Ended || player.Ship != null || NetworkTime.time < player.RespawnAt) return;

            string problem = ShipCatalog.Instance.LaunchProblem(player.Loadout);
            if (problem != null)
            {
                Debug.LogError($"{player.Name} cannot launch: {problem}");
                return;
            }

            player.ServerSetReady(true);
            if (m_State == MatchState.Running) SpawnShip(player);
        }

        [Server]
        private void SpawnShip(PlayerState player)
        {
            Ship ship = Instantiate(m_ShipPrefab, SpawnPoint.Pick(), Quaternion.identity);
            ship.Initialize(player.Loadout, player);
            NetworkServer.Spawn(ship.gameObject, player.connectionToClient);
            ship.Destroyed += HandleShipDestroyed;
            player.ServerSetShip(ship, 0.0);
        }

        [Server]
        private void HandleShipDestroyed(Ship ship)
        {
            PlayerState victim = ship.Owner;
            if (victim != null)
            {
                victim.ServerAddDeath();
                victim.ServerSetShip(null, NetworkTime.time + m_RespawnDelay);
            }

            PlayerState killer = ship.KillCredit(m_KillCreditSeconds);
            if (killer != null) killer.ServerAddKill();
        }

        [Server]
        private void EndMatch()
        {
            m_State = MatchState.Ended;
            foreach (PlayerState player in PlayerState.All)
            {
                if (player.Ship != null) NetworkServer.Destroy(player.Ship.gameObject);
                player.ServerSetShip(null, 0.0);
            }
        }
    }
}
