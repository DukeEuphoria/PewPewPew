using Mirror;
using System.Collections.Generic;
using PewPewPew.GameSystems;
using UnityEngine;

namespace PewPewPew.World
{
    /// Base of everything in the game world. Gravity is applied on the server only.
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class SpaceObject : NetworkBehaviour
    {
        protected Rigidbody2D m_Body;
        private static readonly HashSet<SpaceObject> m_ActiveObjects = new HashSet<SpaceObject>();

        public static IReadOnlyCollection<SpaceObject> ActiveObjects => m_ActiveObjects;

        protected virtual void OnEnable() => m_ActiveObjects.Add(this);

        protected virtual void OnDisable() => m_ActiveObjects.Remove(this);

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
