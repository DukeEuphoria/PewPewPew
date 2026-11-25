using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PewPewPew.Editor
{
    public static class AsteroidMaterialBuilder
    {
        private const string MaterialPath = "Assets/Materials/Asteroid.mat";

        [MenuItem("PewPewPew/Create Default Asteroid Material")]
        public static void CreateDefaults()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                throw new InvalidOperationException("The default asteroid material already exists; edit its textures in the Inspector.");
            Shader shader = Shader.Find("PewPewPew/AsteroidLit");
            if (shader == null) throw new InvalidOperationException("AsteroidLit shader could not be loaded.");
            const int width = 128;
            var intact = new Texture2D(width, width, TextureFormat.RGB24, true);
            try
            {
                for (int y = 0; y < width; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 point = new Vector2(x, y) / width;
                    float grain = Hash(x + y * width + 100);
                    float shade = 0.48f + grain * 0.2f + Mathf.Cos(point.x * Mathf.PI * 8f) * Mathf.Sin(point.y * Mathf.PI * 8f) * 0.05f;
                    var rock = new Color(shade, shade * 0.93f, shade * 0.83f);
                    intact.SetPixel(x, y, rock);
                }
                intact.Apply();
                Texture2D baseMap = Save(intact, "Assets/Materials/AsteroidIntact.png");
                var material = new Material(shader) { name = "Asteroid" };
                material.SetTexture("_BaseMap", baseMap);
                material.SetTextureScale("_BaseMap", Vector2.one * 0.25f);
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(intact);
            }
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
