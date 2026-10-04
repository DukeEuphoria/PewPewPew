using UnityEngine;

namespace PewPewPew.Core
{
    public class PowerBank
    {
        public PowerBank(float max)
        {
            Max = max;
            Stored = max;
        }

        public float Max { get; }
        public float Stored { get; private set; }
        public float Fraction => Max > 0f ? Stored / Max : 0f;

        /// For mirroring a value synced from the server.
        public void SetFraction(float fraction) => Stored = Max * Mathf.Clamp01(fraction);

        public void Charge(float amount) => Stored = Mathf.Min(Max, Stored + amount);

        /// All or nothing: used for activations, which must not happen on partial power.
        public bool TryDraw(float amount)
        {
            if (Stored < amount) return false;
            Stored -= amount;
            return true;
        }

        /// Takes whatever is available up to amount and returns what was taken.
        public float Draw(float amount)
        {
            float drawn = Mathf.Min(Stored, amount);
            Stored -= drawn;
            return drawn;
        }
    }
}
