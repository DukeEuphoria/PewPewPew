using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PewPewPew.Networking
{
    public class MenuButtonFunctions : MonoBehaviour
    {
        private const string MainMenuScene = "SplashScreen";

        private bool m_IsQuitting;

        /// Starts a local host without creating or joining a Steam lobby.
        public void StartSoloGame()
        {
            if (!(NetworkManager.singleton is GameNetworkManager gameNetworkManager))
            {
                Debug.LogError("Cannot start a solo game without a GameNetworkManager.", this);
                return;
            }

            gameNetworkManager.StartOfflineGame();
        }

        /// Leaves the match and lobby, then returns to the title scene.
        public void QuitToMainMenu()
        {
            SteamLobbyManager.Instance?.LeaveLobby();
            StopNetwork();

            // With an offline scene set, Mirror loads it after stopping; otherwise (e.g. offline testing) load it here.
            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager == null || string.IsNullOrEmpty(networkManager.offlineScene)) SceneManager.LoadScene(MainMenuScene);
        }

        public void QuitApplication()
        {
            if (m_IsQuitting) return;
            m_IsQuitting = true;

            StopNetwork();
            SteamLobbyManager.Instance?.LeaveLobby();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void StopNetwork()
        {
            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager == null) return;

            if (networkManager is GameNetworkManager gameNetworkManager && gameNetworkManager.IsOfflineGame)
            {
                gameNetworkManager.StopOfflineGame();
                return;
            }

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
    }
}