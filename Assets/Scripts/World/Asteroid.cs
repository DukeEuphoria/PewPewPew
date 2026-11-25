using System;
using System.Collections.Generic;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using UnityEngine;

namespace PewPewPew.World
{
    /// Outcome of a hit, used to pick the impact explosion.
    public enum HitResult { NoDamage, Damaged, ShieldAbsorbed }

    public interface IDamageable
    {
        /// attacker is the player responsible, or null for environmental damage.
        HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker);
    }

    /// Server-owned, drillable rock. Solidness is capped at 256 bytes regardless of physical size.
    public class Asteroid : SpaceObject, IDamageable
    {
        [SerializeField, Min(0.01f)] private float m_CellSpacing = 1f;
        [SerializeField, Min(0.01f)] private float m_Thickness = 1f;
        [SerializeField, Min(0.01f)] private float m_MaterialDensity = 1f;
        [SerializeField, Range(1f, 6.5f)] private float m_MinOccupiedRadius = 2f;
        [SerializeField, Range(1f, 6.5f)] private float m_MaxOccupiedRadius = 6.5f;
        [SerializeField, Min(0.01f)] private float m_MinScale = 0.9f;
        [SerializeField, Min(0.01f)] private float m_MaxScale = 1.1f;
        [SerializeField] private float m_FragmentScatterSpeed = 2f;
        [SerializeField] private float m_CollisionDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float m_CollisionDamageRadius = 1f;
        [SerializeField] private float m_OrbitalBodyDamageMultiplier = 10f;
        [SerializeField, Min(0.01f)] private float m_MinPieceMass = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_ReliefFraction = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum seeded vertex displacement as a fraction of cell width.")]
        private float m_VertexOffsetFactor = 0.5f;
        [SerializeField, Min(0.1f)] private float m_DespawnDelay = 0.2f;
        [SerializeField] private GameObject m_ChipExplosion;
        [SerializeField] private GameObject m_ShatterExplosion;
        [SerializeField] private AudioClip m_ChipSound;
        [SerializeField] private AudioClip m_ShatterSound;

        private float m_GenerationRadius;
        private uint m_Seed;
        private uint m_Revision;
        private AsteroidGeometry m_Field;
        private Vector2 m_Spacing;
        private float m_Depth;
        private float m_Density;
        private AsteroidSurface m_Surface;
        private bool m_HasSnapshot;
        private bool m_Broken;
        private AsteroidField m_OwnerField;
        private PolygonCollider2D m_SourceCollider;
        private CompositeCollider2D m_Collider;
        private MeshFilter m_Filter;
        private Mesh m_Mesh;
        private MaterialPropertyBlock m_MaterialProperties;
        private static readonly int SurfaceMapping = Shader.PropertyToID("_SurfaceMapping");
        private Vector2 m_EffectPoint;
        private Vector2 m_EffectVelocity;
        private bool m_ShatterEffect;

        public float Mass => m_Body.mass;

        protected override void Awake()
        {
            base.Awake();
            m_Filter = GetComponent<MeshFilter>();
            m_SourceCollider = GetComponent<PolygonCollider2D>();
            m_Collider = GetComponent<CompositeCollider2D>();
            if (m_SourceCollider != null && m_Collider != null)
            {
                m_Collider.generationType = CompositeCollider2D.GenerationType.Manual;
                m_Collider.geometryType = CompositeCollider2D.GeometryType.Polygons;
                m_Collider.vertexDistance = 0.0001f;
                m_Collider.offsetDistance = 0f;
                m_SourceCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
        }

        /// Server only, before the object is spawned.
        public void Initialize()
        {
            if (!Positive(m_CellSpacing) || !Positive(m_Thickness) || !Positive(m_MaterialDensity) ||
                !Positive(m_MinScale) || !Positive(m_MaxScale) || m_MaxScale < m_MinScale)
                throw new InvalidOperationException("Asteroid physical dimensions, density and ordered scale limits must be finite and positive.");
            m_Seed = unchecked((uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            m_GenerationRadius = AsteroidGeometry.InitialRadius(m_Seed, m_MinOccupiedRadius, m_MaxOccupiedRadius);
            m_Field = AsteroidGeometry.Generate(m_Seed, m_GenerationRadius);
            m_Field.ConfigurePositions(m_Seed ^ 0x9E3779B9u, m_VertexOffsetFactor, Vector2Int.zero);
            uint state = m_Seed ^ 0xC2B2AE35u;
            float scale = Mathf.Lerp(m_MinScale, m_MaxScale, (AsteroidGeometry.Next(ref state) >> 8) / 16777216f);
            m_Density = m_MaterialDensity;
            float step = m_CellSpacing * scale;
            m_Spacing = Vector2.one * step;
            m_Depth = m_Thickness * scale;
            Vector2 centre = Vector2.one * (AsteroidGeometry.MaxCells * 0.5f);
            float domeRadius = 0f;
            foreach (Vector2[] polygon in m_Field.Polygons())
                foreach (Vector2 point in polygon)
                    domeRadius = Mathf.Max(domeRadius, (point - centre).magnitude);
            m_Surface = new AsteroidSurface(m_Seed ^ 0x9E3779B9u, centre,
                Vector2.one / step, m_Depth, m_Depth * m_ReliefFraction, domeRadius, m_VertexOffsetFactor);
            m_HasSnapshot = false;
            m_Broken = false;
            m_Revision = 0;
            Rebuild();
            m_Body.linearVelocity = Vector2.zero;
            m_Body.angularVelocity = 0f;
        }

        internal void InitializePiece(AsteroidGeometry field, Vector2 spacing, float depth, float density,
            AsteroidSurface surface, uint seed)
        {
            m_Field = field;
            m_Spacing = spacing;
            m_Depth = depth;
            m_Density = density;
            m_Surface = surface;
            m_Seed = seed;
            m_GenerationRadius = 0f;
            m_HasSnapshot = true;
            Rebuild();
        }

        internal void CountIn(AsteroidField field) => m_OwnerField = field;

        public override void OnStartClient()
        {
            if (m_Field != null && m_Mesh == null) Rebuild();
            if (m_Broken) Hide();
        }

        public override void OnStopServer()
        {
            if (m_OwnerField != null) m_OwnerField.Forget();
            m_OwnerField = null;
        }

        protected override void OnPhysicsStep() { }

        public HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker) =>
            TakeAreaDamage(amount, point, m_CollisionDamageRadius, attacker);

        public HitResult TakeAreaDamage(float amount, Vector2 point, float radius, PlayerState attacker) =>
            ApplyDamage(amount, point, Vector2.zero, radius, 0f, false);

        public HitResult TakeProjectileDamage(float amount, Vector2 point, Vector2 direction,
            float radius, float crackChance, PlayerState attacker) =>
            ApplyDamage(amount, point, direction, radius, crackChance, true);

        private HitResult ApplyDamage(float amount, Vector2 point, Vector2 direction,
            float radius, float crackChance, bool projectile)
        {
            if (!isServer || m_Broken) return HitResult.NoDamage;
            if (!AsteroidGeometry.Finite(amount) || !AsteroidGeometry.Finite(point.x) || !AsteroidGeometry.Finite(point.y) ||
                !AsteroidGeometry.Finite(direction.x) || !AsteroidGeometry.Finite(direction.y) ||
                !AsteroidGeometry.Finite(radius) || radius < 0f ||
                !AsteroidGeometry.Finite(crackChance) || crackChance < 0f || crackChance > 1f ||
                (projectile && radius <= 0.5f && amount > 0f && direction.x == 0f && direction.y == 0f))
            {
                Debug.LogError("Asteroid damage requires finite inputs, a nonnegative radius, crack chance in [0,1], and a nonzero point-projectile impact vector.", this);
                return HitResult.NoDamage;
            }
            if (amount <= 0f) return HitResult.NoDamage;
            Vector3 local = transform.InverseTransformPoint(point);
            Vector2 sample = new Vector2(local.x / m_Spacing.x, local.y / m_Spacing.y) +
                             new Vector2(m_Field.Width - 1, m_Field.Height - 1) * 0.5f;
            Vector3 localDirection = transform.InverseTransformVector(direction);
            Vector2 sampleDirection = new Vector2(localDirection.x / m_Spacing.x, localDirection.y / m_Spacing.y);
            Vector2 axisX = transform.TransformVector(new Vector3(m_Spacing.x, 0f, 0f));
            Vector2 axisY = transform.TransformVector(new Vector3(0f, m_Spacing.y, 0f));
            if (m_Field.ErodeDamage(sample, sampleDirection, axisX, axisY, radius, amount, projectile) <= 0f)
                return HitResult.NoDamage;
            m_HasSnapshot = true;
            m_Revision++;
            SetSyncVarDirtyBit(1UL);
            List<AsteroidGeometry> components = m_Field.Components();
            uint state = unchecked(m_Seed + m_Revision * 747796405u);
            float roll = (AsteroidGeometry.Next(ref state) >> 8) / 16777216f;
            if (components.Count == 1 &&
                roll < crackChance)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    m_Field.Crack(sample, AsteroidGeometry.Next(ref state));
                    components = m_Field.Components();
                    if (components.Count >= 2) break;
                }
            }
            if (components.Count == 0 || (components.Count == 1 &&
                components[0].Area() * m_Spacing.x * m_Spacing.y * m_Depth * m_Density < m_MinPieceMass))
            {
                Retire(point, true);
                return HitResult.Damaged;
            }
            if (components.Count > 1)
            {
                SpawnPieces(components, point);
                return HitResult.Damaged;
            }
            m_Field = components[0];
            Rebuild();
            RecordEffect(point, false, m_Body.linearVelocity);
            return HitResult.Damaged;
        }

        [ServerCallback]
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.contactCount == 0) return;
            bool isOrbitalBody = collision.collider.GetComponentInParent<OrbitalBody>() != null;
            float multiplier = isOrbitalBody ? m_OrbitalBodyDamageMultiplier : m_CollisionDamageMultiplier;
            ContactPoint2D contact = collision.GetContact(0);
            TakeDamage(DamageMath.Impact(collision.relativeVelocity, contact.normal, multiplier), contact.point, null);
        }

        private void SpawnPieces(List<AsteroidGeometry> components, Vector2 point)
        {
            var measured = new List<(AsteroidGeometry field, float area)>(components.Count);
            foreach (AsteroidGeometry component in components)
                measured.Add((component, component.Area()));
            measured.Sort((a, b) => b.area.CompareTo(a.area));
            var retained = new List<AsteroidGeometry>();
            foreach (var component in measured)
            {
                float mass = component.area * m_Spacing.x * m_Spacing.y * m_Depth * m_Density;
                if (mass < m_MinPieceMass) continue;
                retained.Add(component.field);
            }
            if (retained.Count == 0) { Retire(point, true); return; }
            if (retained.Count == 1)
            {
                m_Field = retained[0];
                Rebuild();
                RecordEffect(point, false, m_Body.linearVelocity);
                return;
            }
            if (m_OwnerField == null)
                throw new InvalidOperationException("Cannot split an asteroid without its spawning AsteroidField.");
            var children = new List<Asteroid>();
            m_Collider.enabled = false;
            for (int i = 0; i < retained.Count; i++)
            {
                AsteroidGeometry piece = retained[i].Recenter(out Vector2 sampleOffset);
                Vector2 offset = Vector2.Scale(sampleOffset, m_Spacing);
                Vector2 position = transform.TransformPoint(offset);
                Vector2 direction = position - m_Body.position;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = Quaternion.Euler(0f, 0f, i * 137.5f) * Vector2.right;
                Asteroid child = m_OwnerField.SpawnPiece(piece, m_Spacing, m_Depth, m_Density, m_Surface.Child(offset),
                    m_Seed + (uint)i + m_Revision, position, transform.rotation,
                    m_Body.GetPointVelocity(position), m_Body.angularVelocity);
                child.m_Body.AddForce(direction.normalized * child.Mass * m_FragmentScatterSpeed, ForceMode2D.Impulse);
                children.Add(child);
            }
            m_OwnerField.SeparateSiblings(children);
            Retire(point, true);
        }

        private void Retire(Vector2 point, bool shatter)
        {
            m_Broken = true;
            m_Revision++;
            SetSyncVarDirtyBit(1UL);
            Vector2 velocity = m_Body.linearVelocity;
            Hide();
            RecordEffect(point, shatter, velocity);
            Invoke(nameof(Despawn), m_DespawnDelay);
        }

        private void Despawn() => NetworkServer.Destroy(gameObject);

        private void Hide()
        {
            m_Body.simulated = false;
            if (m_Collider != null) m_Collider.enabled = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        private void RecordEffect(Vector2 point, bool shatter, Vector2 velocity)
        {
            m_EffectPoint = point;
            m_EffectVelocity = velocity;
            m_ShatterEffect = shatter;
            if (isClient) PlayEffect();
        }

        private void PlayEffect()
        {
            var renderer = GetComponent<MeshRenderer>();
            AsteroidEffects.Play(m_EffectPoint, m_ShatterEffect ? m_ShatterExplosion : m_ChipExplosion,
                m_ShatterEffect ? m_ShatterSound : m_ChipSound, m_ShatterEffect, m_Seed + m_Revision, m_EffectVelocity,
                renderer != null ? renderer.sharedMaterial : null,
                Mathf.Min(m_Spacing.x, m_Spacing.y));
        }

        private void Rebuild()
        {
            if (m_Filter == null || m_SourceCollider == null || m_Collider == null)
                throw new InvalidOperationException("Asteroid prefab requires mesh, polygon and composite collider components.");
            transform.localScale = Vector3.one;
            List<Vector2[]> paths = AsteroidMesh.Paths(m_Field, m_Spacing, out float area);
            if (m_Mesh != null) Destroy(m_Mesh);
            m_Mesh = AsteroidMesh.Create(paths, m_Surface.Depth, m_Surface);
            m_Filter.sharedMesh = m_Mesh;
            m_SourceCollider.enabled = false;
            m_SourceCollider.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++) m_SourceCollider.SetPath(i, paths[i]);
            m_SourceCollider.enabled = true;
            m_Collider.GenerateGeometry();
            m_Body.mass = Mathf.Max(0.0001f, area * m_Spacing.x * m_Spacing.y * m_Depth * m_Density);
            UpdateSurface();
        }

        private void UpdateSurface()
        {
            if (m_MaterialProperties == null) m_MaterialProperties = new MaterialPropertyBlock();
            var renderer = GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(m_MaterialProperties);
            m_MaterialProperties.SetVector(SurfaceMapping,
                new Vector4(m_Surface.Scale.x, m_Surface.Scale.y, m_Surface.Offset.x, m_Surface.Offset.y));
            renderer.SetPropertyBlock(m_MaterialProperties);
        }

        private void OnDestroy()
        {
            if (m_Mesh != null) Destroy(m_Mesh);
        }

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteByte(AsteroidGeometry.Version);
            writer.WriteUInt(m_Seed);
            writer.WriteUInt(m_Revision);
            writer.WriteFloat(m_GenerationRadius);
            writer.WriteVector2(m_Spacing);
            writer.WriteFloat(m_Depth);
            writer.WriteFloat(m_Density);
            writer.WriteUInt(m_Surface.Seed);
            writer.WriteVector2(m_Surface.Offset);
            writer.WriteVector2(m_Surface.Scale);
            writer.WriteFloat(m_Surface.Depth);
            writer.WriteFloat(m_Surface.Relief);
            writer.WriteFloat(m_Surface.DomeRadius);
            writer.WriteFloat(m_Field.OffsetFactor);
            writer.WriteBool(m_Broken);
            writer.WriteBool(m_HasSnapshot);
            if (m_HasSnapshot)
            {
                writer.WriteByte((byte)m_Field.Width);
                writer.WriteByte((byte)m_Field.Height);
                writer.WriteBytes(m_Field.Samples, 0, m_Field.Samples.Length);
            }
            if (!initialState)
            {
                writer.WriteVector2(m_EffectPoint);
                writer.WriteVector2(m_EffectVelocity);
                writer.WriteBool(m_ShatterEffect);
            }
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            byte version = reader.ReadByte();
            if (version != AsteroidGeometry.Version) throw new InvalidOperationException($"Unsupported asteroid generator version {version}.");
            uint seed = reader.ReadUInt(), revision = reader.ReadUInt();
            float radius = reader.ReadFloat();
            Vector2 spacing = reader.ReadVector2();
            float depth = reader.ReadFloat(), density = reader.ReadFloat();
            uint surfaceSeed = reader.ReadUInt();
            Vector2 surfaceOffset = reader.ReadVector2(), surfaceScale = reader.ReadVector2();
            float surfaceDepth = reader.ReadFloat(), relief = reader.ReadFloat(), domeRadius = reader.ReadFloat();
            float offsetFactor = reader.ReadFloat();
            var surface = new AsteroidSurface(surfaceSeed, surfaceOffset, surfaceScale,
                surfaceDepth, relief, domeRadius, offsetFactor);
            bool broken = reader.ReadBool(), snapshot = reader.ReadBool();
            if (!AsteroidGeometry.Finite(radius) || radius < 0f || radius > 6.5f || (!snapshot && radius < 1f) ||
                !Positive(spacing.x) || !Positive(spacing.y) || !Positive(depth) ||
                !Positive(density))
                throw new InvalidOperationException("Invalid asteroid descriptor.");
            AsteroidGeometry field;
            if (snapshot)
            {
                int width = reader.ReadByte(), height = reader.ReadByte();
                if (width < 2 || height < 2 || width > AsteroidGeometry.MaxCells + 1 || height > AsteroidGeometry.MaxCells + 1)
                    throw new InvalidOperationException("Asteroid snapshot exceeds the 16x16-sample cap.");
                field = new AsteroidGeometry(width, height, reader.ReadBytes(width * height));
            }
            else field = AsteroidGeometry.Generate(seed, radius);
            Vector2 origin = surface.Offset - new Vector2(field.Width - 1, field.Height - 1) * 0.5f;
            field.ConfigurePositions(surface.Seed, offsetFactor,
                new Vector2Int(Mathf.RoundToInt(origin.x), Mathf.RoundToInt(origin.y)));
            Vector2 effectPoint = Vector2.zero, effectVelocity = Vector2.zero;
            bool shatterEffect = false;
            if (!initialState)
            {
                effectPoint = reader.ReadVector2();
                effectVelocity = reader.ReadVector2();
                shatterEffect = reader.ReadBool();
                if (!AsteroidGeometry.Finite(effectPoint.x) || !AsteroidGeometry.Finite(effectPoint.y) ||
                    !AsteroidGeometry.Finite(effectVelocity.x) || !AsteroidGeometry.Finite(effectVelocity.y))
                    throw new InvalidOperationException("Invalid asteroid effect coordinates.");
            }
            float mass = field.Area() * spacing.x * spacing.y * depth * density;
            if (!broken && !Positive(mass))
                throw new InvalidOperationException("A live asteroid must have finite positive mass.");
            if (isServer || (!initialState && revision <= m_Revision)) return;
            m_Seed = seed; m_Revision = revision; m_GenerationRadius = radius;
            m_Spacing = spacing; m_Depth = depth; m_Density = density;
            m_Broken = broken; m_HasSnapshot = snapshot;
            m_Surface = surface;
            m_Field = field;
            if (broken) Hide();
            else Rebuild();
            if (!initialState)
            {
                m_EffectPoint = effectPoint;
                m_EffectVelocity = effectVelocity;
                m_ShatterEffect = shatterEffect;
                PlayEffect();
            }
        }

        private static bool Positive(float value) => AsteroidGeometry.Finite(value) && value > 0f;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_CellSpacing = Mathf.Max(0.01f, m_CellSpacing);
            m_Thickness = Mathf.Max(0.01f, m_Thickness);
            m_MaterialDensity = Mathf.Max(0.01f, m_MaterialDensity);
            m_MinOccupiedRadius = Mathf.Clamp(m_MinOccupiedRadius, 1f, 6.5f);
            m_MaxOccupiedRadius = Mathf.Clamp(m_MaxOccupiedRadius, m_MinOccupiedRadius, 6.5f);
            m_VertexOffsetFactor = Mathf.Clamp01(m_VertexOffsetFactor);
            m_MinScale = Mathf.Max(0.01f, m_MinScale);
            m_MaxScale = Mathf.Max(m_MinScale, m_MaxScale);
            m_CollisionDamageRadius = Mathf.Max(0f, m_CollisionDamageRadius);
        }
    }
}
