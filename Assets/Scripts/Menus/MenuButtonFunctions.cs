using Mirror;
using UnityEngine;

namespace PewPewPew.Networking
{
    public class MenuButtonFunctions : MonoBehaviour
    {
        private bool m_IsQuitting;

        public void QuitApplication()
        {
            if (m_IsQuitting) return;
            m_IsQuitting = true;

            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager != null)
            {
                if (NetworkServer.active && NetworkClient.active)
                {
                    networkManager.StopHost();
                }
                else if (NetworkClient.active)
                {
                    networkManager.StopClient();
                }
                else if (NetworkServer.active)
                {
                    networkManager.StopServer();
                }
            }

            SteamLobbyManager.Instance?.LeaveLobby();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}