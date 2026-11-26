using System;
using System.Collections.Generic;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.World
{
    [Serializable]
    internal struct TreasureDeposit
    {
        public ushort Id;
        public TreasureMaterial Material;
        public TreasureSize Size;
        public Vector2 Position;
        public float Radius;
        public bool Peeking;
    }

    internal static class AsteroidTreasure
    {
        internal const int MaximumDeposits = 16;
        private const uint SeedSalt = 0xD6E8FEB8u;
        private const int PlacementAttempts = 96;
        private const int MaterialCount = 6;
        private const uint SizeCount = 3;
        private const float SmallRadius = 0.25f;
        private const float MediumRadius = 0.4f;
        private const float LargeRadius = 0.6f;
        private const float UnitDivisor = 16777216f;

        internal static List<TreasureDeposit> Generate(AsteroidGeometry field, Vector2 spacing, uint seed,
            float chance, int maximum, float peekingChance, float[] weights)
        {
            if (!AsteroidGeometry.Finite(chance) || chance < 0f || chance > 1f ||
                !AsteroidGeometry.Finite(peekingChance) || peekingChance < 0f || peekingChance > 1f ||
                maximum < 0 || maximum > MaximumDeposits ||
                weights == null || weights.Length != MaterialCount)
                throw new ArgumentException("Invalid asteroid treasure generation settings.");
            float totalWeight = 0f;
            foreach (float weight in weights)
            {
                if (!AsteroidGeometry.Finite(weight) || weight < 0f)
                    throw new ArgumentException("Treasure material weights must be finite and nonnegative.");
                totalWeight += weight;
            }
            if (!AsteroidGeometry.Finite(totalWeight) || totalWeight <= 0f)
                throw new ArgumentException("Treasure material weights must have a positive finite sum.");

            var result = new List<TreasureDeposit>();
            uint state = seed ^ SeedSalt;
            if (Unit(ref state) >= chance || maximum == 0) return result;
            int count = 1 + (int)(AsteroidGeometry.Next(ref state) % (uint)maximum);
            List<Vector2[]> paths = AsteroidMesh.Paths(field, spacing, out _, out _);
            for (int i = 0; i < count; i++)
            {
                var size = (TreasureSize)(AsteroidGeometry.Next(ref state) % SizeCount);
                float radius = Radius(size) * Mathf.Min(spacing.x, spacing.y);
                float selection = Unit(ref state) * totalWeight;
                int material = weights.Length - 1;
                for (int j = 0; j < weights.Length; j++)
                {
                    selection -= weights[j];
                    if (selection < 0f) { material = j; break; }
                }
                bool peeking = Unit(ref state) < peekingChance;
                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    Vector2 sample = new Vector2(Unit(ref state) * (field.Width - 1),
                        Unit(ref state) * (field.Height - 1));
                    Vector2 centre = Vector2.Scale(sample - new Vector2(field.Width - 1, field.Height - 1) * 0.5f, spacing);
                    if (!AsteroidGeometry.Contains(paths, centre)) continue;
                    bool overlaps = false;
                    foreach (TreasureDeposit existing in result)
                    {
                        float separation = existing.Radius + radius;
                        if ((existing.Position - centre).sqrMagnitude < separation * separation)
                        { overlaps = true; break; }
                    }
                    if (overlaps) continue;
                    result.Add(new TreasureDeposit
                    {
                        Id = (ushort)i, Material = (TreasureMaterial)material, Size = size,
                        Position = centre, Radius = radius, Peeking = peeking
                    });
                    break;
                }
            }
            return result;
        }

        internal static float Radius(TreasureSize size) => size switch
        {
            TreasureSize.Small => SmallRadius,
            TreasureSize.Medium => MediumRadius,
            TreasureSize.Large => LargeRadius,
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };

        private static float Unit(ref uint state) => (AsteroidGeometry.Next(ref state) >> 8) / UnitDivisor;
    }
}
