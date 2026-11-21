using Mirror;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Networking
{
    /// Mirror NetworkManager with its transport assigned in the scene.
    public class GameNetworkManager : NetworkManager
    {
        private Transport m_NetworkTransport;
        private TelepathyTransport m_OfflineTransport;
        private int m_NetworkMaxConnections;
        private bool m_IsOfflineGame;

        public bool IsOfflineGame => m_IsOfflineGame;

        public void StartOfflineGame()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("Cannot start an offline game while networking is active.", this);
                return;
            }

            if (transport == null)
            {
                Debug.LogError("Cannot start an offline game without a network transport.", this);
                return;
            }

            if (m_OfflineTransport == null)
            {
                m_OfflineTransport = GetComponent<TelepathyTransport>();
                if (m_OfflineTransport == null) m_OfflineTransport = gameObject.AddComponent<TelepathyTransport>();
            }

            if (!m_OfflineTransport.Available())
            {
                Debug.LogError("The local network transport is unavailable on this platform.", this);
                return;
            }

            m_NetworkTransport = transport;
            m_NetworkMaxConnections = maxConnections;
            transport = m_OfflineTransport;
            Transport.active = transport;
            maxConnections = 1;
            m_IsOfflineGame = true;
            StartHost();
        }

        public void StopOfflineGame()
        {
            if (!m_IsOfflineGame) return;

            StopHost();
            transport = m_NetworkTransport;
            Transport.active = m_NetworkTransport;
            maxConnections = m_NetworkMaxConnections;
            m_NetworkTransport = null;
            m_IsOfflineGame = false;
        }

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
