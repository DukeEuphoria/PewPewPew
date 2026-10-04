using System;
using UnityEngine;
using Random = System.Random;

namespace PewPewPew.Core
{
    public enum FirePattern { Shotgun, Sinewave, Spread }

    /// One bullet of a burst: which emission point it leaves from and its angle (degrees) relative to that point.
    public readonly struct Shot
    {
        public Shot(int pointIndex, float angle)
        {
            PointIndex = pointIndex;
            Angle = angle;
        }

        public int PointIndex { get; }
        public float Angle { get; }
    }

    public static class FireMath
    {
        public static Shot[] Burst(FirePattern pattern, int bulletCount, float minAngle, float maxAngle, float sweepPhase,
            int burstIndex, int pointsUsed, int pointsConsidered, Random random)
        {
            float[] angles = Angles(pattern, bulletCount, minAngle, maxAngle, sweepPhase, random);
            int[] points = EmissionPoints(burstIndex, pointsUsed, pointsConsidered);

            var shots = new Shot[bulletCount];
            int next = 0;
            for (int i = 0; i < points.Length; i++)
            {
                int count = BulletsAtPoint(bulletCount, pointsUsed, i);
                for (int j = 0; j < count; j++, next++) shots[next] = new Shot(points[i], angles[next]);
            }
            return shots;
        }

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

        /// Emission points for a burst: pointsUsed consecutive points out of pointsConsidered, advancing each burst.
        public static int[] EmissionPoints(int burstIndex, int pointsUsed, int pointsConsidered)
        {
            var points = new int[pointsUsed];
            for (int i = 0; i < pointsUsed; i++) points[i] = (burstIndex * pointsUsed + i) % pointsConsidered;
            return points;
        }

        /// Splits bulletCount evenly across points; earlier points take the remainder.
        public static int BulletsAtPoint(int bulletCount, int pointsUsed, int pointIndex)
        {
            return bulletCount / pointsUsed + (pointIndex < bulletCount % pointsUsed ? 1 : 0);
        }
    }
}
