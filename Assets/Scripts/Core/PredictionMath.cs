using UnityEngine;

namespace PewPewPew.Core
{
    public static class PredictionMath
    {
        /// How far ahead of the network position to draw: straight-line flight over the look-ahead time, blended in by blend (0 = network only).
        public static Vector2 Offset(Vector2 velocity, float lookAheadSeconds, float blend, float maxLookAheadSeconds)
        {
            return velocity * (Mathf.Min(lookAheadSeconds, maxLookAheadSeconds) * blend);
        }
    }
}
