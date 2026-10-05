using Mirror;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Carries the server's armour, shield, power and sub system state to the owning player only, for their HUD.
    [RequireComponent(typeof(Ship))]
    public class ShipHudSync : NetworkBehaviour
    {
        private const float HudSyncInterval = 0.1f;
        [SyncVar] private ShipVitals m_Vitals;

        public ShipVitals Vitals => m_Vitals;

        private void Awake()
        {
            syncMode = SyncMode.Owner;
            syncInterval = HudSyncInterval;
        }

        [Server]
        public void Publish(ShipVitals vitals) => m_Vitals = vitals;
    }
}
