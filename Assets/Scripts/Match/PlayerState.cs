using System.Collections.Generic;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;

namespace PewPewPew.Match
{
    /// One per connected player (the NetworkManager's player prefab). Holds the name, score and loadout; ships are spawned separately.
    public class PlayerState : NetworkBehaviour
    {
        private const int MaxNameLength = 24;

        public static readonly List<PlayerState> All = new List<PlayerState>();

        [SyncVar] private string m_Name = "Player";
        [SyncVar] private int m_Kills;
        [SyncVar] private int m_Deaths;
        [SyncVar] private bool m_Ready;
        [SyncVar] private bool m_HasShip;
        [SyncVar] private double m_RespawnAt;
        [SyncVar] private ShipLoadout m_Loadout = ShipLoadout.Empty;

        public static PlayerState Local { get; private set; }

        public string Name => m_Name;
        public int Kills => m_Kills;
        public int Deaths => m_Deaths;
        public bool Ready => m_Ready;
        public bool HasShip => m_HasShip;
        public double RespawnAt => m_RespawnAt;
        public ShipLoadout Loadout => m_Loadout;

        /// Server only.
        public Ship Ship { get; set; }

        private void Awake()
        {
            // Scores and loadouts must reach everyone whatever the interest management says about this object's position.
            GetComponent<NetworkIdentity>().visibility = Visibility.ForceShown;
        }

        public override void OnStartServer() => Register();

        // Interest management measures distance from this object, so it must travel with the ship.
        [ServerCallback]
        private void LateUpdate()
        {
            if (Ship != null) transform.position = Ship.transform.position;
        }

        public override void OnStartClient() => Register();

        public override void OnStopServer() => All.Remove(this);

        public override void OnStopClient() => All.Remove(this);

        public override void OnStartLocalPlayer()
        {
            Local = this;
            CmdSetName(Steamworks.SteamClient.IsValid ? Steamworks.SteamClient.Name : "Player");
        }

        public override void OnStopLocalPlayer()
        {
            if (Local == this) Local = null;
        }

        private void Register()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        [Command]
        private void CmdSetName(string playerName)
        {
            playerName = playerName?.Trim();
            if (string.IsNullOrEmpty(playerName)) return;

            m_Name = playerName.Length > MaxNameLength ? playerName.Substring(0, MaxNameLength) : playerName;
        }

        [Command]
        public void CmdSetLoadout(ShipLoadout loadout)
        {
            if (ShipCatalog.Instance.IsValid(loadout)) m_Loadout = loadout;
        }

        [Command]
        public void CmdDeploy() => MatchManager.Instance.ServerDeploy(this);

        [Server]
        public void ServerResetScore()
        {
            m_Kills = 0;
            m_Deaths = 0;
        }

        [Server]
        public void ServerAddKill() => m_Kills++;

        [Server]
        public void ServerAddDeath() => m_Deaths++;

        [Server]
        public void ServerSetReady(bool ready) => m_Ready = ready;

        [Server]
        public void ServerSetShip(Ship ship, double respawnAt)
        {
            Ship = ship;
            m_HasShip = ship != null;
            m_RespawnAt = respawnAt;
            if (ship != null) transform.position = ship.transform.position;
        }
    }
}
