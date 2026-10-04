using UnityEngine;

namespace PewPewPew.Core
{
    public static class OrbitMath
    {
        /// Position relative to the parent, which sits at an ellipse focus. Requires 0 < minAltitude <= maxAltitude and period > 0.
        /// phaseAngle is the starting mean anomaly; precessionRate rotates the whole ellipse (radians/second).
        public static Vector2 Position(float minAltitude, float maxAltitude, float phaseAngle, float precessionRate, float period, float time)
        {
            float semiMajor = (minAltitude + maxAltitude) * 0.5f;
            float eccentricity = (maxAltitude - minAltitude) / (maxAltitude + minAltitude);
            float meanAnomaly = phaseAngle + Mathf.PI * 2f * time / period;
            float eccentricAnomaly = SolveKepler(meanAnomaly, eccentricity);

            float x = semiMajor * (Mathf.Cos(eccentricAnomaly) - eccentricity);
            float y = semiMajor * Mathf.Sqrt(1f - eccentricity * eccentricity) * Mathf.Sin(eccentricAnomaly);

            float rotation = precessionRate * time;
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            return new Vector2(x * cos - y * sin, x * sin + y * cos);
        }

        private static float SolveKepler(float meanAnomaly, float eccentricity)
        {
            float anomaly = meanAnomaly;
            for (int i = 0; i < 8; i++)
            {
                anomaly -= (anomaly - eccentricity * Mathf.Sin(anomaly) - meanAnomaly) / (1f - eccentricity * Mathf.Cos(anomaly));
            }
            return anomaly;
        }
    }
}
