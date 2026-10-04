using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Match
{
    /// Marks a place ships can launch from: a station, planet or moon. Parent it to an orbiting body to move with it.
    public class SpawnPoint : MonoBehaviour
    {
        private static readonly List<SpawnPoint> s_Points = new List<SpawnPoint>();

        private void OnEnable() => s_Points.Add(this);

        private void OnDisable() => s_Points.Remove(this);

        public static Vector2 Pick()
        {
            if (s_Points.Count == 0)
            {
                Debug.LogWarning("No SpawnPoint components in the scene; spawning at the origin.");
                return Vector2.zero;
            }
            return s_Points[Random.Range(0, s_Points.Count)].transform.position;
        }
    }
}
