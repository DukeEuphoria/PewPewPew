using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace PewPewPew.Networking
{
    /// Minimal networked player used to visually confirm connections/spawns are working end-to-end.
    public class NetworkPlayer : NetworkBehaviour
    {
        [FormerlySerializedAs("moveSpeed"), SerializeField] private float m_MoveSpeed = 4f;

        [SyncVar(hook = nameof(OnColorChanged))]
        [FormerlySerializedAs("playerColor")]
        private Color m_PlayerColor = Color.white;

        private Renderer m_CachedRenderer;

        private void Awake()
        {
            m_CachedRenderer = GetComponentInChildren<Renderer>();
        }

        public override void OnStartServer()
        {
            m_PlayerColor = new Color(Random.value, Random.value, Random.value);
        }

        public override void OnStartLocalPlayer()
        {
        //    if (Camera.main != null) Camera.main.transform.SetParent(transform);
        }

        private void OnColorChanged(Color oldColor, Color newColor)
        {
            if (m_CachedRenderer != null) m_CachedRenderer.material.color = newColor;
        }

        private void Update()
        {
            if (!isLocalPlayer || Keyboard.current == null) return;

            var input = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) input.z += 1;
            if (Keyboard.current.sKey.isPressed) input.z -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;

            transform.position += input.normalized * m_MoveSpeed * Time.deltaTime;
        }
    }
}