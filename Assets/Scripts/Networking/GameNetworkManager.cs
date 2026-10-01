using Mirror;
using UnityEngine;

namespace PewPewPew.Networking
{
    /// Mirror NetworkManager with its transport assigned in the scene.
    public class GameNetworkManager : NetworkManager
    {
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
