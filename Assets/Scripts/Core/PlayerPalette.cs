using UnityEngine;

namespace PewPewPew.Core
{
    /// The 64 fixed swatches players pick their two ship colours from: an 8x8 grid of seven hue rows and a grey row.
    public static class PlayerPalette
    {
        public const int Columns = 8;
        public const int Rows = 8;
        public const int Count = Columns * Rows;

        private static readonly float[] RowSaturation = { 1f, 0.6f, 0.3f, 1f, 1f, 0.7f, 0.5f };
        private static readonly float[] RowValue = { 1f, 1f, 1f, 0.75f, 0.5f, 0.5f, 0.3f };

        public static Color32 DefaultPrimary => Colour(0 * Columns + 0);
        public static Color32 DefaultSecondary => Colour(Columns * (Rows - 1) + Columns - 1);

        public static Color32 Colour(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            int row = index / Columns;
            int column = index % Columns;
            if (row == Rows - 1)
            {
                byte grey = (byte)Mathf.RoundToInt(255f * column / (Columns - 1));
                return new Color32(grey, grey, grey, 255);
            }

            Color colour = Color.HSVToRGB(column / (float)Columns, RowSaturation[row], RowValue[row]);
            return colour;
        }

        /// Index of the swatch matching the colour, or -1 if it is not a palette colour.
        public static int IndexOf(Color32 colour)
        {
            for (int i = 0; i < Count; i++)
            {
                Color32 candidate = Colour(i);
                if (candidate.r == colour.r && candidate.g == colour.g && candidate.b == colour.b) return i;
            }
            return -1;
        }
    }
}
