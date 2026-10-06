using UnityEngine;

namespace PewPewPew.Core
{
    public static class GravityMath
    {
        /// Force on a body pulled toward a source. softening keeps the force finite at r=0; zero beyond maxDistance.
        public static Vector2 Force(Vector2 toSource, float sourceMass, float bodyMass, float g, float softening, float maxDistance)
        {
            float sqrDistance = toSource.sqrMagnitude;
            if (sqrDistance > maxDistance * maxDistance || sqrDistance < Mathf.Epsilon) return Vector2.zero;

            float magnitude = g * sourceMass * bodyMass / (sqrDistance + softening * softening);
            return toSource.normalized * magnitude;
        }

        /// Speed for a circular orbit at distance under the softened force in Force.
        public static float CircularSpeed(float sourceMass, float g, float softening, float distance)
        {
            if (distance <= 0f) return 0f;
            return Mathf.Sqrt(g * sourceMass * distance / (distance * distance + softening * softening));
        }

        /// Radial (outward) and tangential speed at trueAnomaly on an orbit of the given eccentricity, scaled from the
        /// circular speed at the current distance. trueAnomaly is in radians; 0 is periapsis.
        public static Vector2 OrbitVelocity(float circularSpeed, float eccentricity, float trueAnomaly)
        {
            float shape = 1f + eccentricity * Mathf.Cos(trueAnomaly);
            if (shape <= 0f) return new Vector2(0f, circularSpeed);

            float root = Mathf.Sqrt(shape);
            return new Vector2(circularSpeed * eccentricity * Mathf.Sin(trueAnomaly) / root, circularSpeed * root);
        }

        /// Inward force that grows with the square of the distance beyond radius, fencing in the world.
        public static Vector2 EdgeForce(Vector2 offsetFromCenter, float radius, float strength, float bodyMass)
        {
            float distance = offsetFromCenter.magnitude;
            if (distance <= radius) return Vector2.zero;

            float excess = (distance - radius) / radius;
            return -offsetFromCenter / distance * (strength * bodyMass * excess * excess);
        }

        /// Quadratic drag: opposes velocity with magnitude coefficient * speed^2.
        public static Vector2 Drag(Vector2 velocity, float coefficient)
        {
            return -velocity * (coefficient * velocity.magnitude);
        }
    }
}
