using System;
using Mirror;
using PewPewPew.Networking;
using UnityEngine;

namespace PewPewPew.Match
{
    /// In-flight status and end-of-match controls. The canvas owns the lobby and scoreboard.
    public class MatchHud : MonoBehaviour
    {
        private void OnGUI()
        {
            if (MenuScreenFlow.AnyBlocking) return;
            MatchManager match = MatchManager.Instance;
            if (match == null || PlayerState.Local == null) return;
            if (!PlayerState.Local.HasShip && match.State != MatchState.Ended) return;

            GUILayout.BeginArea(new Rect(10f, 10f, 320f, 100f));
            GUILayout.Label(StatusText(match));
            if (NetworkServer.activeHost)
            {
                if (match.State == MatchState.Ended && GUILayout.Button("Return to lobby")) match.CmdReturnToLobby();
            }
            GUILayout.EndArea();
        }

        private static string StatusText(MatchManager match)
        {
            switch (match.State)
            {
                case MatchState.Running:
                    TimeSpan remaining = TimeSpan.FromSeconds(match.TimeRemaining);
                    return $"Time left {remaining.Minutes}:{remaining.Seconds:00}";
                case MatchState.Ended:
                    return "Match over";
                default:
                    return "Lobby - waiting for the host to start";
            }
        }
    }
}
