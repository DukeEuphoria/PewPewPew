using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace PewPewPew.Networking
{
    [RequireComponent(typeof(NetworkManager))]
    public class LobbyPasswordAuthenticator : NetworkAuthenticator
    {
        private const float m_RejectDelaySeconds = 0.5f;

        private readonly HashSet<NetworkConnectionToClient> m_PendingRejects = new HashSet<NetworkConnectionToClient>();
        private string m_ServerPassword = string.Empty;
        private string m_ClientPassword = string.Empty;

        public event Action<string> AuthenticationFailed;

        public struct PasswordRequestMessage : NetworkMessage
        {
            public string m_Password;
        }

        public struct PasswordResponseMessage : NetworkMessage
        {
            public bool m_Accepted;
        }

        public void SetServerPassword(string password)
        {
            m_ServerPassword = password ?? string.Empty;
        }

        public void SetClientPassword(string password)
        {
            m_ClientPassword = password ?? string.Empty;
        }

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<PasswordRequestMessage>(OnPasswordRequest, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<PasswordRequestMessage>();
            m_PendingRejects.Clear();
            m_ServerPassword = string.Empty;
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient connection)
        {
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<PasswordResponseMessage>(OnPasswordResponse, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<PasswordResponseMessage>();
        }

        public override void OnClientAuthenticate()
        {
            NetworkClient.Send(new PasswordRequestMessage { m_Password = m_ClientPassword });
        }

        private void OnPasswordRequest(NetworkConnectionToClient connection, PasswordRequestMessage message)
        {
            if (m_PendingRejects.Contains(connection)) return;

            bool accepted = string.Equals(message.m_Password, m_ServerPassword, System.StringComparison.Ordinal);
            connection.Send(new PasswordResponseMessage { m_Accepted = accepted });

            if (accepted)
            {
                ServerAccept(connection);
                return;
            }

            m_PendingRejects.Add(connection);
            connection.isAuthenticated = false;
            StartCoroutine(RejectAfterResponse(connection));
        }

        private void OnPasswordResponse(PasswordResponseMessage message)
        {
            if (message.m_Accepted)
            {
                ClientAccept();
                return;
            }

            AuthenticationFailed?.Invoke("Incorrect lobby password.");
            ClientReject();
        }

        private IEnumerator RejectAfterResponse(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(m_RejectDelaySeconds);
            ServerReject(connection);
            m_PendingRejects.Remove(connection);
        }
    }
}