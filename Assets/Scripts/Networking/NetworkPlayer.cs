using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PewPewPew.Networking
{
    /// Minimal networked player used to visually confirm connections/spawns are working end-to-end.
    public class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;

        [SyncVar(hook = nameof(OnColorChanged))]
        private Color playerColor = Color.white;

        private Renderer cachedRenderer;

        private void Awake()
        {
            cachedRenderer = GetComponentInChildren<Renderer>();
        }

        public override void OnStartServer()
        {
            playerColor = new Color(Random.value, Random.value, Random.value);
        }

        public override void OnStartLocalPlayer()
        {
        //    if (Camera.main != null) Camera.main.transform.SetParent(transform);
        }

        private void OnColorChanged(Color _, Color newColor)
        {
            if (cachedRenderer != null) cachedRenderer.material.color = newColor;
        }

        private void Update()
        {
            if (!isLocalPlayer || Keyboard.current == null) return;

            var input = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) input.z += 1;
            if (Keyboard.current.sKey.isPressed) input.z -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;

            transform.position += input.normalized * moveSpeed * Time.deltaTime;
        }
    }
}