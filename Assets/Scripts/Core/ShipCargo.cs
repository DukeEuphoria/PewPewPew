using System;

namespace PewPewPew.Core
{
    public sealed class ShipCargo
    {
        public const int MaterialCount = 6;
        public const int SmallCapacity = 16;
        public const int MediumCapacity = 25;
        public const int LargeCapacity = 49;
        private readonly int[] m_Capacities;
        private readonly byte[] m_Amounts;

        public ShipCargo(int[] capacities)
        {
            if (capacities == null) throw new ArgumentNullException(nameof(capacities));
            m_Capacities = (int[])capacities.Clone();
            m_Amounts = new byte[checked(capacities.Length * MaterialCount)];
            foreach (int capacity in capacities)
                if (capacity < 0 || capacity > LargeCapacity)
                    throw new ArgumentOutOfRangeException(nameof(capacities));
        }

        public int MountCount => m_Capacities.Length;
        public int Capacity(int mount) => m_Capacities[mount];
        public int Amount(int mount, TreasureMaterial material) => m_Amounts[Offset(mount, material)];
        public int Stored(int mount)
        {
            if (mount < 0 || mount >= MountCount) throw new ArgumentOutOfRangeException(nameof(mount));
            int total = 0;
            for (int i = 0; i < MaterialCount; i++) total += m_Amounts[mount * MaterialCount + i];
            return total;
        }

        public int Free
        {
            get
            {
                int free = 0;
                for (int i = 0; i < MountCount; i++) free = checked(free + Capacity(i) - Stored(i));
                return free;
            }
        }

        public static int Units(TreasureSize size) => size switch
        {
            TreasureSize.Small => 1,
            TreasureSize.Medium => 2,
            TreasureSize.Large => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };

        public bool TryAdd(TreasureMaterial material, TreasureSize size)
        {
            int units = Units(size);
            if ((int)material < 0 || (int)material >= MaterialCount) throw new ArgumentOutOfRangeException(nameof(material));
            if (Free < units) return false;
            for (int mount = 0; mount < MountCount && units > 0; mount++)
            {
                int take = Math.Min(units, Capacity(mount) - Stored(mount));
                int index = Offset(mount, material);
                m_Amounts[index] = checked((byte)(m_Amounts[index] + take));
                units -= take;
            }
            return true;
        }

        public int Total(TreasureMaterial material)
        {
            if ((int)material < 0 || (int)material >= MaterialCount) throw new ArgumentOutOfRangeException(nameof(material));
            int total = 0;
            for (int mount = 0; mount < MountCount; mount++) total = checked(total + Amount(mount, material));
            return total;
        }

        public byte[] Snapshot() => (byte[])m_Amounts.Clone();

        public void ApplySnapshot(byte[] amounts)
        {
            if (amounts == null || amounts.Length != m_Amounts.Length)
                throw new ArgumentException("Cargo snapshot does not match the ship loadout.", nameof(amounts));
            for (int mount = 0; mount < MountCount; mount++)
            {
                int total = 0;
                for (int material = 0; material < MaterialCount; material++)
                    total += amounts[mount * MaterialCount + material];
                if (total > Capacity(mount)) throw new ArgumentException("Cargo snapshot exceeds store capacity.", nameof(amounts));
            }
            Array.Copy(amounts, m_Amounts, amounts.Length);
        }

        public void Clear() => Array.Clear(m_Amounts, 0, m_Amounts.Length);

        public static int DropUnits(int amount, float fraction)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (float.IsNaN(fraction) || float.IsInfinity(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(fraction));
            return (int)decimal.Round(amount * (decimal)fraction, 0, MidpointRounding.AwayFromZero);
        }

        private int Offset(int mount, TreasureMaterial material)
        {
            if (mount < 0 || mount >= MountCount) throw new ArgumentOutOfRangeException(nameof(mount));
            if ((int)material < 0 || (int)material >= MaterialCount) throw new ArgumentOutOfRangeException(nameof(material));
            return mount * MaterialCount + (int)material;
        }
    }
}
