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
