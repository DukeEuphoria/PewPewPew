using Mirror;
using Mirror.FizzySteam;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PewPewPew.Networking
{
    /// Spins up the persistent NetworkManager, Steam transport, lobby system and UI once the first scene is loaded.
    public static class NetworkBootstrap
    {
        // TODO: replace with your registered Steam App ID (480 is Valve's public test/Spacewar app).
        private const uint SteamAppId = 480;

        // AfterSceneLoad (not BeforeSceneLoad) so FindAnyObjectByType can see objects already placed in the scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            Debug.Log("Initializing NetworkBootstrap...");

            if (Object.FindAnyObjectByType<GameNetworkManager>() != null) return;

            var systemsObject = new GameObject("~NetworkSystems");
            Object.DontDestroyOnLoad(systemsObject);

            var transport = systemsObject.AddComponent<FizzyFacepunch>();
            transport.SteamAppID = SteamAppId;

            var networkManager = systemsObject.AddComponent<GameNetworkManager>();
            networkManager.transport = transport;
            Transport.active = transport;

            //==========================


            var playerPrefab = Resources.Load<GameObject>("NetworkPlayer");
            if (playerPrefab != null)
            {
                networkManager.playerPrefab = playerPrefab;
            }
            else
            {
                Debug.LogWarning("No NetworkPlayer prefab found in Resources — connections won't spawn a visible player.");
            }

            //==========================
            

            systemsObject.AddComponent<SteamLobbyManager>();

            EnsureEventSystem();

            var uiObject = new GameObject("~LobbyUI");
            uiObject.transform.SetParent(systemsObject.transform);
            uiObject.AddComponent<LobbyBrowserUI>();

            Debug.Log("NetworkBootstrap initialization complete.");
                
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var eventSystemObject = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
