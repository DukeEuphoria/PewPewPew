using System;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PewPewPew.Networking
{
    /// Wraps Steam lobby creation/discovery/joining and bridges it to Mirror's NetworkManager.
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        // Custom lobby data keys used to filter/display lobbies in the browser UI.
        private const string HostAddressKey = "HostAddress";
        private const string LobbyNameKey = "LobbyName";
        private const string GameTagKey = "game";

        // Random GUID (not "PewPewPew") so lobbies aren't mistaken for another project's while sharing App ID 480.   
        private const string GameTagValue = "pewpewpew-9e1f5c2b-4a3e-4c77-9f2e-9a771b5cf310";

        public Lobby? CurrentLobby { get; private set; }
        public event Action<Lobby[]> LobbyListUpdated;
        public event Action<string> StatusChanged;

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

        private void OnDestroy()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

            if (Instance == this) Instance = null;
        }

        public async void HostLobby(string lobbyName, int maxPlayers)
        {
            if (!SteamClient.IsValid)
            {
                StatusChanged?.Invoke("Steam is not running.");
                return;
            }

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
            CurrentLobby.Value.SetData(LobbyNameKey, string.IsNullOrWhiteSpace(lobbyName) ? $"{SteamClient.Name}'s Game" : lobbyName);
            CurrentLobby.Value.SetData(GameTagKey, GameTagValue);
            CurrentLobby.Value.SetData(HostAddressKey, SteamClient.SteamId.Value.ToString());

            StatusChanged?.Invoke($"Hosting '{lobbyName}' ({SteamClient.SteamId})");
        }

        public async void RefreshLobbies()
        {
            StatusChanged?.Invoke("Searching for lobbies...");

            Lobby[] lobbies = await SteamMatchmaking.LobbyList
                .WithKeyValue(GameTagKey, GameTagValue)
                .WithMaxResults(50)
                .RequestAsync();

            LobbyListUpdated?.Invoke(lobbies ?? Array.Empty<Lobby>());
            StatusChanged?.Invoke(lobbies == null ? "Search failed." : $"Found {lobbies.Length} lobbies.");
        }

        public async void JoinLobby(Lobby lobby)
        {
            StatusChanged?.Invoke($"Joining {lobby.GetData(LobbyNameKey)}...");
            await lobby.Join();
        }

        public void LeaveLobby()
        {
            if (CurrentLobby.HasValue)
            {
                CurrentLobby.Value.Leave();
                CurrentLobby = null;
            }
        }

        private void OnLobbyCreated(Result result, Lobby lobby)
        {
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;

            // Host already started the server in HostLobby(); only non-hosts need to connect as a client.
            if (NetworkServer.active) return;

            string hostAddress = lobby.GetData(HostAddressKey);
            if (string.IsNullOrEmpty(hostAddress))
            {
                hostAddress = lobby.Owner.Id.Value.ToString();
            }

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
        }

        private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            JoinLobby(lobby);
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            StatusChanged?.Invoke($"{friend.Name} joined the lobby.");
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            StatusChanged?.Invoke($"{friend.Name} left the lobby.");
        }
    }
}
