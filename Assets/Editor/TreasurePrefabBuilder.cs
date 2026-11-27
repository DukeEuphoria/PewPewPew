using System;
using System.IO;
using Mirror;
using PewPewPew.Core;
using PewPewPew.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PewPewPew.Editor
{
    public static class TreasurePrefabBuilder
    {
        private const string PrefabFolder = "Assets/Resources/SpawnablePrefabs/Treasure";
        private const string AssetFolder = "Assets/Resources/Treasure";
        private const string CatalogPath = "Assets/Resources/AsteroidTreasure.asset";
        private const int MetalSides = 4;
        private const int CrystalSides = 6;
        private const int VerticesPerSide = 6;
        private const uint RenderingLayers = 12;
        private const float Metalness = 0.8f;
        private const float MetalGloss = 0.7f;
        private const float CrystalGloss = 0.85f;
        private static readonly float[] Radii = { 0.25f, 0.4f, 0.6f };
        private static readonly Color[] Colours =
        {
            new Color(1f, 0.68f, 0.08f), new Color(0.75f, 0.8f, 0.85f),
            new Color(0.95f, 0.05f, 0.08f), new Color(0.08f, 0.85f, 0.18f),
            new Color(0.05f, 0.85f, 0.95f), new Color(0.65f, 0.12f, 0.95f)
        };

        [MenuItem("PewPewPew/Create Treasure Catalog and Prefabs")]
        public static void Create()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();
            Mesh metal = MeshAsset("Metal", MetalSides);
            Mesh crystal = MeshAsset("Crystal", CrystalSides);
            var prefabs = new Treasure[ShipCargo.MaterialCount, Radii.Length];
            for (int material = 0; material < ShipCargo.MaterialCount; material++)
            {
                var kind = (TreasureMaterial)material;
                bool isMetal = kind == TreasureMaterial.Gold || kind == TreasureMaterial.Silver;
                Material surface = MaterialAsset(kind, Colours[material], isMetal);
                for (int size = 0; size < Radii.Length; size++)
                    prefabs[material, size] = Prefab(kind, (TreasureSize)size, Radii[size], size + 1,
                        isMetal ? metal : crystal, surface);
            }
            if (AssetDatabase.LoadAssetAtPath<AsteroidTreasure>(CatalogPath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<AsteroidTreasure>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                var serialized = new SerializedObject(catalog);
                SerializedProperty types = serialized.FindProperty("m_Types");
                types.arraySize = ShipCargo.MaterialCount;
                for (int material = 0; material < ShipCargo.MaterialCount; material++)
                {
                    SerializedProperty type = types.GetArrayElementAtIndex(material);
                    type.FindPropertyRelative("m_Material").enumValueIndex = material;
                    type.FindPropertyRelative("m_Weight").floatValue = 1f;
                    SerializedProperty list = type.FindPropertyRelative("m_Prefabs");
                    list.arraySize = Radii.Length;
                    for (int size = 0; size < Radii.Length; size++)
                        list.GetArrayElementAtIndex(size).objectReferenceValue = prefabs[material, size];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material MaterialAsset(TreasureMaterial kind, Color colour, bool metal)
        {
            string path = $"{AssetFolder}/{kind}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Treasure requires the URP Lit shader.");
            material = new Material(shader) { name = $"Treasure {kind}" };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metal ? Metalness : 0f);
            material.SetFloat("_Smoothness", metal ? MetalGloss : CrystalGloss);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh MeshAsset(string name, int sides)
        {
            string path = $"{AssetFolder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
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
            mesh = new Mesh { name = $"Treasure {name}", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Treasure Prefab(TreasureMaterial kind, TreasureSize size, float radius, int units, Mesh mesh, Material material)
        {
            string path = $"{PrefabFolder}/{kind} {size}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                EnsureNetworkIdentity(existing, path);
                return existing.GetComponent<Treasure>() ?? throw new InvalidOperationException($"{path} has no Treasure component.");
            }
            var root = new GameObject($"{kind} {size}");
            root.AddComponent<NetworkIdentity>();
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            root.AddComponent<CircleCollider2D>().radius = radius;
            Treasure item = root.AddComponent<Treasure>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("m_Material").enumValueIndex = (int)kind;
            serialized.FindProperty("m_Size").enumValueIndex = (int)size;
            serialized.FindProperty("m_Units").intValue = units;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var sync = root.AddComponent<NetworkRigidbodyUnreliable2D>();
            sync.target = root.transform;
            sync.updateMethod = UpdateMethod.FixedUpdate;
            sync.syncDirection = SyncDirection.ServerToClient;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * radius;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderingLayerMask = RenderingLayers;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException($"Could not create a treasure prefab at {path}.");
            EnsureNetworkIdentity(prefab, path);
            return prefab.GetComponent<Treasure>();
        }

        private static void EnsureNetworkIdentity(GameObject prefab, string path)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null || identity.assetId == 0)
                throw new InvalidOperationException($"Treasure prefab at {path} has no network asset ID.");
            EditorUtility.SetDirty(identity);
            PrefabUtility.SavePrefabAsset(prefab);
        }
    }
}
