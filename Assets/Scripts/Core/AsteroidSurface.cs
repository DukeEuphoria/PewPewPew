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
        public readonly uint ShapeSeed;
        public readonly float MinSplatRadius;
        public readonly float MaxSplatRadius;
        public readonly int SplatCount;
        private readonly AsteroidGeometry m_Lattice;
        private readonly AsteroidGeometry.Splat[] m_Splats;

        public AsteroidSurface(uint seed, Vector2 offset, Vector2 scale, float depth, float relief, float domeRadius,
            float vertexOffsetFactor = 0f)
        {
            if (!AsteroidGeometry.Finite(offset.x) || !AsteroidGeometry.Finite(offset.y) ||
                !AsteroidGeometry.Finite(scale.x) || !AsteroidGeometry.Finite(scale.y) ||
                !AsteroidGeometry.Finite(depth) || !AsteroidGeometry.Finite(relief) ||
                !AsteroidGeometry.Finite(domeRadius) ||
                scale.x <= 0f || scale.y <= 0f || depth <= 0f || relief < 0f || relief > depth ||
                domeRadius < 0f || domeRadius > Mathf.Sqrt(2f) * (AsteroidGeometry.MaxCells * 0.5f + 1f) ||
                !AsteroidGeometry.Finite(vertexOffsetFactor) || vertexOffsetFactor < 0f || vertexOffsetFactor > 1f)
                throw new ArgumentException("Invalid asteroid surface descriptor.");
            Seed = seed; Offset = offset; Scale = scale; Depth = depth; Relief = relief; DomeRadius = domeRadius;
            ShapeSeed = 0; MinSplatRadius = 0f; MaxSplatRadius = 0f; SplatCount = 0;
            m_Splats = null;
            m_Lattice = null;
            if (vertexOffsetFactor > 0f)
            {
                int size = AsteroidGeometry.MaxCells + 1;
                m_Lattice = new AsteroidGeometry(size, size, new byte[size * size]);
                m_Lattice.ConfigurePositions(seed, vertexOffsetFactor, Vector2Int.zero);
            }
        }

        public AsteroidSurface(uint seed, Vector2 offset, Vector2 scale, float depth, float relief,
            uint shapeSeed, float minSplatRadius, float maxSplatRadius, int splatCount, float vertexOffsetFactor = 0f)
            : this(seed, offset, scale, depth, relief, 0f, vertexOffsetFactor)
        {
            ShapeSeed = shapeSeed; MinSplatRadius = minSplatRadius; MaxSplatRadius = maxSplatRadius;
            SplatCount = splatCount;
            m_Splats = AsteroidGeometry.CreateSplats(shapeSeed, minSplatRadius, maxSplatRadius, splatCount);
        }

        public AsteroidGeometry CreateField()
        {
            if (m_Splats == null) throw new InvalidOperationException("This surface has no generated splat layout.");
            return AsteroidGeometry.Generate(ShapeSeed, m_Splats);
        }

        private AsteroidSurface(AsteroidSurface parent, Vector2 offset)
        {
            Seed = parent.Seed; Offset = parent.Source(offset); Scale = parent.Scale;
            Depth = parent.Depth; Relief = parent.Relief; DomeRadius = parent.DomeRadius;
            ShapeSeed = parent.ShapeSeed; MinSplatRadius = parent.MinSplatRadius;
            MaxSplatRadius = parent.MaxSplatRadius; SplatCount = parent.SplatCount; m_Splats = parent.m_Splats;
            m_Lattice = parent.m_Lattice;
        }

        public Vector2 Source(Vector2 local) => Offset + Vector2.Scale(local, Scale);
        public Vector2 Local(Vector2 source) =>
            new Vector2((source.x - Offset.x) / Scale.x, (source.y - Offset.y) / Scale.y);
        public AsteroidSurface Child(Vector2 offset) => new AsteroidSurface(this, offset);

        public float Front(Vector2 local)
        {
            Vector2 source = Source(local);
            if (m_Lattice != null) return DisplacedFront(source);
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
            Vector2 position = m_Lattice == null ? new Vector2(x, y) : m_Lattice.Position(x, y);
            float domeSquared = 0f;
            if (m_Splats == null)
            {
                Vector2 radial = position - Vector2.one * (AsteroidGeometry.MaxCells * 0.5f);
                domeSquared = DomeRadius * DomeRadius - radial.sqrMagnitude;
            }
            else
                foreach (AsteroidGeometry.Splat splat in m_Splats)
                    domeSquared = Mathf.Max(domeSquared,
                        splat.Radius * splat.Radius - (position - splat.Centre).sqrMagnitude);
            float dome = Mathf.Sqrt(Mathf.Max(0f, domeSquared)) / Scale.x;
            return dome + Relief * (0.15f + (hash >> 8) / 16777216f * 0.85f);
        }

        public List<Vector2[]> Facets(Vector2[] polygon)
        {
            if (m_Lattice != null) return DisplacedFacets(polygon);
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
            if (m_Lattice != null) return DisplacedWallCuts(a, b);
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

        private float DisplacedFront(Vector2 point)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(point.x) - 1);
            int maxX = Mathf.Min(AsteroidGeometry.MaxCells - 1, Mathf.FloorToInt(point.x) + 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt(point.y) - 1);
            int maxY = Mathf.Min(AsteroidGeometry.MaxCells - 1, Mathf.FloorToInt(point.y) + 1);
            float best = -0.0001f, height = 0f;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 a = m_Lattice.Position(x, y), b = m_Lattice.Position(x + 1, y);
                    Vector2 c = m_Lattice.Position(x + 1, y + 1), d = m_Lattice.Position(x, y + 1);
                    float score = Triangle(point, a, b, c, out float u, out float v);
                    if (score > best)
                    {
                        best = score;
                        float ha = Height(x, y), hb = Height(x + 1, y), hc = Height(x + 1, y + 1);
                        height = ha + u * (hb - ha) + v * (hc - ha);
                    }
                    if (score >= 0f) return -Depth * 0.5f - height;
                    score = Triangle(point, a, c, d, out u, out v);
                    if (score > best)
                    {
                        best = score;
                        float ha = Height(x, y), hc = Height(x + 1, y + 1), hd = Height(x, y + 1);
                        height = ha + u * (hc - ha) + v * (hd - ha);
                    }
                    if (score >= 0f) return -Depth * 0.5f - height;
                }
            if (best > -0.0001f) return -Depth * 0.5f - height;
            throw new InvalidOperationException("Asteroid surface point lies outside its displaced lattice.");
        }

        private static float Triangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out float u, out float v)
        {
            Vector2 ab = b - a, ac = c - a, ap = point - a;
            float area = Cross(ab, ac);
            u = Cross(ap, ac) / area;
            v = Cross(ab, ap) / area;
            return Mathf.Min(Mathf.Min(u, v), 1f - u - v);
        }

        private List<Vector2[]> DisplacedFacets(Vector2[] polygon)
        {
            var source = new List<Vector2>(polygon.Length);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 point in polygon)
            {
                Vector2 p = Source(point);
                source.Add(p); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            var result = new List<Vector2[]>();
            AsteroidSurface frame = this;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(min.y) - 1);
                 y <= Mathf.Min(AsteroidGeometry.MaxCells - 1, Mathf.FloorToInt(max.y) + 1); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(min.x) - 1);
                     x <= Mathf.Min(AsteroidGeometry.MaxCells - 1, Mathf.FloorToInt(max.x) + 1); x++)
                {
                    Vector2 a = m_Lattice.Position(x, y), b = m_Lattice.Position(x + 1, y);
                    Vector2 c = m_Lattice.Position(x + 1, y + 1), d = m_Lattice.Position(x, y + 1);
                    Add(a, b, c);
                    Add(a, c, d);
                }
            if (result.Count == 0 && AsteroidGeometry.PolygonArea(polygon) > 0f)
                throw new InvalidOperationException("Asteroid polygon lies outside its displaced surface lattice.");
            return result;

            void Add(Vector2 a, Vector2 b, Vector2 c)
            {
                if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < min.x ||
                    Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > max.x ||
                    Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < min.y ||
                    Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > max.y) return;
                List<Vector2> points = ClipEdge(source, a, b);
                if (points.Count == 0) return;
                points = ClipEdge(points, b, c);
                if (points.Count == 0) return;
                points = ClipEdge(points, c, a);
                if (points.Count < 3) return;
                var facet = new Vector2[points.Count];
                for (int i = 0; i < facet.Length; i++) facet[i] = frame.Local(points[i]);
                if (AsteroidGeometry.PolygonArea(facet) > 0f) result.Add(facet);
            }
        }

        private static List<Vector2> ClipEdge(List<Vector2> points, Vector2 a, Vector2 b)
        {
            var result = new List<Vector2>();
            if (points.Count == 0) return result;
            Vector2 previous = points[points.Count - 1];
            double previousDistance = Distance(previous);
            foreach (Vector2 point in points)
            {
                double distance = Distance(point);
                if ((previousDistance >= 0d) != (distance >= 0d))
                    result.Add(Vector2.LerpUnclamped(previous, point, (float)(previousDistance / (previousDistance - distance))));
                if (distance >= 0d) result.Add(point);
                previous = point;
                previousDistance = distance;
            }
            return result;

            double Distance(Vector2 point) =>
                ((double)b.x - a.x) * ((double)point.y - a.y) -
                ((double)b.y - a.y) * ((double)point.x - a.x);
        }

        private List<float> DisplacedWallCuts(Vector2 start, Vector2 end)
        {
            Vector2 a = Source(start), b = Source(end);
            Vector2 min = Vector2.Min(a, b), max = Vector2.Max(a, b);
            var cuts = new List<float> { 0f, 1f };
            for (int y = Mathf.Max(0, Mathf.FloorToInt(min.y) - 1);
                 y <= Mathf.Min(AsteroidGeometry.MaxCells - 1, Mathf.FloorToInt(max.y) + 1); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(min.x) - 1);
                     x <= Mathf.Min(AsteroidGeometry.MaxCells - 1, Mathf.FloorToInt(max.x) + 1); x++)
                {
                    Vector2 p = m_Lattice.Position(x, y), q = m_Lattice.Position(x + 1, y);
                    Vector2 r = m_Lattice.Position(x + 1, y + 1), s = m_Lattice.Position(x, y + 1);
                    Add(p, q); Add(p, s); Add(p, r);
                    if (x == AsteroidGeometry.MaxCells - 1) Add(q, r);
                    if (y == AsteroidGeometry.MaxCells - 1) Add(s, r);
                }
            cuts.Sort();
            for (int i = cuts.Count - 1; i > 0; i--)
                if (cuts[i] - cuts[i - 1] < 0.000001f) cuts.RemoveAt(i);
            return cuts;

            void Add(Vector2 p, Vector2 q)
            {
                Vector2 direction = b - a, edge = q - p;
                float denominator = Cross(direction, edge);
                if (Mathf.Abs(denominator) < 0.0000001f)
                {
                    if (Mathf.Abs(Cross(p - a, direction)) > 0.0000001f || direction.sqrMagnitude == 0f) return;
                    AddCut(Vector2.Dot(p - a, direction) / direction.sqrMagnitude);
                    AddCut(Vector2.Dot(q - a, direction) / direction.sqrMagnitude);
                    return;
                }
                float t = Cross(p - a, edge) / denominator;
                float u = Cross(p - a, direction) / denominator;
                if (u >= 0f && u <= 1f) AddCut(t);
            }

            void AddCut(float t)
            {
                if (t > 0f && t < 1f) cuts.Add(t);
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
