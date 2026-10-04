using System;
using UnityEngine;
using Random = System.Random;
namespace PewPewPew.Core
{
    public static class AsteroidMath
    {
        private const int MinSplitSize = 2;
        /// Spawn density 0..1 from smooth noise; higher sharpness makes clusters tighter and gaps emptier. position must be non-negative.
        public static float Density(Vector2 position, float scale, float seed, float sharpness)
        {
            float noise = Mathf.PerlinNoise(position.x / scale + seed, position.y / scale + seed);
            return Mathf.Pow(noise, sharpness);
        }

        /// Random fragment sizes that sum to size, 2..size pieces, each at least 1. Size 1 does not split.
        public static int[] Split(int size, Random random)
        {
            if (size < MinSplitSize) return Array.Empty<int>();

            int pieceCount = random.Next(MinSplitSize, size + 1);
            var pieces = new int[pieceCount];
            Array.Fill(pieces, 1);
            for (int i = pieceCount; i < size; i++) pieces[random.Next(pieceCount)]++;
            return pieces;
        }
    }
}
