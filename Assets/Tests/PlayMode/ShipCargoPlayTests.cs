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
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PewPewPew.Tests
{
    public class ShipCargoPlayTests
    {
        private const string ShipPrefab = "Assets/Resources/SpawnablePrefabs/Ships/Basic Ship.prefab";
        private const string TractorAsset = "Assets/ShipComponents/SubSystems/Basic SubSystem Tractor Beam.asset";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject m_TransportObject;
        private GameObject m_World;
        private GameObject m_Ship;
        private Transport m_PreviousTransport;
        private Type m_ShipType;
        private Type m_PickupType;

        [SetUp]
        public void SetUp()
        {
            m_PreviousTransport = Transport.active;
            m_TransportObject = new GameObject("Cargo test transport");
            Transport.active = m_TransportObject.AddComponent<MemoryTransport>();
            NetworkServer.Listen(1);
            m_World = new GameObject("Cargo test world");
            m_World.AddComponent(Type.GetType("PewPewPew.GameSystems.GameWorld, Assembly-CSharp", true));
            m_ShipType = Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp", true);
            m_PickupType = Type.GetType("PewPewPew.World.Treasure, Assembly-CSharp", true);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.active = m_PreviousTransport;
            if (m_Ship != null) Object.DestroyImmediate(m_Ship);
            if (m_World != null) Object.DestroyImmediate(m_World);
            Object.DestroyImmediate(m_TransportObject);
        }

        private Component SpawnShip(bool tractor = false, bool secondTractor = false)
        {
            Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp", true);
            object catalog = catalogType.GetProperty("Instance").GetValue(null);
            var loadout = (ShipLoadout)catalogType.GetMethod("DefaultLoadout").Invoke(catalog, new object[] { 0 });
            for (int i = 0; i < loadout.MountComponents.Length; i++) loadout.MountComponents[i] = -1;
            if (tractor)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TractorAsset);
                var definitions = (Array)catalogType.GetProperty("Components").GetValue(catalog);
                int component = Enumerable.Range(0, definitions.Length)
                    .Where(index => ReferenceEquals(definitions.GetValue(index), definition)).DefaultIfEmpty(-1).First();
                Assert.GreaterOrEqual(component, 0);
                object hull = ((Array)catalogType.GetProperty("Hulls").GetValue(catalog)).GetValue(0);
                object hullPoints = hull.GetType().GetProperty("HullPoints").GetValue(hull);
                var points = (Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints);
                int[] mounts = Enumerable.Range(0, points.Length).Where(i =>
                    (bool)points.GetValue(i).GetType().GetMethod("AcceptsComponent").Invoke(points.GetValue(i), new object[] { definition }))
                    .Take(secondTractor ? 2 : 1).ToArray();
                Assert.AreEqual(secondTractor ? 2 : 1, mounts.Length);
                foreach (int mount in mounts) loadout.MountComponents[mount] = component;
            }
            m_Ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefab));
            Component ship = m_Ship.GetComponent(m_ShipType);
            m_ShipType.GetMethod("Initialize").Invoke(ship, new object[] { loadout, null });
            NetworkServer.Spawn(m_Ship);
            return ship;
        }

        private NetworkIdentity SpawnPickup(TreasureMaterial material, TreasureSize size, Vector2 position, Vector2 velocity,
            float radius = 0.25f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Resources/SpawnablePrefabs/Treasure/{material} {size}.prefab");
            GameObject instance = Object.Instantiate(prefab);
            Component treasure = instance.GetComponent(m_PickupType);
            m_PickupType.GetMethod("Embed", Private).Invoke(treasure,
                new object[] { m_Ship != null ? m_Ship.transform : m_World.transform, (ushort)0, Vector2.zero, radius, false });
            m_PickupType.GetMethod("Release", Private).Invoke(treasure,
                new object[] { position, Quaternion.identity, velocity, 0f });
            return instance.GetComponent<NetworkIdentity>();
        }

        private object TractorBeams(Component ship) => m_ShipType.GetField("m_TractorBeams", Private).GetValue(ship);

        private IDictionary Tractors(Component ship)
        {
            object beams = TractorBeams(ship);
            return (IDictionary)beams.GetType().GetField("m_Tractors", Private).GetValue(beams);
        }

        private object FindTractorTarget(Component ship, object tractor)
        {
            object beams = TractorBeams(ship);
            return beams.GetType().GetMethod("FindTarget", Private).Invoke(beams, new[] { tractor });
        }

        private void StepTractors(Component ship, float deltaTime)
        {
            m_ShipType.GetMethod("StepTractors", Private).Invoke(ship, new object[] { deltaTime });
        }

        private void RenderTractors(Component ship)
        {
            object beams = TractorBeams(ship);
            object targets = m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
            beams.GetType().GetMethod("Render", Private).Invoke(beams, new[] { targets });
        }

        [UnityTest]
        public IEnumerator Collision_CollectsWholePickupAndRejectsWhenFull()
        {
            Component ship = SpawnShip();
            var cargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(ship);
            Assert.Greater(cargo.Free, 3);
            NetworkIdentity pickup = SpawnPickup(TreasureMaterial.Gold, TreasureSize.Large,
                new Vector2(4f, 0f), Vector2.left * 10f);
            uint id = pickup.netId;
            yield return new WaitForSeconds(0.7f);
            Assert.IsFalse(NetworkServer.spawned.ContainsKey(id));
            Assert.AreEqual(3, cargo.Total(TreasureMaterial.Gold));
            while (cargo.Free > 0) Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Silver, TreasureSize.Small));
            m_Ship.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            m_Ship.GetComponent<Rigidbody2D>().angularVelocity = 0f;
            pickup = SpawnPickup(TreasureMaterial.RedCrystal, TreasureSize.Small,
                (Vector2)m_Ship.transform.position + new Vector2(4f, 0f), Vector2.left * 10f);
            id = pickup.netId;
            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(NetworkServer.spawned.ContainsKey(id));
            Assert.AreEqual(0, cargo.Total(TreasureMaterial.RedCrystal));
            Assert.Greater(m_Ship.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0f,
                "The full ship must have physically collided with the pickup.");
        }

        [Test]
        public void Collection_UsesTreasureUnitCountRatherThanSize()
        {
            Component ship = SpawnShip();
            NetworkIdentity pickup = SpawnPickup(TreasureMaterial.Gold, TreasureSize.Small, Vector2.right * 20f, Vector2.zero);
            Component treasure = pickup.GetComponent(m_PickupType);
            const int authoredUnits = 5;
            m_PickupType.GetField("m_Units", Private).SetValue(treasure, authoredUnits);
            Assert.IsTrue((bool)m_ShipType.GetMethod("TryCollect", Private).Invoke(ship, new object[] { treasure }));
            var cargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(ship);
            Assert.AreEqual(authoredUnits, cargo.Total(TreasureMaterial.Gold));
        }

        [UnityTest]
        public IEnumerator Collision_CompetingShipsCollectPickupOnlyOnce()
        {
            Component first = SpawnShip();
            GameObject firstObject = m_Ship;
            Component second = SpawnShip();
            firstObject.transform.position = Vector2.left * 4f;
            m_Ship.transform.position = Vector2.right * 4f;
            firstObject.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 10f;
            m_Ship.GetComponent<Rigidbody2D>().linearVelocity = Vector2.left * 10f;
            NetworkIdentity pickup = SpawnPickup(TreasureMaterial.Gold, TreasureSize.Large, Vector2.zero, Vector2.zero);
            uint id = pickup.netId;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(0.7f);
            Assert.IsFalse(NetworkServer.spawned.ContainsKey(id));
            var firstCargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(first);
            var secondCargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(second);
            Assert.AreEqual(3, firstCargo.Total(TreasureMaterial.Gold) + secondCargo.Total(TreasureMaterial.Gold));
        }

        [UnityTest]
        public IEnumerator RemoteOwner_ReceivesExistingAndUpdatedCargo()
        {
            const int waitSteps = 100;
            var wait = new WaitForSecondsRealtime(0.02f);
            Component ship = SpawnShip();
            var cargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(ship);
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Medium));
            Type hudType = Type.GetType("PewPewPew.Ships.ShipHudSync, Assembly-CSharp", true);
            Component hud = m_Ship.GetComponent(hudType);
            hudType.GetMethod("PublishCargo").Invoke(hud, new object[] { cargo.Snapshot() });
            m_Ship.GetComponent<Rigidbody2D>().simulated = false;
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefab));
            NetworkClient.Connect("localhost");
            for (int i = 0; i < waitSteps && !NetworkClient.isConnected; i++) yield return wait;
            Assert.IsTrue(NetworkClient.isConnected);
            NetworkClient.connection.isAuthenticated = true;
            NetworkConnectionToClient connection = NetworkServer.connections.Values.Single();
            connection.isAuthenticated = true;
            Assert.IsTrue(NetworkServer.AddPlayerForConnection(connection, m_Ship));
            NetworkClient.Ready();
            uint id = m_Ship.GetComponent<NetworkIdentity>().netId;
            for (int i = 0; i < waitSteps && !NetworkClient.spawned.ContainsKey(id); i++) yield return wait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(id));
            GameObject replica = NetworkClient.spawned[id].gameObject;
            Component remoteHud = replica.GetComponent(hudType);
            byte[] snapshot = (byte[])hudType.GetProperty("Cargo").GetValue(remoteHud);
            CollectionAssert.AreEqual(cargo.Snapshot(), snapshot);
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.RedCrystal, TreasureSize.Small));
            hudType.GetMethod("PublishCargo").Invoke(hud, new object[] { cargo.Snapshot() });
            for (int i = 0; i < waitSteps &&
                !cargo.Snapshot().SequenceEqual((byte[])hudType.GetProperty("Cargo").GetValue(remoteHud)); i++)
                yield return wait;
            CollectionAssert.AreEqual(cargo.Snapshot(), (byte[])hudType.GetProperty("Cargo").GetValue(remoteHud));
        }

        [UnityTest]
        public IEnumerator LateObserver_SeesBeamAndClearsItWhenPickupDisappears()
        {
            const int waitSteps = 100;
            var wait = new WaitForSecondsRealtime(0.02f);
            Component ship = SpawnShip(true);
            var tractors = Tractors(ship);
            object tractor = tractors.Values.Cast<object>().Single();
            object system = tractor.GetType().GetProperty("System").GetValue(tractor);
            Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
            NetworkIdentity pickup = SpawnPickup(TreasureMaterial.GreenCrystal, TreasureSize.Small,
                (Vector2)mount.position + (Vector2)mount.up * 5f, Vector2.zero);
            pickup.GetComponent<Rigidbody2D>().simulated = false;
            m_Ship.GetComponent<Rigidbody2D>().simulated = false;
            Physics2D.SyncTransforms();
            StepTractors(ship, 0.02f);
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefab));
            NetworkClient.RegisterPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/SpawnablePrefabs/Treasure/GreenCrystal Small.prefab"));
            NetworkClient.Connect("localhost");
            for (int i = 0; i < waitSteps && !NetworkClient.isConnected; i++) yield return wait;
            Assert.IsTrue(NetworkClient.isConnected);
            NetworkClient.connection.isAuthenticated = true;
            NetworkConnectionToClient connection = NetworkServer.connections.Values.Single();
            connection.isAuthenticated = true;
            Assert.IsTrue(NetworkServer.AddPlayerForConnection(connection, m_Ship));
            NetworkClient.Ready();
            uint id = m_Ship.GetComponent<NetworkIdentity>().netId;
            for (int i = 0; i < waitSteps && !NetworkClient.spawned.ContainsKey(id); i++) yield return wait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(id));
            GameObject replica = NetworkClient.spawned[id].gameObject;
            replica.GetComponent<Rigidbody2D>().simulated = false;
            LineRenderer line = replica.GetComponentInChildren<LineRenderer>(true);
            Assert.IsNotNull(line);
            for (int i = 0; i < waitSteps && !line.enabled; i++) yield return wait;
            Assert.IsTrue(line.enabled);
            Assert.AreEqual(NetworkClient.spawned[pickup.netId].GetComponent<CircleCollider2D>().radius * 2f,
                line.endWidth, 0.0001f);
            Assert.Less(line.startWidth, line.endWidth);
            system.GetType().GetProperty("Enabled").SetValue(system, false);
            StepTractors(ship, 0.02f);
            for (int i = 0; i < waitSteps && line.enabled; i++) yield return wait;
            Assert.IsFalse(line.enabled, "Observers must receive a stopped beam.");
            system.GetType().GetProperty("Enabled").SetValue(system, true);
            StepTractors(ship, 0.02f);
            for (int i = 0; i < waitSteps && !line.enabled; i++) yield return wait;
            Assert.IsTrue(line.enabled, "Observers must receive a restarted beam.");
            NetworkServer.Destroy(pickup.gameObject);
            for (int i = 0; i < waitSteps && line.enabled; i++) yield return wait;
            Assert.IsFalse(line.enabled);
        }

        [Test]
        public void Tractor_MultipleMountsPublishIndependentTargets()
        {
            Component ship = SpawnShip(true, true);
            IDictionary tractors = Tractors(ship);
            Assert.AreEqual(2, tractors.Count);
            foreach (DictionaryEntry entry in tractors)
            {
                object system = entry.Value.GetType().GetProperty("System").GetValue(entry.Value);
                Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
                SpawnPickup(TreasureMaterial.Gold, TreasureSize.Small,
                    (Vector2)mount.position + (Vector2)mount.up * 3f, Vector2.zero)
                    .GetComponent<Rigidbody2D>().simulated = false;
            }
            Physics2D.SyncTransforms();
            StepTractors(ship, 0.02f);
            var targets = (uint[])m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
            foreach (DictionaryEntry entry in tractors) Assert.AreNotEqual(0u, targets[(int)entry.Key]);

            int first = (int)tractors.Keys.Cast<object>().First();
            object firstTractor = tractors[first];
            object firstSystem = firstTractor.GetType().GetProperty("System").GetValue(firstTractor);
            firstSystem.GetType().GetProperty("Enabled").SetValue(firstSystem, false);
            StepTractors(ship, 0.02f);
            targets = (uint[])m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
            Assert.AreEqual(0u, targets[first]);
            int second = (int)tractors.Keys.Cast<object>().Last();
            Assert.AreNotEqual(0u, targets[second]);
        }

        [Test]
        public void Tractor_PullsOnlyWhenVisibleAndPowered()
        {
            Component ship = SpawnShip(true);
            var tractors = Tractors(ship);
            object tractor = tractors.Values.Cast<object>().Single();
            object system = tractor.GetType().GetProperty("System").GetValue(tractor);
            Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
            Vector2 origin = mount.position;
            Vector2 forward = mount.up;
            NetworkIdentity pickup = SpawnPickup(TreasureMaterial.CyanCrystal, TreasureSize.Small,
                origin + forward * 5f, Vector2.zero);
            Physics2D.SyncTransforms();
            Assert.AreEqual(pickup.GetComponent(m_PickupType), FindTractorTarget(ship, tractor));
            var power = (PowerBank)m_ShipType.GetField("m_Power", Private).GetValue(ship);
            float before = power.Stored;
            StepTractors(ship, 0.1f);
            Assert.Less(power.Stored, before);
            uint[] targets = (uint[])m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
            Assert.Contains(pickup.netId, targets);
            var blocker = new GameObject("Tractor blocker", typeof(NetworkIdentity), typeof(CircleCollider2D));
            try
            {
                blocker.transform.position = origin + forward * 2.5f;
                var body = (Behaviour)blocker.AddComponent(Type.GetType("PewPewPew.World.OrbitalBody, Assembly-CSharp", true));
                body.enabled = false;
                Physics2D.SyncTransforms();
                Assert.IsNull(FindTractorTarget(ship, tractor));
                before = power.Stored;
                StepTractors(ship, 0.1f);
                Assert.AreEqual(before, power.Stored);
            }
            finally { Object.DestroyImmediate(blocker); }
            system.GetType().GetProperty("Enabled").SetValue(system, false);
            before = power.Stored;
            StepTractors(ship, 0.1f);
            targets = (uint[])m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
            Assert.IsFalse(targets.Contains(pickup.netId));
            Assert.AreEqual(before, power.Stored);
            system.GetType().GetProperty("Enabled").SetValue(system, true);
            power.SetFraction(0f);
            StepTractors(ship, 0.1f);
            targets = (uint[])m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
            Assert.IsFalse(targets.Contains(pickup.netId));
            Assert.AreEqual(0f, power.Stored);
            power.SetFraction(1f);
            var cargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(ship);
            while (cargo.Free > 0) Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Silver, TreasureSize.Small));
            Assert.IsNull(FindTractorTarget(ship, tractor));
            before = power.Stored;
            StepTractors(ship, 0.1f);
            Assert.AreEqual(before, power.Stored);
        }

        [Test]
        public void Tractor_IgnoresOutOfConeAndTooLargePickups()
        {
            Component ship = SpawnShip(true);
            var cargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(ship);
            while (cargo.Free > 1) Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Small));
            var tractors = Tractors(ship);
            object tractor = tractors.Values.Cast<object>().Single();
            object system = tractor.GetType().GetProperty("System").GetValue(tractor);
            Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
            Vector2 origin = mount.position;
            Vector2 forward = mount.up;
            Vector2 right = mount.right;
            SpawnPickup(TreasureMaterial.Silver, TreasureSize.Large, origin + forward * 2f, Vector2.zero);
            SpawnPickup(TreasureMaterial.Silver, TreasureSize.Small, origin + right * 2f, Vector2.zero);
            NetworkIdentity expected = SpawnPickup(TreasureMaterial.Gold, TreasureSize.Small, origin + forward * 6f, Vector2.zero);
            Physics2D.SyncTransforms();
            object target = FindTractorTarget(ship, tractor);
            Assert.AreEqual(expected.GetComponent(m_PickupType), target);
        }

        [TestCase("PewPewPew.World.Asteroid")]
        [TestCase("PewPewPew.World.OrbitalBody")]
        [TestCase("PewPewPew.Ships.Ship")]
        public void Tractor_ChoosesVisiblePickupWhenNearerOneIsBlocked(string blockerType)
        {
            Component ship = SpawnShip(true);
            var tractors = Tractors(ship);
            object tractor = tractors.Values.Cast<object>().Single();
            object system = tractor.GetType().GetProperty("System").GetValue(tractor);
            Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
            Vector2 origin = mount.position;
            Vector2 forward = mount.up;
            Vector2 right = mount.right;
            SpawnPickup(TreasureMaterial.Gold, TreasureSize.Small, origin + forward * 5f, Vector2.zero);
            NetworkIdentity visible = SpawnPickup(TreasureMaterial.Silver, TreasureSize.Small,
                origin + forward * 6f + right * 2.5f, Vector2.zero);
            var blocker = new GameObject("Tractor blocker", typeof(NetworkIdentity), typeof(CircleCollider2D));
            try
            {
                blocker.transform.position = origin + forward * 2.5f;
                var behaviour = (Behaviour)blocker.AddComponent(Type.GetType(blockerType + ", Assembly-CSharp", true));
                behaviour.enabled = false;
                Physics2D.SyncTransforms();
                object target = FindTractorTarget(ship, tractor);
                Assert.AreEqual(visible.GetComponent(m_PickupType), target);
            }
            finally { Object.DestroyImmediate(blocker); }
        }

        [Test]
        public void Tractor_ScansRepresentativePickupPopulation()
        {
            const int population = 100;
            const int scans = 25;
            Component ship = SpawnShip(true);
            var tractors = Tractors(ship);
            object tractor = tractors.Values.Cast<object>().Single();
            object system = tractor.GetType().GetProperty("System").GetValue(tractor);
            Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
            Vector2 origin = mount.position;
            Vector2 forward = mount.up;
            Vector2 right = mount.right;
            for (int i = 0; i < population; i++)
                SpawnPickup(TreasureMaterial.Gold, TreasureSize.Small,
                    origin + forward * (5f + i * 0.02f) + right * ((i % 5 - 2) * 0.1f), Vector2.zero)
                    .GetComponent<Rigidbody2D>().simulated = false;
            Physics2D.SyncTransforms();
            object beams = TractorBeams(ship);
            MethodInfo find = beams.GetType().GetMethod("FindTarget", Private);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < scans; i++) Assert.IsNotNull(find.Invoke(beams, new[] { tractor }));
            timer.Stop();
            TestContext.WriteLine($"{scans} tractor target scans with {population} pickups: {timer.Elapsed.TotalMilliseconds:0.##} ms");
        }

        [UnityTest]
        public IEnumerator TractorVisual_ScrollsTowardsMountWithoutChangingSharedMaterial()
        {
            const string visualPath = "Assets/Prefabs/Tractor Beam Visuals/Tractor Beam Visual.prefab";
            const string baseMap = "_BaseMap";
            const string baseMapTransform = "_BaseMap_ST";
            const string otherProperty = "_TractorTestProperty";
            const float otherValue = 0.75f;
            var parent = new GameObject("Tractor visual test");
            parent.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
            Assert.IsNotNull(prefab);
            GameObject first = Object.Instantiate(prefab, parent.transform);
            GameObject second = Object.Instantiate(prefab, parent.transform);
            LineRenderer line = first.GetComponent<LineRenderer>();
            LineRenderer other = second.GetComponent<LineRenderer>();
            var material = new Material(line.sharedMaterial);
            var scale = new Vector2(2f, 3f);
            var offset = new Vector2(0.25f, 0.5f);
            try
            {
                material.SetTextureScale(baseMap, scale);
                material.SetTextureOffset(baseMap, offset);
                line.sharedMaterial = material;
                other.sharedMaterial = material;
                parent.SetActive(true);
                line.enabled = true;
                var properties = new MaterialPropertyBlock();
                properties.SetFloat(otherProperty, otherValue);
                line.SetPropertyBlock(properties);
                var otherProperties = new MaterialPropertyBlock();
                other.GetPropertyBlock(otherProperties);
                Vector4 otherTransform = otherProperties.GetVector(baseMapTransform);
                float otherInitialValue = otherProperties.GetFloat(otherProperty);
                yield return null;
                line.GetPropertyBlock(properties);
                float initialOffset = properties.GetVector(baseMapTransform).z;
                yield return new WaitForSeconds(0.1f);
                line.GetPropertyBlock(properties);
                Vector4 transform = properties.GetVector(baseMapTransform);
                Assert.AreEqual(scale.x, transform.x);
                Assert.AreEqual(scale.y, transform.y);
                Assert.Greater(transform.z, offset.x);
                Assert.Less(transform.z, offset.x + 1f);
                Assert.Greater(Mathf.Repeat(transform.z - initialOffset, 1f), 0f);
                Assert.AreEqual(offset.y, transform.w);
                Assert.AreEqual(otherValue, properties.GetFloat(otherProperty));
                Assert.AreEqual(offset, material.GetTextureOffset(baseMap));
                other.GetPropertyBlock(otherProperties);
                Assert.AreEqual(otherTransform, otherProperties.GetVector(baseMapTransform));
                Assert.AreEqual(otherInitialValue, otherProperties.GetFloat(otherProperty),
                    "A second beam must not inherit another beam's animation.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator TractorBeam_RendersVisiblePixelsAtGameZoom()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to verify tractor visibility.");
            const int imageSize = 512;
            const int beamLayer = 31;
            const int waitSteps = 100;
            const int minimumChannel = 40;
            const int minimumColourDifference = 30;
            const int minimumBeamPixels = 10;
            const float zoom = 20f;
            var wait = new WaitForSecondsRealtime(0.02f);
            NetworkClient.ConnectHost();
            HostMode.InvokeOnConnected();
            NetworkClient.connection.isAuthenticated = true;
            NetworkServer.localConnection.isAuthenticated = true;
            typeof(NetworkClient).GetField("isSpawnFinished", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
            Component ship = SpawnShip(true);
            NetworkClient.Ready();
            Assert.IsTrue(NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, m_Ship));
            uint shipId = m_Ship.GetComponent<NetworkIdentity>().netId;
            for (int i = 0; i < waitSteps && !NetworkClient.spawned.ContainsKey(shipId); i++) yield return wait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(shipId));
            var tractors = Tractors(ship);
            object tractor = tractors.Values.Cast<object>().Single();
            object system = tractor.GetType().GetProperty("System").GetValue(tractor);
            Transform mount = (Transform)system.GetType().GetProperty("Point").GetValue(system);
            NetworkIdentity pickup = SpawnPickup(TreasureMaterial.Gold, TreasureSize.Small,
                (Vector2)mount.position + (Vector2)mount.up * 10f, Vector2.zero);
            for (int i = 0; i < waitSteps && !NetworkClient.spawned.ContainsKey(pickup.netId); i++) yield return wait;
            Assert.IsTrue(NetworkClient.spawned.ContainsKey(pickup.netId));
            Physics2D.SyncTransforms();
            StepTractors(ship, 0.02f);
            RenderTractors(ship);
            LineRenderer line = m_Ship.GetComponentInChildren<LineRenderer>(true);
            Assert.IsNotNull(line);
            Assert.IsTrue(line.enabled);
            Assert.AreEqual(pickup.GetComponent<CircleCollider2D>().radius * 2f, line.endWidth, 0.0001f);
            Assert.Less(line.startWidth, line.endWidth);
            line.gameObject.layer = beamLayer;
            var cameraObject = new GameObject("Tractor visibility camera", typeof(Camera));
            var target = new RenderTexture(imageSize, imageSize, 24);
            var image = new Texture2D(imageSize, imageSize, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = zoom;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << beamLayer;
                Vector3 centre = (line.GetPosition(0) + line.GetPosition(1)) * 0.5f;
                cameraObject.transform.position = new Vector3(centre.x, centre.y, -100f);
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int visible = pixels.Count(pixel => pixel.g > minimumChannel && pixel.b > minimumChannel &&
                    pixel.g > pixel.r + minimumColourDifference);
                int nonBlack = pixels.Count(pixel => pixel.r > 0 || pixel.g > 0 || pixel.b > 0);
                TestContext.WriteLine($"Beam visibility at game zoom: {visible} coloured pixels.");
                Assert.Greater(visible, minimumBeamPixels, $"The beam must be visible at game zoom; {nonBlack} non-black pixels, " +
                    $"maximum RGB {pixels.Max(pixel => pixel.r)}/{pixels.Max(pixel => pixel.g)}/{pixels.Max(pixel => pixel.b)}.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
            int mountIndex = (int)tractors.Keys.Cast<object>().Single();
            foreach (float radius in new[] { 0.4f, 0.6f })
            {
                NetworkIdentity other = SpawnPickup(TreasureMaterial.Gold, TreasureSize.Large,
                    (Vector2)mount.position + (Vector2)mount.up * 10f, Vector2.zero, radius);
                for (int i = 0; i < waitSteps && !NetworkClient.spawned.ContainsKey(other.netId); i++) yield return wait;
                Assert.IsTrue(NetworkClient.spawned.ContainsKey(other.netId));
                var targets = (uint[])m_ShipType.GetField("m_TractorTargets", Private).GetValue(ship);
                targets[mountIndex] = other.netId;
                RenderTractors(ship);
                Assert.IsTrue(line.enabled);
                Assert.AreEqual(radius * 2f, line.endWidth, 0.0001f);
                Assert.Less(line.startWidth, line.endWidth);
                NetworkServer.Destroy(other.gameObject);
            }
        }

        [Test]
        public void DestroyingHull_DropsRoundedCargoExactlyOnce()
        {
            Component ship = SpawnShip();
            var cargo = (ShipCargo)m_ShipType.GetField("m_Cargo", Private).GetValue(ship);
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Large));
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Medium));
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Silver, TreasureSize.Large));
            Vector2 shipVelocity = new Vector2(3f, 2f);
            m_Ship.GetComponent<Rigidbody2D>().linearVelocity = shipVelocity;
            object hull = m_ShipType.GetField("m_Hull", Private).GetValue(ship);
            var health = (ComponentHealth)hull.GetType().GetProperty("Health").GetValue(hull);
            health.Damage(health.Current);
            m_ShipType.GetMethod("CheckHull", Private).Invoke(ship, null);
            var pickups = NetworkServer.spawned.Values.Select(identity => identity.GetComponent(m_PickupType))
                .Where(component => component != null).ToArray();
            int GoldUnits() => pickups.Where(item => (TreasureMaterial)m_PickupType.GetProperty("Material").GetValue(item) == TreasureMaterial.Gold)
                .Sum(item => ShipCargo.Units((TreasureSize)m_PickupType.GetProperty("Size").GetValue(item)));
            int SilverUnits() => pickups.Where(item => (TreasureMaterial)m_PickupType.GetProperty("Material").GetValue(item) == TreasureMaterial.Silver)
                .Sum(item => ShipCargo.Units((TreasureSize)m_PickupType.GetProperty("Size").GetValue(item)));
            Assert.AreEqual(2, GoldUnits());
            Assert.AreEqual(1, SilverUnits());
            foreach (Component pickup in pickups)
                Assert.AreEqual(1f, (pickup.GetComponent<Rigidbody2D>().linearVelocity - shipVelocity).magnitude, 0.0001f);
            Assert.AreEqual(0, cargo.Total(TreasureMaterial.Gold));
            m_ShipType.GetMethod("CheckHull", Private).Invoke(ship, null);
            Assert.AreEqual(pickups.Length, NetworkServer.spawned.Values.Count(identity => identity.GetComponent(m_PickupType) != null));
        }
    }
}
#endif
