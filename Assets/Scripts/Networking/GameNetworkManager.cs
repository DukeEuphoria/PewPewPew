using Mirror;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Networking
{
    /// Mirror NetworkManager with its transport assigned in the scene.
    public class GameNetworkManager : NetworkManager
    {
        public override void OnStartServer()
        {
            maxConnections = Mathf.Clamp(maxConnections, 1, LobbyReadiness.MaxPlayers);
            NetworkServer.maxConnections = maxConnections;
            base.OnStartServer();
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            SteamLobbyManager.Instance?.NotifyNetworkClientConnected();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[Network] Client connected: {conn.connectionId}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Network] Client disconnected: {conn.connectionId}");
            base.OnServerDisconnect(conn);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            SteamLobbyManager.Instance?.LeaveLobby();
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            SteamLobbyManager.Instance?.LeaveLobby();
        }
    }
}
