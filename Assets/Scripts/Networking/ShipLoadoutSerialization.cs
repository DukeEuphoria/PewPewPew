using System.IO;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Ships;

namespace PewPewPew.Networking
{
    public static class ShipLoadoutSerialization
    {
        public static void WriteShipLoadout(this NetworkWriter writer, ShipLoadout loadout)
        {
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !catalog.IsValid(loadout))
                throw new InvalidDataException("Cannot serialize an invalid ship loadout.");
            writer.WriteInt(loadout.Hull);
            writer.WriteColor32(loadout.Colour0);
            writer.WriteColor32(loadout.Colour1);
            writer.WriteInt(loadout.MountComponents.Length);
            foreach (int component in loadout.MountComponents) writer.WriteInt(component);
        }

        public static ShipLoadout ReadShipLoadout(this NetworkReader reader)
        {
            int hull = reader.ReadInt();
            var loadout = ShipLoadout.Empty;
            loadout.Hull = hull;
            loadout.Colour0 = reader.ReadColor32();
            loadout.Colour1 = reader.ReadColor32();
            int length = reader.ReadInt();
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !LoadoutMath.InRange(hull, catalog.Hulls.Length, false) ||
                catalog.Hulls[hull] == null || catalog.Hulls[hull].HullPoints == null ||
                length != catalog.Hulls[hull].HullPoints.Points.Length)
                throw new InvalidDataException("Ship loadout mount count does not match the hull.");
            loadout.MountComponents = new int[length];
            for (int i = 0; i < length; i++) loadout.MountComponents[i] = reader.ReadInt();
            if (!catalog.IsValid(loadout)) throw new InvalidDataException("Received an invalid ship loadout.");
            return loadout;
        }
    }
}
