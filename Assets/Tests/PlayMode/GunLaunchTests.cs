#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using Mirror.Tests;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace PewPewPew.Tests
{
    public class ProjectileDamageTests
    {
        private const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
        private Type m_ShipType;
        private Type m_SystemType;
        private GameObject m_ShipObject;
        private GameObject m_TransportObject;
        private Transport m_PreviousTransport;
        private Component m_Ship;
        private ComponentHealth m_Hull;
        private ComponentHealth m_Armour;
        private readonly List<Object> m_Owned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(NetworkServer.active);
            m_PreviousTransport = Transport.active;
            m_TransportObject = new GameObject("Projectile test transport");
            Transport.active = m_TransportObject.AddComponent<MemoryTransport>();
            NetworkServer.Listen(1);
            m_ShipType = Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp", true);
            m_SystemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp", true);
            m_ShipObject = new GameObject("Projectile test ship", typeof(NetworkIdentity));
            m_ShipObject.AddComponent<CircleCollider2D>();
            m_Ship = m_ShipObject.AddComponent(m_ShipType);
            ScriptableObject definition = Definition("HullDef");
            object hull = Activator.CreateInstance(m_SystemType, definition, null, null);
            m_Hull = Health(hull);
            m_Armour = new ComponentHealth(3f);
            Set(m_Ship, "m_Hull", hull);
            Set(m_Ship, "m_HullDef", definition);
            Set(m_Ship, "m_Armour", m_Armour);
            Set(m_Ship, "m_Applied", true);
            ((IList)m_ShipType.GetField("m_Systems", Fields).GetValue(m_Ship)).Add(hull);
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Fields)
                .Invoke(m_ShipObject.GetComponent<NetworkIdentity>(), null);
            NetworkServer.Spawn(m_ShipObject);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            Transport.active = m_PreviousTransport;
            if (m_ShipObject != null) Object.DestroyImmediate(m_ShipObject);
            Object.DestroyImmediate(m_TransportObject);
            foreach (Object owned in m_Owned) if (owned != null) Object.DestroyImmediate(owned);
            m_Owned.Clear();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Fields).SetValue(target, value);
        private ComponentHealth Health(object system) => (ComponentHealth)m_SystemType.GetProperty("Health").GetValue(system);
        private ScriptableObject Definition(string name)
        {
            var definition = ScriptableObject.CreateInstance(Type.GetType($"PewPewPew.Ships.{name}, Assembly-CSharp", true));
            m_Owned.Add(definition);
            return definition;
        }

        private ComponentHealth Shield(float charge, float cap = 100f, bool toggled = true, float angle = 180f)
        {
            ScriptableObject definition = Definition("ShieldDef");
            Set(definition, "m_MaxSingleImpact", cap);
            Set(definition, "m_HalfAngle", angle);
            var mount = new GameObject("Projectile test shield mount");
            mount.transform.SetParent(m_ShipObject.transform);
            object generator = Activator.CreateInstance(m_SystemType, definition, mount.transform, m_ShipObject.transform);
            object shield = Activator.CreateInstance(m_ShipType.GetNestedType("MountedShield", BindingFlags.NonPublic));
            var health = new ComponentHealth(charge);
            shield.GetType().GetField("Def").SetValue(shield, definition);
            shield.GetType().GetField("Generator").SetValue(shield, generator);
            shield.GetType().GetField("Charge").SetValue(shield, health);
            shield.GetType().GetField("Toggled").SetValue(shield, toggled);
            ((IList)m_ShipType.GetField("m_Shields", Fields).GetValue(m_Ship)).Add(shield);
            return health;
        }

        private string Hit(ProjectileDamage damage, Vector2? point = null, Component attacker = null) =>
            m_ShipType.GetMethod("TakeProjectileDamage").Invoke(m_Ship,
                new object[] { damage, point ?? Vector2.up, attacker }).ToString();

        [TestCase(20f, 10f, 8f, 98.4f)]
        [TestCase(20f, 20f, 20f, 93f)]
        public void PartialLayers_UseSurvivingFractionNotFreshDamage(float shieldRating, float armourRating,
            float hullRating, float expectedHull)
        {
            ComponentHealth shield = Shield(10f);
            Assert.AreEqual("Damaged", Hit(new ProjectileDamage(99f, shieldRating, armourRating, hullRating)));
            Assert.AreEqual(0f, shield.Current);
            Assert.AreEqual(0f, m_Armour.Current);
            Assert.AreEqual(expectedHull, m_Hull.Current, 0.00001f);
        }

        [Test]
        public void OverlappingShields_PreserveCapsDirectionsAndFraction()
        {
            ComponentHealth first = Shield(100f, 4f), second = Shield(6f);
            ComponentHealth disabled = Shield(100f, 100f, false);
            ComponentHealth uncovered = Shield(100f, 100f, true, 45f);
            Assert.AreEqual("Damaged", Hit(new ProjectileDamage(0f, 20f, 0f, 8f), Vector2.down));
            Assert.AreEqual(96f, first.Current);
            Assert.AreEqual(0f, second.Current);
            Assert.AreEqual(100f, disabled.Current);
            Assert.AreEqual(100f, uncovered.Current);
            Assert.AreEqual(96f, m_Hull.Current);
            Assert.AreEqual(3f, m_Armour.Current);
        }

        [Test]
        public void FullyStoppedShield_DoesNotReachArmourOrHull()
        {
            Shield(20f);
            Assert.AreEqual("ShieldAbsorbed", Hit(new ProjectileDamage(100f, 20f, 10f, 8f)));
            Assert.AreEqual(3f, m_Armour.Current);
            Assert.AreEqual(100f, m_Hull.Current);
        }

        [TestCase(0f, 0f, 8f, 3f, 92f)]
        [TestCase(0f, 10f, 8f, 0f, 94.4f)]
        [TestCase(0f, 2f, 8f, 1f, 100f)]
        [TestCase(0f, 2f, 0f, 1f, 100f)]
        [TestCase(0f, 0f, 0f, 3f, 100f)]
        public void ZeroRatings_BypassOnlyTheirOwnLayer(float shieldRating, float armourRating, float hullRating,
            float expectedArmour, float expectedHull)
        {
            ComponentHealth shield = Shield(100f);
            string result = Hit(new ProjectileDamage(123f, shieldRating, armourRating, hullRating));
            Assert.AreEqual(100f, shield.Current);
            Assert.AreEqual(expectedArmour, m_Armour.Current, 0.00001f);
            Assert.AreEqual(expectedHull, m_Hull.Current, 0.00001f);
            Assert.AreEqual(armourRating == 0f && hullRating == 0f ? "NoDamage" : "Damaged", result);
        }

        [Test]
        public void BoostedArmour_DepletesInBaseUnitsWithoutCollisionWear()
        {
            Set(m_Ship, "m_ArmourStrengthMultiplier", 2f);
            Assert.AreEqual("Damaged", Hit(new ProjectileDamage(0f, 0f, 4f, 8f)));
            Assert.AreEqual(1f, m_Armour.Current);
            Assert.AreEqual(100f, m_Hull.Current);
            Hit(new ProjectileDamage(0f, 0f, 4f, 8f));
            Assert.AreEqual(0f, m_Armour.Current);
            Assert.AreEqual(96f, m_Hull.Current);
        }

        [Test]
        public void ScalarCollisionPath_KeepsWearRatherThanDirectDepletion()
        {
            m_ShipType.GetMethod("TakeDamage").Invoke(m_Ship, new object[] { 10f, Vector2.up, null });
            Assert.AreEqual(2.7f, m_Armour.Current, 0.00001f);
            Assert.AreEqual(93f, m_Hull.Current);
        }

        [Test]
        public void InvalidRatings_AreReportedWithoutDamage()
        {
            LogAssert.Expect(LogType.Error, "Projectile damage requires finite nonnegative ratings and finite impact coordinates.");
            Assert.AreEqual("NoDamage", Hit(new ProjectileDamage(0f, float.NaN, 10f, 8f)));
            Assert.AreEqual(3f, m_Armour.Current);
            Assert.AreEqual(100f, m_Hull.Current);
            LogAssert.Expect(LogType.Error, "Projectile damage requires finite nonnegative ratings and finite impact coordinates.");
            Hit(new ProjectileDamage(0f, 0f, -1f, 8f));
        }

        [Test]
        public void HullKill_AttributesBeforeDestroyedEvent()
        {
            var attackerObject = new GameObject("Projectile test attacker", typeof(NetworkIdentity));
            m_Owned.Add(attackerObject);
            Component attacker = attackerObject.AddComponent(Type.GetType("PewPewPew.Match.PlayerState, Assembly-CSharp", true));
            Hit(new ProjectileDamage(0f, 0f, 0f, 0f), attacker: attacker);
            Assert.IsNull(m_ShipType.GetMethod("KillCredit").Invoke(m_Ship, new object[] { 5f }));
            object creditAtDeath = null;
            Action<Component> handler = ship => creditAtDeath =
                m_ShipType.GetMethod("KillCredit").Invoke(ship, new object[] { 5f });
            EventInfo destroyed = m_ShipType.GetEvent("Destroyed");
            destroyed.AddEventHandler(m_Ship, Delegate.CreateDelegate(destroyed.EventHandlerType, handler.Target, handler.Method));
            Hit(new ProjectileDamage(0f, 0f, 0f, 100f), attacker: attacker);
            Assert.AreSame(attacker, creditAtDeath);
            Assert.AreEqual(attacker, m_ShipType.GetMethod("KillCredit").Invoke(m_Ship, new object[] { 5f }));
            Assert.IsTrue((bool)m_ShipType.GetProperty("HasExploded").GetValue(m_Ship));
        }

        [Test]
        public void InternalDamage_PreservesFullAndProximityWeighting()
        {
            ComponentHealth System(string name, float x)
            {
                var mount = new GameObject("Projectile component mount");
                mount.transform.SetParent(m_ShipObject.transform);
                mount.transform.localPosition = Vector3.right * x;
                object system = Activator.CreateInstance(m_SystemType, Definition(name), mount.transform, m_ShipObject.transform);
                ((IList)m_ShipType.GetField("m_Systems", Fields).GetValue(m_Ship)).Add(system);
                return Health(system);
            }
            ComponentHealth full = System("ShieldDef", 10f), near = System("ThrusterDef", 1f), far = System("ThrusterDef", 3f);
            Hit(new ProjectileDamage(0f, 0f, 0f, 8f), Vector2.zero);
            Assert.AreEqual(92f, m_Hull.Current);
            Assert.AreEqual(92f, full.Current);
            Assert.AreEqual(96f, near.Current);
            Assert.AreEqual(100f, far.Current);
        }

        [Test]
        public void BulletRoutesRatingsToShipOnceAndLoadoutShowsAllFour()
        {
            Shield(10f);
            Type bulletType = Type.GetType("PewPewPew.Weapons.Bullet, Assembly-CSharp", true);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SpawnablePrefabs/Bullets/SimpleBullet.prefab");
            var bulletObject = Object.Instantiate(prefab);
            Component bullet = bulletObject.GetComponent(bulletType);
            Set(bullet, "m_AsteroidDamage", 99f);
            Set(bullet, "m_ShieldDamage", 20f);
            Set(bullet, "m_ArmourDamage", 10f);
            Set(bullet, "m_HullComponentDamage", 8f);
            NetworkServer.Spawn(bulletObject);
            MethodInfo trigger = bulletType.GetMethod("OnTriggerEnter2D", Fields);
            trigger.Invoke(bullet, new object[] { m_ShipObject.GetComponent<Collider2D>() });
            trigger.Invoke(bullet, new object[] { m_ShipObject.GetComponent<Collider2D>() });
            Assert.AreEqual(98.4f, m_Hull.Current, 0.00001f);
            Assert.IsFalse(bullet.GetComponent<Collider2D>().enabled);
            var gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ShipComponents/Weapons/Basic Gun Single.asset"));
            m_Owned.Add(gun);
            Set(gun, "m_BulletPrefab", bullet);
            Type details = Type.GetType("PewPewPew.Match.LoadoutDetails, Assembly-CSharp", true);
            string text = (string)details.GetMethod("Component").Invoke(null, new object[] { gun });
            StringAssert.Contains("asteroid: 99", text);
            StringAssert.Contains("Shield: 20", text);
            StringAssert.Contains("Armour: 10", text);
            StringAssert.Contains("Hull/components: 8", text);
        }
    }

    public class GunLaunchTests
    {
        [Test]
        public void Step_SpawnsBurstWithShooterAndInheritedVelocity()
        {
            Assert.IsFalse(NetworkServer.active, "The test requires an inactive network server.");
            Transport previousTransport = Transport.active;
            var transportObject = new GameObject("Test transport");
            var shooterObject = new GameObject("Test shooter");
            var ownerObject = new GameObject("Test owner");
            var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Weapons/Basic Gun Single.asset"));
            bool serverStarted = false;
            try
            {
                Transport.active = transportObject.AddComponent<MemoryTransport>();
                NetworkServer.Listen(1);
                serverStarted = true;

                var shooterCollider = shooterObject.AddComponent<CircleCollider2D>();
                shooterObject.AddComponent<NetworkIdentity>();
                Type shipType = Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp");
                Component shooter = shooterObject.AddComponent(shipType);
                ownerObject.AddComponent<NetworkIdentity>();
                Component owner = ownerObject.AddComponent(Type.GetType("PewPewPew.Match.PlayerState, Assembly-CSharp"));
                shipType.GetMethod("Initialize").Invoke(shooter, new object[] { ShipLoadout.Empty, owner });

                var mount = new GameObject("Gun mount").transform;
                mount.SetParent(shooterObject.transform);
                mount.position = new Vector3(3f, 4f, 0f);
                mount.rotation = Quaternion.Euler(0f, 0f, 60f);

                Type definitionType = definition.GetType();
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                definitionType.GetField("m_BulletsPerBurst", privateInstance).SetValue(definition, 2);
                definitionType.GetField("m_MinLaunchAngle", privateInstance).SetValue(definition, -15f);
                definitionType.GetField("m_MaxLaunchAngle", privateInstance).SetValue(definition, 15f);
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object system = Activator.CreateInstance(systemType, definition, mount, shooterObject.transform);
                Type gunType = Type.GetType("PewPewPew.Weapons.Gun, Assembly-CSharp");
                object gun = Activator.CreateInstance(gunType, definition, system, shooter);
                MethodInfo step = gunType.GetMethod("Step");
                var power = new PowerBank(100f);
                var velocity = new Vector2(2f, -3f);
                var random = new Random(1);

                bool Step(object weapon, PowerBank bank, bool fire) =>
                    (bool)step.Invoke(weapon, new object[] { 0.02f, mount, velocity, bank, fire, random });

                Assert.IsFalse(Step(gun, power, false));
                Assert.AreEqual(0, NetworkServer.spawned.Count);
                var emptyPower = new PowerBank(100f);
                emptyPower.SetFraction(0f);
                Assert.IsFalse(Step(gun, emptyPower, true));
                Assert.AreEqual(0, NetworkServer.spawned.Count);
                Assert.IsTrue(Step(gun, power, true));
                Assert.Less(power.Stored, 100f);
                Assert.IsFalse(Step(gun, power, true));

                var bullets = NetworkServer.spawned.Values.OrderBy(identity => identity.netId).ToArray();
                Assert.AreEqual(2, bullets.Length);
                Type bulletType = Type.GetType("PewPewPew.Weapons.Bullet, Assembly-CSharp");
                float speed = (float)definitionType.GetProperty("LaunchSpeed").GetValue(definition);
                for (int i = 0; i < bullets.Length; i++)
                {
                    Component bullet = bullets[i].GetComponent(bulletType);
                    Assert.IsNotNull(bullet);
                    float angle = 60f + (i == 0 ? -15f : 15f);
                    Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                    Assert.Less(Vector3.Distance(mount.position, bullet.transform.position), 0.001f);
                    Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 0f, angle), bullet.transform.rotation), 0.001f);
                    Assert.Less(Vector2.Distance(velocity + direction * speed,
                        bullet.GetComponent<Rigidbody2D>().linearVelocity), 0.001f);
                    Assert.AreSame(owner, bulletType.GetField("m_Attacker", privateInstance).GetValue(bullet));
                    Assert.IsTrue(Physics2D.GetIgnoreCollision(shooterCollider, bullet.GetComponent<Collider2D>()));
                }
            }
            finally
            {
                if (serverStarted) NetworkServer.Shutdown();
                Transport.active = previousTransport;
                Object.DestroyImmediate(transportObject);
                Object.DestroyImmediate(shooterObject);
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
#endif
