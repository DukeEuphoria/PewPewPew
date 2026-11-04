namespace PewPewPew.Core
{
    public static class LobbyReadiness
    {
        public const int MaxPlayers = 16;

        public static bool CanStart(int players, int ready) =>
            players > 0 && players <= MaxPlayers && ready == players;
    }
}
