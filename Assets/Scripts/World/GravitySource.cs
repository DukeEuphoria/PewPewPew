using PewPewPew.GameSystems;
using UnityEngine;

namespace PewPewPew.World
{
    /// Attach to any object to make it pull other space objects toward it.
    public class GravitySource : MonoBehaviour
    {
        [SerializeField] private float m_Mass = 1000f;

        public float Mass => m_Mass;

        private void OnEnable() => GameWorld.Instance.Register(this);

        private void OnDisable()
        {
            if (GameWorld.Instance != null) GameWorld.Instance.Unregister(this);
        }
    }
}
