using Mirror;
using PewPewPew.GameSystems;
using UnityEngine;

namespace PewPewPew.World
{
    /// Base of everything in the game world. Gravity is applied on the server only.
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class SpaceObject : NetworkBehaviour
    {
        protected Rigidbody2D m_Body;

        protected virtual void Awake()
        {
            m_Body = GetComponent<Rigidbody2D>();
            m_Body.gravityScale = 0f;
        }

        private void FixedUpdate() => OnPhysicsStep();

        protected virtual void OnPhysicsStep()
        {
            if (isServer) ApplyGravity();
        }

        public void ApplyGravity()
        {
            m_Body.AddForce(GameWorld.Instance.ForceOn(m_Body.position, m_Body.mass));
        }
    }
}
