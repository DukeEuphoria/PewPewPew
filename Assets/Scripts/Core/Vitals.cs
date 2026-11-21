using UnityEngine;

namespace PewPewPew.Core
{
    public enum HudState : byte { Off, Active, Cooling, Ready, Destroyed }

    /// The server-owned state the owning player's HUD shows, quantised to bytes for syncing.
    public struct ShipVitals
    {
        public byte Armour;
        public byte Power;
        public byte HullPower;
        public byte[] Shields;
        public byte[] ShieldStates;
        public byte[] SubSystemLevels;
        public byte[] SubSystemStates;
    }

    public static class VitalsMath
    {
        public static byte ToByte(float fraction) => (byte)Mathf.RoundToInt(Mathf.Clamp01(fraction) * byte.MaxValue);

        public static float FromByte(byte value) => value / (float)byte.MaxValue;

        public static float PowerSegmentFraction(float stored, float lowerCapacity, float capacity) =>
            capacity > 0f ? Mathf.Clamp01((stored - lowerCapacity) / capacity) : 0f;
    }
}
