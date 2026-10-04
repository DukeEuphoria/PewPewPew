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

        /// Armour blocks up to its current value per impact and wears down by wearRatio of what it blocked.
        public static float AbsorbArmour(float damage, ComponentHealth armour, float wearRatio)
        {
            float blocked = Mathf.Min(damage, armour.Current);
            armour.Damage(blocked * wearRatio);
            return damage - blocked;
        }

        /// 1 at an emission point, falling linearly to 0 at radius; 0 for systems without emission points.
        public static float ProximityWeight(Vector2 hitPoint, Vector2[] emissionPoints, float radius)
        {
            float best = 0f;
            foreach (Vector2 point in emissionPoints)
            {
                best = Mathf.Max(best, 1f - Vector2.Distance(hitPoint, point) / radius);
            }
            return best;
        }
    }
}
