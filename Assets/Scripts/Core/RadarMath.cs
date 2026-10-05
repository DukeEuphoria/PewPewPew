using System;
using UnityEngine;

namespace PewPewPew.Core
{
    public static class RadarMath
    {
        public static bool Crosses(double previous, double current, double first, double period)
        {
            if (period <= 0d || current < first || current < previous) return false;
            if (previous < first) return true;
            return Math.Floor((current - first) / period) > Math.Floor((previous - first) / period);
        }

        public static bool PulseCrosses(double previous, double current, float distance, float speed, float pulsesPerSecond)
        {
            return speed > 0f && pulsesPerSecond > 0f &&
                Crosses(previous, current, distance / speed, 1d / pulsesPerSecond);
        }

        public static bool SweepCrosses(double previous, double current, float angle, float halfAngle, float degreesPerSecond)
        {
            if (degreesPerSecond <= 0f || halfAngle <= 0f) return false;
            double start = previous * degreesPerSecond;
            double end = current * degreesPerSecond;
            if (halfAngle >= 180f) return Crosses(start, end, Mathf.Repeat(angle, 360f), 360d);
            if (Mathf.Abs(angle) > halfAngle) return false;
            double offset = angle + halfAngle;
            double period = 4d * halfAngle;
            return Crosses(start, end, offset, period) || Crosses(start, end, period - offset, period);
        }

        public static float SweepAngle(double time, float halfAngle, float degreesPerSecond)
        {
            if (halfAngle >= 180f) return (float)(time * degreesPerSecond % 360d);
            if (halfAngle <= 0f) return 0f;
            double period = 4d * halfAngle;
            double phase = time * degreesPerSecond % period;
            return (float)(phase <= 2d * halfAngle ? phase - halfAngle : 3d * halfAngle - phase);
        }

        public static float Fade(double age, float lifetime)
        {
            return lifetime <= 0f ? 0f : Mathf.Clamp01(1f - (float)(age / lifetime));
        }
    }
}