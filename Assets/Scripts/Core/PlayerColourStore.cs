using UnityEngine;

namespace PewPewPew.Core
{
    /// Where a player's default ship colours are kept between sessions. Local for now; swap Current for a Steam-backed store later.
    public interface IPlayerColourStore
    {
        void Load(out Color32 primary, out Color32 secondary);
        void Save(Color32 primary, Color32 secondary);
    }

    public class PlayerPrefsColourStore : IPlayerColourStore
    {
        private const string PrimaryKey = "PlayerColour0";
        private const string SecondaryKey = "PlayerColour1";

        public void Load(out Color32 primary, out Color32 secondary)
        {
            primary = Read(PrimaryKey, PlayerPalette.DefaultPrimary);
            secondary = Read(SecondaryKey, PlayerPalette.DefaultSecondary);
        }

        public void Save(Color32 primary, Color32 secondary)
        {
            PlayerPrefs.SetInt(PrimaryKey, Pack(primary));
            PlayerPrefs.SetInt(SecondaryKey, Pack(secondary));
            PlayerPrefs.Save();
        }

        private static Color32 Read(string key, Color32 fallback)
        {
            if (!PlayerPrefs.HasKey(key)) return fallback;

            int packed = PlayerPrefs.GetInt(key);
            return new Color32((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed, 255);
        }

        private static int Pack(Color32 colour) => (colour.r << 16) | (colour.g << 8) | colour.b;
    }

    public static class PlayerColourStore
    {
        public static IPlayerColourStore Current { get; set; } = new PlayerPrefsColourStore();
    }
}
