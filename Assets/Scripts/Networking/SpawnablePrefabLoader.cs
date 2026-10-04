using Mirror;
using UnityEngine;

namespace PewPewPew.Networking
{
    /// Adds every NetworkIdentity prefab under Resources/SpawnablePrefabs (including subfolders) to the NetworkManager.
    public static class SpawnablePrefabLoader
    {
        private const string Folder = "SpawnablePrefabs";

        // Prefabs are only registered with Mirror when a client starts, so adding them after scene load is early enough.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Load()
        {
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
            {
                Debug.LogWarning("[Network] No NetworkManager in the first scene; spawnable prefabs were not loaded.");
                return;
            }

            foreach (GameObject prefab in Resources.LoadAll<GameObject>(Folder))
            {
                if (prefab.GetComponent<NetworkIdentity>() == null)
                {
                    Debug.LogWarning($"[Network] '{prefab.name}' in Resources/{Folder} has no NetworkIdentity and was skipped.");
                    continue;
                }
                if (prefab != manager.playerPrefab && !manager.spawnPrefabs.Contains(prefab)) manager.spawnPrefabs.Add(prefab);
            }
        }
    }
}
