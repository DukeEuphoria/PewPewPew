using UnityEngine;

namespace PewPewPew.Core
{
    public enum HudState : byte { Off, Active, Cooling, Ready, Destroyed }

    /// The server-owned state the owning player's HUD shows, quantised to bytes for syncing.
    public struct ShipVitals
    {
        public byte Armour;
        public byte Shield;
        public byte Power;
        public byte Level0, Level1, Level2, Level3;
        public byte State0, State1, State2, State3;

        public byte GetLevel(int slot)
        {
            switch (slot)
            {
                case 0: return Level0;
                case 1: return Level1;
                case 2: return Level2;
                default: return Level3;
            }
        }

        public HudState GetState(int slot)
        {
            switch (slot)
            {
                case 0: return (HudState)State0;
                case 1: return (HudState)State1;
                case 2: return (HudState)State2;
                default: return (HudState)State3;
            }
        }

        public void SetSubSystem(int slot, float level, HudState state)
        {
            byte quantised = VitalsMath.ToByte(level);
            switch (slot)
            {
                case 0: Level0 = quantised; State0 = (byte)state; break;
                case 1: Level1 = quantised; State1 = (byte)state; break;
                case 2: Level2 = quantised; State2 = (byte)state; break;
                case 3: Level3 = quantised; State3 = (byte)state; break;
            }
        }
    }

    public static class VitalsMath
    {
        public static byte ToByte(float fraction) => (byte)Mathf.RoundToInt(Mathf.Clamp01(fraction) * 255f);

        public static float FromByte(byte value) => value / 255f;
    }
}
