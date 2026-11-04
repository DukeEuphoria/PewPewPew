using System.Linq;
using PewPewPew.Core;
using PewPewPew.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LobbyPanel : MonoBehaviour
    {
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private LobbyPlayerCard[] m_Cards;
        [SerializeField] private LobbyHullRenderer m_Hulls;
        [SerializeField] private LobbySymbol m_ReadySymbol;
        [SerializeField] private TMP_Text m_ReadyCount;
        [SerializeField] private TMP_Text m_Status;
        [SerializeField] private Button m_ReturnToLobby;
        [SerializeField] private Button m_ReadyButton;

        private void Update()
        {
            PlayerState local = PlayerState.Local;
            MatchManager match = MatchManager.Instance;
            bool visible = local != null && match != null && !local.HasShip && !MenuScreenFlow.AnyBlocking;
            m_Panel.SetActive(visible);
            if (!visible) return;
            m_ReadyButton.gameObject.SetActive(match.State != MatchState.Ended);
            PlayerState[] players = PlayerState.All.OrderBy(player => player.netId).ToArray();
            if (players.Length > LobbyReadiness.MaxPlayers)
            {
                Debug.LogError("Lobby player count exceeds the static 16-card capacity.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < m_Cards.Length; i++)
                m_Cards[i].Bind(i < players.Length ? players[i] : null, m_Hulls);
            int ready = players.Count(player => player.Ready);
            m_ReadyCount.text = $"{ready}/{players.Length}";
            bool stopped = match.State == MatchState.Lobby && local.Ready;
            m_ReadySymbol.Kind = stopped ? LobbySymbolKind.Stop : LobbySymbolKind.Play;
            m_ReadySymbol.color = stopped ? new Color(0.5f, 0.55f, 0.58f) : new Color(0.12f, 0.8f, 0.3f);
            m_Status.text = match.State == MatchState.Ended ? "MATCH OVER" :
                match.State == MatchState.Lobby ? "LOBBY - all players ready to start" : $"MATCH IN PROGRESS - {match.TimeRemaining:0}s";
            m_ReturnToLobby.gameObject.SetActive(match.State == MatchState.Ended && Mirror.NetworkServer.activeHost);
        }

        private void Awake()
        {
            m_ReturnToLobby.onClick.AddListener(() => MatchManager.Instance.CmdReturnToLobby());
        }
    }
}
