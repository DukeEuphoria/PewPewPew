using UnityEngine;
using Random = System.Random;

namespace PewPewPew.Core
{
    public enum FirePattern { Shotgun, Sinewave, Spread }

    public static class FireMath
    {
        /// Launch angles (degrees) for one burst. sweepPhase (radians) drives Sinewave and is advanced by the caller.
        public static float[] Angles(FirePattern pattern, int count, float minAngle, float maxAngle, float sweepPhase, Random random)
        {
            var angles = new float[count];
            float middle = (minAngle + maxAngle) * 0.5f;
            float halfRange = (maxAngle - minAngle) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                switch (pattern)
                {
                    case FirePattern.Shotgun:
                        angles[i] = minAngle + (float)random.NextDouble() * (maxAngle - minAngle);
                        break;
                    case FirePattern.Sinewave:
                        angles[i] = middle + halfRange * Mathf.Sin(sweepPhase);
                        break;
                    default:
                        angles[i] = count == 1 ? middle : minAngle + (maxAngle - minAngle) * i / (count - 1);
                        break;
                }
            }
            return angles;
        }
    }
}
