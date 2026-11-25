using System;
using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Core
{
    /// A bounded surface field, not a voxel volume. All coordinates are in sample space.
    public sealed class AsteroidGeometry
    {
        public const byte Version = 10;
        public const int MaxCells = 15;
        public const float IsoLevel = 127.5f;
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Samples;
        private readonly float[] m_PendingErosion;
        private Vector2[] m_Positions;
        private uint m_PositionSeed;
        private float m_OffsetFactor;
        private Vector2Int m_PositionOrigin;
        public float OffsetFactor => m_OffsetFactor;

        public AsteroidGeometry(int width, int height, byte[] samples)
        {
            if (width < 2 || height < 2 || width > MaxCells + 1 || height > MaxCells + 1 ||
                samples == null || samples.Length != width * height)
                throw new ArgumentException($"Asteroid field must contain 2..{MaxCells + 1} samples per axis.");
            Width = width;
            Height = height;
            Samples = (byte[])samples.Clone();
            m_PendingErosion = new float[samples.Length];
        }

        private AsteroidGeometry(int width, int height, byte[] samples, float[] pending) : this(width, height, samples) =>
            Array.Copy(pending, m_PendingErosion, pending.Length);

        public static uint Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return state;
        }

        private static float Unit(ref uint state) => (Next(ref state) >> 8) / 16777216f;

        public static float InitialRadius(uint seed, float minimum, float maximum)
        {
            if (!Finite(minimum) || !Finite(maximum) || minimum < 1f || maximum < minimum || maximum > MaxCells * 0.5f - 1f)
                throw new ArgumentException("Asteroid occupied radii must be ordered within 1..6.5 cells.");
            uint state = seed ^ 0x85EBCA6Bu;
            Next(ref state);
            return Mathf.Lerp(minimum, maximum, Unit(ref state));
        }

        public static AsteroidGeometry Generate(uint seed) => Generate(seed, InitialRadius(seed, 2f, 6.5f));

        public static AsteroidGeometry Generate(uint seed, float radius)
        {
            if (!Finite(radius) || radius < 1f || radius > MaxCells * 0.5f - 1f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            uint state = seed;
            int width = MaxCells + 1;
            var samples = new byte[width * width];
            var centres = new Vector2[5];
            var radii = new float[5];
            centres[0] = Vector2.one * (MaxCells * 0.5f);
            radii[0] = radius * 0.65f;
            for (int i = 1; i < centres.Length; i++)
            {
                float angle = Unit(ref state) * Mathf.PI * 2f;
                centres[i] = centres[0] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * 0.35f * Unit(ref state));
                radii[i] = radius * (0.4f + Unit(ref state) * 0.25f);
            }
            for (int y = 1; y < MaxCells; y++)
            for (int x = 1; x < MaxCells; x++)
            {
                Vector2 point = new Vector2(x, y);
                float distance = float.NegativeInfinity;
                for (int i = 0; i < centres.Length; i++)
                    distance = Mathf.Max(distance, radii[i] - Vector2.Distance(point, centres[i]));
                float noise = Mathf.Sin(point.x * 2.7f + centres[1].x) *
                              Mathf.Sin(point.y * 3.1f + centres[1].y);
                samples[y * width + x] = Solidness(distance + noise * 0.275f);
            }
            var field = new AsteroidGeometry(width, width, samples);
            List<AsteroidGeometry> components = field.Components();
            if (components.Count == 0) throw new InvalidOperationException("Seeded asteroid generation produced no material.");
            if (components.Count == 1) return field;
            AsteroidGeometry largest = components[0];
            float largestArea = largest.Area();
            for (int i = 1; i < components.Count; i++)
            {
                float area = components[i].Area();
                if (area <= largestArea) continue;
                largest = components[i];
                largestArea = area;
            }
            return largest;
        }

        private static byte Solidness(float distance) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(IsoLevel + distance * 255f), 0, 255);

        public void ConfigurePositions(uint seed, float factor, Vector2Int origin)
        {
            if (!Finite(factor) || factor < 0f || factor > 1f)
                throw new ArgumentOutOfRangeException(nameof(factor));
            m_PositionSeed = seed;
            m_OffsetFactor = factor;
            m_PositionOrigin = origin;
            m_Positions = null;
            if (factor == 0f) return;

            int size = MaxCells + 1;
            var source = new Vector2[size * size];
            uint state = seed ^ 0xD1B54A35u;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) source[y * size + x] = new Vector2(x, y);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                Vector2 original = source[index];
                Vector2 displacement = Vector2.zero;
                bool valid = false;
                for (int attempt = 0; attempt < 40 && !valid; attempt++)
                {
                    if (attempt < 8)
                    {
                        float angle = Unit(ref state) * Mathf.PI * 2f;
                        float radius = Mathf.Sqrt(Unit(ref state));
                        displacement = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    }
                    else displacement *= 0.5f;
                    source[index] = original + displacement;
                    valid = true;
                    for (int cy = Mathf.Max(0, y - 1); cy <= Mathf.Min(MaxCells - 1, y); cy++)
                    for (int cx = Mathf.Max(0, x - 1); cx <= Mathf.Min(MaxCells - 1, x); cx++)
                        if (!ValidCell(source, size, cx, cy)) valid = false;
                }
                if (!valid) throw new InvalidOperationException("Could not generate valid asteroid vertex offsets.");
            }
            m_Positions = new Vector2[Samples.Length];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int sx = x + origin.x, sy = y + origin.y;
                Vector2 grid = new Vector2(x, y);
                m_Positions[y * Width + x] = sx >= 0 && sy >= 0 && sx < size && sy < size
                    ? grid + (source[sy * size + sx] - new Vector2(sx, sy)) * factor : grid;
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool ValidCell(Vector2[] positions, int width, int x, int y)
        {
            Vector2 a = positions[y * width + x] - new Vector2(x, y);
            Vector2 b = positions[y * width + x + 1] - new Vector2(x + 1, y);
            Vector2 c = positions[(y + 1) * width + x + 1] - new Vector2(x + 1, y + 1);
            Vector2 d = positions[(y + 1) * width + x] - new Vector2(x, y + 1);
            return ValidCorner(Vector2.right, Vector2.up, b - a, c - b) &&
                   ValidCorner(Vector2.up, Vector2.left, c - b, d - c) &&
                   ValidCorner(Vector2.left, Vector2.down, d - c, a - d) &&
                   ValidCorner(Vector2.down, Vector2.right, a - d, b - a);
        }

        private static bool ValidCorner(Vector2 a, Vector2 b, Vector2 da, Vector2 db)
        {
            // The corner cross product is quadratic as offsets scale from zero to one.
            float linear = Cross(da, b) + Cross(a, db), quadratic = Cross(da, db);
            if (1f + linear + quadratic <= 0.01f) return false;
            if (quadratic <= 0f) return true;
            float minimum = -linear / (2f * quadratic);
            return minimum <= 0f || minimum >= 1f ||
                   1f + linear * minimum + quadratic * minimum * minimum > 0.01f;
        }

        public Vector2 Position(int x, int y) => m_Positions == null ? new Vector2(x, y) : m_Positions[y * Width + x];

        public List<Vector2[]> Polygons()
        {
            var result = new List<Vector2[]>();
            for (int y = 0; y < Height - 1; y++)
            for (int x = 0; x < Width - 1; x++)
            {
                int a = Samples[y * Width + x], b = Samples[y * Width + x + 1];
                int c = Samples[(y + 1) * Width + x + 1], d = Samples[(y + 1) * Width + x];
                int code = (a >= IsoLevel ? 1 : 0) | (b >= IsoLevel ? 2 : 0) |
                           (c >= IsoLevel ? 4 : 0) | (d >= IsoLevel ? 8 : 0);
                if (code == 0) continue;
                Vector2 bl = Position(x, y), br = Position(x + 1, y);
                Vector2 tr = Position(x + 1, y + 1), tl = Position(x, y + 1);
                Vector2 bottom = Edge(bl, br, a, b), right = Edge(br, tr, b, c);
                Vector2 top = Edge(tl, tr, d, c), left = Edge(bl, tl, a, d);
                switch (code)
                {
                    case 1: result.Add(new[] { bl, bottom, left }); break;
                    case 2: result.Add(new[] { bottom, br, right }); break;
                    case 3: result.Add(new[] { bl, br, right, left }); break;
                    case 4: result.Add(new[] { right, tr, top }); break;
                    // Diagonally touching corners are separate material components.
                    case 5:
                        result.Add(new[] { bl, bottom, left });
                        result.Add(new[] { right, tr, top }); break;
                    case 6: result.Add(new[] { bottom, br, tr, top }); break;
                    case 7: result.Add(new[] { bl, br, tr, top, left }); break;
                    case 8: result.Add(new[] { left, top, tl }); break;
                    case 9: result.Add(new[] { bl, bottom, top, tl }); break;
                    case 10:
                        result.Add(new[] { bottom, br, right });
                        result.Add(new[] { left, top, tl }); break;
                    case 11: result.Add(new[] { bl, br, right, top, tl }); break;
                    case 12: result.Add(new[] { left, right, tr, tl }); break;
                    case 13: result.Add(new[] { bl, bottom, right, tr, tl }); break;
                    case 14: result.Add(new[] { bottom, br, tr, tl, left }); break;
                    case 15: result.Add(new[] { bl, br, tr, tl }); break;
                }
            }
            return result;
        }

        private static Vector2 Edge(Vector2 a, Vector2 b, int va, int vb) =>
            va == vb ? (a + b) * 0.5f : Vector2.LerpUnclamped(a, b, (IsoLevel - va) / (float)(vb - va));

        public float Area()
        {
            float sum = 0f;
            foreach (Vector2[] polygon in Polygons()) sum += PolygonArea(polygon);
            return sum;
        }

        /// Impact/direction use sample coordinates; axes are world-space offsets for one sample step.
        public float ErodeDamage(Vector2 point, Vector2 direction, Vector2 axisX, Vector2 axisY,
            float radius, float damage, bool projectile)
        {
            if (!Finite(radius) || radius < 0f || !Finite(damage) || damage <= 0f ||
                !Finite(point.x) || !Finite(point.y) || !Finite(direction.x) || !Finite(direction.y) ||
                !Finite(axisX.x) || !Finite(axisX.y) || !Finite(axisY.x) || !Finite(axisY.y) ||
                (double)axisX.x * axisY.y - (double)axisX.y * axisY.x == 0d)
                throw new ArgumentException("Damage requires finite coordinates, nondegenerate world axes, nonnegative radius and positive damage.");
            if (radius <= 0.5f)
            {
                if (m_Positions != null && point.x >= -0.5f - m_OffsetFactor && point.y >= -0.5f - m_OffsetFactor &&
                    point.x <= Width - 0.5f + m_OffsetFactor && point.y <= Height - 0.5f + m_OffsetFactor)
                {
                    double nearest = double.PositiveInfinity;
                    Vector2 closest = point;
                    for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        Vector2 delta = Position(x, y) - point;
                        double worldX = (double)axisX.x * delta.x + (double)axisY.x * delta.y;
                        double worldY = (double)axisX.y * delta.x + (double)axisY.y * delta.y;
                        double distance = worldX * worldX + worldY * worldY;
                        if (distance >= nearest) continue;
                        nearest = distance;
                        closest = new Vector2(x, y);
                    }
                    point = closest;
                }
                return ErodeImpact(point, direction, damage, projectile);
            }
            float spent = 0f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Vector2 position = Position(x, y);
                double dx = position.x - (double)point.x, dy = position.y - (double)point.y;
                double worldX = dx * axisX.x + dy * axisY.x, worldY = dx * axisX.y + dy * axisY.y;
                double distance = Math.Sqrt(worldX * worldX + worldY * worldY);
                if (distance >= radius) continue;
                float localDamage = (float)(damage * (1d - distance / radius));
                if (localDamage > 0f)
                    spent += ErodeImpact(new Vector2(x, y), Vector2.zero, localDamage, false);
            }
            return spent;
        }

        public float ErodeImpact(Vector2 point, Vector2 direction, float damage, bool propagate)
        {
            if (!Finite(point.x) || !Finite(point.y) || !Finite(direction.x) || !Finite(direction.y) ||
                !Finite(damage) || damage <= 0f || (propagate && direction.x == 0f && direction.y == 0f))
                throw new ArgumentException("Impact erosion requires finite coordinates, positive damage and a nonzero propagation vector.");
            // Scaling first also normalizes extremely small/large finite impact vectors safely.
            if (propagate) direction /= Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            float enter = 0f, exit = float.PositiveInfinity;
            if (propagate)
            {
                if (!Clip(point.x, direction.x, Width) || !Clip(point.y, direction.y, Height)) return 0f;
                point += direction * enter;
            }
            int x = Nearest(point.x, direction.x), y = Nearest(point.y, direction.y);
            x = Mathf.Clamp(x, 0, Width - 1); y = Mathf.Clamp(y, 0, Height - 1);
            if (!propagate && (point.x < -0.5f || point.y < -0.5f || point.x > Width - 0.5f || point.y > Height - 0.5f))
                return 0f;
            int stepX = direction.x > 0f ? 1 : -1, stepY = direction.y > 0f ? 1 : -1;
            float dx = direction.x == 0f ? float.PositiveInfinity : 1f / Mathf.Abs(direction.x);
            float dy = direction.y == 0f ? float.PositiveInfinity : 1f / Mathf.Abs(direction.y);
            float nextX = direction.x == 0f ? float.PositiveInfinity : (x + stepX * 0.5f - point.x) / direction.x;
            float nextY = direction.y == 0f ? float.PositiveInfinity : (y + stepY * 0.5f - point.y) / direction.y;
            float remaining = damage, spent = 0f;
            for (int visited = 0; visited < Width + Height && x >= 0 && y >= 0 && x < Width && y < Height; visited++)
            {
                int index = y * Width + x;
                if (Samples[index] >= IsoLevel)
                {
                    float capacity = Samples[index] - m_PendingErosion[index];
                    float applied = Mathf.Min(remaining, capacity);
                    if (applied >= capacity)
                    {
                        Samples[index] = 0;
                        m_PendingErosion[index] = 0f;
                    }
                    else
                    {
                        float total = m_PendingErosion[index] + applied;
                        int levels = Mathf.FloorToInt(total);
                        Samples[index] -= (byte)levels;
                        m_PendingErosion[index] = total - levels;
                    }
                    spent += applied;
                    remaining -= applied;
                }
                if (!propagate || remaining <= 0f) break;
                if (nextX < nextY) { x += stepX; nextX += dx; }
                else if (nextY < nextX) { y += stepY; nextY += dy; }
                else { x += stepX; y += stepY; nextX += dx; nextY += dy; }
            }
            return spent;

            bool Clip(float origin, float delta, int count)
            {
                if (delta == 0f) return origin >= -0.5f && origin <= count - 0.5f;
                float a = (-0.5f - origin) / delta, b = (count - 0.5f - origin) / delta;
                enter = Mathf.Max(enter, Mathf.Min(a, b));
                exit = Mathf.Min(exit, Mathf.Max(a, b));
                return enter <= exit;
            }

            int Nearest(float coordinate, float delta) => delta < 0f
                ? Mathf.CeilToInt(coordinate - 0.5f) : Mathf.FloorToInt(coordinate + 0.5f);
        }

        public static float PolygonArea(Vector2[] polygon)
        {
            float sum = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                sum += a.x * b.y - a.y * b.x;
            }
            return sum * 0.5f;
        }

        public bool Erode(Vector2 point, float radius, float strength)
        {
            if (!Finite(point.x) || !Finite(point.y) || !Finite(radius) || !Finite(strength) ||
                radius <= 0f || strength <= 0f)
                throw new ArgumentException("Excavation requires a finite point, radius and positive strength.");
            bool changed = false;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float weight = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), point) / radius);
                if (weight <= 0f) continue;
                int index = y * Width + x;
                int reduction = Mathf.Max(1, Mathf.RoundToInt(weight * strength));
                byte next = (byte)Mathf.Max(0, Samples[index] - reduction);
                changed |= next != Samples[index];
                Samples[index] = next;
                m_PendingErosion[index] = 0f;
            }
            return changed;
        }

        public List<AsteroidGeometry> Components()
        {
            var result = new List<AsteroidGeometry>();
            var visited = new bool[Samples.Length];
            var queue = new Queue<int>();
            for (int start = 0; start < Samples.Length; start++)
            {
                if (visited[start] || Samples[start] < IsoLevel) continue;
                var component = new byte[Samples.Length];
                var pending = new float[Samples.Length];
                visited[start] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    component[index] = Samples[index];
                    pending[index] = m_PendingErosion[index];
                    int x = index % Width, y = index / Width;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                        int neighbour = ny * Width + nx;
                        if (Samples[neighbour] >= IsoLevel) continue;
                        component[neighbour] = Samples[neighbour];
                        pending[neighbour] = m_PendingErosion[neighbour];
                    }
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }
                var part = new AsteroidGeometry(Width, Height, component, pending);
                if (m_Positions != null) part.ConfigurePositions(m_PositionSeed, m_OffsetFactor, m_PositionOrigin);
                result.Add(part);

                void Visit(int x, int y)
                {
                    if (x < 0 || x >= Width || y < 0 || y >= Height) return;
                    int index = y * Width + x;
                    if (visited[index] || Samples[index] < IsoLevel) return;
                    visited[index] = true;
                    queue.Enqueue(index);
                }
            }
            return result;
        }

        public void Crack(Vector2 impact, uint seed)
        {
            uint state = seed;
            Vector2 centre = Vector2.zero;
            int solid = 0;
            for (int i = 0; i < Samples.Length; i++)
                if (Samples[i] >= IsoLevel) { centre += new Vector2(i % Width, i / Width); solid++; }
            if (solid == 0) return;
            centre /= solid;
            Vector2 direction = centre - impact;
            bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
            if (direction.sqrMagnitude < 0.01f) horizontal = (Next(ref state) & 1) == 0;
            int length = horizontal ? Width : Height, breadth = horizontal ? Height : Width;
            float hitAlong = horizontal ? impact.x : impact.y;
            float hitAcross = horizontal ? impact.y : impact.x;
            float along = horizontal ? direction.x : direction.y;
            float across = horizontal ? direction.y : direction.x;
            float slope = Mathf.Abs(along) > 0.01f ? Mathf.Clamp(across / along, -1f, 1f) : 0f;
            var costs = new float[length, breadth];
            var previous = new int[length, breadth];
            for (int step = 0; step < length; step++)
            for (int j = 0; j < breadth; j++)
            {
                float guide = Mathf.Clamp(hitAcross + (step - hitAlong) * slope, 0, breadth - 1);
                int index = horizontal ? j * Width + step : step * Width + j;
                float cost = Samples[index] / 255f + Mathf.Abs(j - guide) * 0.6f + Unit(ref state) * 0.35f;
                if (step == Mathf.Clamp(Mathf.RoundToInt(hitAlong), 0, length - 1) &&
                    Mathf.Abs(j - hitAcross) > 1f) cost += 1000f;
                costs[step, j] = cost;
                if (step == 0) continue;
                float best = float.PositiveInfinity;
                for (int k = Mathf.Max(0, j - 1); k <= Mathf.Min(breadth - 1, j + 1); k++)
                    if (costs[step - 1, k] < best)
                    { best = costs[step - 1, k]; previous[step, j] = k; }
                costs[step, j] += best;
            }
            int end = 0;
            for (int j = 1; j < breadth; j++)
                if (costs[length - 1, j] < costs[length - 1, end]) end = j;
            for (int step = length - 1; step >= 0; step--)
            {
                Carve(step, end);
                if (step == 0) break;
                int next = previous[step, end];
                // Join diagonal steps with an orthogonal sample to match four-neighbour connectivity.
                Carve(step - 1, end);
                end = next;
            }

            void Carve(int step, int j)
            {
                int index = horizontal ? j * Width + step : step * Width + j;
                Samples[index] = 0;
                m_PendingErosion[index] = 0f;
            }
        }

        public AsteroidGeometry Recenter(out Vector2 offset)
        {
            int minX = Width, minY = Height, maxX = -1, maxY = -1;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Samples[y * Width + x] >= IsoLevel)
                {
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
            if (maxX < 0) throw new InvalidOperationException("Cannot recenter empty asteroid material.");
            int width = MaxCells + 1;
            int originX = Mathf.RoundToInt((minX + maxX) * 0.5f) - MaxCells / 2;
            int originY = Mathf.RoundToInt((minY + maxY) * 0.5f) - MaxCells / 2;
            var samples = new byte[width * width];
            var pending = new float[samples.Length];
            for (int y = 0; y < width; y++)
            for (int x = 0; x < width; x++)
            {
                int sourceX = x + originX, sourceY = y + originY;
                if (sourceX < 0 || sourceY < 0 || sourceX >= Width || sourceY >= Height) continue;
                samples[y * width + x] = Samples[sourceY * Width + sourceX];
                pending[y * width + x] = m_PendingErosion[sourceY * Width + sourceX];
            }
            offset = new Vector2(originX, originY) + Vector2.one * (MaxCells * 0.5f) -
                     new Vector2(Width - 1, Height - 1) * 0.5f;
            var result = new AsteroidGeometry(width, width, samples, pending);
            if (m_Positions != null)
                result.ConfigurePositions(m_PositionSeed, m_OffsetFactor,
                    m_PositionOrigin + new Vector2Int(originX, originY));
            return result;
        }

        public readonly struct BoundaryEdge : IEquatable<BoundaryEdge>
        {
            public readonly Vector2 A;
            public readonly Vector2 B;
            public BoundaryEdge(Vector2 a, Vector2 b) { A = a; B = b; }
            public bool Equals(BoundaryEdge other) => A.Equals(other.A) && B.Equals(other.B);
            public override bool Equals(object obj) => obj is BoundaryEdge other && Equals(other);
            public override int GetHashCode() => unchecked(A.GetHashCode() * 397 ^ B.GetHashCode());
        }

        public static List<BoundaryEdge> Boundary(List<Vector2[]> polygons)
        {
            var edges = new HashSet<BoundaryEdge>();
            foreach (Vector2[] polygon in polygons)
            for (int i = 0; i < polygon.Length; i++)
            {
                var edge = new BoundaryEdge(polygon[i], polygon[(i + 1) % polygon.Length]);
                if (!edges.Remove(new BoundaryEdge(edge.B, edge.A))) edges.Add(edge);
            }
            return new List<BoundaryEdge>(edges);
        }

        public static bool Contains(List<Vector2[]> polygons, Vector2 point)
        {
            foreach (Vector2[] polygon in polygons)
            {
                bool inside = true;
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 a = polygon[i], edge = polygon[(i + 1) % polygon.Length] - a;
                    Vector2 relative = point - a;
                    if (edge.x * relative.y - edge.y * relative.x < -0.0000001f) { inside = false; break; }
                }
                if (inside) return true;
            }
            return false;
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    }
}
