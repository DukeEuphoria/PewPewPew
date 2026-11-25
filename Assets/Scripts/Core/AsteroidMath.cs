using UnityEngine;

namespace PewPewPew.Core
{
    public static class AsteroidMath
    {
        /// Spawn density 0..1 from smooth noise; higher sharpness makes clusters tighter and gaps emptier. position must be non-negative.
        public static float Density(Vector2 position, float scale, float seed, float sharpness)
        {
            float noise = Mathf.PerlinNoise(position.x / scale + seed, position.y / scale + seed);
            return Mathf.Pow(noise, sharpness);
        }
    }
}
