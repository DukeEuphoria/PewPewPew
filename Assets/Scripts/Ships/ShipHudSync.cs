using Mirror;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Carries server-owned vitals and mounted component state to the owning player's HUD.
    [RequireComponent(typeof(Ship))]
    public class ShipHudSync : NetworkBehaviour
    {
        private const float HudSyncInterval = 0.1f;
        [SyncVar] private ShipVitals m_Vitals;
        [SyncVar] private byte[] m_Cargo;

        public ShipVitals Vitals => m_Vitals;
        public byte[] Cargo => m_Cargo;

        private void Awake()
        {
            syncMode = SyncMode.Owner;
            syncInterval = HudSyncInterval;
        }

        [Server]
        public void Publish(ShipVitals vitals) => m_Vitals = vitals;

        [Server]
        public void PublishCargo(byte[] cargo) => m_Cargo = cargo;
    }
}
