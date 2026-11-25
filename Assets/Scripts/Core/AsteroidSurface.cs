using System;
using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Core
{
    public readonly struct AsteroidSurface
    {
        public readonly uint Seed;
        public readonly Vector2 Offset;
        public readonly Vector2 Scale;
        public readonly float Depth;
        public readonly float Relief;
        public readonly float DomeRadius;

        public AsteroidSurface(uint seed, Vector2 offset, Vector2 scale, float depth, float relief, float domeRadius)
        {
            if (!AsteroidGeometry.Finite(offset.x) || !AsteroidGeometry.Finite(offset.y) ||
                !AsteroidGeometry.Finite(scale.x) || !AsteroidGeometry.Finite(scale.y) ||
                !AsteroidGeometry.Finite(depth) || !AsteroidGeometry.Finite(relief) ||
                !AsteroidGeometry.Finite(domeRadius) ||
                scale.x <= 0f || scale.y <= 0f || depth <= 0f || relief < 0f || relief > depth ||
                domeRadius < 0f || domeRadius > AsteroidGeometry.MaxCells * 0.5f)
                throw new ArgumentException("Invalid asteroid surface descriptor.");
            Seed = seed; Offset = offset; Scale = scale; Depth = depth; Relief = relief; DomeRadius = domeRadius;
        }

        public Vector2 Source(Vector2 local) => Offset + Vector2.Scale(local, Scale);
        public Vector2 Local(Vector2 source) =>
            new Vector2((source.x - Offset.x) / Scale.x, (source.y - Offset.y) / Scale.y);
        public AsteroidSurface Child(Vector2 offset) =>
            new AsteroidSurface(Seed, Source(offset), Scale, Depth, Relief, DomeRadius);

        public float Front(Vector2 local)
        {
            Vector2 source = Source(local);
            int x = Mathf.FloorToInt(source.x), y = Mathf.FloorToInt(source.y);
            float u = source.x - x, v = source.y - y;
            float a = Height(x, y), c = Height(x + 1, y + 1);
            float height = v <= u
                ? a + (Height(x + 1, y) - a) * u + (c - Height(x + 1, y)) * v
                : a + (c - Height(x, y + 1)) * u + (Height(x, y + 1) - a) * v;
            return -Depth * 0.5f - height;
        }

        private float Height(int x, int y)
        {
            uint hash = unchecked(Seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u);
            hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
            hash ^= hash >> 16;
            Vector2 radial = new Vector2(x, y) - Vector2.one * (AsteroidGeometry.MaxCells * 0.5f);
            float dome = Mathf.Sqrt(Mathf.Max(0f, DomeRadius * DomeRadius - radial.sqrMagnitude)) / Scale.x;
            return dome + Relief * (0.15f + (hash >> 8) / 16777216f * 0.85f);
        }

        public List<Vector2[]> Facets(Vector2[] polygon)
        {
            var source = new List<Vector2>(polygon.Length);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 point in polygon)
            {
                Vector2 p = Source(point);
                source.Add(p); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            if (min.x < -1f || min.y < -1f || max.x > AsteroidGeometry.MaxCells + 1f ||
                max.y > AsteroidGeometry.MaxCells + 1f)
                throw new InvalidOperationException("Asteroid surface lies outside its bounded source lattice.");
            var result = new List<Vector2[]>();
            AsteroidSurface frame = this;
            for (int y = Mathf.FloorToInt(min.y); y <= Mathf.FloorToInt(max.y); y++)
            for (int x = Mathf.FloorToInt(min.x); x <= Mathf.FloorToInt(max.x); x++)
            {
                List<Vector2> cell = Clip(source, Vector2.right, x);
                cell = Clip(cell, Vector2.left, -x - 1);
                cell = Clip(cell, Vector2.up, y);
                cell = Clip(cell, Vector2.down, -y - 1);
                Add(Clip(cell, new Vector2(1f, -1f), x - y));
                Add(Clip(cell, new Vector2(-1f, 1f), y - x));
            }
            return result;

            void Add(List<Vector2> points)
            {
                if (points.Count < 3) return;
                var facet = new Vector2[points.Count];
                for (int i = 0; i < facet.Length; i++) facet[i] = frame.Local(points[i]);
                if (AsteroidGeometry.PolygonArea(facet) > 0f) result.Add(facet);
            }
        }

        private static List<Vector2> Clip(List<Vector2> points, Vector2 normal, float limit)
        {
            var result = new List<Vector2>();
            if (points.Count == 0) return result;
            Vector2 a = points[points.Count - 1];
            float da = Vector2.Dot(a, normal) - limit;
            foreach (Vector2 b in points)
            {
                float db = Vector2.Dot(b, normal) - limit;
                if ((da >= 0f) != (db >= 0f)) result.Add(Vector2.LerpUnclamped(a, b, da / (da - db)));
                if (db >= 0f) result.Add(b);
                a = b; da = db;
            }
            return result;
        }

        public List<float> WallCuts(Vector2 a, Vector2 b)
        {
            a = Source(a); b = Source(b);
            var cuts = new List<float> { 0f, 1f };
            Axis(a.x, b.x); Axis(a.y, b.y); Axis(a.x - a.y, b.x - b.y);
            cuts.Sort();
            return cuts;

            void Axis(float start, float end)
            {
                if (Mathf.Abs(end - start) < 0.000001f) return;
                for (int i = Mathf.CeilToInt(Mathf.Min(start, end)); i <= Mathf.FloorToInt(Mathf.Max(start, end)); i++)
                {
                    float t = (i - start) / (end - start);
                    if (t > 0f && t < 1f) cuts.Add(t);
                }
            }
        }
    }
}
