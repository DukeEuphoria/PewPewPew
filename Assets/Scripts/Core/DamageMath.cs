using UnityEngine;

namespace PewPewPew.Core
{
    public static class DamageMath
    {
        /// Speed scaled by the cosine of the angle between velocity and the contact normal; glancing hits do little.
        public static float Impact(Vector2 relativeVelocity, Vector2 contactNormal, float multiplier)
        {
            float speed = relativeVelocity.magnitude;
            if (speed < Mathf.Epsilon) return 0f;

            float cosine = Mathf.Abs(Vector2.Dot(relativeVelocity / speed, contactNormal.normalized));
            return speed * cosine * multiplier;
        }

        /// Shield absorbs at most maxPerImpact and no more than its health; returns the damage that gets through.
        public static float AbsorbShield(float damage, ComponentHealth shield, float maxPerImpact)
        {
            float absorbed = Mathf.Min(damage, shield.Current, maxPerImpact);
            shield.Damage(absorbed);
            return damage - absorbed;
        }

        /// Tests an impact direction relative to the shield centre against the mount's Up direction.
        public static bool ShieldCovers(Vector2 forward, Vector2 impactOffset, float halfAngle)
        {
            if (halfAngle >= 180f || impactOffset.sqrMagnitude < 1e-10f) return true;
            if (forward.sqrMagnitude < 1e-10f) return false;
            return Vector2.Angle(forward, impactOffset) <= halfAngle + 0.0001f;
        }

        /// Armour blocks up to its current value per impact and wears down by wearRatio of what it blocked.
        public static float AbsorbArmour(float damage, ComponentHealth armour, float wearRatio, float strengthMultiplier = 1f)
        {
            strengthMultiplier = Mathf.Max(1f, strengthMultiplier);
            float blocked = Mathf.Min(damage, armour.Current * strengthMultiplier);
            armour.Damage(blocked * wearRatio / strengthMultiplier);
            return damage - blocked;
        }

        /// 1 at the mount point, falling linearly to 0 at radius; 0 for systems without a point.
        public static float ProximityWeight(Vector2 hitPoint, Vector2? point, float radius)
        {
            if (!point.HasValue) return 0f;
            return Mathf.Max(0f, 1f - Vector2.Distance(hitPoint, point.Value) / radius);
        }
    }
}
