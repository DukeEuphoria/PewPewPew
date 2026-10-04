using UnityEngine;

namespace PewPewPew.Core
{
    public class PowerBank
    {
        public PowerBank(float max)
        {
            Max = max;
        }

        public float Max { get; }
        public float Stored { get; private set; }

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
