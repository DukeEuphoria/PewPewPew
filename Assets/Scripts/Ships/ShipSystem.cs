using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Runtime state of one installed component and its hull point, if any.
    public class ShipSystem
    {
        /// space is the ship transform that PointPosition is expressed in; it may be null when point is null.
        public ShipSystem(ShipComponentDef def, Transform point = null, Transform space = null)
        {
            Def = def;
            Health = new ComponentHealth(def.MaxHealth);
            Point = point;
            PointPosition = point == null ? (Vector2?)null : (Vector2)space.InverseTransformPoint(point.position);
        }

        public ShipComponentDef Def { get; }
        public ComponentHealth Health { get; }
        public bool Enabled { get; set; } = true;
        public Transform Point { get; }
        public Vector2? PointPosition { get; }
    }
}
