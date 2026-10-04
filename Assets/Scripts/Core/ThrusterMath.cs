using UnityEngine;

namespace PewPewPew.Core
{
    public static class ThrusterMath
    {
        /// Moves throttle toward target, taking rampUpTime/rampDownTime seconds to cover the full 0..1 range.
        public static float Ramp(float throttle, float target, float rampUpTime, float rampDownTime, float deltaTime)
        {
            float rampTime = target > throttle ? rampUpTime : rampDownTime;
            float step = rampTime > 0f ? deltaTime / rampTime : 1f;
            return Mathf.MoveTowards(throttle, target, step);
        }
    }
}
