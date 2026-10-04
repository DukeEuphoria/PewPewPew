using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Runtime state of one installed component and the hull emission points it uses.
    public class ShipSystem
    {
        public ShipSystem(ShipComponentDef def, EmissionPoint[] points)
        {
            Def = def;
            Health = new ComponentHealth(def.MaxHealth);
            Points = points;
            PointPositions = System.Array.ConvertAll(points, point => point.Position);
        }

        public ShipComponentDef Def { get; }
        public ComponentHealth Health { get; }
        public EmissionPoint[] Points { get; }
        public Vector2[] PointPositions { get; }
    }
}
