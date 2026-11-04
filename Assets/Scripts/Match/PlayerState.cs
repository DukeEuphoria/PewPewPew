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
        [SyncVar] private ulong m_SteamId;
        [SyncVar] private int m_Kills;
        [SyncVar] private int m_Deaths;
        [SyncVar] private bool m_Ready;
        [SyncVar] private bool m_HasShip;
        [SyncVar] private double m_RespawnAt;
        [SyncVar] private ShipLoadout m_Loadout = ShipLoadout.Empty;

        public static PlayerState Local { get; private set; }

        public string Name => m_Name;
        // Cosmetic identity for avatar lookup, not authentication or ownership.
        public ulong SteamId => m_SteamId;
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
            CmdSetIdentity(Steamworks.SteamClient.IsValid ? Steamworks.SteamClient.Name : "Player",
                Steamworks.SteamClient.IsValid ? Steamworks.SteamClient.SteamId.Value : 0);
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
        private void CmdSetIdentity(string playerName, ulong steamId)
        {
            playerName = playerName?.Trim();
            if (string.IsNullOrEmpty(playerName)) return;

            m_Name = playerName.Length > MaxNameLength ? playerName.Substring(0, MaxNameLength) : playerName;
            m_SteamId = steamId;
        }

        [Command]
        public void CmdSetLoadout(ShipLoadout loadout)
        {
            if (m_Ready || m_HasShip)
            {
                Debug.LogWarning($"Rejected loadout change for {m_Name}: unready before editing.", this);
                return;
            }
            if (!ShipCatalog.Instance.IsValid(loadout)) return;
            string locked = ShipCatalog.Instance.LockedProblem(loadout);
            if (locked != null)
            {
                Debug.LogWarning($"Rejected loadout for {m_Name}: {locked}", this);
                return;
            }

            if (PlayerPalette.IndexOf(loadout.Colour0) < 0) loadout.Colour0 = PlayerPalette.DefaultPrimary;
            if (PlayerPalette.IndexOf(loadout.Colour1) < 0) loadout.Colour1 = PlayerPalette.DefaultSecondary;
            loadout.Colour0.a = 255;
            loadout.Colour1.a = 255;
            m_Loadout = loadout;
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
