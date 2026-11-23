using System;
using UnityEngine;

namespace PewPewPew.Core
{
    public class PowerBank
    {
        private sealed class Segment
        {
            public Segment(float max)
            {
                Max = max;
                Stored = max;
                Enabled = true;
            }

            public float Max { get; }
            public float Stored { get; set; }
            public bool Enabled { get; set; }
        }

        private readonly Segment[] m_Segments;

        public PowerBank(float max)
        {
            m_Segments = new[] { new Segment(RequireAmount(max, nameof(max))) };
        }

        private PowerBank(float[] segmentMaxima)
        {
            m_Segments = new Segment[segmentMaxima.Length];
            for (int i = 0; i < segmentMaxima.Length; i++)
                m_Segments[i] = new Segment(RequireAmount(segmentMaxima[i], nameof(segmentMaxima)));
        }

        /// <summary>
        /// Creates a bank ordered as hull first, followed by storage segments in supplied order.
        /// With no capacities, the bank has no segments.
        /// </summary>
        public static PowerBank CreateSegmented(params float[] segmentMaxima) =>
            new PowerBank(segmentMaxima ?? throw new ArgumentNullException(nameof(segmentMaxima)));

        public int SegmentCount => m_Segments.Length;

        /// Total stored charge, including charge in disabled segments.
        public float Stored => SumStored(false);

        /// Total capacity, including disabled segments.
        public float Max => SumMax(false);

        public float Fraction => Max > 0f ? Stored / Max : 0f;

        /// Charge available to draw from enabled segments.
        public float UsableStored => SumStored(true);

        /// Capacity available to charge in enabled segments.
        public float UsableMax => SumMax(true);

        public float UsableFraction => UsableMax > 0f ? UsableStored / UsableMax : 0f;

        public float GetSegmentStored(int index) => m_Segments[index].Stored;
        public float GetSegmentMax(int index) => m_Segments[index].Max;
        public float GetSegmentFraction(int index)
        {
            Segment segment = m_Segments[index];
            return segment.Max > 0f ? segment.Stored / segment.Max : 0f;
        }

        public bool IsSegmentEnabled(int index) => m_Segments[index].Enabled;

        /// Disabling a segment preserves its charge but excludes it from bank charge and draw operations.
        public void SetSegmentEnabled(int index, bool enabled) => m_Segments[index].Enabled = enabled;

        /// Sets the fraction of available capacity, distributing charge hull-first and preserving disabled segments.
        public void SetFraction(float fraction)
        {
            if (float.IsNaN(fraction) || float.IsInfinity(fraction))
                throw new ArgumentOutOfRangeException(nameof(fraction));
            float remaining = UsableMax * Mathf.Clamp01(fraction);
            for (int i = 0; i < m_Segments.Length; i++)
            {
                Segment segment = m_Segments[i];
                if (!segment.Enabled) continue;
                segment.Stored = Mathf.Min(segment.Max, remaining);
                remaining -= segment.Stored;
            }
        }

        /// Charges hull-first, then storage segments in their configured order.
        public void Charge(float amount)
        {
            float remaining = RequireAmount(amount, nameof(amount));
            for (int i = 0; i < m_Segments.Length && remaining > 0f; i++)
            {
                Segment segment = m_Segments[i];
                if (!segment.Enabled) continue;

                float accepted = Mathf.Min(segment.Max - segment.Stored, remaining);
                segment.Stored += accepted;
                remaining -= accepted;
            }
        }

        /// All or nothing: used for activations, which must not happen on partial power.
        public bool TryDraw(float amount)
        {
            RequireAmount(amount, nameof(amount));
            if (UsableStored < amount) return false;
            DrawFromSegments(amount);
            return true;
        }

        /// Takes whatever is available up to amount and returns what was taken.
        public float Draw(float amount)
        {
            float remaining = RequireAmount(amount, nameof(amount));
            float drawn = Mathf.Min(UsableStored, remaining);
            DrawFromSegments(drawn);
            return drawn;
        }

        private void DrawFromSegments(float amount)
        {
            float remaining = amount;
            for (int i = m_Segments.Length - 1; i >= 0 && remaining > 0f; i--)
            {
                Segment segment = m_Segments[i];
                if (!segment.Enabled) continue;

                float taken = Mathf.Min(segment.Stored, remaining);
                segment.Stored -= taken;
                remaining -= taken;
            }
        }

        private float SumStored(bool enabledOnly)
        {
            float total = 0f;
            for (int i = 0; i < m_Segments.Length; i++)
                if (!enabledOnly || m_Segments[i].Enabled)
                    total += m_Segments[i].Stored;
            return total;
        }

        private float SumMax(bool enabledOnly)
        {
            float total = 0f;
            for (int i = 0; i < m_Segments.Length; i++)
                if (!enabledOnly || m_Segments[i].Enabled)
                    total += m_Segments[i].Max;
            return total;
        }

        private static float RequireAmount(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }
}
