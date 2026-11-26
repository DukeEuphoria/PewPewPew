using System.IO;
using Mirror;
using PewPewPew.World;
using UnityEditor;
using UnityEngine;

namespace PewPewPew.Editor
{
    public static class TreasurePrefabBuilder
    {
        private const string Folder = "Assets/Resources/SpawnablePrefabs/Treasure";
        private const string Path = Folder + "/TreasurePickup.prefab";
        private const string MaterialFolder = "Assets/Resources/Treasure";
        private const string MaterialPath = MaterialFolder + "/TreasureLit.mat";

        [MenuItem("PewPewPew/Create Treasure Pickup Prefab")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.InvalidOperationException("Treasure requires the URP Lit shader.");
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
                AssetDatabase.CreateAsset(new Material(shader) { name = "TreasureLit" }, MaterialPath);
                AssetDatabase.SaveAssets();
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) return;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var root = new GameObject("TreasurePickup");
            root.AddComponent<NetworkIdentity>();
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            root.AddComponent<CircleCollider2D>();
            root.AddComponent<TreasurePickup>();
            var sync = root.AddComponent<NetworkRigidbodyUnreliable2D>();
            sync.target = root.transform;
            sync.updateMethod = UpdateMethod.FixedUpdate;
            sync.syncDirection = SyncDirection.ServerToClient;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Path);
            Object.DestroyImmediate(root);
            if (prefab == null) throw new System.InvalidOperationException("Could not save the treasure pickup prefab.");
            var identity = prefab.GetComponent<NetworkIdentity>();
            if (identity.assetId == 0) throw new System.InvalidOperationException("Treasure prefab has no network asset ID.");
            EditorUtility.SetDirty(identity);
            AssetDatabase.SaveAssets();
        }
    }
}
