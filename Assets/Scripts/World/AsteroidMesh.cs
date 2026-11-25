using System;
using System.Collections.Generic;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.World
{
    internal static class AsteroidMesh
    {
        public static List<Vector2[]> Paths(AsteroidGeometry field, Vector2 spacing, out float area,
            out List<float[]> solidWeights)
        {
            solidWeights = new List<float[]>();
            List<Vector2[]> paths = field.Polygons(solidWeights);
            Vector2 centre = new Vector2(field.Width - 1, field.Height - 1) * 0.5f;
            area = 0f;
            foreach (Vector2[] path in paths)
            {
                area += AsteroidGeometry.PolygonArea(path);
                for (int i = 0; i < path.Length; i++) path[i] = Vector2.Scale(path[i] - centre, spacing);
            }
            return paths;
        }

        public static Mesh Create(List<Vector2[]> paths, List<float[]> solidWeights, float depth,
            AsteroidSurface? surface = null)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            var boundaryWeights = new Dictionary<Vector2, float>();
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                Vector2[] path = paths[pathIndex];
                float[] weights = solidWeights[pathIndex];
                for (int i = 0; i < path.Length; i++)
                    boundaryWeights[path[i]] = weights[i];
                for (int i = 1; i < path.Length - 1; i++)
                {
                    Face(new Vector3(path[0].x, path[0].y, depth * 0.5f),
                        new Vector3(path[i].x, path[i].y, depth * 0.5f),
                        new Vector3(path[i + 1].x, path[i + 1].y, depth * 0.5f),
                        weights[0], weights[i], weights[i + 1], true);
                }
                foreach (Vector2[] facet in surface.HasValue ? surface.Value.Facets(path) : new List<Vector2[]> { path })
                {
                    float first = Interpolate(path, weights, facet[0]);
                    float previous = Interpolate(path, weights, facet[1]);
                    for (int i = 1; i < facet.Length - 1; i++)
                    {
                        float next = Interpolate(path, weights, facet[i + 1]);
                        Face(Front(facet[0]), Front(facet[i + 1]), Front(facet[i]),
                            first, next, previous, true);
                        previous = next;
                    }
                }
            }
            foreach (AsteroidGeometry.BoundaryEdge edge in AsteroidGeometry.Boundary(paths))
            {
                float startWeight = boundaryWeights[edge.A], endWeight = boundaryWeights[edge.B];
                List<float> cuts = surface.HasValue ? surface.Value.WallCuts(edge.A, edge.B) : new List<float> { 0f, 1f };
                for (int i = 1; i < cuts.Count; i++)
                {
                    Vector2 start = Vector2.Lerp(edge.A, edge.B, cuts[i - 1]);
                    Vector2 end = Vector2.Lerp(edge.A, edge.B, cuts[i]);
                    Vector3 a = Front(start), b = Front(end);
                    Vector3 c = new Vector3(end.x, end.y, depth * 0.5f);
                    Vector3 d = new Vector3(start.x, start.y, depth * 0.5f);
                    float wa = Mathf.LerpUnclamped(startWeight, endWeight, cuts[i - 1]);
                    float wb = Mathf.LerpUnclamped(startWeight, endWeight, cuts[i]);
                    Face(a, b, c, wa, wb, wb, false);
                    Face(a, c, d, wa, wb, wa, false);
                }
            }
            var mesh = new Mesh { name = "Seeded asteroid rock" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            Vector3 Front(Vector2 point) => new Vector3(point.x, point.y,
                surface.HasValue ? surface.Value.Front(point) : -depth * 0.5f);

            void Face(Vector3 a, Vector3 b, Vector3 c, float wa, float wb, float wc, bool cap)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-20f) return;
                normal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(Uv(a)); uv.Add(Uv(b)); uv.Add(Uv(c));
                colors.Add(WeightColor(wa)); colors.Add(WeightColor(wb)); colors.Add(WeightColor(wc));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);

                Vector2 Uv(Vector3 vertex)
                {
                    if (cap) return new Vector2(vertex.x, vertex.y);
                    return normal.x >= normal.y ? new Vector2(vertex.y, vertex.z) : new Vector2(vertex.x, vertex.z);
                }
            }
        }

        private static Color32 WeightColor(float weight) =>
            new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(weight) * byte.MaxValue), 0, 0, byte.MaxValue);

        private static float Interpolate(Vector2[] polygon, float[] weights, Vector2 point)
        {
            float best = float.NegativeInfinity, value = 0f;
            Vector2 a = polygon[0];
            for (int i = 1; i < polygon.Length - 1; i++)
            {
                Vector2 b = polygon[i] - a, c = polygon[i + 1] - a, p = point - a;
                float area = b.x * c.y - b.y * c.x;
                if (area == 0f) continue;
                float u = (p.x * c.y - p.y * c.x) / area;
                float v = (b.x * p.y - b.y * p.x) / area;
                float w = 1f - u - v;
                float score = Mathf.Min(w, Mathf.Min(u, v));
                if (score > best)
                {
                    best = score;
                    value = w * weights[0] + u * weights[i] + v * weights[i + 1];
                }
                if (score >= 0f) return Mathf.Clamp01(value);
            }
            if (float.IsNegativeInfinity(best))
                throw new InvalidOperationException("Cannot interpolate a degenerate asteroid polygon.");
            return Mathf.Clamp01(value);
        }
    }
}
