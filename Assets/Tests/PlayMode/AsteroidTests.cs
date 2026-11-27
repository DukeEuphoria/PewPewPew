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
        private GameObject m_WorldObject;
        private Transport m_PreviousTransport;
        private object m_Catalog;
        private float m_OriginalChance;
        private int m_OriginalMaximum;
        private float m_OriginalPeekingChance;

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(NetworkServer.active);
            m_Type = Type.GetType("PewPewPew.World.Asteroid, Assembly-CSharp", true);
            Type catalogType = Type.GetType("PewPewPew.World.AsteroidTreasure, Assembly-CSharp", true);
            m_Catalog = catalogType.GetProperty("Instance").GetValue(null);
            m_OriginalChance = (float)catalogType.GetField("m_Chance", Private).GetValue(m_Catalog);
            m_OriginalMaximum = (int)catalogType.GetField("m_Maximum", Private).GetValue(m_Catalog);
            m_OriginalPeekingChance = (float)catalogType.GetField("m_PeekingChance", Private).GetValue(m_Catalog);
            m_PreviousTransport = Transport.active;
            m_TransportObject = new GameObject("Asteroid test transport");
            m_TransportObject.AddComponent<AudioListener>();
            Transport.active = m_TransportObject.AddComponent<MemoryTransport>();
            NetworkServer.Listen(1);
            m_WorldObject = new GameObject("Asteroid test world");
            m_WorldObject.AddComponent(Type.GetType("PewPewPew.GameSystems.GameWorld, Assembly-CSharp", true));
            m_Server = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            m_Server.name = "Test asteroid";
            Set(m_Server.GetComponent(m_Type), "m_MinOccupiedRadius", 6.5f);
            Set(m_Server.GetComponent(m_Type), "m_MaxOccupiedRadius", 6.5f);
            Set(m_Server.GetComponent(m_Type), "m_VertexOffsetFactor", 0f);
            Set(m_Server.GetComponent(m_Type), "m_TreasureChance", 0f);
            m_Type.GetMethod("Initialize").Invoke(m_Server.GetComponent(m_Type), null);
            NetworkServer.Spawn(m_Server);
        }

        [TearDown]
        public void TearDown()
        {
            Type catalogType = m_Catalog.GetType();
            catalogType.GetField("m_Chance", Private).SetValue(m_Catalog, m_OriginalChance);
            catalogType.GetField("m_Maximum", Private).SetValue(m_Catalog, m_OriginalMaximum);
            catalogType.GetField("m_PeekingChance", Private).SetValue(m_Catalog, m_OriginalPeekingChance);
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.active = m_PreviousTransport;
            if (m_Server != null) Object.DestroyImmediate(m_Server);
            if (m_Client != null) Object.DestroyImmediate(m_Client);
            if (m_FieldObject != null) Object.DestroyImmediate(m_FieldObject);
            if (m_WorldObject != null) Object.DestroyImmediate(m_WorldObject);
            Object.DestroyImmediate(m_TransportObject);
            foreach (GameObject debris in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (debris.name == "Temporary asteroid fragment") Object.DestroyImmediate(debris);
        }

        private Component Server => m_Server.GetComponent(m_Type);
        private IList Deposits(Component asteroid) => (IList)m_Type.GetField("m_Treasure", Private).GetValue(asteroid);
        private static T DepositValue<T>(object deposit, string field) =>
            (T)deposit.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(deposit);
        private float Value(string name) => (float)m_Type.GetProperty(name).GetValue(Server);
        private void Set(Component asteroid, string field, object value)
        {
            string catalogField = field == "m_TreasureChance" ? "m_Chance" :
                field == "m_MaxTreasure" ? "m_Maximum" :
                field == "m_PeekingTreasureChance" ? "m_PeekingChance" : null;
            if (catalogField != null) m_Catalog.GetType().GetField(catalogField, Private).SetValue(m_Catalog, value);
            else m_Type.GetField(field, Private).SetValue(asteroid, value);
        }

        private void SetDeposit(object deposit, Vector2 position, float radius)
        {
            deposit.GetType().GetMethod("Embed", Private).Invoke(deposit, new object[]
            {
                m_Server.transform, DepositValue<ushort>(deposit, "Id"), position, radius,
                DepositValue<bool>(deposit, "Peeking")
            });
        }

        private object PrefabFor(object treasure) => m_Catalog.GetType().GetMethod("Prefab").Invoke(m_Catalog,
            new object[] { DepositValue<TreasureMaterial>(treasure, "Material"), DepositValue<TreasureSize>(treasure, "Size") });
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

        [Test]
        public void TreasureGeneration_UsesAllPrefabVariants()
        {
            Type generator = Type.GetType("PewPewPew.World.AsteroidTreasure, Assembly-CSharp", true);
            MethodInfo generate = generator.GetMethod("Generate", Private);
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            var materials = new System.Collections.Generic.HashSet<TreasureMaterial>();
            var sizes = new System.Collections.Generic.HashSet<TreasureSize>();
            for (uint seed = 0; seed < 1200; seed++)
            {
                var deposits = (IList)generate.Invoke(m_Catalog, new object[] { seed });
                Assert.AreEqual(1, deposits.Count);
                object deposit = deposits[0];
                materials.Add(DepositValue<TreasureMaterial>(deposit, "Material"));
                sizes.Add(DepositValue<TreasureSize>(deposit, "Size"));
            }
            Assert.AreEqual(6, materials.Count);
            Assert.AreEqual(3, sizes.Count);
        }

        [Test]
        public void TreasureGeneration_UsesCatalogWeights()
        {
            Type generator = m_Catalog.GetType();
            MethodInfo generate = generator.GetMethod("Generate", Private);
            var types = (IList)generator.GetField("m_Types", Private).GetValue(m_Catalog);
            var weights = new float[types.Count];
            var sizeWeights = (float[])generator.GetField("m_SizeWeights", Private).GetValue(m_Catalog);
            float[] originalSizeWeights = (float[])sizeWeights.Clone();
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            try
            {
                for (int i = 0; i < types.Count; i++)
                {
                    FieldInfo weight = types[i].GetType().GetField("m_Weight", Private);
                    weights[i] = (float)weight.GetValue(types[i]);
                    weight.SetValue(types[i], i == (int)TreasureMaterial.RedCrystal ? 1f : 0f);
                }
                for (int i = 0; i < sizeWeights.Length; i++)
                    sizeWeights[i] = i == (int)TreasureSize.Large ? 1f : 0f;
                for (uint seed = 0; seed < 100; seed++)
                {
                    var generated = (IList)generate.Invoke(m_Catalog, new object[] { seed });
                    Assert.AreEqual(TreasureMaterial.RedCrystal, DepositValue<TreasureMaterial>(generated[0], "Material"));
                    Assert.AreEqual(TreasureSize.Large, DepositValue<TreasureSize>(generated[0], "Size"));
                }
            }
            finally
            {
                for (int i = 0; i < types.Count; i++)
                    types[i].GetType().GetField("m_Weight", Private).SetValue(types[i], weights[i]);
                Array.Copy(originalSizeWeights, sizeWeights, sizeWeights.Length);
            }
        }

        [Test]
        public void TreasurePeeking_IsVisibleAtGameZoom()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to measure treasure visibility.");
            const int imageSize = 512;
            const int population = 100;
            const int channelThreshold = 20;
            const int visiblePixels = 2;
            var cameraObject = new GameObject("Treasure visibility camera", typeof(Camera));
            var lightObject = new GameObject("Treasure visibility light", typeof(Light));
            var target = new RenderTexture(imageSize, imageSize, 24);
            var image = new Texture2D(imageSize, imageSize, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                m_Server.layer = 31;
                Set(Server, "m_TreasureChance", 1f);
                Set(Server, "m_MaxTreasure", 1);
                Set(Server, "m_PeekingTreasureChance", 0.3f);
                Set(Server, "m_MinOccupiedRadius", 2f);
                Set(Server, "m_MaxOccupiedRadius", 6.5f);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 20f;
                camera.farClipPlane = 200f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                cameraObject.transform.position = new Vector3(0f, 0f, -100f);
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                lightObject.AddComponent<UniversalAdditionalLightData>().renderingLayers = 12u;
                target.Create();
                int visible = 0, visiblePeeking = 0, peeking = 0, eligible = 0;
                for (int i = 0; i < population; i++)
                {
                    UnityEngine.Random.InitState(i);
                    m_Type.GetMethod("Initialize").Invoke(Server, null);
                    if (Deposits(Server).Count == 0) continue;
                    eligible++;
                    bool peeks = DepositValue<bool>(Deposits(Server)[0], "Peeking");
                    if (peeks) peeking++;
                    m_Type.GetMethod("UpdateTreasureVisuals", Private).Invoke(Server, null);
                    var visual = ((Component)Deposits(Server)[0]).gameObject;
                    if (!visual.GetComponentInChildren<Renderer>().enabled) continue;
                    visual.SetActive(false);
                    Color32[] before = Render();
                    visual.SetActive(true);
                    Color32[] after = Render();
                    int changed = 0;
                    for (int p = 0; p < before.Length; p++)
                        if (Mathf.Max(Mathf.Abs(before[p].r - after[p].r), Mathf.Abs(before[p].g - after[p].g),
                                Mathf.Abs(before[p].b - after[p].b)) > channelThreshold) changed++;
                    if (changed >= visiblePixels)
                    {
                        visible++;
                        if (peeks) visiblePeeking++;
                    }
                }
                TestContext.WriteLine($"Visible items at game zoom: {visible}/{eligible}; " +
                    $"visible peeking items: {visiblePeeking}/{peeking}.");
                Assert.GreaterOrEqual(eligible, population * 0.9f);
                Assert.That((float)visible / eligible, Is.InRange(0.2f, 0.4f));
                Assert.GreaterOrEqual(visiblePeeking, peeking * 0.9f);

                Color32[] Render()
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
                    image.Apply();
                    return image.GetPixels32();
                }
            }
            finally
            {
                UnityEngine.Random.state = random;
                RenderTexture.active = previous;
                foreach (Object item in new Object[] { image, target, cameraObject, lightObject })
                    Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void TreasureSnapshot_ReconstructsAuthoritativeDeposit()
        {
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            Set(Server, "m_PeekingTreasureChance", 1f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            Assert.AreEqual(1, Deposits(Server).Count);
            m_Client = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var replica = (NetworkBehaviour)m_Client.GetComponent(m_Type);
            var writer = new NetworkWriter();
            ((NetworkBehaviour)Server).OnSerialize(writer, true);
            Assert.AreEqual(90, writer.Position);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            Assert.AreEqual(1, Deposits(replica).Count);
            object original = Deposits(Server)[0], copy = Deposits(replica)[0];
            Assert.AreEqual(DepositValue<TreasureMaterial>(original, "Material"), DepositValue<TreasureMaterial>(copy, "Material"));
            object[] detected = { TreasureMaterial.Gold };
            Assert.IsTrue((bool)m_Type.GetMethod("TryGetTreasureMaterial", Private).Invoke(replica, detected));
            Assert.AreEqual(DepositValue<TreasureMaterial>(original, "Material"), detected[0]);
            Assert.AreEqual(DepositValue<TreasureSize>(original, "Size"), DepositValue<TreasureSize>(copy, "Size"));
            Assert.AreEqual(DepositValue<Vector2>(original, "Position"), DepositValue<Vector2>(copy, "Position"));
            Assert.AreEqual(DepositValue<float>(original, "Radius"), DepositValue<float>(copy, "Radius"));
            Assert.AreEqual(DepositValue<bool>(original, "Peeking"), DepositValue<bool>(copy, "Peeking"));
            byte[] malformed = writer.ToArray();
            malformed[76] = byte.MaxValue;
            Assert.Throws<InvalidOperationException>(() => replica.OnDeserialize(new NetworkReader(malformed), true));
            Damage(1f, Vector2.zero);
            Assert.AreEqual(1, Deposits(Server).Count);
            writer.Reset();
            ((NetworkBehaviour)Server).OnSerialize(writer, true);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            Assert.AreEqual(1, Deposits(replica).Count);
            CollectionAssert.AreEqual(Geometry(Server).Samples, Geometry(replica).Samples);
        }

        [Test]
        public void TreasureSnapshots_RemainBoundedAcrossPopulation()
        {
            const int population = 250;
            const int emptyBytes = 73;
            const int depositBytes = 17;
            const int maximumDeposits = 16;
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", maximumDeposits);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var writer = new NetworkWriter();
            int totalDeposits = 0, maximumPayload = 0;
            for (int i = 0; i < population; i++)
            {
                m_Type.GetMethod("Initialize").Invoke(Server, null);
                int count = Deposits(Server).Count;
                writer.Reset();
                ((NetworkBehaviour)Server).OnSerialize(writer, true);
                Assert.AreEqual(emptyBytes + depositBytes * count, writer.Position);
                Assert.LessOrEqual(writer.Position, emptyBytes + depositBytes * maximumDeposits);
                totalDeposits += count;
                maximumPayload = Mathf.Max(maximumPayload, writer.Position);
            }
            timer.Stop();
            TestContext.WriteLine($"{population} treasure-bearing asteroid generations: {timer.ElapsedMilliseconds} ms; " +
                $"{totalDeposits} items; largest initial payload {maximumPayload} bytes.");
            Assert.Greater(totalDeposits, population);
        }

        [Test]
        public void TreasurePrefab_HasPersistentIdentityAndServerPhysics()
        {
            Type rigidbodySync = Type.GetType("Mirror.NetworkRigidbodyUnreliable2D, Mirror.Components", true);
            foreach (TreasureMaterial material in Enum.GetValues(typeof(TreasureMaterial)))
            foreach (TreasureSize size in Enum.GetValues(typeof(TreasureSize)))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Resources/SpawnablePrefabs/Treasure/{material} {size}.prefab");
                Assert.IsNotNull(prefab);
                Assert.AreNotEqual(0u, prefab.GetComponent<NetworkIdentity>().assetId);
                Assert.AreEqual(SyncDirection.ServerToClient, ((NetworkBehaviour)prefab.GetComponent(rigidbodySync)).syncDirection);
                Assert.IsFalse(prefab.GetComponent<CircleCollider2D>().isTrigger);
                Component treasure = prefab.GetComponent(Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true));
                Assert.AreEqual(material, DepositValue<TreasureMaterial>(treasure, "Material"));
                Assert.AreEqual(size, DepositValue<TreasureSize>(treasure, "Size"));
                Assert.AreSame(treasure, PrefabFor(treasure));
                Assert.AreEqual(ShipCargo.Units(size), DepositValue<int>(treasure, "Units"));
                Assert.Greater(DepositValue<float>(treasure, "Radius"), 0f);
                Assert.IsNotNull(prefab.GetComponentInChildren<MeshFilter>().sharedMesh);
                Assert.IsNotNull(prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            }
        }

        [Test]
        public void TreasureCatalog_RegistersReferencedPrefabsOutsideSpawnableResources()
        {
            Assert.IsTrue(NetworkManager.singleton == null);
            var types = (IList)m_Catalog.GetType().GetField("m_Types", Private).GetValue(m_Catalog);
            var prefabs = (IList)types[0].GetType().GetField("m_Prefabs", Private).GetValue(types[0]);
            object original = prefabs[0];
            GameObject external = Object.Instantiate(((Component)original).gameObject);
            var managerObject = new GameObject("Treasure catalog test manager");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<NetworkManager>();
            manager.dontDestroyOnLoad = false;
            manager.transport = Transport.active;
            try
            {
                managerObject.SetActive(true);
                prefabs[0] = external.GetComponent(original.GetType());
                Type loader = Type.GetType("PewPewPew.Networking.SpawnablePrefabLoader, Assembly-CSharp", true);
                MethodInfo load = loader.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic);
                load.Invoke(null, null);
                load.Invoke(null, null);
                Assert.AreEqual(1, manager.spawnPrefabs.Count(prefab => prefab == external));
            }
            finally
            {
                prefabs[0] = original;
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(external);
            }
        }

        [Test]
        public void TreasureRelease_RequiresClearedFootprintAndInheritsPointVelocity()
        {
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++) bytes[y * 9 + x] = 255;
            Set(Server, "m_Field", new AsteroidGeometry(9, 9, bytes));
            Set(Server, "m_Spacing", Vector2.one);
            object deposit = Deposits(Server)[0];
            SetDeposit(deposit, Vector2.zero, 0.25f);
            Rebuild(Server);
            var body = m_Server.GetComponent<Rigidbody2D>();
            body.linearVelocity = new Vector2(3f, 2f);
            body.angularVelocity = 45f;
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            Damage(1f, Vector2.zero);
            Assert.AreEqual(1, Deposits(Server).Count);
            Assert.IsFalse(NetworkServer.spawned.Values.Any(identity => identity.GetComponent(pickupType) != null));
            Vector2 expectedVelocity = body.GetPointVelocity(Vector2.zero);
            Damage(254f, Vector2.zero);
            Assert.AreEqual(0, Deposits(Server).Count);
            NetworkIdentity pickup = NetworkServer.spawned.Values.Single(identity => identity.GetComponent(pickupType) != null);
            Assert.AreEqual(expectedVelocity, pickup.GetComponent<Rigidbody2D>().linearVelocity);
            Assert.AreEqual(body.angularVelocity, pickup.GetComponent<Rigidbody2D>().angularVelocity);
            Damage(1f, new Vector2(1f, 0f));
            Assert.AreEqual(1, NetworkServer.spawned.Values.Count(identity => identity.GetComponent(pickupType) != null));
        }

        [Test]
        public void BuriedTreasure_AppearsWhenItsCentreIsExcavatedButRemainsEmbedded()
        {
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            Set(Server, "m_PeekingTreasureChance", 0f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++) bytes[y * 9 + x] = 255;
            Set(Server, "m_Field", new AsteroidGeometry(9, 9, bytes));
            Set(Server, "m_Spacing", Vector2.one);
            object deposit = Deposits(Server)[0];
            SetDeposit(deposit, Vector2.zero, 0.75f);
            Rebuild(Server);
            m_Type.GetMethod("UpdateTreasureVisuals", Private).Invoke(Server, null);
            var renderer = ((Component)deposit).GetComponentInChildren<Renderer>();
            Assert.IsFalse(renderer.enabled);
            Damage(255f, Vector2.zero);
            m_Type.GetMethod("UpdateTreasureVisuals", Private).Invoke(Server, null);
            Assert.AreEqual(1, Deposits(Server).Count);
            Assert.IsTrue(renderer.enabled);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TreasureSplit_TransfersSingleSupportOrReleasesSplitSupport(bool spansSplit)
        {
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            Type fieldType = Type.GetType("PewPewPew.World.AsteroidField, Assembly-CSharp", true);
            m_FieldObject = new GameObject("Asteroid test field", typeof(NetworkIdentity));
            Component owner = m_FieldObject.AddComponent(fieldType);
            fieldType.GetField("m_Prefab", Private).SetValue(owner,
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent(m_Type));
            fieldType.GetField("m_Count", Private).SetValue(owner, 1);
            fieldType.GetField("m_TargetCount", Private).SetValue(owner, 0);
            Set(Server, "m_OwnerField", owner);
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++)
                if (x != 4) bytes[y * 9 + x] = 255;
            Set(Server, "m_Field", new AsteroidGeometry(9, 9, bytes));
            Set(Server, "m_Spacing", Vector2.one);
            object deposit = Deposits(Server)[0];
            Vector2 localPosition = spansSplit ? Vector2.zero : new Vector2(-2f, 0f);
            SetDeposit(deposit, localPosition, spansSplit ? 1f : 0.25f);
            m_Server.transform.SetPositionAndRotation(new Vector3(10f, 5f, 0f), Quaternion.Euler(0f, 0f, 35f));
            Rebuild(Server);
            Vector2 originalPosition = m_Server.transform.TransformPoint(localPosition);
            Damage(1f, m_Server.transform.TransformPoint(new Vector2(-2f, 0f)));
            var children = NetworkServer.spawned.Values.Where(identity =>
                identity.gameObject != m_Server && identity.GetComponent(m_Type) != null).ToArray();
            Assert.AreEqual(2, children.Length);
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            var pickups = NetworkServer.spawned.Values.Where(identity => identity.GetComponent(pickupType) != null).ToArray();
            Assert.AreEqual(spansSplit ? 1 : 0, pickups.Length);
            Assert.AreEqual(spansSplit ? 0 : 1, children.Sum(identity => Deposits(identity.GetComponent(m_Type)).Count));
            if (!spansSplit)
            {
                NetworkIdentity child = children.Single(identity => Deposits(identity.GetComponent(m_Type)).Count != 0);
                Vector2 childLocal = DepositValue<Vector2>(Deposits(child.GetComponent(m_Type))[0], "Position");
                Assert.That(Vector2.Distance(originalPosition, child.transform.TransformPoint(childLocal)), Is.LessThan(0.0001f));
            }
        }

        [UnityTest]
        public IEnumerator AsteroidAndPickup_AccelerateUnderWorldGravity()
        {
            Type sourceType = Type.GetType("PewPewPew.World.GravitySource, Assembly-CSharp", true);
            var source = new GameObject("Test gravity source");
            source.transform.position = Vector3.left * 20f;
            source.AddComponent(sourceType);
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            object deposit = Deposits(Server)[0];
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            pickupType.GetMethod("Spawn", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { PrefabFor(deposit), new Vector2(0f, 20f), Quaternion.identity, Vector2.zero, 0f });
            NetworkIdentity pickup = NetworkServer.spawned.Values.Single(identity => identity.GetComponent(pickupType) != null);
            try
            {
                var asteroidBody = m_Server.GetComponent<Rigidbody2D>();
                var pickupBody = pickup.GetComponent<Rigidbody2D>();
                Type worldType = Type.GetType("PewPewPew.GameSystems.GameWorld, Assembly-CSharp", true);
                Component world = m_WorldObject.GetComponent(worldType);
                MethodInfo forceOn = worldType.GetMethod("ForceOn");
                float asteroidAcceleration = ((Vector2)forceOn.Invoke(world,
                    new object[] { asteroidBody.position, asteroidBody.mass })).magnitude / asteroidBody.mass;
                float pickupAcceleration = ((Vector2)forceOn.Invoke(world,
                    new object[] { pickupBody.position, pickupBody.mass })).magnitude / pickupBody.mass;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.Less(asteroidBody.linearVelocity.x, 0f);
                Assert.Less(pickupBody.linearVelocity.x, 0f);
                Assert.Less(pickupBody.linearVelocity.y, 0f);
                Assert.AreEqual(asteroidBody.linearVelocity.magnitude / asteroidAcceleration,
                    pickupBody.linearVelocity.magnitude / pickupAcceleration, 0.001f);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [UnityTest]
        public IEnumerator Pickup_IsRemovedByOrbitalBodyCollision()
        {
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            object deposit = Deposits(Server)[0];
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            foreach (string typeName in new[] { "PewPewPew.World.OrbitalBody" })
            {
                var target = new GameObject("Pickup collision target", typeof(NetworkIdentity), typeof(CircleCollider2D));
                target.transform.position = new Vector2(30f, 0f);
                var targetBehaviour = (Behaviour)target.AddComponent(Type.GetType(typeName + ", Assembly-CSharp", true));
                targetBehaviour.enabled = false;
                pickupType.GetMethod("Spawn", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { PrefabFor(deposit), new Vector2(32f, 0f), Quaternion.identity, Vector2.left * 10f, 0f });
                NetworkIdentity pickup = NetworkServer.spawned.Values.Single(identity => identity.GetComponent(pickupType) != null);
                uint id = pickup.netId;
                yield return new WaitForSeconds(0.5f);
                Assert.IsFalse(NetworkServer.spawned.ContainsKey(id), typeName);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator Pickup_CollidesWithAsteroidWithoutDamagingIt()
        {
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            byte[] initial = (byte[])Geometry(Server).Samples.Clone();
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            pickupType.GetMethod("Spawn", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { PrefabFor(Deposits(Server)[0]), new Vector2(15f, 0f), Quaternion.identity, Vector2.left * 20f, 0f });
            NetworkIdentity pickup = NetworkServer.spawned.Values.Single(identity => identity.GetComponent(pickupType) != null);
            uint id = pickup.netId;
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(NetworkServer.spawned.ContainsKey(id));
            Assert.Greater(m_Server.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0f);
            CollectionAssert.AreEqual(initial, Geometry(Server).Samples);
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
            Set(Server, "m_MinSplats", 4);
            Set(Server, "m_MaxSplats", 4);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            m_Client = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var replica = (NetworkBehaviour)m_Client.GetComponent(m_Type);
            var server = (NetworkBehaviour)Server;
            var writer = new NetworkWriter();
            server.OnSerialize(writer, true);
            Assert.AreEqual(73, writer.Position);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            Assert.AreEqual(Value("Mass"), m_Type.GetProperty("Mass").GetValue(replica));
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.colors,
                m_Client.GetComponent<MeshFilter>().sharedMesh.colors);
            Damage(1f);
            writer.Reset();
            server.OnSerialize(writer, true);
            Assert.LessOrEqual(writer.Position, 331);
            replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
            CollectionAssert.AreEqual(((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server)).Samples,
                ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(replica)).Samples);
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.colors,
                m_Client.GetComponent<MeshFilter>().sharedMesh.colors);
            Assert.AreEqual(Value("Mass"), m_Type.GetProperty("Mass").GetValue(replica));
            TestContext.WriteLine($"Untouched/damaged per-object geometry payload without treasure: 73/{writer.Position} bytes; 250 damaged: {writer.Position * 250} bytes.");
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
                Assert.AreEqual(surface.ShapeSeed, inherited.ShapeSeed);
                Assert.AreEqual(surface.SplatCount, inherited.SplatCount);
                Vector2 offset = m_Server.transform.InverseTransformPoint(child.transform.position);
                Assert.AreEqual(surface.Front(offset), inherited.Front(Vector2.zero), 0.00001f);
                Assert.IsTrue(retained.Any(piece => piece.Recenter(out _).Samples.SequenceEqual(field.Samples)));
                var writer = new NetworkWriter();
                ((NetworkBehaviour)child.GetComponent(m_Type)).OnSerialize(writer, true);
                Assert.LessOrEqual(writer.Position, 331);
                var replica = (NetworkBehaviour)m_Client.GetComponent(m_Type);
                replica.OnDeserialize(new NetworkReader(writer.ToArray()), true);
                CollectionAssert.AreEqual(child.GetComponent<MeshFilter>().sharedMesh.vertices,
                    m_Client.GetComponent<MeshFilter>().sharedMesh.vertices);
                CollectionAssert.AreEqual(child.GetComponent<MeshFilter>().sharedMesh.colors,
                    m_Client.GetComponent<MeshFilter>().sharedMesh.colors);
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
                Assert.AreEqual(73, writer.Position);
                payload += writer.Position;
                Mesh mesh = m_Server.GetComponent<MeshFilter>().sharedMesh;
                maxVertices = Mathf.Max(maxVertices, mesh.vertexCount);
            }
            stopwatch.Stop();
            Assert.AreEqual(18250, payload);
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
            const int networkWaitSteps = 100;
            var networkWait = new WaitForSecondsRealtime(0.02f);
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Damage(1f);
            m_Server.GetComponent<Rigidbody2D>().simulated = false;
            NetworkClient.Connect("localhost");
            for (int i = 0; i < networkWaitSteps && !NetworkClient.isConnected; i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.isConnected);
            NetworkClient.connection.isAuthenticated = true;
            NetworkConnectionToClient connection = NetworkServer.connections.Values.Single();
            connection.isAuthenticated = true;
            // Mirror streams initial observers only after a connection has a player identity.
            typeof(NetworkConnection).GetProperty("identity").SetValue(connection, m_Server.GetComponent<NetworkIdentity>());
            NetworkClient.Ready();
            uint id = m_Server.GetComponent<NetworkIdentity>().netId;
            for (int i = 0; i < networkWaitSteps && !NetworkClient.spawned.ContainsKey(id); i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(id));
            Component replica = NetworkClient.spawned[id].GetComponent(m_Type);
            CollectionAssert.AreEqual(((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(Server)).Samples,
                ((AsteroidGeometry)m_Type.GetField("m_Field", Private).GetValue(replica)).Samples);
            Assert.IsFalse(Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Any(item => item.name == "Temporary asteroid fragment"), "Joining must not replay old debris.");
            Damage(1f);
            for (int i = 0; i < networkWaitSteps && !Geometry(replica).Samples.SequenceEqual(Geometry(Server).Samples); i++)
                yield return networkWait;
            CollectionAssert.AreEqual(Geometry(Server).Samples, Geometry(replica).Samples);
            Assert.AreEqual(5, Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Count(item => item.name == "Temporary asteroid fragment"));
            CollectionAssert.AreEqual(m_Server.GetComponent<MeshFilter>().sharedMesh.vertices,
                replica.GetComponent<MeshFilter>().sharedMesh.vertices);
        }

        [UnityTest]
        public IEnumerator RemoteClient_ReceivesEmbeddedTreasureAndReleasedPickup()
        {
            const int networkWaitSteps = 100;
            var networkWait = new WaitForSecondsRealtime(0.02f);
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            Set(Server, "m_PeekingTreasureChance", 1f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            object original = Deposits(Server)[0];
            TreasureMaterial material = DepositValue<TreasureMaterial>(original, "Material");
            TreasureSize size = DepositValue<TreasureSize>(original, "Size");
            float radius = DepositValue<float>(original, "Radius");
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Resources/SpawnablePrefabs/Treasure/{material} {size}.prefab"));
            m_Server.GetComponent<Rigidbody2D>().simulated = false;
            NetworkClient.Connect("localhost");
            for (int i = 0; i < networkWaitSteps && !NetworkClient.isConnected; i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.isConnected);
            NetworkClient.connection.isAuthenticated = true;
            NetworkConnectionToClient connection = NetworkServer.connections.Values.Single();
            connection.isAuthenticated = true;
            typeof(NetworkConnection).GetProperty("identity").SetValue(connection, m_Server.GetComponent<NetworkIdentity>());
            NetworkClient.Ready();
            uint id = m_Server.GetComponent<NetworkIdentity>().netId;
            for (int i = 0; i < networkWaitSteps && !NetworkClient.spawned.ContainsKey(id); i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(id));
            Component replica = NetworkClient.spawned[id].GetComponent(m_Type);
            Assert.AreEqual(1, Deposits(replica).Count);
            Assert.AreEqual(material, DepositValue<TreasureMaterial>(Deposits(replica)[0], "Material"));
            var embedded = (Component)Deposits(replica)[0];
            Assert.IsFalse(embedded.GetComponent<Rigidbody2D>().simulated);
            Damage(100000f, radius: 100f);
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            uint pickupId = NetworkServer.spawned.Values.Single(identity => identity.GetComponent(pickupType) != null).netId;
            for (int i = 0; i < networkWaitSteps && !NetworkClient.spawned.ContainsKey(pickupId); i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(pickupId));
            Component clientPickup = NetworkClient.spawned[pickupId].GetComponent(pickupType);
            Assert.AreEqual(material, pickupType.GetProperty("Material").GetValue(clientPickup));
            Assert.AreEqual(size, pickupType.GetProperty("Size").GetValue(clientPickup));
            Assert.AreEqual(radius, (float)pickupType.GetProperty("Radius").GetValue(clientPickup), 0.0001f);
            for (int i = 0; i < networkWaitSteps && replica != null && Deposits(replica).Count != 0; i++) yield return networkWait;
            Assert.IsNotNull(replica);
            Assert.AreEqual(0, Deposits(replica).Count);
        }

        [UnityTest]
        public IEnumerator LateClient_ReceivesPreviouslyReleasedPickup()
        {
            const int networkWaitSteps = 100;
            var networkWait = new WaitForSecondsRealtime(0.02f);
            Set(Server, "m_TreasureChance", 1f);
            Set(Server, "m_MaxTreasure", 1);
            Set(Server, "m_DespawnDelay", 10f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            object original = Deposits(Server)[0];
            TreasureMaterial material = DepositValue<TreasureMaterial>(original, "Material");
            TreasureSize size = DepositValue<TreasureSize>(original, "Size");
            float radius = DepositValue<float>(original, "Radius");
            Damage(100000f, radius: 100f);
            Type pickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
            uint pickupId = NetworkServer.spawned.Values.Single(identity => identity.GetComponent(pickupType) != null).netId;
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Resources/SpawnablePrefabs/Treasure/{material} {size}.prefab"));
            NetworkClient.Connect("localhost");
            for (int i = 0; i < networkWaitSteps && !NetworkClient.isConnected; i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.isConnected);
            NetworkClient.connection.isAuthenticated = true;
            NetworkConnectionToClient connection = NetworkServer.connections.Values.Single();
            connection.isAuthenticated = true;
            typeof(NetworkConnection).GetProperty("identity").SetValue(connection, m_Server.GetComponent<NetworkIdentity>());
            NetworkClient.Ready();
            for (int i = 0; i < networkWaitSteps && !NetworkClient.spawned.ContainsKey(pickupId); i++) yield return networkWait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(pickupId));
            Component clientPickup = NetworkClient.spawned[pickupId].GetComponent(pickupType);
            Assert.AreEqual(material, pickupType.GetProperty("Material").GetValue(clientPickup));
            Assert.AreEqual(size, pickupType.GetProperty("Size").GetValue(clientPickup));
            Assert.AreEqual(radius, (float)pickupType.GetProperty("Radius").GetValue(clientPickup), 0.0001f);
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
            Set(Server, "m_MinScale", 1f);
            Set(Server, "m_MaxScale", 1f);
            m_Type.GetMethod("Initialize").Invoke(Server, null);
            FireBullet(1000f, radius: 1.1f);
            foreach (int index in new[] { 119, 120, 135, 136 })
                Assert.AreEqual(0, Geometry(Server).Samples[index]);
            Assert.AreEqual(255, Geometry(Server).Samples[137]);
        }

        [Test]
        public void SurfaceTexture_BlendsCrackedAndSolidTextures()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Rock texture validation requires a graphics device.");
            var asteroidMaterial = m_Server.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/AsteroidIntact.png"),
                asteroidMaterial.GetTexture("_BaseMap"));
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/AsteroidFractured.png"),
                asteroidMaterial.GetTexture("_CrackedMap"));
            var cameraObject = new GameObject("Rock blend camera", typeof(Camera));
            var lightObject = new GameObject("Rock blend light", typeof(Light));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("PewPewPew/AsteroidLit"));
            var intact = new Texture2D(1, 1);
            var cracked = new Texture2D(1, 1);
            var target = new RenderTexture(64, 64, 24);
            var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            Mesh quadMesh = null;
            try
            {
                m_Server.SetActive(false);
                intact.SetPixel(0, 0, Color.green); intact.Apply();
                cracked.SetPixel(0, 0, Color.red); cracked.Apply();
                material.SetTexture("_BaseMap", intact);
                material.SetTexture("_CrackedMap", cracked);
                quadMesh = quad.GetComponent<MeshFilter>().mesh;
                quadMesh.colors = quadMesh.vertices.Select(vertex =>
                    new Color(vertex.x + 0.5f, 0f, 0f, 1f)).ToArray();
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
                Assert.Greater(left.r - left.g, 0.1f, "The cracked texture must dominate the left side.");
                Assert.Greater(right.g - right.r, 0.1f, "The solid texture must dominate the right side.");
                Assert.Greater(middle.r, right.r);
                Assert.Greater(middle.g, left.g);
            }
            finally
            {
                m_Server.SetActive(true);
                RenderTexture.active = previous;
                foreach (Object item in new Object[] { image, target, intact, cracked, material, quad, quadMesh, cameraObject, lightObject })
                    Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void SurfaceTexture_RealAsteroidShowsCracksAtGameZoom()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Rock texture validation requires a graphics device.");
            const int size = 512;
            const int channelThreshold = 5;
            Set(Server, "m_MinScale", 1f);
            Set(Server, "m_MaxScale", 1f);
            Set(Server, "m_VertexOffsetFactor", 0.45f);
            Set(Server, "m_ReliefFraction", 1f);
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(42);
                m_Type.GetMethod("Initialize").Invoke(Server, null);
            }
            finally { UnityEngine.Random.state = random; }

            var renderer = m_Server.GetComponent<MeshRenderer>();
            Material originalMaterial = renderer.sharedMaterial;
            var material = new Material(originalMaterial);
            var cameraObject = new GameObject("Rock visibility camera", typeof(Camera));
            var lightObject = new GameObject("Rock visibility light", typeof(Light));
            var target = new RenderTexture(size, size, 24);
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                m_Server.layer = 31;
                renderer.sharedMaterial = material;
                renderer.renderingLayerMask = 12u;
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 20f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                cameraObject.transform.position = new Vector3(0, 0, -100);
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.5f;
                lightObject.AddComponent<UniversalAdditionalLightData>().renderingLayers = 12u;
                target.Create();

                Texture cracked = material.GetTexture("_CrackedMap");
                material.SetTexture("_CrackedMap", material.GetTexture("_BaseMap"));
                Color32[] solid = Render();
                material.SetTexture("_CrackedMap", cracked);
                Color32[] initial = Render();
                Geometry(Server).ErodeImpact(new Vector2(8, 8), Vector2.zero, 25f, false);
                Rebuild(Server);
                Color32[] hit = Render();
                Geometry(Server).ErodeImpact(new Vector2(8, 8), Vector2.zero, 75f, false);
                Rebuild(Server);
                Color32[] damaged = Render();

                Assert.Greater(DifferentPixels(solid, initial), 1200,
                    "The real cracked texture should remain visible at game zoom.");
                Assert.Greater(DifferentPixels(initial, hit), 30,
                    "A 25-level hit should change the rendered rock.");
                Assert.Greater(DifferentPixels(initial, damaged), 150,
                    "A weakened sample should visibly change the rendered rock.");

                Color32[] Render()
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    image.Apply();
                    return image.GetPixels32();
                }

                int DifferentPixels(Color32[] a, Color32[] b)
                {
                    int count = 0;
                    for (int i = 0; i < a.Length; i++)
                        if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g),
                                Mathf.Abs(a[i].b - b[i].b)) > channelThreshold) count++;
                    return count;
                }
            }
            finally
            {
                renderer.sharedMaterial = originalMaterial;
                RenderTexture.active = previous;
                foreach (Object item in new Object[] { image, target, material, cameraObject, lightObject })
                    Object.DestroyImmediate(item);
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void SurfaceWeights_FollowDamageOnRoofBackAndWalls(float offsetFactor)
        {
            const int size = 5;
            const uint seed = 123;
            const float damage = 64f;
            var samples = new byte[size * size];
            for (int y = 1; y < size - 1; y++)
            for (int x = 1; x < size - 1; x++) samples[y * size + x] = byte.MaxValue;
            var field = new AsteroidGeometry(size, size, samples);
            field.ConfigurePositions(seed, offsetFactor, Vector2Int.zero);
            Set(Server, "m_Field", field);
            Set(Server, "m_Spacing", Vector2.one);
            Set(Server, "m_Surface", new AsteroidSurface(seed, Vector2.one * 2f, Vector2.one,
                1f, 0.2f, 3f, offsetFactor));
            Rebuild(Server);
            Vector3[] before = m_Server.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector2 centre = field.Position(2, 2) - Vector2.one * 2f;
            AssertCentreWeight(1f);
            field.ErodeImpact(new Vector2(2, 2), Vector2.zero, damage, false);
            Rebuild(Server);
            Mesh mesh = m_Server.GetComponent<MeshFilter>().sharedMesh;
            CollectionAssert.AreEqual(before, mesh.vertices);
            AssertCentreWeight((byte.MaxValue - damage - AsteroidGeometry.IsoLevel) /
                (byte.MaxValue - AsteroidGeometry.IsoLevel));
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            Vector3[] vertices = mesh.vertices;
            var boundary = AsteroidGeometry.Boundary(field.Polygons());
            int wallVertices = 0;
            for (int i = 0; i < normals.Length; i++)
            {
                if (Mathf.Abs(normals[i].z) < 0.00001f)
                {
                    Assert.AreEqual(0f, colors[i].r, 0.00001f);
                    wallVertices++;
                }
                Vector2 samplePoint = (Vector2)vertices[i] + Vector2.one * 2f;
                foreach (var edge in boundary)
                {
                    Vector2 direction = edge.B - edge.A;
                    if (direction.sqrMagnitude == 0f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(samplePoint - edge.A, direction) / direction.sqrMagnitude);
                    if (Vector2.Distance(samplePoint, edge.A + direction * t) < 0.00001f)
                        Assert.AreEqual(0f, colors[i].r, "Contour vertices must use the cracked texture.");
                }
            }
            Assert.Greater(wallVertices, 0);

            void AssertCentreWeight(float expected)
            {
                Mesh current = m_Server.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = current.vertices;
                Color[] weights = current.colors;
                int front = 0, back = 0;
                for (int i = 0; i < vertices.Length; i++)
                    if (Vector2.Distance(vertices[i], centre) < 0.00001f)
                    {
                        Assert.AreEqual(expected, weights[i].r, 0.0001f);
                        if (vertices[i].z < 0f) front++; else back++;
                    }
                Assert.Greater(front, 0);
                Assert.Greater(back, 0);
            }
        }

        [Test]
        public void SurfaceWeights_RemainValidAcrossDisplacedSeeds()
        {
            const float offsetFactor = 1f;
            const uint seedCount = 40;
            Vector2 centre = Vector2.one * (AsteroidGeometry.MaxCells * 0.5f);
            for (uint seed = 0; seed < seedCount; seed++)
            {
                var field = AsteroidGeometry.Generate(seed, 6.5f);
                field.ConfigurePositions(seed, offsetFactor, Vector2Int.zero);
                Set(Server, "m_Field", field);
                Set(Server, "m_Spacing", Vector2.one);
                Set(Server, "m_Surface", new AsteroidSurface(seed, centre, Vector2.one, 1f, 0.2f,
                    7f, offsetFactor));
                Rebuild(Server);
                Mesh mesh = m_Server.GetComponent<MeshFilter>().sharedMesh;
                Color[] weights = mesh.colors;
                Assert.AreEqual(mesh.vertexCount, weights.Length, $"Seed {seed}");
                Assert.IsTrue(weights.All(weight => weight.r >= 0f && weight.r <= 1f), $"Seed {seed}");
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
            var domesOnly = new AsteroidSurface(surface.Seed, surface.Offset, surface.Scale, surface.Depth, 0f,
                surface.ShapeSeed, surface.MinSplatRadius, surface.MaxSplatRadius, surface.SplatCount,
                Geometry(Server).OffsetFactor);
            Assert.Less(surface.Front(Vector2.zero), domesOnly.Front(Vector2.zero),
                "The roof should include relief in addition to the splat domes.");
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
                caster.transform.position = new Vector3(8f, 8f, -20f);
                caster.transform.localScale = new Vector3(5f, 5f, 1f);
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
