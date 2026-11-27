using System;
using Mirror;
using PewPewPew.World;
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
                Register(manager, prefab);

            var types = AsteroidTreasure.Instance.Types;
            if (types == null) throw new InvalidOperationException("The treasure catalog has no types.");
            foreach (AsteroidTreasure.TreasureType type in types)
            {
                if (type == null || type.Prefabs == null)
                    throw new InvalidOperationException("The treasure catalog contains an invalid prefab list.");
                foreach (Treasure treasure in type.Prefabs)
                {
                    if (treasure == null) throw new InvalidOperationException("The treasure catalog contains a missing prefab.");
                    if (treasure.GetComponent<NetworkIdentity>() == null)
                        throw new InvalidOperationException($"Treasure prefab {treasure.name} has no NetworkIdentity.");
                    Register(manager, treasure.gameObject);
                }
            }
        }

        private static void Register(NetworkManager manager, GameObject prefab)
        {
            if (prefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogWarning($"[Network] '{prefab.name}' in Resources/{Folder} has no NetworkIdentity and was skipped.");
                return;
            }
            if (prefab != manager.playerPrefab && !manager.spawnPrefabs.Contains(prefab)) manager.spawnPrefabs.Add(prefab);
        }
    }
}
