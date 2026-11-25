#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mirror;
using Mirror.Tests;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PewPewPew.Tests
{
    public class AsteroidTests
    {
        private const string PrefabPath = "Assets/Resources/SpawnablePrefabs/Asteroids/PlaceHolder Asteroid.prefab";
        private const string BulletPrefabPath = "Assets/Resources/SpawnablePrefabs/Bullets/SimpleBullet.prefab";
        private const string InvalidDamage = "Asteroid damage requires finite inputs, a nonnegative radius, crack chance in [0,1], and a nonzero point-projectile impact vector.";
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private Type m_Type;
        private GameObject m_Server;
        private GameObject m_Client;
        private GameObject m_TransportObject;
        private GameObject m_FieldObject;
        private Transport m_PreviousTransport;

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(NetworkServer.active);
            m_Type = Type.GetType("PewPewPew.World.Asteroid, Assembly-CSharp", true);
            m_PreviousTransport = Transport.active;
            m_TransportObject = new GameObject("Asteroid test transport");
            m_TransportObject.AddComponent<AudioListener>();
            Transport.active = m_TransportObject.AddComponent<MemoryTransport>();
            NetworkServer.Listen(1);
            m_Server = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            m_Server.name = "Test asteroid";
            Set(m_Server.GetComponent(m_Type), "m_MinOccupiedRadius", 6.5f);
            Set(m_Server.GetComponent(m_Type), "m_MaxOccupiedRadius", 6.5f);
            Set(m_Server.GetComponent(m_Type), "m_VertexOffsetFactor", 0f);
            m_Type.GetMethod("Initialize").Invoke(m_Server.GetComponent(m_Type), null);
            NetworkServer.Spawn(m_Server);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.active = m_PreviousTransport;
            if (m_Server != null) Object.DestroyImmediate(m_Server);
            if (m_Client != null) Object.DestroyImmediate(m_Client);
            if (m_FieldObject != null) Object.DestroyImmediate(m_FieldObject);
            Object.DestroyImmediate(m_TransportObject);
            foreach (GameObject debris in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (debris.name == "Temporary asteroid fragment") Object.DestroyImmediate(debris);
        }

        private Component Server => m_Server.GetComponent(m_Type);
        private float Value(string name) => (float)m_Type.GetProperty(name).GetValue(Server);
        private void Set(Component asteroid, string field, object value) => m_Type.GetField(field, Private).SetValue(asteroid, value);
        private object Damage(float amount, Vector2? point = null, float radius = 0.5f) => m_Type.GetMethod("TakeAreaDamage").Invoke(Server,
            new object[] { amount, point ?? (Vector2)m_Server.transform.position, radius, null });
        private object Projectile(float amount, float chance = 0f, float radius = 0.5f,
            Vector2? point = null, Vector2? direction = null) => m_Type.GetMethod("TakeProjectileDamage").Invoke(Server,
            new object[] { amount, point ?? (Vector2)m_Server.transform.position, direction ?? Vector2.right, radius, chance, null });
        private AsteroidGeometry Geometry(Component asteroid) => (AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(asteroid);
        private int Solidness(Component asteroid) => Geometry(asteroid).Samples.Sum(value => (int)value);
        private void Rebuild(Component asteroid) => m_Type.GetMethod("Rebuild", Private).Invoke(asteroid, null);
        private void FireBullet(float asteroidRating, float shipRating = 0f, float chance = 0f, float radius = 0.5f)
        {
            Type type = Type.GetType("PewPewPew.Weapons.Bullet, Assembly-CSharp", true);
            var bulletObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath),
                m_Server.transform.position, Quaternion.identity);
            Component bullet = bulletObject.GetComponent(type);
            foreach (string field in new[] { "m_ShieldDamage", "m_ArmourDamage", "m_HullComponentDamage" })
                type.GetField(field, Private).SetValue(bullet, shipRating);
            type.GetField("m_AsteroidDamage", Private).SetValue(bullet, asteroidRating);
            type.GetField("m_AsteroidCrackChance", Private).SetValue(bullet, chance);
            type.GetField("m_DamageRadius", Private).SetValue(bullet, radius);
            bulletObject.GetComponent<Rigidbody2D>().linearVelocity = m_Server.transform.TransformDirection(Vector2.right) * 100f;
            NetworkServer.Spawn(bulletObject);
            type.GetMethod("OnTriggerEnter2D", Private).Invoke(bullet,
                new object[] { m_Server.GetComponent<CompositeCollider2D>() });
        }

        [TestCase(2f)]
        [TestCase(6.5f)]
        public void PositiveDamage_AccumulatesRegardlessOfMassWhileNonpositiveHitsDoNothing(float radius)
        {
            Set(Server, "m_MinOccupiedRadius", radius);
            Set(Server, "m_MaxOccupiedRadius", radius);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            var field = (AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server);
            byte[] before = (byte[])field.Samples.Clone();
            Assert.AreEqual("NoDamage", Damage(0f).ToString());
            Assert.AreEqual("NoDamage", Damage(-10f).ToString());
            CollectionAssert.AreEqual(before, field.Samples);
            Assert.AreEqual(0u, m_Type.GetField("m_Revision", Private).GetValue(Server));
            Assert.AreEqual("Damaged", Damage(0.25f).ToString());
            CollectionAssert.AreEqual(before, Geometry(Server).Samples);
            Assert.AreEqual("Damaged", Damage(0.75f).ToString());
            Assert.AreEqual(2u, m_Type.GetField("m_Revision", Private).GetValue(Server));
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(i == 136 ? 1 : 0, before[i] - Geometry(Server).Samples[i]);
        }

        [Test]
        public void Damage_NonfiniteInputIsReportedWithoutCorruptingMaterial()
        {
            byte[] before = (byte[])Geometry(Server).Samples.Clone();
            LogAssert.Expect(LogType.Error, InvalidDamage);
            Assert.AreEqual("NoDamage", Damage(float.NaN).ToString());
            CollectionAssert.AreEqual(before, Geometry(Server).Samples);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DrilledCorners_AreEmptyForRealPhysicsAndMesh(int span)
        {
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++) bytes[y * 9 + x] = 255;
            var field = new AsteroidGeometry(9, 9, bytes);
            for (int y = 4; y < 4 + span; y++)
            for (int x = 4; x < 4 + span; x++)
                field.ErodeImpact(new Vector2(x, y), Vector2.zero, 255f, false);
            Set(Server, "m_Field", field);
            Set(Server, "m_Spacing", Vector2.one);
            Rebuild(Server);
            Physics2D.SyncTransforms();
            var collider = m_Server.GetComponent<CompositeCollider2D>();
            Assert.Greater(collider.pathCount, 1, "The hole needs an inner contour.");
            Vector2 hole = Vector2.one * ((span - 1) * 0.5f);
            Assert.IsFalse(AsteroidGeometry.Contains(field.Polygons(), Vector2.one * 4f + hole));
            Assert.IsFalse(collider.OverlapPoint(hole));
            Assert.IsTrue(collider.OverlapPoint(new Vector2(-2f, 0f)));
            Assert.IsTrue(collider.OverlapPoint(new Vector2(-2f, 0.5f)), "No internal seam should leave a gap.");
            Mesh mesh = m_Server.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Less(mesh.vertexCount, 10000);
            Vector2[] uv = mesh.uv;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 a = uv[triangles[i + 1]] - uv[triangles[i]];
                Vector2 b = uv[triangles[i + 2]] - uv[triangles[i]];
                Assert.Greater(Mathf.Abs(a.x * b.y - a.y * b.x), 1e-12f,
                    "Exposed rock walls must not collapse their texture coordinates.");
            }
        }

        [Test]
        public void Snapshots_ReconstructDamagedAndUntouchedLateObserversWithinBudget()
        {
            Set(Server, "m_VertexOffsetFactor", 1f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            m_Client = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var replica = (NetworkBehaviour)m_Client.GetComponent(m_Type);
            var server = (NetworkBehaviour)Server;
            var writer = new NetworkWriter();
            server.OnSerialize(writer, true);
            Assert.AreEqual(67, writer.Position);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            Assert.AreEqual(Value("Mass"), m_Type.GetProperty("Mass").GetValue(replica));
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
            Damage(1f);
            writer.Reset();
            server.OnSerialize(writer, true);
            Assert.LessOrEqual(writer.Position, 325);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            CollectionAssert.AreEqual(((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server)).Samples,
                ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(replica)).Samples);
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
            Assert.AreEqual(Value("Mass"), m_Type.GetProperty("Mass").GetValue(replica));
            TestContext.WriteLine($"Untouched/damaged per-object geometry payload: 67/{writer.Position} bytes; 250 damaged: {writer.Position * 250} bytes.");
            byte[] malformed = writer.ToArray();
            int sampleCount = ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server)).Samples.Length;
            malformed[malformed.Length - sampleCount - 2] = 18;
            Assert.Throws<InvalidOperationException>(() => replica.OnDeserialize(new NetworkReader(malformed), true));
            byte[] incompatible = writer.ToArray();
            incompatible[0] = (byte)(AsteroidGeometry.Version - 1);
            Assert.Throws<InvalidOperationException>(() => replica.OnDeserialize(new NetworkReader(incompatible), true));
        }

        [Test]
        public void DisplacedGrid_IsVisibleInTheRoofMesh()
        {
            Set(Server, "m_VertexOffsetFactor", 1f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            AsteroidGeometry field = Geometry(Server);
            Vector2 spacing = (Vector2)m_Type.GetField("m_Spacing", Private).GetValue(Server);
            Vector3[] vertices = m_Server.GetComponent<MeshFilter>().sharedMesh.vertices;
            int checkedCorners = 0;
            for (int y = 6; y <= 9; y++)
            for (int x = 6; x <= 9; x++)
            {
                bool surrounded = true;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    surrounded &= field.Samples[(y + dy) * field.Width + x + dx] == 255;
                Vector2 grid = new Vector2(x, y);
                Vector2 displaced = field.Position(x, y);
                if (!surrounded || Vector2.Distance(grid, displaced) < 0.05f) continue;
                Vector2 expected = Vector2.Scale(displaced - Vector2.one * 7.5f, spacing);
                Vector2 oldGrid = Vector2.Scale(grid - Vector2.one * 7.5f, spacing);
                Assert.IsTrue(vertices.Any(v => v.z < 0f && Vector2.Distance(v, expected) < 0.0001f));
                Assert.IsFalse(vertices.Any(v => v.z < 0f && Vector2.Distance(v, oldGrid) < 0.0001f),
                    "The visible roof must not retain an undisplaced interior grid vertex.");
                checkedCorners++;
            }
            Assert.Greater(checkedCorners, 0);
        }

        [UnityTest]
        public IEnumerator InitialRock_RemainsStationaryButCanReceiveAnImpulse()
        {
            var body = m_Server.GetComponent<Rigidbody2D>();
            Assert.AreEqual(Vector2.zero, body.linearVelocity);
            Assert.AreEqual(0f, body.angularVelocity);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(Vector2.zero, body.linearVelocity);
            body.AddForce(Vector2.right * body.mass, ForceMode2D.Impulse);
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.x, 0f);
        }

        [Test]
        public void LargeCollisionDamage_EmptiesOnlyItsNearestCornerWithoutPropagation()
        {
            byte[] before = (byte[])Geometry(Server).Samples.Clone();
            Assert.AreEqual("Damaged", Damage(10000f).ToString());
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(i == 136 ? 0 : before[i], Geometry(Server).Samples[i]);
            Assert.IsTrue(m_Server.GetComponent<Rigidbody2D>().simulated);
            Assert.AreEqual(1, NetworkServer.spawned.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RotatedAsteroid_ProjectileOverflowTravelsForwardButCollisionStaysLocal(bool projectile)
        {
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++) bytes[y * 9 + x] = 255;
            Set(Server, "m_Field", new AsteroidGeometry(9, 9, bytes));
            Set(Server, "m_Spacing", Vector2.one * 0.8f);
            Rebuild(Server);
            m_Server.transform.SetPositionAndRotation(new Vector3(10, -6, 0), Quaternion.Euler(0, 0, 37));
            Vector2 point = m_Server.transform.TransformPoint(new Vector2(-0.8f, 0f));
            if (projectile)
                Projectile(256f, point: point, direction: m_Server.transform.TransformDirection(Vector2.right));
            else Damage(256f, point);
            Assert.AreEqual(0, Geometry(Server).Samples[39]);
            Assert.AreEqual(projectile ? 254 : 255, Geometry(Server).Samples[40]);
            Assert.AreEqual(255, Geometry(Server).Samples[38]);
        }

        [Test]
        public void ProjectileWithoutAnImpactVector_IsReportedWithoutChangingGeometry()
        {
            byte[] before = (byte[])Geometry(Server).Samples.Clone();
            LogAssert.Expect(LogType.Error, InvalidDamage);
            object result = Projectile(1f, direction: Vector2.zero);
            Assert.AreEqual("NoDamage", result.ToString());
            CollectionAssert.AreEqual(before, Geometry(Server).Samples);
        }

        [Test]
        public void CollisionRadius_DamagesFourCornersWithoutAChanceOfCracking()
        {
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++) bytes[y * 9 + x] = 255;
            Set(Server, "m_Field", new AsteroidGeometry(9, 9, bytes));
            Set(Server, "m_Spacing", Vector2.one);
            Rebuild(Server);
            m_Type.GetMethod("TakeDamage").Invoke(Server, new object[] { 100f, new Vector2(0.5f, 0.5f), null });
            for (int i = 0; i < bytes.Length; i++)
                Assert.AreEqual(i == 40 || i == 41 || i == 49 || i == 50 ? 226 : bytes[i], Geometry(Server).Samples[i]);
            Assert.AreEqual(1, NetworkServer.spawned.Count);
        }

        [TestCase(-1f, 0f)]
        [TestCase(0.5f, 1.01f)]
        [TestCase(float.NaN, 0f)]
        public void InvalidRadiusOrCrackChance_IsReportedWithoutChangingMaterial(float radius, float chance)
        {
            byte[] before = (byte[])Geometry(Server).Samples.Clone();
            LogAssert.Expect(LogType.Error, InvalidDamage);
            Assert.AreEqual("NoDamage", Projectile(1f, chance, radius).ToString());
            CollectionAssert.AreEqual(before, Geometry(Server).Samples);
        }

        [Test]
        public void TinyRemainingMaterial_RetiresInsteadOfCreatingAZeroMassRock()
        {
            Set(Server, "m_Field", new AsteroidGeometry(2, 2, new byte[] { 255, 0, 0, 0 }));
            Set(Server, "m_Spacing", Vector2.one * 0.01f);
            Set(Server, "m_Depth", 0.01f);
            Rebuild(Server);
            Damage(1f, new Vector2(-0.005f, -0.005f));
            Assert.IsFalse(m_Server.GetComponent<Rigidbody2D>().simulated);
            Assert.AreEqual(1, NetworkServer.spawned.Count);
        }

        [Test]
        public void TinyDetachedDebris_DoesNotReportAMultiPieceShatter()
        {
            var bytes = new byte[81];
            for (int y = 1; y <= 3; y++)
            for (int x = 1; x <= 3; x++) bytes[y * 9 + x] = 255;
            bytes[6 * 9 + 6] = 255;
            Set(Server, "m_Field", new AsteroidGeometry(9, 9, bytes));
            Set(Server, "m_Spacing", Vector2.one);
            Set(Server, "m_Depth", 1f);
            Set(Server, "m_MinPieceMass", 2f);
            Rebuild(Server);
            Damage(1f, new Vector2(-2f, -2f));
            Assert.AreEqual(1, NetworkServer.spawned.Count);
            Assert.IsTrue(m_Server.GetComponent<Rigidbody2D>().simulated);
            Assert.IsFalse((bool)m_Type.GetField("m_ShatterEffect", Private).GetValue(Server));
            Assert.AreEqual(1, ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server)).Components().Count);
        }

        [UnityTest]
        public IEnumerator PropagatedCrack_SpawnsConservedChildrenThatSeparateAndKeepFieldCount()
        {
            Set(Server, "m_VertexOffsetFactor", 1f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            Type fieldType = Type.GetType("PewPewPew.World.AsteroidField, Assembly-CSharp", true);
            m_FieldObject = new GameObject("Asteroid test field", typeof(NetworkIdentity));
            Component owner = m_FieldObject.AddComponent(fieldType);
            fieldType.GetField("m_Prefab", Private).SetValue(owner,
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent(m_Type));
            fieldType.GetField("m_Count", Private).SetValue(owner, 1);
            fieldType.GetField("m_TargetCount", Private).SetValue(owner, 0);
            Set(Server, "m_OwnerField", owner);
            m_Server.transform.SetPositionAndRotation(new Vector3(10f, -6f, 0f), Quaternion.Euler(0f, 0f, 37f));
            Physics2D.SyncTransforms();
            float originalMass = Value("Mass");
            int originalSolidness = Solidness(Server);
            var surface = (AsteroidSurface)m_Type.GetField("m_Surface", Private).GetValue(Server);
            FireBullet(1f, chance: 1f);
            var children = NetworkServer.spawned.Values.Where(identity =>
                identity.gameObject != m_Server && identity.GetComponent(m_Type) != null).ToArray();
            Assert.That(children.Length, Is.InRange(2, 4));
            int childSolidness = children.Sum(identity => Solidness(identity.GetComponent(m_Type)));
            Assert.Less(childSolidness, originalSolidness);
            float mass = children.Sum(identity => identity.GetComponent<Rigidbody2D>().mass);
            AsteroidGeometry cut = ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server));
            Vector2 spacing = (Vector2)m_Type.GetField("m_Spacing", Private).GetValue(Server);
            float depth = (float)m_Type.GetField("m_Depth", Private).GetValue(Server);
            float density = (float)m_Type.GetField("m_Density", Private).GetValue(Server);
            var retained = cut.Components().Where(piece => piece.Area() * spacing.x * spacing.y * depth * density >= 0.5f).ToArray();
            float expectedMass = retained.Sum(piece => piece.Area()) * spacing.x * spacing.y * depth * density;
            Assert.AreEqual(retained.Sum(piece => piece.Samples.Sum(value => (int)value)), childSolidness);
            Assert.AreEqual(expectedMass, mass, expectedMass * 0.00001f);
            Assert.LessOrEqual(mass, originalMass + 0.001f);
            Assert.Greater(mass, originalMass * 0.5f);
            m_Client = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            m_Client.GetComponent<Rigidbody2D>().simulated = false;
            foreach (NetworkIdentity child in children)
            {
                var field = (AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(child.GetComponent(m_Type));
                Assert.AreEqual(256, field.Samples.Length);
                Assert.AreEqual(spacing, m_Type.GetField("m_Spacing", Private).GetValue(child.GetComponent(m_Type)));
                Assert.AreEqual(depth, m_Type.GetField("m_Depth", Private).GetValue(child.GetComponent(m_Type)));
                Assert.AreEqual(density, m_Type.GetField("m_Density", Private).GetValue(child.GetComponent(m_Type)));
                var inherited = (AsteroidSurface)m_Type.GetField("m_Surface", Private).GetValue(child.GetComponent(m_Type));
                Assert.AreEqual(surface.DomeRadius, inherited.DomeRadius);
                Vector2 offset = m_Server.transform.InverseTransformPoint(child.transform.position);
                Assert.AreEqual(surface.Front(offset), inherited.Front(Vector2.zero), 0.00001f);
                Assert.IsTrue(retained.Any(piece => piece.Recenter(out _).Samples.SequenceEqual(field.Samples)));
                var writer = new NetworkWriter();
                ((NetworkBehaviour)child.GetComponent(m_Type)).OnSerialize(writer, true);
                Assert.LessOrEqual(writer.Position, 325);
                var replica = (NetworkBehaviour)m_Client.GetComponent(m_Type);
                replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
                CollectionAssert.AreEqual(child.GetComponent<MeshFilter>().sharedMesh.vertices,
                    m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
                Assert.AreEqual(child.GetComponent<Rigidbody2D>().mass, replica.GetComponent<Rigidbody2D>().mass, 0.00001f);
                Assert.Greater(child.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0f);
                Assert.IsTrue(Physics2D.GetIgnoreCollision(children[0].GetComponent<CompositeCollider2D>(),
                    children[1].GetComponent<CompositeCollider2D>()));
            }
            Vector2 before = children[0].transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(Vector2.Distance(before, children[0].transform.position), 0f);
            Assert.AreEqual(children.Length, fieldType.GetField("m_Count", Private).GetValue(owner));
            Assert.IsFalse(Physics2D.GetIgnoreCollision(children[0].GetComponent<CompositeCollider2D>(),
                children[1].GetComponent<CompositeCollider2D>()));
        }

        [Test]
        public void PopulationGeneration_VariesCoverageAndScaleWithGeometricMassAndConstantPayload()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long heap = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            long payload = 0;
            int maxVertices = 0;
            var writer = new NetworkWriter();
            float minimumArea = float.PositiveInfinity, maximumArea = 0f;
            float minimumScale = float.PositiveInfinity, maximumScale = 0f;
            Set(Server, "m_MinOccupiedRadius", 2f);
            Set(Server, "m_MaxOccupiedRadius", 6.5f);
            Set(Server, "m_MinScale", 0.9f);
            Set(Server, "m_MaxScale", 1.1f);
            for (int i = 0; i < 250; i++)
            {
                m_Type.GetMethod("Initialize").Invoke(Server, null);
                float area = Geometry(Server).Area();
                float scale = ((Vector2)m_Type.GetField("m_Spacing", Private).GetValue(Server)).x;
                Assert.That(scale, Is.InRange(0.9f, 1.1f));
                Assert.AreEqual(scale, (float)m_Type.GetField("m_Depth", Private).GetValue(Server), 0.00001f);
                Assert.AreEqual(area * scale * scale * scale, Value("Mass"), Value("Mass") * 0.00001f);
                minimumArea = Mathf.Min(minimumArea, area); maximumArea = Mathf.Max(maximumArea, area);
                minimumScale = Mathf.Min(minimumScale, scale); maximumScale = Mathf.Max(maximumScale, scale);
                writer.Reset();
                ((NetworkBehaviour)Server).OnSerialize(writer, true);
                Assert.AreEqual(67, writer.Position);
                payload += writer.Position;
                Mesh mesh = m_Server.GetComponent<MeshFilter>().sharedMesh;
                maxVertices = Mathf.Max(maxVertices, mesh.vertexCount);
            }
            stopwatch.Stop();
            Assert.AreEqual(16750, payload);
            Assert.Greater(maximumArea, minimumArea * 5f);
            Assert.Less(minimumScale, 0.95f);
            Assert.Greater(maximumScale, 1.05f);
            TestContext.WriteLine($"250 generated meshes/colliders: {stopwatch.ElapsedMilliseconds} ms; net managed heap change {UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - heap} bytes; descriptor bytes {payload}; maximum vertices {maxVertices}.");
        }

        [Test]
        public void DamageBurst_RemeshesAndRecentersWithinTheFixedGrid()
        {
            long heap = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 32; i++) Damage(1f);
            long remesh = timer.ElapsedMilliseconds;
            int pieces = 0;
            for (uint seed = 0; seed < 16; seed++)
            {
                var field = AsteroidGeometry.Generate(seed);
                field.Crack(new Vector2(1f, 8f), seed + 100);
                foreach (AsteroidGeometry piece in field.Components())
                {
                    if (piece.Area() < 1f) continue;
                    var fit = piece.Recenter(out _);
                    Assert.AreEqual(256, fit.Samples.Length);
                    Assert.AreEqual(piece.Area(), fit.Area(), 0.00001f);
                    pieces++;
                }
            }
            timer.Stop();
            TestContext.WriteLine($"32-hit remesh burst: {remesh} ms; 16 fractures/{pieces} recentered pieces: {timer.ElapsedMilliseconds - remesh} ms; net managed heap change {UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - heap} bytes.");
            Assert.Greater(pieces, 16);
        }

        [Test]
        public void LiveUpdate_AppliesGeometryBeforeEffectsAndDoesNotReplayOnLateJoin()
        {
            m_Client = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var replica = (NetworkBehaviour)m_Client.GetComponent(m_Type);
            var server = (NetworkBehaviour)Server;
            var writer = new NetworkWriter();
            server.OnSerialize(writer, true);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            Damage(1f);
            writer.Reset();
            server.OnSerialize(writer, true);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            Assert.IsFalse(Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Any(item => item.name == "Temporary asteroid fragment"));
            Damage(1f);
            writer.Reset();
            server.OnSerialize(writer, false);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), false);
            int fragments = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Count(item => item.name == "Temporary asteroid fragment");
            Assert.AreEqual(5, fragments);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), false);
            Assert.AreEqual(fragments, Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Count(item => item.name == "Temporary asteroid fragment"));
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
        }

        [UnityTest]
        public IEnumerator RemoteClient_ReceivesCurrentGeometryAndLiveEffectsThroughMirror()
        {
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Damage(1f);
            m_Server.GetComponent<Rigidbody2D>().simulated = false;
            NetworkClient.Connect("localhost");
            for (int i = 0; i < 30 && !NetworkClient.isConnected; i++) yield return null;
            Assert.IsTrue(NetworkClient.isConnected);
            NetworkClient.connection.isAuthenticated = true;
            NetworkConnectionToClient connection = NetworkServer.connections.Values.Single();
            connection.isAuthenticated = true;
            // Mirror streams initial observers only after a connection has a player identity.
            typeof(NetworkConnection).GetProperty("identity").SetValue(connection, m_Server.GetComponent<NetworkIdentity>());
            NetworkClient.Ready();
            uint id = m_Server.GetComponent<NetworkIdentity>().netId;
            for (int i = 0; i < 30 && !NetworkClient.spawned.ContainsKey(id); i++) yield return null;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(id));
            Component replica = NetworkClient.spawned[id].GetComponent(m_Type);
            CollectionAssert.AreEqual(((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server)).Samples,
                ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(replica)).Samples);
            Assert.IsFalse(Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Any(item => item.name == "Temporary asteroid fragment"), "Joining must not replay old debris.");
            Damage(1f);
            for (int i = 0; i < 30 && !Geometry(replica).Samples.SequenceEqual(Geometry(Server).Samples); i++)
                yield return null;
            CollectionAssert.AreEqual(Geometry(Server).Samples, Geometry(replica).Samples);
            Assert.AreEqual(5, Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Count(item => item.name == "Temporary asteroid fragment"));
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                replica.GetComponent<MeshFilter>().sharedMesh.vertices);
        }

        [Test]
        public void Host_SharedAsteroidPlaysItsEffectOnlyOnce()
        {
            NetworkClient.ConnectHost();
            HostMode.InvokeOnConnected();
            typeof(NetworkClient).GetField("isSpawnFinished", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, true);
            NetworkServer.Destroy(m_Server);
            m_Server = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            NetworkServer.Spawn(m_Server);
            Assert.IsTrue(((NetworkBehaviour)Server).isClient);
            Damage(1f);
            int Count() => Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Count(item => item.name == "Temporary asteroid fragment");
            Assert.AreEqual(5, Count());
            var writer = new NetworkWriter();
            ((NetworkBehaviour)Server).OnSerialize(writer, false);
            ((NetworkBehaviour)Server).OnDeserialize(new NetworkReader(writer.ToArray()), false);
            Assert.AreEqual(5, Count());
        }

        [Test]
        public void BulletAsteroidRating_IsIndependentOfShipRatings()
        {
            Type bulletType = Type.GetType("PewPewPew.Weapons.Bullet, Assembly-CSharp", true);
            Component prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath).GetComponent(bulletType);
            Assert.AreEqual(0.01f, bulletType.GetProperty("AsteroidCrackChance").GetValue(prefab));
            Assert.AreEqual(0.5f, bulletType.GetProperty("DamageRadius").GetValue(prefab));
            int solidness = Solidness(Server);
            FireBullet(0f, 1000f, chance: 1f);
            Assert.AreEqual(solidness, Solidness(Server));
            FireBullet(256f);
            Assert.Less(Solidness(Server), solidness);
            Assert.AreEqual(0, Geometry(Server).Samples[136]);
            Assert.AreEqual(254, Geometry(Server).Samples[137], "Bullet velocity must reach asteroid overflow traversal.");
        }

        [Test]
        public void BulletExplosion_UsesItsRadiusWithoutForwardPropagation()
        {
            Set(Server, "m_Spacing", Vector2.one);
            Rebuild(Server);
            FireBullet(1000f, radius: 1.1f);
            foreach (int index in new[] { 119, 120, 135, 136 })
                Assert.AreEqual(0, Geometry(Server).Samples[index]);
            Assert.AreEqual(255, Geometry(Server).Samples[137]);
        }

        [Test]
        public void SurfaceTexture_RendersOneRockTextureAcrossTheSurface()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Rock texture validation requires a graphics device.");
            var cameraObject = new GameObject("Rock blend camera", typeof(Camera));
            var lightObject = new GameObject("Rock blend light", typeof(Light));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("PewPewPew/AsteroidLit"));
            var intact = new Texture2D(1, 1);
            var target = new RenderTexture(64, 64, 24);
            var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                m_Server.SetActive(false);
                intact.SetPixel(0, 0, Color.green); intact.Apply();
                material.SetTexture("_BaseMap", intact);
                quad.layer = 31;
                quad.transform.localScale = Vector3.one * 2f;
                quad.GetComponent<Renderer>().sharedMaterial = material;
                quad.GetComponent<Renderer>().renderingLayerMask = 12u;
                var camera = cameraObject.GetComponent<Camera>();
                camera.cullingMask = 1 << quad.layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 1.2f;
                cameraObject.transform.position = new Vector3(0, 0, -10);
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.25f;
                lightObject.AddComponent<UniversalAdditionalLightData>().renderingLayers = 12u;
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); image.Apply();
                Color left = image.GetPixel(12, 32), middle = image.GetPixel(32, 32), right = image.GetPixel(52, 32);
                foreach (Color pixel in new[] { left, middle, right })
                {
                    Assert.Greater(pixel.g - pixel.r, 0.1f, "The single green rock texture must be visible across the surface.");
                    Assert.AreEqual(middle.g, pixel.g, 0.03f);
                    Assert.AreEqual(middle.r, pixel.r, 0.03f);
                }
            }
            finally
            {
                m_Server.SetActive(true);
                RenderTexture.active = previous;
                foreach (Object item in new Object[] { image, target, intact, material, quad, cameraObject, lightObject })
                    Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Relief_IsVisualOnlyAndSurvivingFacetsStayAnchored()
        {
            var surface = (AsteroidSurface)m_Type.GetField("m_Surface", Private).GetValue(Server);
            float mass = Value("Mass");
            var collider = m_Server.GetComponent<CompositeCollider2D>();
            int points = collider.pointCount;
            Vector3[] before = m_Server.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.IsTrue(before.Any(point => point.z < -surface.Depth * 0.5f));
            Assert.IsTrue(before.Where(point => point.z > 0f).All(point => Mathf.Abs(point.z - surface.Depth * 0.5f) < 0.00001f));
            foreach (Vector3 point in before.Where(point => point.z < 0f))
                Assert.AreEqual(surface.Front(point), point.z, surface.Depth * 0.00001f);
            Damage(1f);
            foreach (Vector3 point in m_Server.GetComponent<MeshFilter>().sharedMesh.vertices.Where(point => point.z < 0f))
                Assert.AreEqual(surface.Front(point), point.z, surface.Depth * 0.00001f);
            byte[] damaged = (byte[])Geometry(Server).Samples.Clone();
            Assert.Greater(-surface.Front(Vector2.zero) - surface.Depth * 0.5f,
                surface.DomeRadius / surface.Scale.x, "The original centre should include both dome height and rock relief.");
            Set(Server, "m_Surface", new AsteroidSurface(surface.Seed, surface.Offset, surface.Scale, surface.Depth, 0f, 0f));
            Rebuild(Server);
            CollectionAssert.AreEqual(damaged, Geometry(Server).Samples);
            Assert.AreEqual(mass, Value("Mass"), mass * 0.0001f);
            Assert.AreEqual(points, collider.pointCount);
            Assert.IsTrue(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices
                .Where(point => point.z < 0f).All(point => Mathf.Abs(point.z + surface.Depth * 0.5f) < 0.00001f));
        }

        [TestCase(20f)]
        [TestCase(50f)]
        public void GameDistanceRendering_CastsAndReceivesRealShadows(float zoom)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Shadow validation requires a graphics device.");
            var cameraObject = new GameObject("Asteroid shadow camera", typeof(Camera));
            var lightObject = new GameObject("Asteroid shadow light", typeof(Light));
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var caster = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caster.SetActive(false);
            var target = new RenderTexture(512, 512, 24);
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            var rock = m_Server.GetComponent<MeshRenderer>();
            var receiverMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var population = new System.Collections.Generic.List<GameObject>();
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = zoom;
                camera.farClipPlane = 200f;
                cameraObject.transform.position = new Vector3(0f, 0f, -100f);
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Hard;
                light.shadowBias = 0.02f;
                light.shadowNormalBias = 0.1f;
                light.intensity = 2f;
                lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
                lightObject.AddComponent<UniversalAdditionalLightData>().renderingLayers = 12u;
                receiver.transform.position = new Vector3(0f, 0f, 5f);
                receiver.transform.localScale = Vector3.one * 35f;
                receiver.GetComponent<Renderer>().sharedMaterial = receiverMaterial;
                receiver.GetComponent<Renderer>().renderingLayerMask = 12u;
                receiver.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                target.Create();
                Color[] Capture()
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    image.Apply();
                    return image.GetPixels();
                }
                Color[] shadowed = Capture();
                rock.shadowCastingMode = ShadowCastingMode.Off;
                Color[] unshadowed = Capture();
                int darker = shadowed.Zip(unshadowed, (a, b) => b.grayscale - a.grayscale).Count(delta => delta > 0.02f);
                Assert.Greater(darker, 100, "Casting must actually darken receiving surfaces at the game's Z=-100 camera distance.");
                rock.shadowCastingMode = ShadowCastingMode.On;
                receiver.SetActive(false);
                caster.SetActive(true);
                caster.transform.position = new Vector3(3f, 3f, -10f);
                caster.transform.localScale = new Vector3(3f, 3f, 1f);
                caster.GetComponent<Renderer>().sharedMaterial = receiverMaterial;
                caster.GetComponent<Renderer>().renderingLayerMask = 12u;
                Color[] receiving = Capture();
                caster.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Color[] notReceiving = Capture();
                int selfShadow = receiving.Zip(notReceiving, (a, b) => b.grayscale - a.grayscale).Count(delta => delta > 0.01f);
                TestContext.WriteLine($"Game-distance shadow pixels: cast {darker}, received {selfShadow}.");
                Assert.Greater(selfShadow, 5, "Rock surfaces must receive shadows, not merely set a renderer flag.");
                if (zoom == 50f)
                {
                    caster.SetActive(false);
                    rock.enabled = false;
                    population = RenderPopulation();
                    Capture();
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 5; i++) Capture();
                    double withShadows = timer.Elapsed.TotalMilliseconds / 5d;
                    foreach (GameObject copy in population)
                        copy.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Capture();
                    timer.Restart();
                    for (int i = 0; i < 5; i++) Capture();
                    TestContext.WriteLine($"250-renderer fixture at zoom 50: shadows on {withShadows:0.00} ms/render, off {timer.Elapsed.TotalMilliseconds / 5d:0.00} ms/render (synchronous render + GPU readback + pixel copy, not isolated GPU timing). Frame timing available: {FrameTimingManager.IsFeatureEnabled()}.");
                }
            }
            finally
            {
                rock.shadowCastingMode = ShadowCastingMode.On;
                rock.receiveShadows = true;
                rock.enabled = true;
                foreach (GameObject copy in population) Object.DestroyImmediate(copy);
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(receiver);
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(receiverMaterial);
            }
        }

        private System.Collections.Generic.List<GameObject> RenderPopulation()
        {
            var population = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < 250; i++)
            {
                var copy = new GameObject("Asteroid render-cost fixture", typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.position = new Vector3((i % 16 - 7.5f) * 6f, (i / 16 - 7.5f) * 6f, 0f);
                copy.transform.localScale = Vector3.one * 0.25f;
                copy.GetComponent<MeshFilter>().sharedMesh = m_Server.GetComponent<MeshFilter>().sharedMesh;
                copy.GetComponent<MeshRenderer>().sharedMaterial = m_Server.GetComponent<MeshRenderer>().sharedMaterial;
                copy.GetComponent<MeshRenderer>().renderingLayerMask = 12u;
                population.Add(copy);
            }
            return population;
        }

        [UnityTest]
        public IEnumerator ShadowPopulation_ReportsGpuFrameTimingWhenSupported()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !FrameTimingManager.IsFeatureEnabled())
                Assert.Ignore("GPU frame timing is unavailable on this graphics device.");
            var population = RenderPopulation();
            var cameraObject = new GameObject("Asteroid GPU camera", typeof(Camera));
            var lightObject = new GameObject("Asteroid GPU light", typeof(Light));
            var target = new RenderTexture(512, 512, 24);
            var timings = new FrameTiming[1];
            var enabled = new System.Collections.Generic.List<double>();
            var disabled = new System.Collections.Generic.List<double>();
            try
            {
                m_Server.GetComponent<MeshRenderer>().enabled = false;
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 50f;
                camera.farClipPlane = 200f;
                cameraObject.transform.position = new Vector3(0f, 0f, -100f);
                target.Create();
                camera.targetTexture = target;
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Hard;
                light.intensity = 2f;
                lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
                lightObject.AddComponent<UniversalAdditionalLightData>().renderingLayers = 12u;
                ulong previous = 0;
                for (int mode = 0; mode < 2; mode++)
                {
                    foreach (GameObject copy in population)
                        copy.GetComponent<Renderer>().shadowCastingMode = mode == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    for (int frame = 0; frame < 40; frame++)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        if (frame < 8 || FrameTimingManager.GetLatestTimings(1, timings) == 0 ||
                            timings[0].frameStartTimestamp == previous || timings[0].gpuFrameTime <= 0d) continue;
                        previous = timings[0].frameStartTimestamp;
                        (mode == 0 ? enabled : disabled).Add(timings[0].gpuFrameTime);
                    }
                }
                TestContext.WriteLine(enabled.Count > 0 && disabled.Count > 0
                    ? $"250-renderer GPU frame time: shadows on {enabled.Average():0.00} ms ({enabled.Count} samples), off {disabled.Average():0.00} ms ({disabled.Count} samples). Editor fixture, not a production frame budget."
                    : "Frame timing reports no positive GPU samples; use target-PC GPU profiling. Synchronous render/readback comparison is reported separately.");
            }
            finally
            {
                foreach (GameObject copy in population) Object.DestroyImmediate(copy);
                m_Server.GetComponent<MeshRenderer>().enabled = true;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void GeneratedRock_RendersWithItsExistingMaterial()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Visual validation requires a graphics device.");
            var cameraObject = new GameObject("Asteroid preview camera", typeof(Camera));
            var lightObject = new GameObject("Asteroid preview light", typeof(Light));
            var target = new RenderTexture(512, 512, 24);
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = m_Server.GetComponent<MeshFilter>().sharedMesh.bounds.size.magnitude * 0.55f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                cameraObject.transform.position = new Vector3(0f, -4f, -30f);
                cameraObject.transform.LookAt(Vector3.zero);
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                lightObject.transform.rotation = Quaternion.Euler(25f, -25f, 0f);
                lightObject.AddComponent<UniversalAdditionalLightData>().renderingLayers = 12u;
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request));
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                image.Apply();
                Assert.Greater(image.GetPixels().Count(pixel => pixel.maxColorComponent > 0.05f), 1000);
                string preview = Environment.GetEnvironmentVariable("PEWPEW_ASTEROID_PREVIEW");
                if (!string.IsNullOrEmpty(preview)) System.IO.File.WriteAllBytes(preview, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
            }
        }
    }
}
#endif
