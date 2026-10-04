using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Runtime state of one installed component and the hull emission points it uses.
    public class ShipSystem
    {
        /// space is the ship transform that PointPositions are expressed in; it may be null when there are no points.
        public ShipSystem(ShipComponentDef def, Transform[] points, Transform space = null)
        {
            Def = def;
            Health = new ComponentHealth(def.MaxHealth);
            Points = points;
            PointPositions = System.Array.ConvertAll(points, point => (Vector2)space.InverseTransformPoint(point.position));
        }

        public ShipComponentDef Def { get; }
        public ComponentHealth Health { get; }
        public Transform[] Points { get; }
        public Vector2[] PointPositions { get; }
    }
}
