using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Feeds a player's two colours to the hull shader, which swaps its key colours for them.
    public static class ShipColours
    {
        private static readonly int Colour0Id = Shader.PropertyToID("_PlayerColour0");
        private static readonly int Colour1Id = Shader.PropertyToID("_PlayerColour1");

        public static void Apply(GameObject root, ShipLoadout loadout)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(Colour0Id, loadout.Colour0);
                block.SetColor(Colour1Id, loadout.Colour1);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
