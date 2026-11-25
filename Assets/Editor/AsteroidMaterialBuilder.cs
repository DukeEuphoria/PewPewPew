using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PewPewPew.Editor
{
    public static class AsteroidMaterialBuilder
    {
        private const string MaterialPath = "Assets/Materials/Asteroid.mat";
        private const string CrackedTexturePath = "Assets/Materials/AsteroidFractured.png";
        private const int TextureSize = 128;
        private const int CrackCells = 2;
        private const int CrackSeedX = 101;
        private const int CrackSeedY = 149;
        private const int GrainSeed = 100;
        private const float SiteMargin = 0.2f;
        private const float CrackCore = 0.16f;
        private const float CrackEdge = 0.32f;
        private const float CrackDarkness = 0.12f;
        private const float CrackedRockShade = 0.7f;
        private const float BaseShade = 0.48f;
        private const float GrainAmount = 0.2f;
        private const float WaveFrequency = 8f;
        private const float WaveAmount = 0.05f;
        private const float GreenTint = 0.93f;
        private const float BlueTint = 0.83f;

        [MenuItem("PewPewPew/Create Default Asteroid Material")]
        public static void CreateDefaults()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                throw new InvalidOperationException("The default asteroid material already exists; edit its textures in the Inspector.");
            Shader shader = Shader.Find("PewPewPew/AsteroidLit");
            if (shader == null) throw new InvalidOperationException("AsteroidLit shader could not be loaded.");
            const int width = TextureSize;
            var intact = new Texture2D(width, width, TextureFormat.RGB24, true);
            try
            {
                for (int y = 0; y < width; y++)
                for (int x = 0; x < width; x++)
                    intact.SetPixel(x, y, RockColor(x, y));
                intact.Apply();
                Texture2D baseMap = Save(intact, "Assets/Materials/AsteroidIntact.png");
                Texture2D cracked = AssetDatabase.LoadAssetAtPath<Texture2D>(CrackedTexturePath);
                if (cracked == null) cracked = GenerateCrackedTexture();
                var material = new Material(shader) { name = "Asteroid" };
                material.SetTexture("_BaseMap", baseMap);
                material.SetTexture("_CrackedMap", cracked);
                material.SetTextureScale("_BaseMap", Vector2.one * 0.25f);
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(intact);
            }
        }

        [MenuItem("PewPewPew/Create Default Cracked Asteroid Texture")]
        public static void CreateCrackedTexture() => GenerateCrackedTexture();

        private static Texture2D GenerateCrackedTexture()
        {
            if (File.Exists(CrackedTexturePath))
                throw new InvalidOperationException($"Refusing to overwrite {CrackedTexturePath}.");
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, true);
            try
            {
                for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    Vector2 point = new Vector2(x, y) / TextureSize;
                    Vector2 cellPoint = point * CrackCells;
                    int cellX = Mathf.FloorToInt(cellPoint.x), cellY = Mathf.FloorToInt(cellPoint.y);
                    float nearest = float.PositiveInfinity, second = float.PositiveInfinity;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int sx = cellX + dx, sy = cellY + dy;
                        int wrappedX = (sx + CrackCells) % CrackCells, wrappedY = (sy + CrackCells) % CrackCells;
                        int index = wrappedX + wrappedY * CrackCells;
                        var site = new Vector2(sx + Mathf.Lerp(SiteMargin, 1f - SiteMargin, Hash(index + CrackSeedX)),
                            sy + Mathf.Lerp(SiteMargin, 1f - SiteMargin, Hash(index + CrackSeedY)));
                        float distance = Vector2.Distance(cellPoint, site);
                        if (distance < nearest) { second = nearest; nearest = distance; }
                        else if (distance < second) second = distance;
                    }
                    float crack = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CrackCore, CrackEdge, second - nearest));
                    texture.SetPixel(x, y, RockColor(x, y) * CrackedRockShade * Mathf.Lerp(CrackDarkness, 1f, crack));
                }
                texture.Apply();
                return Save(texture, CrackedTexturePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Color RockColor(int x, int y)
        {
            Vector2 point = new Vector2(x, y) / TextureSize;
            float grain = Hash(x + y * TextureSize + GrainSeed);
            float shade = BaseShade + grain * GrainAmount +
                Mathf.Cos(point.x * Mathf.PI * WaveFrequency) * Mathf.Sin(point.y * Mathf.PI * WaveFrequency) * WaveAmount;
            return new Color(shade, shade * GreenTint, shade * BlueTint);
        }

        private static Texture2D Save(Texture2D texture, string path)
        {
            if (File.Exists(path)) throw new InvalidOperationException($"Refusing to overwrite {path}.");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Hash(int value)
        {
            uint state = unchecked((uint)value * 747796405u + 2891336453u);
            state = (state ^ (state >> 16)) * 2246822519u;
            return (state >> 8) / 16777216f;
        }
    }
}
