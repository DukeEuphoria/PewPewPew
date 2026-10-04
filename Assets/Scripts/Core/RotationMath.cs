using UnityEngine;

namespace PewPewPew.Core
{
    public static class RotationMath
    {
        /// Angular acceleration (deg/s^2) that turns toward a target at the fastest rate that still lets it stop on the mark.
        public static float Acceleration(float angleError, float angularVelocity, float maxAcceleration, float deltaTime)
        {
            float desiredVelocity = Mathf.Sign(angleError) * Mathf.Sqrt(2f * maxAcceleration * Mathf.Abs(angleError));
            return Mathf.Clamp((desiredVelocity - angularVelocity) / deltaTime, -maxAcceleration, maxAcceleration);
        }
    }

    public static class SpeedMath
    {
        /// Blends from atRest to atFullSpeed as speed rises from 0 to speedForFull; used for camera zoom and dust density.
        public static float Blend(float speed, float atRest, float atFullSpeed, float speedForFull)
        {
            return Mathf.Lerp(atRest, atFullSpeed, Mathf.Clamp01(speed / speedForFull));
        }
    }
}
