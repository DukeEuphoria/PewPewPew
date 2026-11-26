using System;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;

namespace PewPewPew.World
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class TreasurePickup : SpaceObject
    {
        private const float MassPerRadiusSquared = 1f;
        private const string PrefabPath = "SpawnablePrefabs/Treasure/TreasurePickup";
        [SyncVar] private TreasureMaterial m_Material;
        [SyncVar] private TreasureSize m_Size;
        [SyncVar] private float m_Radius;
        private bool m_Removed;

        public TreasureMaterial Material => m_Material;
        public TreasureSize Size => m_Size;
        internal float Radius => m_Radius;
        public bool IsAvailable => !m_Removed;

        internal void Pull(Vector2 force) => m_Body.AddForce(force);

        internal static void Spawn(TreasureDeposit deposit, Vector2 position, Quaternion rotation,
            Vector2 velocity, float angularVelocity)
        {
            Validate(deposit, velocity, angularVelocity);
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException($"Missing network pickup prefab at Resources/{PrefabPath}.");
            if (prefab.GetComponent<TreasurePickup>() == null)
                throw new InvalidOperationException("The treasure pickup prefab lacks TreasurePickup.");
            GameObject instance = Instantiate(prefab, position, rotation);
            var pickup = instance.GetComponent<TreasurePickup>();
            pickup.Initialize(deposit, velocity, angularVelocity);
            NetworkServer.Spawn(instance);
        }

        internal void Initialize(TreasureDeposit deposit, Vector2 velocity, float angularVelocity)
        {
            Validate(deposit, velocity, angularVelocity);
            m_Material = deposit.Material;
            m_Size = deposit.Size;
            m_Radius = deposit.Radius;
            GetComponent<CircleCollider2D>().radius = m_Radius;
            m_Body.mass = MassPerRadiusSquared * m_Radius * m_Radius;
            m_Body.linearVelocity = velocity;
            m_Body.angularVelocity = angularVelocity;
        }

        private static void Validate(TreasureDeposit deposit, Vector2 velocity, float angularVelocity)
        {
            if ((int)deposit.Material < 0 || (int)deposit.Material > (int)TreasureMaterial.PurpleCrystal ||
                (int)deposit.Size < 0 || (int)deposit.Size > (int)TreasureSize.Large ||
                !AsteroidGeometry.Finite(deposit.Radius) || deposit.Radius <= 0f ||
                !AsteroidGeometry.Finite(velocity.x) || !AsteroidGeometry.Finite(velocity.y) ||
                !AsteroidGeometry.Finite(angularVelocity))
                throw new ArgumentException("Invalid treasure pickup descriptor.");
        }

        public override void OnStartClient()
        {
            if (!AsteroidGeometry.Finite(m_Radius) || m_Radius <= 0f ||
                (int)m_Material > (int)TreasureMaterial.PurpleCrystal || (int)m_Size > (int)TreasureSize.Large)
                throw new InvalidOperationException("Invalid synchronized treasure pickup descriptor.");
            GetComponent<CircleCollider2D>().radius = m_Radius;
            TreasureVisual.Create(transform, m_Material, m_Radius);
        }

        [ServerCallback]
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (m_Removed) return;
            Ship ship = collision.collider.GetComponentInParent<Ship>();
            if (ship != null)
            {
                if (!ship.TryCollect(this)) return;
            }
            else if (collision.collider.GetComponentInParent<OrbitalBody>() == null) return;
            m_Removed = true;
            NetworkServer.Destroy(gameObject);
        }
    }
}
