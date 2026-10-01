using System;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PewPewPew.Networking
{
    public enum GameMode
    {
        FreeForAll,
        TeamDeathmatch
    }

    /// Wraps Steam lobby creation/discovery/joining and bridges it to Mirror's NetworkManager.
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        // Custom lobby data keys used to filter/display lobbies in the browser UI.
        private const string m_HostAddressKey = "HostAddress";
        private const string m_LobbyNameKey = "LobbyName";
        private const string m_GameTagKey = "game";
        private const string m_GameModeKey = "GameMode";
        private const string m_PasswordRequiredKey = "PasswordRequired";

        // Random GUID (not "PewPewPew") so lobbies aren't mistaken for another project's while sharing App ID 480.   
        private const string m_GameTagValue = "pewpewpew-9e1f5c2b-4a3e-4c77-9f2e-9a771b5cf310";

        [SerializeField, Min(5f)] private float m_ActiveLobbyRefreshInterval = 30f;

        private LobbyPasswordAuthenticator m_PasswordAuthenticator;
        private float m_ActiveLobbyRefreshTimer;
        private bool m_IsCountRequestPending;

        public Lobby? CurrentLobby { get; private set; }
        public event Action<Lobby[]> LobbyListUpdated;
        public event Action<string> StatusChanged;
        public event Action<int> ActiveLobbyCountChanged;
        public event Action GameplayReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        private void Start()
        {
            if (NetworkManager.singleton != null)
            {
                m_PasswordAuthenticator = NetworkManager.singleton.GetComponent<LobbyPasswordAuthenticator>();
                if (m_PasswordAuthenticator != null)
                {
                    m_PasswordAuthenticator.AuthenticationFailed += OnAuthenticationFailed;
                }
            }

            if (!SteamClient.IsValid)
            {
                StatusChanged?.Invoke("Steam is unavailable. Start Steam before launching the game.");
                return;
            }

            RefreshActiveLobbyCount();
            m_ActiveLobbyRefreshTimer = m_ActiveLobbyRefreshInterval;
        }

        private void Update()
        {
            if (!SteamClient.IsValid) return;

            m_ActiveLobbyRefreshTimer -= Time.unscaledDeltaTime;
            if (m_ActiveLobbyRefreshTimer > 0f) return;

            m_ActiveLobbyRefreshTimer = m_ActiveLobbyRefreshInterval;
            RefreshActiveLobbyCount();
        }

        private void OnDestroy()
        {
            if (m_PasswordAuthenticator != null)
            {
                m_PasswordAuthenticator.AuthenticationFailed -= OnAuthenticationFailed;
            }

            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

            if (Instance == this) Instance = null;
        }

        public void HostLobby(string lobbyName, int maxPlayers)
        {
            HostLobby(lobbyName, string.Empty, GameMode.FreeForAll, maxPlayers);
        }

        public async void HostLobby(string lobbyName, string password, GameMode gameMode, int maxPlayers)
        {
            if (!SteamClient.IsValid)
            {
                StatusChanged?.Invoke("Steam is not running.");
                return;
            }

            if (maxPlayers < 2 || maxPlayers > 250)
            {
                StatusChanged?.Invoke("Player count must be between 2 and 250.");
                return;
            }

            if (NetworkManager.singleton == null)
            {
                StatusChanged?.Invoke("Network manager is unavailable.");
                return;
            }

            LobbyPasswordAuthenticator passwordAuthenticator = NetworkManager.singleton.GetComponent<LobbyPasswordAuthenticator>();
            if (!string.IsNullOrEmpty(password) && passwordAuthenticator == null)
            {
                StatusChanged?.Invoke("Password-protected hosting is not configured.");
                return;
            }
            passwordAuthenticator?.SetServerPassword(password);
            passwordAuthenticator?.SetClientPassword(password);

            NetworkManager.singleton.maxConnections = maxPlayers;
            NetworkManager.singleton.StartHost();

            Lobby? result = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (!result.HasValue)
            {
                StatusChanged?.Invoke("Failed to create Steam lobby.");
                NetworkManager.singleton.StopHost();
                return;
            }

            CurrentLobby = result.Value;
            CurrentLobby.Value.SetPublic();
            CurrentLobby.Value.SetJoinable(true);
            CurrentLobby.Value.SetData(m_LobbyNameKey, string.IsNullOrWhiteSpace(lobbyName) ? $"{SteamClient.Name}'s Game" : lobbyName);
            CurrentLobby.Value.SetData(m_GameTagKey, m_GameTagValue);
            CurrentLobby.Value.SetData(m_GameModeKey, gameMode.ToString());
            CurrentLobby.Value.SetData(m_HostAddressKey, SteamClient.SteamId.Value.ToString());
            CurrentLobby.Value.SetData(m_PasswordRequiredKey, string.IsNullOrEmpty(password) ? "false" : "true");

            StatusChanged?.Invoke($"Hosting '{CurrentLobby.Value.GetData(m_LobbyNameKey)}' ({gameMode}, {maxPlayers} players)");
            GameplayReady?.Invoke();
        }

        public async void RefreshLobbies()
        {
            RefreshLobbies(false);
        }

        public async void RefreshLobbies(bool friendsOnly)
        {
            if (!SteamClient.IsValid)
            {
                StatusChanged?.Invoke("Steam is unavailable.");
                LobbyListUpdated?.Invoke(Array.Empty<Lobby>());
                return;
            }

            StatusChanged?.Invoke(friendsOnly ? "Searching friends' games..." : "Searching public games...");
            var lobbiesById = new Dictionary<ulong, Lobby>();

            try
            {
                if (!friendsOnly)
                {
                    Lobby[] publicLobbies = await SteamMatchmaking.LobbyList
                        .WithKeyValue(m_GameTagKey, m_GameTagValue)
                        .WithMaxResults(50)
                        .RequestAsync();

                    if (publicLobbies != null)
                    {
                        foreach (Lobby lobby in publicLobbies)
                        {
                            lobbiesById[lobby.Id.Value] = lobby;
                        }
                    }
                }
                else
                {
                    foreach (Friend friend in SteamFriends.GetFriends())
                    {
                        if (!friend.IsPlayingThisGame || !friend.GameInfo.HasValue) continue;

                        Lobby? friendLobby = friend.GameInfo.Value.Lobby;
                        if (!friendLobby.HasValue) continue;

                        Lobby lobby = friendLobby.Value;
                        if (lobby.GetData(m_GameTagKey) != m_GameTagValue) continue;
                        lobbiesById[lobby.Id.Value] = lobby;
                    }
                }

                var lobbies = new Lobby[lobbiesById.Count];
                lobbiesById.Values.CopyTo(lobbies, 0);
                LobbyListUpdated?.Invoke(lobbies);
                StatusChanged?.Invoke(friendsOnly
                    ? $"Found {lobbies.Length} games hosted by friends."
                    : $"Found {lobbies.Length} public games.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                LobbyListUpdated?.Invoke(Array.Empty<Lobby>());
                StatusChanged?.Invoke("Lobby search failed.");
            }
        }

        public async void JoinLobby(Lobby lobby)
        {
            JoinLobby(lobby, string.Empty);
        }

        public async void JoinLobby(Lobby lobby, string password)
        {
            if (!SteamClient.IsValid)
            {
                StatusChanged?.Invoke("Steam is unavailable.");
                return;
            }

            if (NetworkManager.singleton == null)
            {
                StatusChanged?.Invoke("Network manager is unavailable.");
                return;
            }

            bool passwordRequired = lobby.GetData(m_PasswordRequiredKey) == "true";
            if (passwordRequired)
            {
                if (string.IsNullOrEmpty(password))
                {
                    StatusChanged?.Invoke("This lobby requires a password.");
                    return;
                }
            }
            else
            {
                password = string.Empty;
            }

            LobbyPasswordAuthenticator passwordAuthenticator = NetworkManager.singleton.GetComponent<LobbyPasswordAuthenticator>();
            if (passwordRequired && passwordAuthenticator == null)
            {
                StatusChanged?.Invoke("Password-protected joining is not configured.");
                return;
            }
            passwordAuthenticator?.SetClientPassword(password);

            StatusChanged?.Invoke($"Joining {lobby.GetData(m_LobbyNameKey)}...");
            RoomEnter result = await lobby.Join();
            if (result == RoomEnter.Success)
            {
                StatusChanged?.Invoke($"Joined lobby OK: {lobby.GetData(m_LobbyNameKey)}.");
            }
            else
            {
                StatusChanged?.Invoke($"Could not join lobby: {lobby.GetData(m_LobbyNameKey)} , {result}.");
            }
        }

        public async void RefreshActiveLobbyCount()
        {
            if (!SteamClient.IsValid || m_IsCountRequestPending) return;

            m_IsCountRequestPending = true;
            try
            {
                var activeLobbyIds = new HashSet<ulong>();
                Lobby[] lobbies = await SteamMatchmaking.LobbyList
                    .WithKeyValue(m_GameTagKey, m_GameTagValue)
                    .WithMaxResults(50)
                    .RequestAsync();

                if (lobbies == null) return;
                foreach (Lobby lobby in lobbies)
                {
                    activeLobbyIds.Add(lobby.Id.Value);
                }

                foreach (Friend friend in SteamFriends.GetFriends())
                {
                    if (!friend.IsPlayingThisGame || !friend.GameInfo.HasValue) continue;
                    Lobby? friendLobby = friend.GameInfo.Value.Lobby;
                    if (friendLobby.HasValue) activeLobbyIds.Add(friendLobby.Value.Id.Value);
                }

                ActiveLobbyCountChanged?.Invoke(activeLobbyIds.Count);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                m_IsCountRequestPending = false;
            }
        }

        public void LeaveLobby()
        {
            if (CurrentLobby.HasValue)
            {
                CurrentLobby.Value.Leave();
                CurrentLobby = null;
            }
        }

        public void NotifyNetworkClientConnected()
        {
            if (NetworkServer.active) return;

            GameplayReady?.Invoke();
        }

        private void OnLobbyCreated(Result result, Lobby lobby)
        {
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;

            // Host already started the server in HostLobby(); only non-hosts need to connect as a client.
            if (NetworkServer.active) return;

            if (NetworkManager.singleton == null)
            {
                StatusChanged?.Invoke("Network manager is unavailable.");
                LeaveLobby();
                return;
            }

            string hostAddress = lobby.GetData(m_HostAddressKey);
            if (string.IsNullOrEmpty(hostAddress))
            {
                hostAddress = lobby.Owner.Id.Value.ToString();
            }

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
        }

        private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            JoinLobby(lobby, string.Empty);
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            StatusChanged?.Invoke($"{friend.Name} joined the lobby.");
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            StatusChanged?.Invoke($"{friend.Name} left the lobby.");
        }

        private void OnAuthenticationFailed(string status)
        {
            StatusChanged?.Invoke(status);
        }

    }
}
