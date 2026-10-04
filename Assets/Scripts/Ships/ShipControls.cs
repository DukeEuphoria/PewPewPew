using UnityEngine;

namespace PewPewPew.Ships
{
    /// What the owning player is asking the ship to do, sent to the server.
    public struct ShipControls
    {
        public float Thrust;
        public Vector2 AimPoint;
        public bool FireMain;
        public bool FireSecondary;
    }
}
