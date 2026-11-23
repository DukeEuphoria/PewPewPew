using System;
using UnityEngine;

namespace PewPewPew.Core
{
    /// A ship's hull and per-point component indices into the ShipCatalog; -1 means an empty point.
    public struct ShipLoadout
    {
        public int Hull;
        public int[] MountComponents;
        public Color32 Colour0;
        public Color32 Colour1;

        public static ShipLoadout Empty => new ShipLoadout
        {
            Colour0 = PlayerPalette.DefaultPrimary,
            Colour1 = PlayerPalette.DefaultSecondary,
        };

        public void SetMountComponent(int mount, int index)
        {
            if (MountComponents == null) throw new InvalidOperationException("The loadout has no mount assignments.");
            if (mount < 0 || mount >= MountComponents.Length) throw new ArgumentOutOfRangeException(nameof(mount));
            int[] updated = (int[])MountComponents.Clone();
            updated[mount] = index;
            MountComponents = updated;
        }
    }

    public static class LoadoutMath
    {
        public static int OccupiedMountAt(int[] components, int shortcut)
        {
            if (components == null || shortcut < 0) return -1;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] < 0) continue;
                if (shortcut-- == 0) return i;
            }
            return -1;
        }

        public static bool InRange(int index, int count, bool allowNone)
        {
            return index < count && index >= (allowNone ? -1 : 0);
        }

        /// Like Cycle, but skips entries that are not available; stays put if none is.
        public static int CycleAvailable(int index, int count, int step, bool allowNone, Func<int, bool> available)
        {
            int candidate = index;
            for (int i = 0; i <= count; i++)
            {
                candidate = Cycle(candidate, count, step, allowNone);
                if (candidate < 0 || available(candidate)) return candidate;
            }
            return index;
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
