using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PewPewPew.Ships
{
    internal static class ShieldMeshGenerator
    {
        public static Mesh CreateArcMesh(Vector2 forward, Vector3 scale, float halfAngle, int resolution,
            float fadeStart, float maxAlpha, float edgeAlpha)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(halfAngle * 2f * resolution / 120f));
            var angles = new List<float>(segments + 4);
            for (int i = 0; i <= segments; i++)
                angles.Add(Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments));
            AddAngle(angles, 0f);
            AddAngle(angles, -fadeStart);
            AddAngle(angles, fadeStart);
            angles.Sort();

            int columns = angles.Count;
            var vertices = new Vector3[(resolution + 1) * columns];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[resolution * (columns - 1) * 6];
            float centre = Mathf.Atan2(-forward.x, forward.y);
            float previousU = 0f;
            var longitude = new float[columns];
            for (int segment = 0; segment < columns; segment++)
            {
                float angle = centre + angles[segment] * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(-Mathf.Sin(angle) / scale.x, Mathf.Cos(angle) / scale.y).normalized;
                float u = Mathf.Atan2(direction.y, direction.x) / (2f * Mathf.PI) + 0.5f;
                if (segment > 0) u += Mathf.Round(previousU - u);
                longitude[segment] = previousU = u;
            }
            for (int latitude = 0; latitude <= resolution; latitude++)
            {
                float elevation = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, latitude / (float)resolution);
                for (int segment = 0; segment < columns; segment++)
                {
                    float angle = centre + angles[segment] * Mathf.Deg2Rad;
                    Vector2 ellipseDirection = new Vector2(-Mathf.Sin(angle) / scale.x, Mathf.Cos(angle) / scale.y).normalized;
                    int vertex = latitude * columns + segment;
                    vertices[vertex] = new Vector3(ellipseDirection.x * Mathf.Cos(elevation),
                        ellipseDirection.y * Mathf.Cos(elevation), Mathf.Sin(elevation)) * 0.5f;
                    uv[vertex] = new Vector2(longitude[segment], latitude / (float)resolution);
                    colors[vertex] = new Color(1f, 1f, 1f, Alpha(Mathf.Abs(angles[segment]), halfAngle, fadeStart, maxAlpha, edgeAlpha));
                    if (latitude == resolution || segment == columns - 1) continue;
                    int index = (latitude * (columns - 1) + segment) * 6;
                    triangles[index] = vertex;
                    triangles[index + 1] = vertex + 1;
                    triangles[index + 2] = vertex + columns;
                    triangles[index + 3] = vertex + 1;
                    triangles[index + 4] = vertex + columns + 1;
                    triangles[index + 5] = vertex + columns;
                }
            }
            var mesh = new Mesh { name = "Directional shield grid" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddAngle(List<float> angles, float angle)
        {
            foreach (float existing in angles)
                if (Mathf.Abs(existing - angle) < 0.00001f) return;
            angles.Add(angle);
        }

        public static Mesh CreateGeodesicArcMesh(Vector2 forward, Vector3 scale, float halfAngle, int resolution,
            float fadeStart, float maxAlpha, float edgeAlpha)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var source = new List<Vector3>
            {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
            };
            for (int i = 0; i < source.Count; i++) source[i] = source[i].normalized;
            var faces = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2,
                10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5,
                2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            for (int level = 0; level < resolution; level++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(faces.Count * 4);
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Midpoint(a, b, source, midpoints);
                    int bc = Midpoint(b, c, source, midpoints);
                    int ca = Midpoint(c, a, source, midpoints);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            float centre = Mathf.Atan2(-forward.x, forward.y);
            var boundaries = new List<float> { -halfAngle };
            if (halfAngle < 180f)
            {
                AddAngle(boundaries, -fadeStart);
                AddAngle(boundaries, 0f);
                AddAngle(boundaries, fadeStart);
            }
            AddAngle(boundaries, halfAngle);
            boundaries.Sort();
            for (int i = 0; i < faces.Count; i += 3)
            {
                var face = new List<Vector3> { source[faces[i]], source[faces[i + 1]], source[faces[i + 2]] };
                if (halfAngle >= 180f)
                {
                    AddPolygon(face, forward, scale, halfAngle, fadeStart, maxAlpha, edgeAlpha, vertices, normals, uvs, colors, triangles);
                    continue;
                }
                for (int sector = 0; sector < boundaries.Count - 1; sector++)
                {
                    Vector2 start = Direction(centre + boundaries[sector] * Mathf.Deg2Rad);
                    Vector2 end = Direction(centre + boundaries[sector + 1] * Mathf.Deg2Rad);
                    List<Vector3> polygon = Clip(face, start, scale, true);
                    polygon = Clip(polygon, end, scale, false);
                    AddPolygon(polygon, forward, scale, halfAngle, fadeStart, maxAlpha, edgeAlpha, vertices, normals, uvs, colors, triangles);
                }
            }
            var mesh = new Mesh { name = "Directional shield geodesic" };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
            if (cache.TryGetValue(key, out int index)) return index;
            index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]).normalized);
            cache.Add(key, index);
            return index;
        }

        private static Vector2 Direction(float angle) => new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

        private static float Side(Vector3 vertex, Vector2 edge, Vector3 scale, bool start)
        {
            float cross = edge.x * vertex.y * scale.y - edge.y * vertex.x * scale.x;
            return start ? cross : -cross;
        }

        private static List<Vector3> Clip(List<Vector3> polygon, Vector2 edge, Vector3 scale, bool start)
        {
            var result = new List<Vector3>();
            if (polygon.Count == 0) return result;
            Vector3 previous = polygon[polygon.Count - 1];
            float previousSide = Side(previous, edge, scale, start);
            foreach (Vector3 current in polygon)
            {
                float currentSide = Side(current, edge, scale, start);
                bool inside = currentSide >= 0f;
                bool previousInside = previousSide >= 0f;
                if (inside != previousInside)
                    result.Add(Vector3.Lerp(previous, current, previousSide / (previousSide - currentSide)).normalized);
                if (inside) result.Add(current);
                previous = current;
                previousSide = currentSide;
            }
            return result;
        }

        private static void AddPolygon(List<Vector3> polygon, Vector2 forward, Vector3 scale, float halfAngle,
            float fadeStart, float maxAlpha, float edgeAlpha, List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<Color> colors, List<int> triangles)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                Vector3 a = polygon[0], b = polygon[i], c = polygon[i + 1];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(cross, a + b + c) < 0f) { Vector3 temp = b; b = c; c = temp; }
                Vector3[] face = { a, b, c };
                var faceUv = new Vector2[3];
                for (int j = 0; j < 3; j++)
                    faceUv[j] = new Vector2(Mathf.Atan2(face[j].y, face[j].x) / (2f * Mathf.PI) + 0.5f,
                        Mathf.Asin(Mathf.Clamp(face[j].z, -1f, 1f)) / Mathf.PI + 0.5f);
                float minU = Mathf.Min(faceUv[0].x, faceUv[1].x, faceUv[2].x);
                float maxU = Mathf.Max(faceUv[0].x, faceUv[1].x, faceUv[2].x);
                if (maxU - minU > 0.5f)
                    for (int j = 0; j < 3; j++) if (faceUv[j].x < 0.5f) faceUv[j].x += 1f;
                for (int j = 0; j < 3; j++)
                {
                    if (face[j].x * face[j].x + face[j].y * face[j].y < 1e-10f)
                        faceUv[j].x = (faceUv[(j + 1) % 3].x + faceUv[(j + 2) % 3].x) * 0.5f;
                    Vector2 offset = new Vector2(face[j].x * scale.x, face[j].y * scale.y);
                    if (offset.sqrMagnitude < 1e-10f)
                        offset = new Vector2((face[(j + 1) % 3].x + face[(j + 2) % 3].x) * scale.x,
                            (face[(j + 1) % 3].y + face[(j + 2) % 3].y) * scale.y);
                    float angle = Vector2.Angle(forward, offset);
                    float alpha = halfAngle >= 180f ? maxAlpha : Alpha(angle, halfAngle, fadeStart, maxAlpha, edgeAlpha);
                    triangles.Add(vertices.Count);
                    vertices.Add(face[j] * 0.5f);
                    normals.Add(face[j]);
                    uvs.Add(faceUv[j]);
                    colors.Add(new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        private static float Alpha(float angle, float halfAngle, float fadeStart, float maxAlpha, float edgeAlpha) =>
            Mathf.Lerp(maxAlpha, edgeAlpha, Mathf.Clamp01((angle - fadeStart) / (halfAngle - fadeStart)));
    }
}
