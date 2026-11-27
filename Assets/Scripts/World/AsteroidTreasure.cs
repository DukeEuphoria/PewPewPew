using System;
using System.Collections.Generic;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.World
{
    [CreateAssetMenu(menuName = "PewPewPew/Asteroid Treasure")]
    public sealed class AsteroidTreasure : ScriptableObject
    {
        [Serializable]
        public sealed class TreasureType
        {
            [SerializeField] private TreasureMaterial m_Material;
            [SerializeField, Min(0f)] private float m_Weight = 1f;
            [SerializeField] private List<Treasure> m_Prefabs = new List<Treasure>();

            public TreasureMaterial Material => m_Material;
            public float Weight => m_Weight;
            public IReadOnlyList<Treasure> Prefabs => m_Prefabs;
        }

        internal const int MaximumDeposits = 16;
        internal const uint SeedSalt = 0xD6E8FEB8u;
        private const float UnitDivisor = 16777216f;
        private const string ResourcePath = "AsteroidTreasure";
        private const float DefaultChance = 0.7f;
        private const int DefaultMaximum = 3;
        private const float DefaultPeekingChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float m_Chance = DefaultChance;
        [SerializeField, Range(0, MaximumDeposits)] private int m_Maximum = DefaultMaximum;
        [SerializeField, Range(0f, 1f)] private float m_PeekingChance = DefaultPeekingChance;
        [SerializeField, Tooltip("Weights for small, medium and large prefabs, in that order.")]
        private float[] m_SizeWeights = { 1f, 1f, 1f };
        [SerializeField] private List<TreasureType> m_Types = new List<TreasureType>();
        private static AsteroidTreasure s_Instance;

        public float Chance => m_Chance;
        public int Maximum => m_Maximum;
        public float PeekingChance => m_PeekingChance;
        public IReadOnlyList<float> SizeWeights => m_SizeWeights;
        public IReadOnlyList<TreasureType> Types => m_Types;

        public static AsteroidTreasure Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Resources.Load<AsteroidTreasure>(ResourcePath);
                    if (s_Instance == null) throw new InvalidOperationException($"Missing treasure catalog at Resources/{ResourcePath}.");
                }
                return s_Instance;
            }
        }

        internal List<Treasure> Generate(ref uint state)
        {
            float total = Validate();
            var result = new List<Treasure>();
            if (Unit(ref state) >= m_Chance || m_Maximum == 0) return result;
            int count = 1 + (int)(AsteroidGeometry.Next(ref state) % (uint)m_Maximum);
            for (int i = 0; i < count; i++)
            {
                float selection = Unit(ref state) * total;
                TreasureType selected = null;
                foreach (TreasureType type in m_Types)
                {
                    if (type.Weight <= 0f) continue;
                    selected = type;
                    selection -= type.Weight;
                    if (selection < 0f) break;
                }
                float sizeTotal = 0f;
                foreach (Treasure prefab in selected.Prefabs) sizeTotal += m_SizeWeights[(int)prefab.Size];
                float sizeSelection = Unit(ref state) * sizeTotal;
                Treasure chosen = null;
                foreach (Treasure prefab in selected.Prefabs)
                {
                    float weight = m_SizeWeights[(int)prefab.Size];
                    if (weight <= 0f) continue;
                    chosen = prefab;
                    sizeSelection -= weight;
                    if (sizeSelection < 0f) break;
                }
                result.Add(chosen);
            }
            return result;
        }

        public Treasure Prefab(TreasureMaterial material, TreasureSize size)
        {
            if (m_Types == null) throw new InvalidOperationException("The treasure catalog has no types.");
            foreach (TreasureType type in m_Types)
                if (type != null && type.Material == material && type.Prefabs != null)
                    foreach (Treasure prefab in type.Prefabs)
                        if (prefab != null && prefab.Material == material && prefab.Size == size) return prefab;
            throw new ArgumentOutOfRangeException(nameof(size), $"No treasure prefab for {material}/{size}.");
        }

        internal Treasure DropPrefab(TreasureMaterial material, int remaining, System.Random random)
        {
            Validate();
            var eligible = new List<Treasure>();
            foreach (TreasureType type in m_Types)
                if (type.Material == material)
                    foreach (Treasure prefab in type.Prefabs)
                        if (prefab.Units <= remaining) eligible.Add(prefab);
            if (eligible.Count == 0) throw new InvalidOperationException($"No {material} prefab can represent {remaining} cargo units.");
            return eligible[random.Next(eligible.Count)];
        }

        private float Validate()
        {
            if (!float.IsFinite(m_Chance) || m_Chance < 0f || m_Chance > 1f ||
                !float.IsFinite(m_PeekingChance) || m_PeekingChance < 0f || m_PeekingChance > 1f ||
                m_Maximum < 0 || m_Maximum > MaximumDeposits ||
                m_SizeWeights == null || m_SizeWeights.Length != (int)TreasureSize.Large + 1 || m_Types == null ||
                m_Types.Count != ShipCargo.MaterialCount)
                throw new InvalidOperationException("Invalid asteroid treasure catalog settings.");
            foreach (float weight in m_SizeWeights)
                if (!float.IsFinite(weight) || weight < 0f)
                    throw new InvalidOperationException("Treasure size weights must be finite and nonnegative.");
            float total = 0f;
            var materials = new HashSet<TreasureMaterial>();
            foreach (TreasureType type in m_Types)
            {
                if (type == null || (int)type.Material >= ShipCargo.MaterialCount || !materials.Add(type.Material) ||
                    !float.IsFinite(type.Weight) || type.Weight < 0f || type.Prefabs == null || type.Prefabs.Count == 0)
                    throw new InvalidOperationException("Invalid treasure type in asteroid treasure catalog.");
                total += type.Weight;
                var sizes = new HashSet<TreasureSize>();
                bool hasSingleUnit = false;
                float sizeTotal = 0f;
                foreach (Treasure prefab in type.Prefabs)
                {
                    if (prefab == null || prefab.Material != type.Material || (int)prefab.Size > (int)TreasureSize.Large ||
                        !sizes.Add(prefab.Size) || prefab.Units <= 0 || !float.IsFinite(prefab.BaseRadius) || prefab.BaseRadius <= 0f)
                        throw new InvalidOperationException($"Invalid prefab in treasure type {type.Material}.");
                    hasSingleUnit |= prefab.Units == 1;
                    sizeTotal += m_SizeWeights[(int)prefab.Size];
                }
                if (!hasSingleUnit) throw new InvalidOperationException($"Treasure type {type.Material} needs a one-unit prefab for cargo drops.");
                if (!float.IsFinite(sizeTotal) || sizeTotal <= 0f)
                    throw new InvalidOperationException($"Treasure type {type.Material} needs a positive finite sum of size weights.");
            }
            if (!float.IsFinite(total) || total <= 0f) throw new InvalidOperationException("Treasure weights must have a positive finite sum.");
            return total;
        }

        internal static float Unit(ref uint state) => (AsteroidGeometry.Next(ref state) >> 8) / UnitDivisor;
    }
}
