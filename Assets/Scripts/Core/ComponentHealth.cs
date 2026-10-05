using UnityEngine;

namespace PewPewPew.Core
{
    /// Current/max pool used for component health, shields and armour.
    public class ComponentHealth
    {
        public ComponentHealth(float max)
        {
            Max = max;
            Current = max;
        }

        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDestroyed => Current <= 0f;
        public float Fraction => Max > 0f ? Current / Max : 0f;

        public void Damage(float amount) => Current = Mathf.Max(0f, Current - amount);

        public void Repair(float amount) => Current = Mathf.Min(Max, Current + amount);

        public void SetMax(float max)
        {
            max = Mathf.Max(0f, max);
            if (Max == max) return;
            float fraction = Fraction;
            Max = max;
            Current = Max * fraction;
        }

        /// For mirroring a value synced from the server.
        public void SetFraction(float fraction) => Current = Max * Mathf.Clamp01(fraction);
    }
}
