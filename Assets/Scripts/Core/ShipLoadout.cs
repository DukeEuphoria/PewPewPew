namespace PewPewPew.Core
{
    /// A ship's chosen components as indices into the ShipCatalog; sub system slots use -1 for empty.
    public struct ShipLoadout
    {
        public const int SubSystemSlots = 4;

        public int Hull;
        public int Shield;
        public int MainGun;
        public int SecondaryGun;
        public int Thruster;
        public int SubSystem0;
        public int SubSystem1;
        public int SubSystem2;
        public int SubSystem3;

        public static ShipLoadout Empty => new ShipLoadout { SubSystem0 = -1, SubSystem1 = -1, SubSystem2 = -1, SubSystem3 = -1 };

        public int GetSubSystem(int slot)
        {
            switch (slot)
            {
                case 0: return SubSystem0;
                case 1: return SubSystem1;
                case 2: return SubSystem2;
                case 3: return SubSystem3;
                default: return -1;
            }
        }

        public void SetSubSystem(int slot, int index)
        {
            switch (slot)
            {
                case 0: SubSystem0 = index; break;
                case 1: SubSystem1 = index; break;
                case 2: SubSystem2 = index; break;
                case 3: SubSystem3 = index; break;
            }
        }
    }

    public static class LoadoutMath
    {
        public static bool InRange(int index, int count, bool allowNone)
        {
            return index < count && index >= (allowNone ? -1 : 0);
        }

        /// Steps through 0..count-1 (and -1 for none when allowed), wrapping at both ends.
        public static int Cycle(int index, int count, int step, bool allowNone)
        {
            int first = allowNone ? -1 : 0;
            int options = count - first;
            if (options <= 0) return first;

            int offset = ((index - first + step) % options + options) % options;
            return first + offset;
        }
    }
}
