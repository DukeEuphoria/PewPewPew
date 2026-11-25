using System.Collections.Generic;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.World
{
    internal static class AsteroidMesh
    {
        public static List<Vector2[]> Paths(AsteroidGeometry field, Vector2 spacing, out float area)
        {
            List<Vector2[]> paths = field.Polygons();
            Vector2 centre = new Vector2(field.Width - 1, field.Height - 1) * 0.5f;
            area = 0f;
            foreach (Vector2[] path in paths)
            {
                area += AsteroidGeometry.PolygonArea(path);
                for (int i = 0; i < path.Length; i++) path[i] = Vector2.Scale(path[i] - centre, spacing);
            }
            return paths;
        }

        public static Mesh Create(List<Vector2[]> paths, float depth, AsteroidSurface? surface = null)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            foreach (Vector2[] path in paths)
            {
                for (int i = 1; i < path.Length - 1; i++)
                {
                    Face(new Vector3(path[0].x, path[0].y, depth * 0.5f),
                        new Vector3(path[i].x, path[i].y, depth * 0.5f),
                        new Vector3(path[i + 1].x, path[i + 1].y, depth * 0.5f), true);
                }
                foreach (Vector2[] facet in surface.HasValue ? surface.Value.Facets(path) : new List<Vector2[]> { path })
                    for (int i = 1; i < facet.Length - 1; i++)
                        Face(Front(facet[0]), Front(facet[i + 1]), Front(facet[i]), true);
            }
            foreach (AsteroidGeometry.BoundaryEdge edge in AsteroidGeometry.Boundary(paths))
            {
                List<float> cuts = surface.HasValue ? surface.Value.WallCuts(edge.A, edge.B) : new List<float> { 0f, 1f };
                for (int i = 1; i < cuts.Count; i++)
                {
                    Vector2 start = Vector2.Lerp(edge.A, edge.B, cuts[i - 1]);
                    Vector2 end = Vector2.Lerp(edge.A, edge.B, cuts[i]);
                    Vector3 a = Front(start), b = Front(end);
                    Vector3 c = new Vector3(end.x, end.y, depth * 0.5f);
                    Vector3 d = new Vector3(start.x, start.y, depth * 0.5f);
                    Face(a, b, c, false);
                    Face(a, c, d, false);
                }
            }
            var mesh = new Mesh { name = "Seeded asteroid rock" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            Vector3 Front(Vector2 point) => new Vector3(point.x, point.y,
                surface.HasValue ? surface.Value.Front(point) : -depth * 0.5f);

            void Face(Vector3 a, Vector3 b, Vector3 c, bool cap)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-20f) return;
                normal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(Uv(a)); uv.Add(Uv(b)); uv.Add(Uv(c));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);

                Vector2 Uv(Vector3 vertex)
                {
                    if (cap) return new Vector2(vertex.x, vertex.y);
                    return normal.x >= normal.y ? new Vector2(vertex.y, vertex.z) : new Vector2(vertex.x, vertex.z);
                }
            }
        }
    }
}
