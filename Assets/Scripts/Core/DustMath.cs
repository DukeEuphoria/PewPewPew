using UnityEngine;

namespace PewPewPew.Core
{
    public static class DustMath
    {
        /// 0 at birth and death, 1 in between, ramping linearly over fadeTime at each end.
        public static float Fade(float age, float lifetime, float fadeTime)
        {
            if (age <= 0f || age >= lifetime) return 0f;
            if (fadeTime <= 0f) return 1f;
            float fadeIn = age / fadeTime;
            float fadeOut = (lifetime - age) / fadeTime;
            return Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));
        }

        /// 0 until speed reaches startFraction of speedForMax, rising to 1 at speedForMax.
        public static float StreakAmount(float speed, float speedForMax, float startFraction)
        {
            if (speedForMax <= 0f) return 0f;
            float start = speedForMax * Mathf.Clamp01(startFraction);
            if (start >= speedForMax) return speed >= speedForMax ? 1f : 0f;
            return Mathf.Clamp01((speed - start) / (speedForMax - start));
        }

        /// Wraps value into [-limit, limit].
        public static float Wrap(float value, float limit)
        {
            if (limit <= 0f) return 0f;
            return Mathf.Repeat(value + limit, 2f * limit) - limit;
        }
    }
}
