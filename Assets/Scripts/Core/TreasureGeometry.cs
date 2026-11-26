using System;
using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Core
{
    public enum TreasureMaterial : byte { Gold, Silver, RedCrystal, GreenCrystal, CyanCrystal, PurpleCrystal }
    public enum TreasureSize : byte { Small, Medium, Large }

    public static class TreasureGeometry
    {
        private const float ContactTolerance = 0.00001f;

        /// Tests a circular footprint against the displaced marching-squares polygons.
        /// Tangency alone does not count as supporting rock.
        public static bool Overlaps(List<Vector2[]> polygons, Vector2 centre, float radius)
        {
            if (polygons == null) throw new ArgumentNullException(nameof(polygons));
            ValidateFootprint(centre, radius);
            if (AsteroidGeometry.Contains(polygons, centre)) return true;
            float threshold = Mathf.Max(0f, radius - ContactTolerance);
            float thresholdSquared = threshold * threshold;
            foreach (Vector2[] polygon in polygons)
            {
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 a = polygon[i], edge = polygon[(i + 1) % polygon.Length] - a;
                    float lengthSquared = edge.sqrMagnitude;
                    float t = lengthSquared > 0f
                        ? Mathf.Clamp01(Vector2.Dot(centre - a, edge) / lengthSquared) : 0f;
                    if ((centre - (a + edge * t)).sqrMagnitude < thresholdSquared) return true;
                }
            }
            return false;
        }

        private static void ValidateFootprint(Vector2 centre, float radius)
        {
            if (!AsteroidGeometry.Finite(centre.x) || !AsteroidGeometry.Finite(centre.y) ||
                !AsteroidGeometry.Finite(radius) || radius <= 0f)
                throw new ArgumentException("Treasure footprint must have a finite centre and positive radius.");
        }

        /// Returns the sole retained supporting component, or -1 if the item is released.
        public static int SupportingComponent(IReadOnlyList<List<Vector2[]>> components,
            IReadOnlyList<bool> retained, Vector2 centre, float radius)
        {
            if (components == null) throw new ArgumentNullException(nameof(components));
            if (retained == null) throw new ArgumentNullException(nameof(retained));
            if (components.Count != retained.Count)
                throw new ArgumentException("Each asteroid component needs a retention decision.");
            ValidateFootprint(centre, radius);
            int support = -1;
            for (int i = 0; i < components.Count; i++)
            {
                if (!Overlaps(components[i], centre, radius)) continue;
                if (support >= 0) return -1;
                support = i;
            }
            return support >= 0 && retained[support] ? support : -1;
        }
    }
}
