using System;
using PewPewPew.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PewPewPew.World
{
    internal static class TreasureVisual
    {
        private const float MetalGloss = 0.7f;
        private const float CrystalGloss = 0.85f;
        private const float Metalness = 0.8f;
        internal const uint RenderingLayers = 12;
        private const string MaterialPath = "Treasure/TreasureLit";
        private const int MetalSides = 4;
        private const int CrystalSides = 6;
        private const int VerticesPerSide = 6;
        private static readonly Color Gold = new Color(1f, 0.68f, 0.08f);
        private static readonly Color Silver = new Color(0.75f, 0.8f, 0.85f);
        private static readonly Color Red = new Color(0.95f, 0.05f, 0.08f);
        private static readonly Color Green = new Color(0.08f, 0.85f, 0.18f);
        private static readonly Color Cyan = new Color(0.05f, 0.85f, 0.95f);
        private static readonly Color Purple = new Color(0.65f, 0.12f, 0.95f);
        private static readonly Color[] Colours = { Gold, Silver, Red, Green, Cyan, Purple };
        private static readonly Material[] Materials = new Material[Colours.Length];
        private static Mesh s_Metal;
        private static Mesh s_Crystal;

        internal static Color Colour(TreasureMaterial material)
        {
            int index = (int)material;
            if (index < 0 || index >= Colours.Length) throw new ArgumentOutOfRangeException(nameof(material));
            return Colours[index];
        }

        internal static GameObject Create(Transform parent, TreasureMaterial material, float radius)
        {
            int index = (int)material;
            if (index < 0 || index >= Colours.Length) throw new ArgumentOutOfRangeException(nameof(material));
            if (!AsteroidGeometry.Finite(radius) || radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            bool metal = material == TreasureMaterial.Gold || material == TreasureMaterial.Silver;
            if (s_Metal == null) s_Metal = CreateMesh(false);
            if (s_Crystal == null) s_Crystal = CreateMesh(true);
            if (Materials[index] == null)
            {
                Material template = Resources.Load<Material>(MaterialPath);
                if (template == null) throw new InvalidOperationException($"Missing treasure material at Resources/{MaterialPath}.");
                var created = new Material(template) { name = $"Treasure {material}" };
                created.SetColor("_BaseColor", Colours[index]);
                created.SetFloat("_Metallic", metal ? Metalness : 0f);
                created.SetFloat("_Smoothness", metal ? MetalGloss : CrystalGloss);
                Materials[index] = created;
            }
            var visual = new GameObject($"Treasure {material}");
            visual.layer = parent.gameObject.layer;
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * radius;
            visual.AddComponent<MeshFilter>().sharedMesh = metal ? s_Metal : s_Crystal;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials[index];
            renderer.renderingLayerMask = RenderingLayers;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return visual;
        }

        private static Mesh CreateMesh(bool crystal)
        {
            int sides = crystal ? CrystalSides : MetalSides;
            var vertices = new Vector3[sides * VerticesPerSide];
            var triangles = new int[vertices.Length];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 q = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
                int offset = i * VerticesPerSide;
                vertices[offset] = Vector3.back;
                vertices[offset + 1] = q;
                vertices[offset + 2] = p;
                vertices[offset + 3] = Vector3.forward;
                vertices[offset + 4] = p;
                vertices[offset + 5] = q;
                for (int j = 0; j < VerticesPerSide; j++) triangles[offset + j] = offset + j;
            }
            var mesh = new Mesh { name = crystal ? "Treasure crystal" : "Treasure metal" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
