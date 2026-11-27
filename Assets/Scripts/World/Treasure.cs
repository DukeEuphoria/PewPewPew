using System;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;

namespace PewPewPew.World
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Treasure : SpaceObject
    {
        private const float MassPerRadiusSquared = 1f;
        private const string BaseColorProperty = "_BaseColor";
        internal const uint RenderingLayers = 12;
        [SerializeField] private TreasureMaterial m_Material;
        [SerializeField] private TreasureSize m_Size;
        [SerializeField, Min(1)] private int m_Units = 1;
        [SyncVar] private float m_Scale = 1f;
        private bool m_Embedded;
        private bool m_Removed;

        public TreasureMaterial Material => m_Material;
        public TreasureSize Size => m_Size;
        public int Units => m_Units;
        public float BaseRadius => GetComponent<CircleCollider2D>().radius;
        public float Radius => BaseRadius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        public bool IsAvailable => !m_Embedded && !m_Removed;
        internal ushort Id { get; private set; }
        internal bool Peeking { get; private set; }
        internal Vector2 Position
        {
            get => transform.localPosition;
            set => transform.localPosition = new Vector3(value.x, value.y, transform.localPosition.z);
        }

        internal void Pull(Vector2 force) => m_Body.AddForce(force);

        internal void Embed(Transform parent, ushort id, Vector2 position, float radius, bool peeking)
        {
            float baseRadius = BaseRadius;
            if (!float.IsFinite(radius) || radius <= 0f || !float.IsFinite(baseRadius) || baseRadius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            Id = id;
            Peeking = peeking;
            m_Embedded = true;
            m_Scale = radius / baseRadius;
            transform.SetParent(parent, false);
            transform.localScale = Vector3.one * m_Scale;
            Position = position;
            m_Body.simulated = false;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = parent.gameObject.layer;
        }

        internal void Release(Vector2 position, Quaternion rotation, Vector2 velocity, float angularVelocity)
        {
            ValidateRelease(position, rotation, velocity, angularVelocity);
            m_Embedded = false;
            transform.SetParent(null, false);
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = Vector3.one * m_Scale;
            m_Body.mass = MassPerRadiusSquared * Radius * Radius;
            m_Body.simulated = true;
            m_Body.linearVelocity = velocity;
            m_Body.angularVelocity = angularVelocity;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            NetworkServer.Spawn(gameObject);
        }

        private static void ValidateRelease(Vector2 position, Quaternion rotation, Vector2 velocity, float angularVelocity)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                !float.IsFinite(rotation.x) || !float.IsFinite(rotation.y) ||
                !float.IsFinite(rotation.z) || !float.IsFinite(rotation.w) ||
                !float.IsFinite(velocity.x) || !float.IsFinite(velocity.y) || !float.IsFinite(angularVelocity))
                throw new ArgumentException("Invalid treasure pickup transform or velocity.");
        }

        internal static void Spawn(Treasure prefab, Vector2 position, Quaternion rotation, Vector2 velocity, float angularVelocity)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            ValidateRelease(position, rotation, velocity, angularVelocity);
            Treasure instance = Instantiate(prefab);
            instance.Release(position, rotation, velocity, angularVelocity);
        }

        public override void OnStartClient()
        {
            if (!float.IsFinite(m_Scale) || m_Scale <= 0f)
                throw new InvalidOperationException("Invalid synchronized treasure scale.");
            transform.localScale = Vector3.one * m_Scale;
        }

        public static Color Colour(TreasureMaterial material)
        {
            Treasure prefab = AsteroidTreasure.Instance.Prefab(material, TreasureSize.Small);
            Renderer renderer = prefab.GetComponentInChildren<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(BaseColorProperty))
                throw new InvalidOperationException($"Treasure prefab {prefab.name} needs a material with a base colour.");
            return renderer.sharedMaterial.GetColor(BaseColorProperty);
        }

        [ServerCallback]
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (m_Removed || m_Embedded) return;
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
