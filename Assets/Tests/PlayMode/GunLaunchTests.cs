#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Mirror;
using Mirror.Tests;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace PewPewPew.Tests
{
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
