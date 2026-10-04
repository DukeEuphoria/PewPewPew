using System;
using System.Linq;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Networking;
using PewPewPew.Ships;
using UnityEngine;

namespace PewPewPew.Match
{
    /// Placeholder immediate-mode UI: match status, scoreboard, loadout selection and the deploy / host buttons.
    public class MatchHud : MonoBehaviour
    {
        private ShipLoadout m_Pending;
        private bool m_HasPending;

        private void OnGUI()
        {
            if (MenuScreenFlow.AnyBlocking) return;
            MatchManager match = MatchManager.Instance;
            PlayerState local = PlayerState.Local;
            if (match == null || local == null || ShipCatalog.Instance == null) return;

            if (!m_HasPending)
            {
                m_Pending = local.Loadout;
                m_HasPending = true;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 320f, Screen.height - 20f));
            GUILayout.Label(StatusText(match));
            DrawScoreboard();
            if (!local.HasShip && match.State != MatchState.Ended) DrawDeployPanel(match, local);
            DrawHostButtons(match);
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

        private static void DrawScoreboard()
        {
            foreach (PlayerState player in PlayerState.All.OrderByDescending(p => p.Kills).ThenBy(p => p.Deaths))
            {
                GUILayout.Label($"{player.Name}   K {player.Kills}   D {player.Deaths}");
            }
        }

        private void DrawDeployPanel(MatchManager match, PlayerState local)
        {
            ShipCatalog catalog = ShipCatalog.Instance;
            bool changed = false;
            changed |= Choose("Hull", ref m_Pending.Hull, catalog.Hulls, false, null);
            HullDef hull = catalog.Hulls[m_Pending.Hull];
            changed |= Choose("Shield", ref m_Pending.Shield, catalog.Shields, false, null);
            changed |= Choose("Main gun", ref m_Pending.MainGun, catalog.MainGuns, false, i => catalog.IsCompatible(hull, catalog.MainGuns[i], false));
            changed |= Choose("Secondary gun", ref m_Pending.SecondaryGun, catalog.SecondaryGuns, false, i => catalog.IsCompatible(hull, catalog.SecondaryGuns[i], true));
            changed |= Choose("Thruster", ref m_Pending.Thruster, catalog.Thrusters, false, i => catalog.IsCompatible(hull, catalog.Thrusters[i]));

            int slots = catalog.Hulls[m_Pending.Hull].SubSystemSlots;
            for (int slot = 0; slot < ShipLoadout.SubSystemSlots; slot++)
            {
                int index = m_Pending.GetSubSystem(slot);
                if (slot >= slots)
                {
                    if (index >= 0) m_Pending.SetSubSystem(slot, -1);
                    continue;
                }

                if (Choose($"Sub system {slot + 1}", ref index, catalog.SubSystems, true, null))
                {
                    m_Pending.SetSubSystem(slot, index);
                    changed = true;
                }
            }
            if (changed) local.CmdSetLoadout(m_Pending);

            double wait = local.RespawnAt - NetworkTime.time;
            string problem = catalog.LaunchProblem(m_Pending);
            if (problem != null) GUILayout.Label(problem);
            GUI.enabled = wait <= 0.0 && problem == null && !(match.State == MatchState.Lobby && local.Ready);
            string label = wait > 0.0 ? $"Deploy in {wait:0}s" : match.State == MatchState.Lobby ? (local.Ready ? "Ready" : "Ready up") : "Deploy";
            if (GUILayout.Button(label)) local.CmdDeploy();
            GUI.enabled = true;
        }

        private static void DrawHostButtons(MatchManager match)
        {
            if (!NetworkServer.activeHost) return;

            if (match.State == MatchState.Lobby && GUILayout.Button("Start match")) match.CmdStartMatch();
            if (match.State == MatchState.Ended && GUILayout.Button("Return to lobby")) match.CmdReturnToLobby();
        }

        // A current choice the hull cannot take is moved to the next one it can.
        private static bool Choose<T>(string label, ref int index, T[] items, bool allowNone, Func<int, bool> available) where T : UnityEngine.Object
        {
            int before = index;
            if (available != null && index >= 0 && !available(index)) index = LoadoutMath.CycleAvailable(index, items.Length, 1, allowNone, available);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100f));
            if (GUILayout.Button("<", GUILayout.Width(24f))) index = Step(index, items.Length, -1, allowNone, available);
            GUILayout.Label(index >= 0 ? items[index].name : "None", GUILayout.Width(140f));
            if (GUILayout.Button(">", GUILayout.Width(24f))) index = Step(index, items.Length, 1, allowNone, available);
            GUILayout.EndHorizontal();
            return index != before;
        }

        private static int Step(int index, int count, int step, bool allowNone, Func<int, bool> available)
        {
            return available == null ? LoadoutMath.Cycle(index, count, step, allowNone) : LoadoutMath.CycleAvailable(index, count, step, allowNone, available);
        }
    }
}
