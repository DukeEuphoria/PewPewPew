using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEngine;
using UnityEngine.Events;
using Random = System.Random;

namespace PewPewPew.Tests
{
    public class MountedComponentControlTests
    {
        [Test]
        public void ShipSystem_StoresSinglePointInShipSpaceOrNoPoint()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Weapons/Basic Gun Single.asset");
            var ship = new GameObject("Ship space");
            var mount = new GameObject("Mount");
            try
            {
                ship.transform.SetPositionAndRotation(new Vector3(7f, -3f, 0f), Quaternion.Euler(0f, 0f, 45f));
                mount.transform.SetParent(ship.transform, false);
                mount.transform.localPosition = new Vector3(2f, 4f, 0f);
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object system = Activator.CreateInstance(systemType, definition, mount.transform, ship.transform);
                object hull = Activator.CreateInstance(systemType, definition, null, null);
                var point = systemType.GetProperty("Point");
                var position = systemType.GetProperty("PointPosition");

                Assert.AreSame(mount.transform, point.GetValue(system));
                Assert.Less(Vector2.Distance(new Vector2(2f, 4f), (Vector2)position.GetValue(system)), 0.0001f);
                Assert.IsNull(point.GetValue(hull));
                Assert.IsNull(position.GetValue(hull));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ship);
            }
        }

        [Test]
        public void Shortcuts_SkipEmptyMountsAndStopAtEnd()
        {
            int[] mounts = { -1, 2, -1, 5, 3 };
            Assert.AreEqual(1, LoadoutMath.OccupiedMountAt(mounts, 0));
            Assert.AreEqual(3, LoadoutMath.OccupiedMountAt(mounts, 1));
            Assert.AreEqual(4, LoadoutMath.OccupiedMountAt(mounts, 2));
            Assert.AreEqual(-1, LoadoutMath.OccupiedMountAt(mounts, 3));
            Assert.AreEqual(-1, LoadoutMath.OccupiedMountAt(mounts, -1));
        }

        [Test]
        public void TriggeredComponent_TriggersOnceAndStaysEnabledThroughCooldown()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/SubSystems/Basic SubSystem Thrust Booster.asset");
            Type type = Type.GetType("PewPewPew.Ships.SubSystem, Assembly-CSharp");
            object subsystem = Activator.CreateInstance(type, definition, null, null);
            object system = type.GetProperty("System").GetValue(subsystem);
            object activation = type.GetProperty("Activation").GetValue(subsystem);
            var enabled = system.GetType().GetProperty("Enabled");
            var active = activation.GetType().GetProperty("IsActive");
            var toggle = type.GetMethod("TogglePower");
            var step = type.GetMethod("Step");
            var refresh = type.GetMethod("RefreshHud");
            var state = type.GetProperty("HudState");
            var power = new PowerBank(100f);

            step.Invoke(subsystem, new object[] { 0.1f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsFalse((bool)active.GetValue(activation));
            Assert.AreEqual(100f, power.Stored);
            Assert.AreEqual(HudState.Ready, state.GetValue(subsystem));
            Assert.AreEqual(1f, (float)type.GetProperty("HudLevel").GetValue(subsystem));
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsTrue((bool)active.GetValue(activation));
            Assert.AreEqual(HudState.Active, state.GetValue(subsystem));
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsTrue((bool)active.GetValue(activation));
            step.Invoke(subsystem, new object[] { 4.85f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsFalse((bool)active.GetValue(activation));
            Assert.AreEqual(HudState.Cooling, state.GetValue(subsystem));
            Assert.AreEqual(0f, (float)type.GetProperty("HudLevel").GetValue(subsystem));
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 2f, power });
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsFalse((bool)active.GetValue(activation));
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 3.1f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsFalse((bool)active.GetValue(activation));
            Assert.AreEqual(HudState.Ready, state.GetValue(subsystem));
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsTrue((bool)active.GetValue(activation));
            var health = (ComponentHealth)system.GetType().GetProperty("Health").GetValue(system);
            health.Damage(health.Max);
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsFalse((bool)active.GetValue(activation));
            Assert.AreEqual(HudState.Destroyed, state.GetValue(subsystem));
        }

        [Test]
        public void TriggeredComponent_PowerFailureDoesNotDisableIt()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/SubSystems/Basic SubSystem Thrust Booster.asset");
            Type type = Type.GetType("PewPewPew.Ships.SubSystem, Assembly-CSharp");
            object subsystem = Activator.CreateInstance(type, definition, null, null);
            object system = type.GetProperty("System").GetValue(subsystem);
            object activation = type.GetProperty("Activation").GetValue(subsystem);
            var enabled = system.GetType().GetProperty("Enabled");
            var active = activation.GetType().GetProperty("IsActive");
            var toggle = type.GetMethod("TogglePower");
            var step = type.GetMethod("Step");
            var refresh = type.GetMethod("RefreshHud");
            var state = type.GetProperty("HudState");
            var power = new PowerBank(100f);
            power.SetFraction(0f);

            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsFalse((bool)active.GetValue(activation));
            Assert.AreEqual(HudState.Ready, state.GetValue(subsystem));
            power.Charge(10f);
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            Assert.IsTrue((bool)active.GetValue(activation));
            power.SetFraction(0f);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            refresh.Invoke(subsystem, null);
            Assert.IsTrue((bool)enabled.GetValue(system));
            Assert.IsFalse((bool)active.GetValue(activation));
            Assert.AreEqual(HudState.Cooling, state.GetValue(subsystem));
            power.Charge(10f);
            step.Invoke(subsystem, new object[] { 5.1f, power });
            toggle.Invoke(subsystem, null);
            step.Invoke(subsystem, new object[] { 0.1f, power });
            Assert.IsTrue((bool)active.GetValue(activation));
        }

        [Test]
        public void PassiveBooster_StopsRunningWhileDisabled()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/SubSystems/Basic SubSystem Armour Booster.asset");
            Type type = Type.GetType("PewPewPew.Ships.SubSystem, Assembly-CSharp");
            object subsystem = Activator.CreateInstance(type, definition, null, null);
            var running = type.GetProperty("IsRunning");
            var toggle = type.GetMethod("TogglePower");
            Assert.IsTrue((bool)running.GetValue(subsystem));
            toggle.Invoke(subsystem, null);
            Assert.IsFalse((bool)running.GetValue(subsystem));
            toggle.Invoke(subsystem, null);
            Assert.IsTrue((bool)running.GetValue(subsystem));
        }

        [Test]
        public void DisabledThruster_RampsDownWithoutDrawingPower()
        {
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Thrusters/Basic Thruster.asset");
            var definition = UnityEngine.Object.Instantiate(original);
            var mount = new GameObject("Thruster mount");
            try
            {
                definition.GetType().GetField("m_RampDownTime", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(definition, 1f);
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object system = Activator.CreateInstance(systemType, definition, mount.transform, mount.transform);
                Type thrusterType = Type.GetType("PewPewPew.Ships.Thruster, Assembly-CSharp");
                object thruster = Activator.CreateInstance(thrusterType, definition, system);
                var step = thrusterType.GetMethod("Step");
                var throttle = thrusterType.GetProperty("Throttle");
                var power = new PowerBank(100f);

                step.Invoke(thruster, new object[] { 0.1f, 1f, power });
                Assert.AreEqual(1f, (float)throttle.GetValue(thruster), 0.001f);
                float remaining = power.Stored;
                systemType.GetProperty("Enabled").SetValue(system, false);
                step.Invoke(thruster, new object[] { 0.1f, 1f, power });
                Assert.AreEqual(0.9f, (float)throttle.GetValue(thruster), 0.001f);
                Assert.AreEqual(remaining, power.Stored);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mount);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void DisabledGun_DoesNotFireOrDrawPower()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Weapons/Basic Gun Single.asset");
            var mount = new GameObject("Gun mount");
            var shooterObject = new GameObject("Shooter");
            try
            {
                shooterObject.AddComponent(Type.GetType("Mirror.NetworkIdentity, Mirror"));
                Component shooter = shooterObject.AddComponent(Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp"));
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object system = Activator.CreateInstance(systemType, definition, mount.transform, mount.transform);
                Type gunType = Type.GetType("PewPewPew.Weapons.Gun, Assembly-CSharp");
                object gun = Activator.CreateInstance(gunType, definition, system, shooter);
                var power = new PowerBank(100f);
                systemType.GetProperty("Enabled").SetValue(system, false);
                var fire = gunType.GetMethod("TryFire", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNull(fire.Invoke(gun, new object[] { 1f, power, new Random(1) }));
                Assert.AreEqual(100f, power.Stored);
                systemType.GetProperty("Enabled").SetValue(system, true);
                Assert.IsNotNull(fire.Invoke(gun, new object[] { 1f, power, new Random(1) }));
                Assert.Less(power.Stored, 100f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mount);
                UnityEngine.Object.DestroyImmediate(shooterObject);
            }
        }

        [Test]
        public void Gun_FiresBurstAnglesAndPreservesPowerAndCooldownRules()
        {
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Weapons/Basic Gun Single.asset");
            var definition = UnityEngine.Object.Instantiate(original);
            var mount = new GameObject("Gun mount");
            var shooterObject = new GameObject("Shooter");
            try
            {
                shooterObject.AddComponent(Type.GetType("Mirror.NetworkIdentity, Mirror"));
                Component shooter = shooterObject.AddComponent(Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp"));
                Type definitionType = definition.GetType();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                definitionType.GetField("m_BulletsPerBurst", fields).SetValue(definition, 4);
                definitionType.GetField("m_MinLaunchAngle", fields).SetValue(definition, -30f);
                definitionType.GetField("m_MaxLaunchAngle", fields).SetValue(definition, 30f);
                definitionType.GetField("m_Pattern", fields).SetValue(definition, FirePattern.Spread);
                definitionType.GetField("m_FireRate", fields).SetValue(definition, 2f);
                definitionType.BaseType.GetField("m_ActivePowerDrain", fields).SetValue(definition, 7f);
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object system = Activator.CreateInstance(systemType, definition, mount.transform, mount.transform);
                Type gunType = Type.GetType("PewPewPew.Weapons.Gun, Assembly-CSharp");
                object gun = Activator.CreateInstance(gunType, definition, system, shooter);
                var fire = gunType.GetMethod("TryFire", BindingFlags.Instance | BindingFlags.NonPublic);
                var power = new PowerBank(20f);
                var random = new Random(1);

                var angles = (float[])fire.Invoke(gun, new object[] { 0f, power, random });
                CollectionAssert.AreEqual(new[] { -30f, -10f, 10f, 30f }, angles);
                Assert.AreEqual(13f, power.Stored);
                Assert.IsNull(fire.Invoke(gun, new object[] { 0.49f, power, random }));
                Assert.AreEqual(13f, power.Stored);
                Assert.IsNotNull(fire.Invoke(gun, new object[] { 0.5f, power, random }));
                Assert.AreEqual(6f, power.Stored);
                Assert.IsNull(fire.Invoke(gun, new object[] { 1f, power, random }));
                Assert.AreEqual(6f, power.Stored);
                power.Charge(7f);
                Assert.IsNotNull(fire.Invoke(gun, new object[] { 1f, power, random }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mount);
                UnityEngine.Object.DestroyImmediate(shooterObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Gun_AdvancesSweepWithoutFiring()
        {
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Weapons/Basic Gun Single.asset");
            var definition = UnityEngine.Object.Instantiate(original);
            var mount = new GameObject("Gun mount");
            var shooterObject = new GameObject("Shooter");
            try
            {
                shooterObject.AddComponent(Type.GetType("Mirror.NetworkIdentity, Mirror"));
                Component shooter = shooterObject.AddComponent(Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp"));
                Type definitionType = definition.GetType();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                definitionType.GetField("m_Pattern", fields).SetValue(definition, FirePattern.Sinewave);
                definitionType.GetField("m_MinLaunchAngle", fields).SetValue(definition, -20f);
                definitionType.GetField("m_MaxLaunchAngle", fields).SetValue(definition, 20f);
                definitionType.GetField("m_SweepSpeed", fields).SetValue(definition, 2f);
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object system = Activator.CreateInstance(systemType, definition, mount.transform, mount.transform);
                Type gunType = Type.GetType("PewPewPew.Weapons.Gun, Assembly-CSharp");
                object gun = Activator.CreateInstance(gunType, definition, system, shooter);
                var power = new PowerBank(100f);
                var random = new Random(1);

                Assert.IsFalse((bool)gunType.GetMethod("Step").Invoke(gun,
                    new object[] { 0.25f, mount.transform, Vector2.zero, power, false, random }));
                Assert.AreEqual(100f, power.Stored);
                var angles = (float[])gunType.GetMethod("TryFire", fields)
                    .Invoke(gun, new object[] { 0f, power, random });
                Assert.AreEqual(20f * Mathf.Sin(0.5f), angles[0], 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mount);
                UnityEngine.Object.DestroyImmediate(shooterObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void DisabledComponents_NeitherGenerateNorConsumePassivePower()
        {
            var generator = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/SubSystems/Basic SubSystem Power Generator.asset");
            var consumer = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Thrusters/Basic Thruster.asset");
            var shipObject = new GameObject("Power test ship");
            try
            {
                shipObject.AddComponent(Type.GetType("Mirror.NetworkIdentity, Mirror"));
                Type shipType = Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp");
                Component ship = shipObject.AddComponent(shipType);
                Type systemType = Type.GetType("PewPewPew.Ships.ShipSystem, Assembly-CSharp");
                object generatorSystem = Activator.CreateInstance(systemType, generator, null, null);
                object consumerSystem = Activator.CreateInstance(systemType, consumer, null, null);
                var enabled = systemType.GetProperty("Enabled");
                enabled.SetValue(generatorSystem, false);
                enabled.SetValue(consumerSystem, false);
                var systems = (IList)shipType.GetField("m_Systems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ship);
                systems.Add(generatorSystem);
                systems.Add(consumerSystem);
                var power = new PowerBank(10f);
                power.SetFraction(0.5f);
                shipType.GetField("m_Power", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ship, power);
                var step = shipType.GetMethod("StepPower", BindingFlags.Instance | BindingFlags.NonPublic);

                step.Invoke(ship, new object[] { 1f });
                Assert.AreEqual(5f, power.Stored);
                enabled.SetValue(generatorSystem, true);
                step.Invoke(ship, new object[] { 1f });
                Assert.Greater(power.Stored, 5f);
                float generated = power.Stored;
                enabled.SetValue(generatorSystem, false);
                enabled.SetValue(consumerSystem, true);
                step.Invoke(ship, new object[] { 1f });
                Assert.Less(power.Stored, generated);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shipObject);
            }
        }
    }

    public class ShipHudRootTests
    {
        [Test]
        public void PaperdollSize_SetsPanelSizeAndMovesBarsToItsRight()
        {
            var rootObject = new GameObject("HUD root", typeof(RectTransform));
            var paperdollObject = new GameObject("Paperdoll", typeof(RectTransform));
            var barObject = new GameObject("Bar", typeof(RectTransform));
            try
            {
                var root = rootObject.AddComponent(Type.GetType("PewPewPew.Ships.ShipHudRoot, Assembly-CSharp"));
                root.GetType().GetField("m_PaperdollSize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(root, 180f);
                var paperdoll = (RectTransform)paperdollObject.transform;
                var bar = (RectTransform)barObject.transform;
                root.GetType().GetMethod("PlacePaperdoll").Invoke(root, new object[] { paperdoll });
                root.GetType().GetMethod("PlaceBar").Invoke(root, new object[] { bar, 0, 2 });
                Assert.AreEqual(new Vector2(180f, 180f), paperdoll.sizeDelta);
                Assert.AreEqual(new Vector2(90f, 90f), paperdoll.anchoredPosition);
                Assert.AreEqual(new Vector2(304f, 28f), bar.anchoredPosition);
                root.GetType().GetMethod("PlaceBar").Invoke(root, new object[] { bar, 1, 2 });
                Assert.AreEqual(paperdoll.anchoredPosition.y - paperdoll.rect.height * 0.5f,
                    bar.anchoredPosition.y - bar.rect.height * 0.5f, 0.001f);
                Assert.AreEqual(0f, paperdoll.anchoredPosition.x - paperdoll.rect.width * 0.5f, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(paperdollObject);
                UnityEngine.Object.DestroyImmediate(barObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [TestCase(10f, 10f)]
        [TestCase(20f, 10f)]
        public void PlaceSubSystem_ScalesWholePrefabWithoutChangingItsLayout(float width, float height)
        {
            var rootObject = new GameObject("HUD root", typeof(RectTransform));
            var elementObject = new GameObject("Subsystem", typeof(RectTransform));
            try
            {
                var root = rootObject.AddComponent(Type.GetType("PewPewPew.Ships.ShipHudRoot, Assembly-CSharp"));
                var element = (RectTransform)elementObject.transform;
                element.sizeDelta = new Vector2(width, height);
                var child = new GameObject("Child", typeof(RectTransform)).GetComponent<RectTransform>();
                child.SetParent(element, false);
                child.sizeDelta = new Vector2(3f, 4f);

                var place = root.GetType().GetMethod("PlaceSubSystem");
                place.Invoke(root, new object[] { element, 0 });
                Assert.AreEqual(new Vector2(width, height), element.sizeDelta);
                Assert.AreEqual(new Vector3(57f / width, 57f / height, 1f), element.localScale);
                Assert.AreEqual(new Vector2(28.5f, 28.5f), element.anchoredPosition);
                Assert.AreEqual(new Vector2(3f, 4f), child.sizeDelta);

                place.Invoke(root, new object[] { element, 5 });
                Assert.AreEqual(new Vector3(57f / width, 57f / height, 1f), element.localScale);
                Assert.AreEqual(new Vector2(89.5f, 89.5f), element.anchoredPosition);
                Assert.AreEqual(new Vector2(width, height), element.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(elementObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PlaceSubSystem_RejectsZeroSizedElement()
        {
            var rootObject = new GameObject("HUD root", typeof(RectTransform));
            var elementObject = new GameObject("Subsystem", typeof(RectTransform));
            try
            {
                var root = rootObject.AddComponent(Type.GetType("PewPewPew.Ships.ShipHudRoot, Assembly-CSharp"));
                var element = (RectTransform)elementObject.transform;
                element.sizeDelta = new Vector2(0f, 10f);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    "HUD subsystem must have a positive width and height.");
                root.GetType().GetMethod("PlaceSubSystem").Invoke(root, new object[] { element, 0 });
                Assert.AreEqual(Vector3.one, element.localScale);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(elementObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }
    }

    public class ComponentIconRotationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void DirectionalCoverage_RotatesOnlyBelow180Degrees(bool radar)
        {
            string path = radar
                ? "Assets/ShipComponents/SubSystems/Basic SubSystem Radar Cone.asset"
                : "Assets/ShipComponents/Shields/Basic Shield 90.asset";
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            var definition = UnityEngine.Object.Instantiate(original);
            ScriptableObject emitter = null;
            try
            {
                ScriptableObject coverage = definition;
                if (radar)
                {
                    emitter = UnityEngine.Object.Instantiate(
                        (ScriptableObject)definition.GetType().GetProperty("Radar").GetValue(definition));
                    definition.GetType().GetField("m_Radar", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(definition, emitter);
                    coverage = emitter;
                }
                FieldInfo halfAngle = coverage.GetType().GetField("m_HalfAngle", BindingFlags.Instance | BindingFlags.NonPublic);
                Type symbolType = Type.GetType("PewPewPew.Match.LobbySymbol, Assembly-CSharp");
                MethodInfo rotation = symbolType.GetMethod("ComponentRotation");
                halfAngle.SetValue(coverage, 179.999f);
                Assert.AreEqual(37f, (float)rotation.Invoke(null, new object[] { definition, 37f }));
                halfAngle.SetValue(coverage, 180f);
                Assert.AreEqual(0f, (float)rotation.Invoke(null, new object[] { definition, 37f }));
            }
            finally
            {
                if (emitter != null) UnityEngine.Object.DestroyImmediate(emitter);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [TestCase("Assets/ShipComponents/Weapons/Basic Gun Single.asset", 37f)]
        [TestCase("Assets/ShipComponents/Thrusters/Basic Thruster.asset", 217f)]
        [TestCase("Assets/ShipComponents/Shields/Basic Shield 90.asset", 37f)]
        [TestCase("Assets/ShipComponents/Shields/Basic Shield.asset", 0f)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Radar Cone.asset", 37f)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Radar Circle.asset", 0f)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Thrust Booster.asset", 0f)]
        public void LoadoutMarker_OrientsFittedArtworkWithoutRotatingTheBox(string assetPath, float expectedAngle)
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            Assert.IsNotNull(definition);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/HUD/LoadoutSlotMarker.prefab");
            GameObject markerObject = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
                object catalog = catalogType.GetProperty("Instance").GetValue(null);
                object hull = ((Array)catalogType.GetProperty("Hulls").GetValue(catalog)).GetValue(0);
                object hullPoints = hull.GetType().GetProperty("HullPoints").GetValue(hull);
                object point = ((Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints)).GetValue(0);
                Type markerType = Type.GetType("PewPewPew.Match.LoadoutPreviewMarker, Assembly-CSharp");
                Component marker = markerObject.GetComponent(markerType);
                markerType.GetMethod("Bind").Invoke(marker, new object[]
                {
                    point, definition, Vector2.one * 0.5f, 37f, new Vector3(30f, 42f, 56f),
                    3f, false, false, null, null
                });
                Assert.AreEqual(0f, Mathf.DeltaAngle(37f, marker.transform.eulerAngles.z), 0.001f);
                var artwork = (Component)markerType.GetProperty("Artwork").GetValue(marker);
                Assert.AreEqual(0f, Mathf.DeltaAngle(expectedAngle, artwork.transform.eulerAngles.z), 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void LoadoutMarker_OrientsFallbackAndResetsEmptySlotSymbols()
        {
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/ShipComponents/Thrusters/Basic Thruster.asset");
            var definition = UnityEngine.Object.Instantiate(original);
            definition.GetType().BaseType.GetField("m_ButtonArtwork", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, null);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/HUD/LoadoutSlotMarker.prefab");
            GameObject markerObject = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
                object catalog = catalogType.GetProperty("Instance").GetValue(null);
                object hull = ((Array)catalogType.GetProperty("Hulls").GetValue(catalog)).GetValue(0);
                object hullPoints = hull.GetType().GetProperty("HullPoints").GetValue(hull);
                object point = ((Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints)).GetValue(0);
                Type markerType = Type.GetType("PewPewPew.Match.LoadoutPreviewMarker, Assembly-CSharp");
                Component marker = markerObject.GetComponent(markerType);
                MethodInfo bind = markerType.GetMethod("Bind");
                object[] args = { point, definition, Vector2.one * 0.5f, 37f, new Vector3(30f, 42f, 56f),
                    3f, false, false, null, null };
                bind.Invoke(marker, args);
                object componentType = definition.GetType().GetProperty("Type").GetValue(definition);
                var symbol = (Component)markerType.GetMethod("TypeIcon").Invoke(marker, new[] { componentType });
                Assert.IsTrue(symbol.gameObject.activeSelf);
                Assert.AreEqual(0f, Mathf.DeltaAngle(217f, symbol.transform.eulerAngles.z), 0.001f);
                args[1] = null;
                bind.Invoke(marker, args);
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, symbol.transform.localEulerAngles.z), 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(markerObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [TestCase("Assets/ShipComponents/Weapons/Basic Gun Single.asset", 0)]
        [TestCase("Assets/ShipComponents/Thrusters/Basic Thruster.asset", 1)]
        [TestCase("Assets/ShipComponents/Shields/Basic Shield 90.asset", 0)]
        [TestCase("Assets/ShipComponents/Shields/Basic Shield.asset", 2)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Radar Cone.asset", 0)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Radar Circle.asset", 2)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Thrust Booster.asset", 2)]
        public void Paperdoll_OrientsInstalledComponents(string assetPath, int orientation)
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            Assert.IsNotNull(definition);
            Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
            object catalog = catalogType.GetProperty("Instance").GetValue(null);
            var components = (Array)catalogType.GetProperty("Components").GetValue(catalog);
            int componentIndex = Enumerable.Range(0, components.Length).First(i => ReferenceEquals(components.GetValue(i), definition));
            var hulls = (Array)catalogType.GetProperty("Hulls").GetValue(catalog);
            int hullIndex = -1;
            int mount = -1;
            float angle = 0f;
            for (int h = 0; h < hulls.Length && mount < 0; h++)
            {
                object hull = hulls.GetValue(h);
                var hullPoints = (Component)hull.GetType().GetProperty("HullPoints").GetValue(hull);
                var points = (Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints);
                for (int i = 0; i < points.Length; i++)
                {
                    var point = (Component)points.GetValue(i);
                    Vector2 up = hullPoints.transform.InverseTransformDirection(point.transform.up);
                    float candidate = Vector2.SignedAngle(Vector2.up, up);
                    if (Mathf.Abs(candidate) < 0.01f ||
                        !(bool)point.GetType().GetMethod("AcceptsComponent").Invoke(point, new object[] { definition })) continue;
                    hullIndex = h;
                    mount = i;
                    angle = candidate;
                    break;
                }
            }
            Assert.GreaterOrEqual(mount, 0, "A rotated compatible hull point is required.");
            var loadout = (ShipLoadout)catalogType.GetMethod("DefaultLoadout").Invoke(catalog, new object[] { hullIndex });
            loadout.MountComponents[mount] = componentIndex;
            var canvas = new GameObject("HUD canvas", typeof(RectTransform), typeof(Canvas));
            var owner = new GameObject("Paperdoll", typeof(RectTransform));
            try
            {
                owner.transform.SetParent(canvas.transform, false);
                Type type = Type.GetType("PewPewPew.Ships.ShipHudPaperdoll, Assembly-CSharp");
                Component paperdoll = owner.AddComponent(type);
                type.GetMethod("Initialize").Invoke(paperdoll, new object[] { loadout, null, 360f, 0.65f, 1f });
                var buttons = (Array)type.GetField("m_Buttons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                var button = (Component)buttons.GetValue(mount);
                float expected = orientation == 0 ? angle : orientation == 1 ? angle + 180f : 0f;
                Assert.AreEqual(0f, Mathf.DeltaAngle(expected, button.transform.localEulerAngles.z), 0.001f);
                var gauge = (Component)type.GetMethod("GetMountGauge").Invoke(paperdoll, new object[] { mount });
                if (gauge != null)
                    Assert.AreEqual(0f, Mathf.DeltaAngle(expected, gauge.transform.eulerAngles.z), 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }
    }

    public class ShipHudPaperdollTests
    {
        [Test]
        public void Paperdoll_EmptyMountsShowCountableCargoWithoutInterceptingClicks()
        {
            Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
            object catalog = catalogType.GetProperty("Instance").GetValue(null);
            var loadout = (ShipLoadout)catalogType.GetMethod("DefaultLoadout").Invoke(catalog, new object[] { 0 });
            for (int i = 0; i < loadout.MountComponents.Length; i++) loadout.MountComponents[i] = -1;
            object hull = ((Array)catalogType.GetProperty("Hulls").GetValue(catalog)).GetValue(0);
            object hullPoints = hull.GetType().GetProperty("HullPoints").GetValue(hull);
            var points = (Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints);
            var capacities = new int[points.Length];
            Type shipType = Type.GetType("PewPewPew.Ships.Ship, Assembly-CSharp");
            for (int i = 0; i < points.Length; i++)
                capacities[i] = (int)shipType.GetMethod("CargoCapacity").Invoke(null,
                    new[] { points.GetValue(i).GetType().GetProperty("Size").GetValue(points.GetValue(i)) });
            CollectionAssert.Contains(capacities, 16);
            CollectionAssert.Contains(capacities, 25);
            CollectionAssert.Contains(capacities, 49);
            var cargo = new ShipCargo(capacities);
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Large));
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.RedCrystal, TreasureSize.Small));
            var canvas = new GameObject("HUD canvas", typeof(RectTransform), typeof(Canvas));
            var owner = new GameObject("Paperdoll", typeof(RectTransform));
            try
            {
                owner.transform.SetParent(canvas.transform, false);
                Type type = Type.GetType("PewPewPew.Ships.ShipHudPaperdoll, Assembly-CSharp");
                Component paperdoll = owner.AddComponent(type);
                type.GetMethod("Initialize").Invoke(paperdoll, new object[] { loadout, null, 240f, 0.65f, 1f });
                type.GetMethod("SetCargo").Invoke(paperdoll, new object[] { cargo });
                var grids = (Array)type.GetField("m_CargoCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                for (int mount = 0; mount < points.Length; mount++)
                {
                    var cells = (Array)grids.GetValue(mount);
                    Assert.AreEqual(capacities[mount], cells.Length);
                    var gridRect = (RectTransform)((Component)cells.GetValue(0)).transform.parent;
                    float slotWidth = capacities[mount] == 16 ? 24f : capacities[mount] == 25 ? 36f : 50f;
                    Assert.AreEqual(slotWidth, gridRect.rect.width, 0.001f);
                    foreach (Component cell in cells)
                    {
                        Assert.IsFalse((bool)cell.GetType().GetProperty("raycastTarget").GetValue(cell));
                        var rect = (RectTransform)cell.transform;
                        var grid = (RectTransform)rect.parent;
                        Assert.LessOrEqual(Mathf.Abs(rect.anchoredPosition.x) + rect.rect.width * 0.5f, grid.rect.width * 0.5f + 0.001f);
                        Assert.LessOrEqual(Mathf.Abs(rect.anchoredPosition.y) + rect.rect.height * 0.5f, grid.rect.height * 0.5f + 0.001f);
                    }
                    var left = (RectTransform)((Component)cells.GetValue(0)).transform;
                    var right = (RectTransform)((Component)cells.GetValue(1)).transform;
                    Assert.Less(left.rect.width, right.anchoredPosition.x - left.anchoredPosition.x);
                }
                var first = (Array)grids.GetValue(0);
                Color gold = (Color)((Component)first.GetValue(0)).GetType().GetProperty("color").GetValue(first.GetValue(0));
                Color red = (Color)((Component)first.GetValue(3)).GetType().GetProperty("color").GetValue(first.GetValue(3));
                Color empty = (Color)((Component)first.GetValue(4)).GetType().GetProperty("color").GetValue(first.GetValue(4));
                Assert.AreNotEqual(gold, red);
                Assert.AreNotEqual(red, empty);
                Assert.AreEqual(gold, (Color)((Component)first.GetValue(2)).GetType().GetProperty("color").GetValue(first.GetValue(2)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [TestCase(240f, 1f)]
        [TestCase(360f, 1f)]
        [TestCase(360f, 1.25f)]
        public void Paperdoll_SizeDoesNotChangeIconsOrCargoSlotFootprint(float panelSize, float markerScale)
        {
            Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
            object catalog = catalogType.GetProperty("Instance").GetValue(null);
            var loadout = (ShipLoadout)catalogType.GetMethod("DefaultLoadout").Invoke(catalog, new object[] { 0 });
            var components = (Array)catalogType.GetProperty("Components").GetValue(catalog);
            object hull = ((Array)catalogType.GetProperty("Hulls").GetValue(catalog)).GetValue(0);
            object hullPoints = hull.GetType().GetProperty("HullPoints").GetValue(hull);
            var points = (Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints);
            int mount = Enumerable.Range(0, points.Length).First(i => loadout.MountComponents[i] >= 0 &&
                points.GetValue(i).GetType().GetProperty("Size").GetValue(points.GetValue(i)).Equals(
                    components.GetValue(loadout.MountComponents[i]).GetType().GetProperty("Size")
                        .GetValue(components.GetValue(loadout.MountComponents[i]))));
            object size = points.GetValue(mount).GetType().GetProperty("Size").GetValue(points.GetValue(mount));
            float expectedWidth = size.ToString() == "Small" ? 24f : size.ToString() == "Medium" ? 36f : 50f;
            var canvas = new GameObject("HUD canvas", typeof(RectTransform), typeof(Canvas));
            var owner = new GameObject("Paperdoll", typeof(RectTransform));
            try
            {
                owner.transform.SetParent(canvas.transform, false);
                Type type = Type.GetType("PewPewPew.Ships.ShipHudPaperdoll, Assembly-CSharp");
                Component paperdoll = owner.AddComponent(type);
                type.GetMethod("Initialize").Invoke(paperdoll, new object[] { loadout, null, panelSize, 0.65f, markerScale });
                var panel = (RectTransform)type.GetProperty("Rect").GetValue(paperdoll);
                Assert.AreEqual(panelSize, panel.rect.width, 0.001f);
                var buttons = (Array)type.GetField("m_Buttons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                var icon = (RectTransform)((Component)buttons.GetValue(mount)).transform;
                Assert.AreEqual(expectedWidth * markerScale, icon.rect.width, 0.001f);

                loadout.MountComponents[mount] = -1;
                type.GetMethod("Initialize").Invoke(paperdoll, new object[] { loadout, null, panelSize, 0.65f, markerScale });
                var grids = (Array)type.GetField("m_CargoCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                var cells = (Array)grids.GetValue(mount);
                var grid = (RectTransform)((Component)cells.GetValue(0)).transform.parent;
                Assert.AreEqual(expectedWidth * markerScale, grid.rect.width, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Thrust Booster.asset", false)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Power Storage.asset", false)]
        [TestCase("Assets/ShipComponents/SubSystems/Basic SubSystem Thrust Booster.asset", true)]
        public void Paperdoll_ReusesSubsystemGaugeAndKeepsMountClickable(string assetPath, bool missingGauge)
        {
            Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
            object catalog = catalogType.GetProperty("Instance").GetValue(null);
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            Assert.IsNotNull(definition);
            var components = (Array)catalogType.GetProperty("Components").GetValue(catalog);
            int componentIndex = Enumerable.Range(0, components.Length).First(i => ReferenceEquals(components.GetValue(i), definition));
            var hulls = (Array)catalogType.GetProperty("Hulls").GetValue(catalog);
            object hull = hulls.GetValue(0);
            object hullPoints = hull.GetType().GetProperty("HullPoints").GetValue(hull);
            var points = (Array)hullPoints.GetType().GetProperty("Points").GetValue(hullPoints);
            int mount = Enumerable.Range(0, points.Length).First(i =>
                (bool)points.GetValue(i).GetType().GetMethod("AcceptsComponent").Invoke(points.GetValue(i), new object[] { definition }));
            var loadout = (ShipLoadout)catalogType.GetMethod("DefaultLoadout").Invoke(catalog, new object[] { 0 });
            loadout.MountComponents[mount] = componentIndex;
            var canvas = new GameObject("HUD canvas", typeof(RectTransform), typeof(Canvas));
            var owner = new GameObject("Paperdoll", typeof(RectTransform));
            ScriptableObject clone = null;
            try
            {
                if (missingGauge)
                {
                    clone = UnityEngine.Object.Instantiate(definition);
                    clone.GetType().GetField("m_Gauge", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clone, null);
                    components.SetValue(clone, componentIndex);
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, $"{clone.name} has no HUD gauge; using its component artwork.");
                }

                owner.transform.SetParent(canvas.transform, false);
                Type type = Type.GetType("PewPewPew.Ships.ShipHudPaperdoll, Assembly-CSharp");
                Component paperdoll = owner.AddComponent(type);
                int clicked = -1;
                type.GetMethod("Initialize").Invoke(paperdoll,
                    new object[] { loadout, new Action<int>(index => clicked = index), 240f, 0.65f, 1f });
                var buttons = (Array)type.GetField("m_Buttons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                var button = (Component)buttons.GetValue(mount);
                Type graphicType = button.GetType().Assembly.GetType("UnityEngine.UI.Graphic");
                foreach (Component graphic in button.GetComponentsInChildren<Component>(true).Where(graphicType.IsInstanceOfType))
                    Assert.AreEqual(graphic.gameObject == button.gameObject,
                        (bool)graphicType.GetProperty("raycastTarget").GetValue(graphic));
                var onClick = (UnityEvent)button.GetType().GetProperty("onClick").GetValue(button);
                onClick.Invoke();
                Assert.AreEqual(mount, clicked);

                var gauge = (Component)type.GetMethod("GetMountGauge").Invoke(paperdoll, new object[] { mount });
                if (missingGauge)
                {
                    Assert.IsNull(gauge);
                    var icon = (Component)((Array)type.GetField("m_Icons", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(paperdoll)).GetValue(mount);
                    Assert.AreNotEqual(Color.clear, (Color)icon.GetType().GetProperty("color").GetValue(icon));
                    return;
                }

                Assert.IsNotNull(gauge);
                var prefab = (Component)definition.GetType().GetProperty("Gauge").GetValue(definition);
                var gaugeRect = (RectTransform)gauge.transform;
                Assert.AreEqual(((RectTransform)prefab.transform).sizeDelta, gaugeRect.sizeDelta);
                Assert.AreEqual(((RectTransform)button.transform).rect.size.x,
                    gaugeRect.rect.width * gaugeRect.localScale.x, 0.001f);
                Assert.IsFalse(owner.GetComponentsInChildren<Transform>(true).Any(child => child.name == "Progress track"));

                var rootGraphic = gauge.GetComponent("Image") ?? gauge.GetComponent("RawImage");
                Assert.IsNotNull(rootGraphic);
                var colorProperty = rootGraphic.GetType().GetProperty("color");
                Color original = (Color)colorProperty.GetValue(rootGraphic);
                Type subsystemType = Type.GetType("PewPewPew.Ships.SubSystem, Assembly-CSharp");
                object subsystem = Activator.CreateInstance(subsystemType, definition, null, null);
                subsystemType.GetProperty("Gauge").SetValue(subsystem, gauge);
                MethodInfo setHud = subsystemType.GetMethod("SetHud");
                Type stateType = setHud.GetParameters()[1].ParameterType;
                setHud.Invoke(subsystem, new[] { (object)0.5f, Enum.Parse(stateType, "Active") });
                subsystemType.GetMethod("Render").Invoke(subsystem, null);
                var fill = (Component)gauge.GetType().GetField("m_Fill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gauge);
                Assert.IsNotNull(fill);
                Assert.AreEqual(0.5f, (float)fill.GetType().GetProperty("fillAmount").GetValue(fill), 0.001f);

                setHud.Invoke(subsystem, new[] { (object)0f, Enum.Parse(stateType, "Off") });
                subsystemType.GetMethod("Render").Invoke(subsystem, null);
                type.GetMethod("SetMountState").Invoke(paperdoll, new object[] { mount, false, false });
                Color dimmed = (Color)colorProperty.GetValue(rootGraphic);
                Assert.AreEqual(new Color(0.4f, 0.4f, 0.4f, original.a), dimmed);
                Assert.IsTrue((bool)button.GetType().GetProperty("interactable").GetValue(button));
                type.GetMethod("SetMountState").Invoke(paperdoll, new object[] { mount, false, true });
                Assert.IsFalse((bool)button.GetType().GetProperty("interactable").GetValue(button));
                type.GetMethod("SetMountState").Invoke(paperdoll, new object[] { mount, true, false });
                Assert.AreEqual(original, (Color)colorProperty.GetValue(rootGraphic));
            }
            finally
            {
                components.SetValue(definition, componentIndex);
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Paperdoll_RendersHullAndBindsInstalledMounts()
        {
            Type catalogType = Type.GetType("PewPewPew.Ships.ShipCatalog, Assembly-CSharp");
            object catalog = catalogType.GetProperty("Instance").GetValue(null);
            var loadout = (ShipLoadout)catalogType.GetMethod("DefaultLoadout").Invoke(catalog, new object[] { 0 });
            var canvas = new GameObject("HUD canvas", typeof(RectTransform), typeof(Canvas));
            var owner = new GameObject("Paperdoll", typeof(RectTransform));
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                owner.transform.SetParent(canvas.transform, false);
                Type type = Type.GetType("PewPewPew.Ships.ShipHudPaperdoll, Assembly-CSharp");
                Component paperdoll = owner.AddComponent(type);
                int clicked = -1;
                type.GetMethod("Initialize").Invoke(paperdoll,
                    new object[] { loadout, new Action<int>(mount => clicked = mount), 240f, 0.65f, 1f });
                object hullImage = type.GetField("m_HullImage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                var imageRect = (RectTransform)((Component)hullImage).transform;
                var panelRect = (RectTransform)type.GetProperty("Rect").GetValue(paperdoll);
                var imageCorners = new Vector3[4];
                var panelCorners = new Vector3[4];
                imageRect.GetWorldCorners(imageCorners);
                panelRect.GetWorldCorners(panelCorners);
                Assert.AreEqual(panelCorners[0].x, imageCorners[0].x, 0.001f);
                Assert.AreEqual(panelCorners[0].y, imageCorners[0].y, 0.001f);
                Color hullTint = (Color)hullImage.GetType().GetProperty("color").GetValue(hullImage);
                Assert.AreEqual(0.35f, hullTint.r, 0.001f);
                Assert.IsFalse((bool)hullImage.GetType().GetProperty("raycastTarget").GetValue(hullImage));
                var buttons = (Array)type.GetField("m_Buttons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                var icons = (Array)type.GetField("m_Icons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                for (int i = 0; i < loadout.MountComponents.Length; i++)
                    Assert.AreEqual(loadout.MountComponents[i] >= 0, buttons.GetValue(i) != null);

                int first = Array.FindIndex(loadout.MountComponents, index => index >= 0);
                Assert.GreaterOrEqual(first, 0);
                object button = buttons.GetValue(first);
                var onClick = (UnityEvent)button.GetType().GetProperty("onClick").GetValue(button);
                onClick.Invoke();
                Assert.AreEqual(first, clicked);
                type.GetMethod("SetMountState").Invoke(paperdoll, new object[] { first, false, false });
                object icon = icons.GetValue(first);
                Color colour = (Color)icon.GetType().GetProperty("color").GetValue(icon);
                Assert.AreEqual(0.4f, colour.r, 0.001f);
                Assert.AreEqual(0.4f, colour.g, 0.001f);
                Assert.AreEqual(0.4f, colour.b, 0.001f);

                var texture = (RenderTexture)type.GetField("m_Texture", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(paperdoll);
                Assert.IsTrue(texture.IsCreated());
                RenderTexture.active = texture;
                pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                pixels.Apply();
                Assert.IsTrue(pixels.GetPixels32().Any(pixel => pixel.a > 0), "The hull render must not be blank.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }
    }

    public class HudGaugeTests
    {
        [TestCase(1f, 1f)]
        [TestCase(8f, 4f)]
        public void PlaceIcon_ScalesToBarHeightWithoutChangingSize(float width, float height)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/HUD/HudArmourBar.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var gauge = instance.GetComponent(Type.GetType("PewPewPew.Ships.HudGauge, Assembly-CSharp"));
                var bar = (RectTransform)instance.transform;
                var icon = (RectTransform)instance.transform.Find("Icon");
                icon.sizeDelta = new Vector2(width, height);
                bar.sizeDelta = new Vector2(240f, 16f);

                Assert.IsTrue((bool)gauge.GetType().GetMethod("PlaceIcon").Invoke(gauge, new object[] { 4f }));
                Assert.AreEqual(new Vector2(width, height), icon.sizeDelta);
                Assert.AreEqual(new Vector3(16f / width, 16f / height, 1f), icon.localScale);
                Assert.AreEqual(new Vector2(-4f, 0f), icon.anchoredPosition);

                bar.sizeDelta = new Vector2(240f, 24f);
                Assert.IsTrue((bool)gauge.GetType().GetMethod("PlaceIcon").Invoke(gauge, new object[] { 4f }));
                Assert.AreEqual(new Vector3(24f / width, 24f / height, 1f), icon.localScale);
                Assert.AreEqual(new Vector2(width, height), icon.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PlaceIcon_RejectsZeroSizedIcon()
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/HUD/HudArmourBar.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var gauge = instance.GetComponent(Type.GetType("PewPewPew.Ships.HudGauge, Assembly-CSharp"));
                var icon = (RectTransform)instance.transform.Find("Icon");
                icon.sizeDelta = new Vector2(0f, 1f);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    "HUD gauge icon must have a positive width and height.");
                Assert.IsFalse((bool)gauge.GetType().GetMethod("PlaceIcon").Invoke(gauge, new object[] { 4f }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }

    public class RadarMathTests
    {
        [Test]
        public void RadarPower_DrawsThreePerSecondAndStopsOnEmpty()
        {
            var power = new PowerBank(3f);
            var activation = new Activation(ActivationMode.Toggle, 0f, 0f, 3f);
            activation.Press();
            activation.Step(0.5f, power);
            Assert.IsTrue(activation.IsActive);
            Assert.AreEqual(1.5f, power.Stored);
            activation.Step(0.5f, power);
            Assert.IsTrue(activation.IsActive);
            Assert.AreEqual(0f, power.Stored);
            activation.Step(0.1f, power);
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void Pulse_DelaysByDistanceAndDoesNotSkipLongUpdates()
        {
            Assert.IsFalse(RadarMath.PulseCrosses(-0.01d, 0.4d, 100f, 100f, 2f));
            Assert.IsTrue(RadarMath.PulseCrosses(0.9d, 1.1d, 100f, 100f, 2f));
            Assert.IsFalse(RadarMath.PulseCrosses(1.1d, 1.2d, 100f, 100f, 2f));
            Assert.IsTrue(RadarMath.PulseCrosses(1.2d, 4d, 100f, 100f, 2f));
        }

        [Test]
        public void Sweep_WrapsAndCoversSkippedAngles()
        {
            Assert.IsTrue(RadarMath.SweepCrosses(3.9d, 4.2d, 5f, 180f, 90f));
            Assert.IsTrue(RadarMath.SweepCrosses(0d, 10d, 170f, 180f, 90f));
            Assert.IsFalse(RadarMath.SweepCrosses(0.1d, 0.2d, 90f, 180f, 90f));
        }

        [Test]
        public void Cone_ReflectsAtEdgesAndRejectsOutsideBearings()
        {
            Assert.IsTrue(RadarMath.SweepCrosses(0.9d, 1.1d, 0f, 30f, 30f));
            Assert.IsTrue(RadarMath.SweepCrosses(2.9d, 3.1d, 0f, 30f, 30f));
            Assert.IsFalse(RadarMath.SweepCrosses(0d, 100d, 31f, 30f, 30f));
            Assert.AreEqual(30f, RadarMath.SweepAngle(2d, 30f, 30f));
            Assert.AreEqual(0f, RadarMath.SweepAngle(3d, 30f, 30f));
        }

        [Test]
        public void Fade_ExpiresAndClamps()
        {
            Assert.AreEqual(1f, RadarMath.Fade(0d, 2f));
            Assert.AreEqual(0.5f, RadarMath.Fade(1d, 2f));
            Assert.AreEqual(0f, RadarMath.Fade(3d, 2f));
            Assert.AreEqual(0f, RadarMath.Fade(0d, 0f));
        }
    }

    public class MinimapEmitterTests
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject m_RootObject;
        private GameObject m_MountA;
        private GameObject m_MountB;
        private Component m_Root;
        private ScriptableObject m_EmitterA;
        private ScriptableObject m_EmitterB;

        [SetUp]
        public void SetUp()
        {
            Type rootType = GameType("PewPewPew.Presentation.MinimapRoot");
            Type emitterType = GameType("PewPewPew.Presentation.RadarEmitterDef");
            m_RootObject = new GameObject("Minimap test", typeof(RectTransform));
            m_Root = m_RootObject.AddComponent(rootType);
            m_MountA = new GameObject("First mount");
            m_MountB = new GameObject("Second mount");
            m_EmitterA = ScriptableObject.CreateInstance(emitterType);
            m_EmitterB = ScriptableObject.CreateInstance(emitterType);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_RootObject);
            UnityEngine.Object.DestroyImmediate(m_MountA);
            UnityEngine.Object.DestroyImmediate(m_MountB);
            UnityEngine.Object.DestroyImmediate(m_EmitterA);
            UnityEngine.Object.DestroyImmediate(m_EmitterB);
        }

        [Test]
        public void LateUpdate_TracksActiveSubsystemsAndDefaultEmitter()
        {
            var shipObject = new GameObject("Local radar ship");
            Type subsystemDefType = GameType("PewPewPew.Ships.SubSystemDef");
            var defA = ScriptableObject.CreateInstance(subsystemDefType);
            var defB = ScriptableObject.CreateInstance(subsystemDefType);
            Component ship = null;
            try
            {
                Component identity = shipObject.AddComponent(FindType("Mirror.NetworkIdentity"));
                identity.GetType().GetProperty("isClient").SetValue(identity, true);
                identity.GetType().GetProperty("isOwned").SetValue(identity, true);
                ship = shipObject.AddComponent(GameType("PewPewPew.Ships.Ship"));
                ship.GetType().GetProperty("netIdentity").SetValue(ship, identity);
                ship.GetType().BaseType.GetMethod("OnEnable", InstanceFields).Invoke(ship, null);
                Set(defA, "m_Radar", m_EmitterA);
                Set(defB, "m_Radar", m_EmitterB);
                Set(m_EmitterA, "m_Range", 300f);
                Set(m_EmitterB, "m_Range", 900f);
                Type systemType = GameType("PewPewPew.Ships.SubSystem");
                object systemA = Activator.CreateInstance(systemType, defA, m_MountA.transform, shipObject.transform);
                object systemB = Activator.CreateInstance(systemType, defB, m_MountB.transform, shipObject.transform);
                object unmounted = Activator.CreateInstance(systemType, defB, null, null);
                MethodInfo setHud = systemType.GetMethod("SetHud");
                setHud.Invoke(systemA, new object[] { 1f, HudState.Active });
                setHud.Invoke(systemB, new object[] { 1f, HudState.Active });
                setHud.Invoke(unmounted, new object[] { 1f, HudState.Active });
                Assert.IsFalse((bool)systemType.GetProperty("IsRadarActive").GetValue(unmounted));
                var systems = (IList)Get(ship, "m_SubSystems");
                systems.Add(systemA);
                systems.Add(systemB);
                systems.Add(unmounted);
                if (Get(m_Root, "m_Display") == null) Call(m_Root, "Awake");
                Set(m_Root, "m_Activation", Enum.ToObject(GameType("PewPewPew.Presentation.MinimapActivation"), 1));
                Call(m_Root, "LateUpdate");
                Assert.AreEqual(900f, Get(m_Root, "m_DisplayRange"));
                Assert.IsTrue(((GameObject)Get(m_Root, "m_Display")).activeSelf);

                int redraws = 0;
                var markers = (Component)Get(m_Root, "m_Markers");
                markers.GetType().GetMethod("RegisterDirtyVerticesCallback")
                    .Invoke(markers, new object[] { new UnityAction(() => redraws++) });
                Set(m_Root, "m_RecordContact", null);
                Call(m_Root, "LateUpdate");
                Assert.Greater(redraws, 0, "Display redraws even when no emitter samples again.");
                Assert.IsNotNull(Get(m_Root, "m_RecordContact"));

                setHud.Invoke(systemA, new object[] { 0f, HudState.Off });
                Call(m_Root, "LateUpdate");
                Assert.AreEqual(900f, Get(m_Root, "m_DisplayRange"));

                setHud.Invoke(systemA, new object[] { 1f, HudState.Active });
                setHud.Invoke(systemB, new object[] { 0f, HudState.Off });
                Call(m_Root, "LateUpdate");
                Assert.AreEqual(300f, Get(m_Root, "m_DisplayRange"));
                Set(m_EmitterA, "m_Range", 450f);
                Call(m_Root, "LateUpdate");
                Assert.AreEqual(450f, Get(m_Root, "m_DisplayRange"));

                setHud.Invoke(systemA, new object[] { 0f, HudState.Off });
                Call(m_Root, "LateUpdate");
                Assert.IsFalse(((GameObject)Get(m_Root, "m_Display")).activeSelf);
                Set(m_Root, "m_DefaultEmitter", m_EmitterB);
                Set(m_Root, "m_Activation", Enum.ToObject(GameType("PewPewPew.Presentation.MinimapActivation"), 0));
                Call(m_Root, "LateUpdate");
                Assert.AreEqual(900f, Get(m_Root, "m_DisplayRange"));
                Assert.IsTrue(((GameObject)Get(m_Root, "m_Display")).activeSelf);
            }
            finally
            {
                if (ship != null) ship.GetType().BaseType.GetMethod("OnDisable", InstanceFields).Invoke(ship, null);
                UnityEngine.Object.DestroyImmediate(shipObject);
                UnityEngine.Object.DestroyImmediate(defA);
                UnityEngine.Object.DestroyImmediate(defB);
            }
        }

        [Test]
        public void Scan_UsesEachMountRangeAndCone()
        {
            Set(m_EmitterA, "m_Range", 100f);
            Set(m_EmitterA, "m_HalfAngle", 45f);
            m_MountA.transform.position = new Vector3(50f, 0f, 0f);
            AddEmitter(m_EmitterA, m_MountA.transform);
            GameObject inRange = MakeClientAsteroid(new Vector3(50f, 100f, 0f));
            GameObject outsideRange = MakeClientAsteroid(new Vector3(50f, 101f, 0f));
            GameObject outsideCone = MakeClientAsteroid(new Vector3(140f, 0f, 0f));
            var shipObject = new GameObject("Local ship");
            var graphicObject = new GameObject("Radar contacts", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                Sample(Emitters()[0], 0d);
                var contacts = (IDictionary)Get(m_Root, "m_Contacts");
                Assert.IsTrue(contacts.Contains(inRange.GetComponent(GameType("PewPewPew.World.Asteroid"))));
                Assert.IsFalse(contacts.Contains(outsideRange.GetComponent(GameType("PewPewPew.World.Asteroid"))));
                Assert.IsFalse(contacts.Contains(outsideCone.GetComponent(GameType("PewPewPew.World.Asteroid"))));
                Assert.IsTrue((bool)Call(m_Root, "IsWithinScanRange", new Vector2(50f, 100f)));
                Assert.IsFalse((bool)Call(m_Root, "IsWithinScanRange", new Vector2(50f, 101f)));
                object contact = contacts[inRange.GetComponent(GameType("PewPewPew.World.Asteroid"))];
                inRange.transform.position = new Vector3(50f, 80f, 0f);
                Sample(Emitters()[0], 0.1d);
                Assert.AreEqual(new Vector2(50f, 100f), Get(contact, "Position"));
                Sample(Emitters()[0], 0.5d);
                Assert.AreEqual(new Vector2(50f, 80f), Get(contact, "Position"));
                inRange.transform.position = new Vector3(50f, 100f, 0f);
                Sample(Emitters()[0], 1d);

                shipObject.AddComponent(FindType("Mirror.NetworkIdentity"));
                Set(m_Root, "m_Ship", shipObject.AddComponent(GameType("PewPewPew.Ships.Ship")));
                Set(m_Root, "m_ShowScan", false);
                Set(m_Root, "m_DisplayRange", 100f);
                Component graphic = graphicObject.AddComponent(GameType("PewPewPew.Presentation.MinimapGraphic"));
                Type layerType = graphic.GetType().GetNestedType("Layer", BindingFlags.NonPublic);
                Call(graphic, "Initialize", m_Root, Enum.Parse(layerType, "Contacts"));
                Type meshType = FindType("UnityEngine.UI.VertexHelper");
                MethodInfo populate = graphic.GetType().GetMethod("OnPopulateMesh", InstanceFields, null, new[] { meshType }, null);
                using (var mesh = (IDisposable)Activator.CreateInstance(meshType))
                {
                    populate.Invoke(graphic, new object[] { mesh });
                    Assert.AreEqual(7, mesh.GetType().GetProperty("currentVertCount").GetValue(mesh));
                    m_MountA.transform.position = new Vector3(500f, 0f, 0f);
                    populate.Invoke(graphic, new object[] { mesh });
                    Assert.AreEqual(3, mesh.GetType().GetProperty("currentVertCount").GetValue(mesh));
                }
                Assert.IsFalse((bool)Call(m_Root, "IsWithinScanRange", new Vector2(50f, 100f)));
                Set(m_EmitterB, "m_Range", 20f);
                m_MountB.transform.position = new Vector3(50f, 80f, 0f);
                AddEmitter(m_EmitterB, m_MountB.transform);
                Assert.IsTrue((bool)Call(m_Root, "IsWithinScanRange", new Vector2(50f, 100f)));
                Assert.IsFalse((bool)Call(m_Root, "IsWithinScanRange", new Vector2(50f, 101f)));
                m_MountB.transform.position = new Vector3(140f, -10f, 0f);
                Sample(Emitters()[1], 0d);
                Assert.IsTrue(contacts.Contains(outsideCone.GetComponent(GameType("PewPewPew.World.Asteroid"))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graphicObject);
                UnityEngine.Object.DestroyImmediate(shipObject);
                DisableAsteroid(inRange);
                DisableAsteroid(outsideRange);
                DisableAsteroid(outsideCone);
                UnityEngine.Object.DestroyImmediate(inRange);
                UnityEngine.Object.DestroyImmediate(outsideRange);
                UnityEngine.Object.DestroyImmediate(outsideCone);
            }
        }

        [Test]
        public void PulseEmitter_AddsContactOnlyWhenPulseReachesIt()
        {
            Type scanMode = GameType("PewPewPew.Presentation.RadarScanMode");
            Set(m_EmitterA, "m_Mode", Enum.Parse(scanMode, "CircularPulse"));
            Set(m_EmitterA, "m_Range", 200f);
            Set(m_EmitterA, "m_PulseSpeed", 100f);
            Set(m_EmitterA, "m_SampleRate", 10f);
            AddEmitter(m_EmitterA, m_MountA.transform);
            GameObject target = MakeClientAsteroid(new Vector3(0f, 100f, 0f));
            try
            {
                var contacts = (IDictionary)Get(m_Root, "m_Contacts");
                Sample(Emitters()[0], 0.5d);
                Assert.AreEqual(0, contacts.Count);
                Sample(Emitters()[0], 1d);
                Assert.IsTrue(contacts.Contains(target.GetComponent(GameType("PewPewPew.World.Asteroid"))));
            }
            finally
            {
                DisableAsteroid(target);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void TreasureDetection_ColoursScannedAsteroidsAndSurvivesOrdinaryScans()
        {
            Set(m_EmitterB, "m_DetectTreasure", true);
            Set(m_Root, "m_FadeContacts", false);
            AddEmitter(m_EmitterA, m_MountA.transform);
            AddEmitter(m_EmitterB, m_MountB.transform);
            GameObject target = MakeClientAsteroid(new Vector3(0f, 10f, 0f));
            Component asteroid = target.GetComponent(GameType("PewPewPew.World.Asteroid"));
            var deposits = (IList)Get(asteroid, "m_Treasure");
            Type depositType = GameType("PewPewPew.World.TreasureDeposit");
            object gold = Activator.CreateInstance(depositType);
            object red = Activator.CreateInstance(depositType);
            depositType.GetField("Material").SetValue(gold, TreasureMaterial.Gold);
            depositType.GetField("Material").SetValue(red, TreasureMaterial.RedCrystal);
            deposits.Add(gold);
            deposits.Add(red);
            try
            {
                double now = Time.timeAsDouble;
                Sample(Emitters()[0], now);
                var contacts = (IDictionary)Get(m_Root, "m_Contacts");
                object contact = contacts[asteroid];
                Assert.IsNull(Get(contact, "Treasure"));
                Color ordinary = (Color)Call(m_Root, "ContactColor", contact);

                Sample(Emitters()[1], now);
                Assert.AreEqual(TreasureMaterial.Gold, Get(contact, "Treasure"));
                Color detected = (Color)Call(m_Root, "ContactColor", contact);
                Assert.Greater(detected.r, detected.g);
                Assert.AreNotEqual(ordinary, detected);

                Sample(Emitters()[0], now + 0.5d);
                Assert.AreEqual(TreasureMaterial.Gold, Get(contact, "Treasure"));
                deposits.RemoveAt(0);
                Sample(Emitters()[1], now + 0.5d);
                Assert.AreEqual(TreasureMaterial.RedCrystal, Get(contact, "Treasure"));
                detected = (Color)Call(m_Root, "ContactColor", contact);
                Assert.Greater(detected.r, detected.g);
                Assert.Less(detected.g, 0.1f);

                Set(contact, "TreasureLastSeen", Time.timeAsDouble - 5d);
                Assert.AreEqual(ordinary, (Color)Call(m_Root, "ContactColor", contact));
                deposits.Clear();
                Sample(Emitters()[1], now + 1d);
                Assert.IsNull(Get(contact, "Treasure"));
                Assert.AreEqual(ordinary, (Color)Call(m_Root, "ContactColor", contact));
            }
            finally
            {
                DisableAsteroid(target);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private GameObject MakeClientAsteroid(Vector3 position)
        {
            var target = new GameObject("Radar contact");
            target.transform.position = position;
            Component identity = target.AddComponent(FindType("Mirror.NetworkIdentity"));
            Component asteroid = target.AddComponent(GameType("PewPewPew.World.Asteroid"));
            identity.GetType().GetProperty("isClient").SetValue(identity, true);
            asteroid.GetType().GetProperty("netIdentity").SetValue(asteroid, identity);
            asteroid.GetType().BaseType.GetMethod("OnEnable", InstanceFields).Invoke(asteroid, null);
            return target;
        }

        private static void DisableAsteroid(GameObject target)
        {
            Component asteroid = target.GetComponent(GameType("PewPewPew.World.Asteroid"));
            asteroid.GetType().BaseType.GetMethod("OnDisable", InstanceFields).Invoke(asteroid, null);
        }

        private void AddEmitter(ScriptableObject def, Transform mount) => Call(m_Root, "AddEmitter", def, def, mount, 0d);
        private IList Emitters() => (IList)Get(m_Root, "m_Emitters");
        private void Sample(object emitter, double now)
        {
            Type callbackType = typeof(Action<,,>).MakeGenericType(GameType("PewPewPew.World.SpaceObject"), typeof(double), typeof(bool));
            Delegate callback = Delegate.CreateDelegate(callbackType, m_Root, m_Root.GetType().GetMethod("RecordContact", InstanceFields));
            Call(emitter, "Sample", now, null, callback);
        }
        private static Type GameType(string name) => FindType(name);
        private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null);
        private static object Get(object obj, string name) => obj.GetType().GetField(name, InstanceFields).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, InstanceFields).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) =>
            obj.GetType().GetMethod(name, InstanceFields).Invoke(obj, args);
    }

    public class GravityMathTests
    {
        [Test]
        public void CircularSpeed_CentripetalAccelerationMatchesGravity()
        {
            const float mass = 5000f, g = 1.5f, softening = 2f, distance = 120f;
            float speed = GravityMath.CircularSpeed(mass, g, softening, distance);
            float gravity = GravityMath.Force(new Vector2(distance, 0f), mass, 1f, g, softening, 1000f).magnitude;
            Assert.AreEqual(gravity, speed * speed / distance, 1e-4f);
            Assert.AreEqual(0f, GravityMath.CircularSpeed(mass, g, softening, 0f));
        }

        [Test]
        public void OrbitVelocity_ZeroEccentricityIsCircular()
        {
            Vector2 velocity = GravityMath.OrbitVelocity(4f, 0f, 1.3f);
            Assert.AreEqual(0f, velocity.x, 1e-6f);
            Assert.AreEqual(4f, velocity.y, 1e-6f);
        }

        [Test]
        public void OrbitVelocity_SatisfiesVisViva()
        {
            // With GM/r = circular^2, vis-viva gives v^2 = circular^2 * (2 - r/a) where r/a = (1 - e^2) / (1 + e cos nu).
            const float circular = 3f, eccentricity = 0.6f, anomaly = 2.1f;
            Vector2 velocity = GravityMath.OrbitVelocity(circular, eccentricity, anomaly);
            float rOverA = (1f - eccentricity * eccentricity) / (1f + eccentricity * Mathf.Cos(anomaly));
            Assert.AreEqual(circular * circular * (2f - rOverA), velocity.sqrMagnitude, 1e-4f);
            Assert.Greater(velocity.x, 0f); // Heading out from periapsis toward apoapsis.
        }

        [Test]
        public void OrbitVelocity_PeriapsisIsFasterThanApoapsis()
        {
            float periapsis = GravityMath.OrbitVelocity(1f, 0.5f, 0f).y;
            float apoapsis = GravityMath.OrbitVelocity(1f, 0.5f, Mathf.PI).y;
            Assert.AreEqual(Mathf.Sqrt(1.5f), periapsis, 1e-5f);
            Assert.AreEqual(Mathf.Sqrt(0.5f), apoapsis, 1e-5f);
        }

        [Test]
        public void Force_FollowsInverseSquare()
        {
            Vector2 near = GravityMath.Force(new Vector2(10, 0), 100, 1, 1, 0, 1000);
            Vector2 far = GravityMath.Force(new Vector2(20, 0), 100, 1, 1, 0, 1000);
            Assert.AreEqual(4f, near.x / far.x, 1e-4f);
        }

        [Test]
        public void Force_PointsTowardSource()
        {
            Vector2 force = GravityMath.Force(new Vector2(0, -5), 10, 1, 1, 0, 100);
            Assert.Less(force.y, 0f);
            Assert.AreEqual(0f, force.x, 1e-6f);
        }

        [Test]
        public void Force_IsZeroBeyondMaxDistanceAndAtZeroDistance()
        {
            Assert.AreEqual(Vector2.zero, GravityMath.Force(new Vector2(50, 0), 10, 1, 1, 0, 40));
            Assert.AreEqual(Vector2.zero, GravityMath.Force(Vector2.zero, 10, 1, 1, 1, 40));
        }

        [Test]
        public void EdgeForce_ZeroInsideAndInwardOutside()
        {
            Assert.AreEqual(Vector2.zero, GravityMath.EdgeForce(new Vector2(90, 0), 100, 5, 1));
            Vector2 force = GravityMath.EdgeForce(new Vector2(150, 0), 100, 5, 2);
            Assert.AreEqual(-2.5f, force.x, 1e-4f); // 5 * 2 * 0.5^2
        }

        [Test]
        public void Drag_OpposesVelocityAndScalesWithSpeedSquared()
        {
            Vector2 slow = GravityMath.Drag(new Vector2(2, 0), 0.5f);
            Vector2 fast = GravityMath.Drag(new Vector2(4, 0), 0.5f);
            Assert.AreEqual(-2f, slow.x, 1e-5f);
            Assert.AreEqual(4f, fast.x / slow.x, 1e-5f);
        }

        [TestCase(0f, 1f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 0.5f)]
        [TestCase(10f, 0.1f)]
        public void FrictionScale_FullThroughThresholdThenFallsOffInversely(float acceleration, float expected)
        {
            Assert.AreEqual(expected, GravityMath.FrictionScale(new Vector2(acceleration, 0f), 1f), 1e-6f);
        }

        [Test]
        public void FrictionScale_UsesNetMagnitudeNotDirectionOrIndividualPulls()
        {
            Assert.AreEqual(0.2f, GravityMath.FrictionScale(new Vector2(-3f, 4f), 1f), 1e-6f);
            Vector2 cancelled = new Vector2(10f, 0f) + new Vector2(-10f, 0f);
            Assert.AreEqual(1f, GravityMath.FrictionScale(cancelled, 1f));
        }

        [TestCase(1f)]
        [TestCase(5f)]
        public void FrictionScale_SameGravityFieldIsIndependentOfBodyMass(float bodyMass)
        {
            Vector2 force = GravityMath.Force(new Vector2(10f, 0f), 200f, bodyMass, 1f, 0f, 1000f);
            Assert.AreEqual(0.5f, GravityMath.FrictionScale(force / bodyMass, 1f), 1e-6f);
        }

        [Test]
        public void FrictionScale_UsesConfiguredThresholdAndHandlesZeroThreshold()
        {
            Assert.AreEqual(0.25f, GravityMath.FrictionScale(new Vector2(4f, 0f), 1f));
            Assert.AreEqual(0.5f, GravityMath.FrictionScale(new Vector2(4f, 0f), 2f));
            Assert.AreEqual(1f, GravityMath.FrictionScale(Vector2.zero, 0f));
            Assert.AreEqual(0f, GravityMath.FrictionScale(Vector2.up, 0f));
        }

        [Test]
        public void Drag_GravityFalloffPreservesFlatSpaceBrakingAndReducesStrongFieldDrag()
        {
            Vector2 velocity = new Vector2(10f, 0f);
            Vector2 flat = GravityMath.Drag(velocity, 0.05f * GravityMath.FrictionScale(Vector2.zero, 1f));
            Vector2 strong = GravityMath.Drag(velocity, 0.05f * GravityMath.FrictionScale(new Vector2(10f, 0f), 1f));
            Assert.AreEqual(-5f, flat.x, 1e-6f);
            Assert.AreEqual(-0.5f, strong.x, 1e-6f);
            Assert.AreEqual(Vector2.zero, GravityMath.Drag(Vector2.zero, 0.05f));

            for (int step = 0; step < 10000; step++)
                velocity += GravityMath.Drag(velocity, 0.05f) * 0.02f;

            Assert.GreaterOrEqual(velocity.x, 0f);
            Assert.Less(velocity.magnitude, 0.1f);
        }
    }

    public class ShieldVisualTests
    {
        [TestCase(0, 90f)]
        [TestCase(1, 90f)]
        [TestCase(0, 180f)]
        [TestCase(1, 180f)]
        public void LatitudeFade_RendersBandAndRespondsToInspectorChanges(int meshType, float halfAngle)
        {
            Type visualType = Type.GetType("PewPewPew.Ships.ShieldVisual, Assembly-CSharp");
            Type definitionType = Type.GetType("PewPewPew.Ships.ShieldDef, Assembly-CSharp");
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Shield Meshes/Basic Shield Visual.prefab");
            var ship = new GameObject("Shield latitude test");
            var mount = new GameObject("Mount");
            var cameraObject = new GameObject("Shield latitude camera");
            var definition = ScriptableObject.CreateInstance(definitionType);
            var target = new RenderTexture(128, 128, 24);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var material = new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Shield Hex.mat"));
            try
            {
                mount.transform.SetParent(ship.transform, false);
                GameObject visualObject = UnityEngine.Object.Instantiate(prefab, ship.transform);
                visualObject.layer = 31;
                Component visual = visualObject.GetComponent(visualType);
                Renderer renderer = visualObject.GetComponent<Renderer>();
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetColor("_BaseColor", Color.white);
                renderer.sharedMaterial = material;
                SetField(definition, "m_HalfAngle", halfAngle);
                SetField(visual, "m_FadeStartProportion", 0.75f);
                SetField(visual, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), meshType));
                SetField(visual, "m_GeodesicResolution", 3);
                FieldInfo latitude = visualType.GetField("m_LatitudeFadeAngle", BindingFlags.Instance | BindingFlags.NonPublic);
                latitude.SetValue(visual, 30f);
                visualType.GetMethod("Fit").Invoke(visual,
                    new object[] { new Bounds(Vector3.zero, new Vector3(2f, 2f, 1f)), mount.transform, definition });
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0f, 0f, -5f);
                camera.orthographic = true;
                camera.orthographicSize = 1.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;

                RenderShield(camera, target, pixels);
                Assert.Less(pixels.GetPixel(64, 64).r, 0.02f, "The pole is transparent");
                Assert.Less(pixels.GetPixel(64, 100).r, 0.02f, "The interior beyond 30 degrees is transparent");
                Assert.Less(pixels.GetPixel(64, 106).r, 0.02f, "The fade reaches zero just beyond the selected latitude");
                Assert.Greater(pixels.GetPixel(64, 111).r, 0.03f, "The equatorial band remains visible");

                latitude.SetValue(visual, 60f);
                visualType.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                visualType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                RenderShield(camera, target, pixels);
                Assert.Greater(pixels.GetPixel(64, 100).r, 0.03f, "A larger latitude makes the band wider");
                Assert.Less(pixels.GetPixel(64, 64).r, 0.02f, "The pole stays transparent");
                camera.transform.position = new Vector3(0f, 0f, 5f);
                camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                RenderShield(camera, target, pixels);
                Assert.Greater(pixels.GetPixel(64, 100).r, 0.03f, "The opposite hemisphere has the same band");
                Assert.Less(pixels.GetPixel(64, 64).r, 0.02f, "The opposite pole is transparent");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ship);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void RenderShield(Camera camera, RenderTexture target, Texture2D pixels)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        [TestCase(0, 90f, 0f)]
        [TestCase(0, 45f, 53f)]
        [TestCase(0, 22.5f, -72f)]
        [TestCase(1, 90f, 0f)]
        [TestCase(1, 45f, 53f)]
        [TestCase(1, 22.5f, -72f)]
        [TestCase(1, 135f, 29f)]
        public void DirectionalMesh_MatchesCoverageAndAlphaOnAnEllipticalRotatedShip(int meshType, float halfAngle, float mountAngle)
        {
            Type visualType = Type.GetType("PewPewPew.Ships.ShieldVisual, Assembly-CSharp");
            Type definitionType = Type.GetType("PewPewPew.Ships.ShieldDef, Assembly-CSharp");
            Assert.IsNotNull(visualType);
            Assert.IsNotNull(definitionType);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Shield Meshes/Basic Shield Visual.prefab");
            Assert.IsNotNull(prefab);
            var ship = new GameObject("Shield visual test");
            var mount = new GameObject("Mount");
            ScriptableObject definition = ScriptableObject.CreateInstance(definitionType);
            try
            {
                SetField(definition, "m_HalfAngle", halfAngle);
                ship.transform.rotation = Quaternion.Euler(0f, 0f, 37f);
                mount.transform.SetParent(ship.transform, false);
                mount.transform.localRotation = Quaternion.Euler(0f, 0f, mountAngle);
                GameObject visualObject = UnityEngine.Object.Instantiate(prefab, ship.transform);
                Component visual = visualObject.GetComponent(visualType);
                SetField(visual, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), meshType));
                SetField(visual, "m_FadeStartProportion", 0.5f);
                SetField(visual, "m_MaxAlpha", 0.8f);
                SetField(visual, "m_EdgeAlpha", 0.2f);
                MeshFilter filter = visualObject.GetComponent<MeshFilter>();
                Mesh original = filter.sharedMesh;
                MethodInfo fit = visualType.GetMethod("Fit");
                var bounds = new Bounds(new Vector3(1f, -2f, 0f), new Vector3(12f, 4f, 1f));
                fit.Invoke(visual, new object[] { bounds, mount.transform, definition });
                Mesh mesh = filter.sharedMesh;
                Assert.AreNotSame(original, mesh);
                Vector2 centre = ship.transform.TransformPoint(bounds.center);
                Vector2 forward = mount.transform.up;
                float minimum = 180f;
                float maximum = -180f;
                Vector3[] vertices = mesh.vertices;
                Color[] colors = mesh.colors;
                Vector2[] uvs = mesh.uv;
                Assert.AreEqual(vertices.Length, colors.Length);
                Assert.AreEqual(vertices.Length, uvs.Length);
                bool sawFadeStart = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 offset = (Vector2)visualObject.transform.TransformPoint(vertices[i]) - centre;
                    if (offset.sqrMagnitude < 1e-8f) continue;
                    float angle = Vector2.SignedAngle(forward, offset);
                    minimum = Mathf.Min(minimum, angle);
                    maximum = Mathf.Max(maximum, angle);
                    Assert.LessOrEqual(Mathf.Abs(angle), halfAngle + 0.01f);
                    float expectedAlpha = Mathf.Lerp(0.8f, 0.2f,
                        Mathf.Clamp01((Mathf.Abs(angle) - halfAngle * 0.5f) / (halfAngle * 0.5f)));
                    Assert.That(colors[i].a, Is.EqualTo(expectedAlpha).Within(0.002f));
                    if (Mathf.Abs(Mathf.Abs(angle) - halfAngle * 0.5f) < 0.01f) sawFadeStart = true;
                }
                Assert.That(minimum, Is.EqualTo(-halfAngle).Within(0.01f));
                Assert.That(maximum, Is.EqualTo(halfAngle).Within(0.01f));
                Assert.IsTrue(sawFadeStart, "The mesh has vertices at the fade boundary");
                int[] triangles = mesh.triangles;
                int checkedTriangles = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-12f) continue;
                    Assert.Greater(Vector3.Dot(normal, a + b + c), 0f, "Faces must point outward");
                    if (meshType == 1)
                    {
                        float u0 = uvs[triangles[i]].x, u1 = uvs[triangles[i + 1]].x, u2 = uvs[triangles[i + 2]].x;
                        Assert.LessOrEqual(Mathf.Max(u0, u1, u2) - Mathf.Min(u0, u1, u2), 0.501f, "UV seam crosses a triangle");
                    }
                    checkedTriangles++;
                }
                Assert.Greater(checkedTriangles, 0);
                SetField(visual, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), 0));
                SetField(definition, "m_HalfAngle", 180f);
                fit.Invoke(visual, new object[] { bounds, mount.transform, definition });
                Assert.AreSame(original, filter.sharedMesh);
                Assert.IsTrue(mesh == null, "Replacing a generated mesh releases it");
                var properties = new MaterialPropertyBlock();
                visualObject.GetComponent<Renderer>().GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_ShieldAlpha"), Is.EqualTo(0.8f).Within(1e-6f));
                Assert.AreEqual(0f, properties.GetFloat("_UseVertexColors"));
                SetField(visual, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), 1));
                fit.Invoke(visual, new object[] { bounds, mount.transform, definition });
                Mesh fullGeodesic = filter.sharedMesh;
                Assert.AreNotSame(original, fullGeodesic);
                Assert.IsTrue(fullGeodesic.colors.All(color => Mathf.Abs(color.a - 0.8f) < 1e-6f));
                SetField(visual, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), 0));
                fit.Invoke(visual, new object[] { bounds, mount.transform, definition });
                Assert.IsTrue(fullGeodesic == null, "Replacing the full geodesic mesh releases it");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ship);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [TestCase(0, 6, 24)]
        [TestCase(1, 0, 2)]
        public void Resolution_IncreasesMeshDetail(int meshType, int low, int high)
        {
            Type generator = Type.GetType("PewPewPew.Ships.ShieldMeshGenerator, Assembly-CSharp");
            MethodInfo create = generator.GetMethod(meshType == 0 ? "CreateArcMesh" : "CreateGeodesicArcMesh");
            Mesh coarse = (Mesh)create.Invoke(null, new object[] { Vector2.up, Vector3.one, 90f, low, 67.5f, 1f, 0f });
            Mesh fine = (Mesh)create.Invoke(null, new object[] { Vector2.up, Vector3.one, 90f, high, 67.5f, 1f, 0f });
            try { Assert.Greater(fine.triangles.Length, coarse.triangles.Length); }
            finally { UnityEngine.Object.DestroyImmediate(coarse); UnityEngine.Object.DestroyImmediate(fine); }
        }

        [Test]
        public void GeodesicFullSphere_HasUniformAlphaAndOutwardFaces()
        {
            Type generator = Type.GetType("PewPewPew.Ships.ShieldMeshGenerator, Assembly-CSharp");
            Mesh mesh = (Mesh)generator.GetMethod("CreateGeodesicArcMesh").Invoke(null,
                new object[] { Vector2.up, Vector3.one, 180f, 2, 135f, 0.65f, 0f });
            try
            {
                Assert.AreEqual(20 * 16 * 3, mesh.triangles.Length);
                Assert.IsTrue(mesh.colors.All(color => Mathf.Abs(color.a - 0.65f) < 1e-6f));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(0.02f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(0.02f));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(1f).Within(0.02f));
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c), 0f);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ShieldVisual_ConstrainsFadeSettings()
        {
            Type type = Type.GetType("PewPewPew.Ships.ShieldVisual, Assembly-CSharp");
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Shield Meshes/Basic Shield Visual.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            Component visual = instance.GetComponent(type);
            try
            {
                SetField(visual, "m_FadeStartProportion", 1f);
                SetField(visual, "m_MaxAlpha", 0.3f);
                SetField(visual, "m_EdgeAlpha", 0.9f);
                type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                Assert.Less((float)type.GetField("m_FadeStartProportion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual), 1f);
                Assert.That((float)type.GetField("m_EdgeAlpha", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual), Is.EqualTo(0.3f).Within(1e-6f));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [TestCase("Basic Shield 45", 0.66f, 33.75f)]
        [TestCase("Basic Shield 180", 0.66f, 67.5f)]
        [TestCase("Basic Shield 45_Grid", 0.66f, 16f)]
        [TestCase("Basic Shield 45_Geo", 0.66f, 16f)]
        [TestCase("Basic Shield 360", 1f, 135f)]
        public void ShieldAssets_PreserveVisualSettings(string assetName, float alpha, float fadeStartAngle)
        {
            Type definitionType = Type.GetType("PewPewPew.Ships.ShieldDef, Assembly-CSharp");
            Type visualType = Type.GetType("PewPewPew.Ships.ShieldVisual, Assembly-CSharp");
            UnityEngine.Object definition = UnityEditor.AssetDatabase.LoadAssetAtPath(
                "Assets/ShipComponents/Shields/" + assetName + ".asset", definitionType);
            Assert.IsNotNull(definition);
            Component visual = (Component)definitionType.GetProperty("Visual").GetValue(definition);
            Assert.IsNotNull(visual);
            float halfAngle = (float)definitionType.GetProperty("HalfAngle").GetValue(definition);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            float actualAlpha = (float)visualType.GetField("m_MaxAlpha", flags).GetValue(visual);
            float proportion = (float)visualType.GetField("m_FadeStartProportion", flags).GetValue(visual);
            Assert.That(actualAlpha, Is.EqualTo(alpha).Within(1e-6f));
            Assert.That(halfAngle * proportion, Is.EqualTo(fadeStartAngle).Within(0.001f));
        }

        [Test]
        public void GeodesicMaximumResolution_ProducesValidIndexedMesh()
        {
            Type generator = Type.GetType("PewPewPew.Ships.ShieldMeshGenerator, Assembly-CSharp");
            Mesh mesh = (Mesh)generator.GetMethod("CreateGeodesicArcMesh").Invoke(null,
                new object[] { Vector2.up, new Vector3(3f, 1f, 1f), 179f, 5, 134.25f, 1f, 0f });
            try
            {
                Assert.Greater(mesh.vertexCount, 0);
                Assert.IsTrue(mesh.triangles.All(index => index >= 0 && index < mesh.vertexCount));
                if (mesh.vertexCount > ushort.MaxValue)
                    Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, mesh.indexFormat);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ShieldMaterial_HasDedicatedVertexColorShaderAndTexture()
        {
            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Shield Hex.mat");
            Assert.IsNotNull(material);
            Assert.AreEqual("PewPewPew/ShieldVertexTransparent", material.shader.name);
            Assert.IsNotNull(material.GetTexture("_BaseMap"));
            Assert.IsTrue(material.HasProperty("_BaseColor"));
            Assert.IsTrue(material.HasProperty("_UseVertexColors"));
            Assert.IsTrue(material.HasProperty("_ShieldAlpha"));
        }

        private static void SetField(UnityEngine.Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    public class DamageMathTests
    {
        [TestCase(90f)]
        [TestCase(45f)]
        [TestCase(22.5f)]
        public void ShieldCovers_BothEdgesAndRejectsOutside(float halfAngle)
        {
            Vector2 forward = Quaternion.Euler(0f, 0f, 37f) * Vector2.up;
            Vector2 left = Quaternion.Euler(0f, 0f, halfAngle) * forward;
            Vector2 right = Quaternion.Euler(0f, 0f, -halfAngle) * forward;
            Vector2 outside = Quaternion.Euler(0f, 0f, halfAngle + 0.1f) * forward;
            Assert.IsTrue(DamageMath.ShieldCovers(forward, forward, halfAngle));
            Assert.IsTrue(DamageMath.ShieldCovers(forward, left, halfAngle));
            Assert.IsTrue(DamageMath.ShieldCovers(forward, right, halfAngle));
            Assert.IsFalse(DamageMath.ShieldCovers(forward, outside, halfAngle));
            Assert.IsFalse(DamageMath.ShieldCovers(forward, -forward, halfAngle));
        }

        [Test]
        public void ShieldCovers_FullShieldAndCentreImpact()
        {
            Assert.IsTrue(DamageMath.ShieldCovers(Vector2.up, Vector2.down, 180f));
            Assert.IsTrue(DamageMath.ShieldCovers(Vector2.up, Vector2.zero, 22.5f));
            Assert.IsFalse(DamageMath.ShieldCovers(Vector2.zero, Vector2.up, 22.5f));
        }

        [Test]
        public void AbsorbArmour_BoostsBlockingAndKeepsWearInBaseUnits()
        {
            var armour = new ComponentHealth(10f);
            Assert.AreEqual(30f, DamageMath.AbsorbArmour(50f, armour, 0.1f, 2f));
            Assert.AreEqual(9f, armour.Current, 1e-5f);
            Assert.AreEqual(41f, DamageMath.AbsorbArmour(50f, armour, 0f), 1e-5f);
        }

        [Test]
        public void AbsorbArmour_BoostNeverBlocksMoreThanTheHit()
        {
            var armour = new ComponentHealth(10f);
            Assert.AreEqual(0f, DamageMath.AbsorbArmour(5f, armour, 0.1f, 2f));
            Assert.AreEqual(9.75f, armour.Current, 1e-5f);
        }

        [Test]
        public void Impact_HeadOnIsMaximumAndGlancingIsMinimal()
        {
            float headOn = DamageMath.Impact(new Vector2(0, -10), Vector2.up, 1);
            float glancing = DamageMath.Impact(new Vector2(10, -0.1f), Vector2.up, 1);
            Assert.AreEqual(10f, headOn, 1e-4f);
            Assert.Less(glancing, 0.2f);
        }

        [Test]
        public void Impact_ScalesWithMultiplierAndIsZeroAtRest()
        {
            Assert.AreEqual(20f, DamageMath.Impact(new Vector2(0, 10), Vector2.up, 2), 1e-4f);
            Assert.AreEqual(0f, DamageMath.Impact(Vector2.zero, Vector2.up, 2));
        }

        [Test]
        public void AbsorbArmour_BlocksFixedAmountAndNeverMoreThanTheHit()
        {
            var armour = new ComponentHealth(10);
            Assert.AreEqual(40f, DamageMath.AbsorbArmour(50, armour, 0f));
            Assert.AreEqual(0f, DamageMath.AbsorbArmour(5, armour, 0f));
        }

        [Test]
        public void AbsorbArmour_WearsDownAndBlocksLess()
        {
            var armour = new ComponentHealth(10);
            DamageMath.AbsorbArmour(50, armour, 0.1f);
            Assert.AreEqual(9f, armour.Current, 1e-5f);
            Assert.AreEqual(41f, DamageMath.AbsorbArmour(50, armour, 0.1f), 1e-5f);
        }

        [Test]
        public void AbsorbArmour_DepletedArmourBlocksNothing()
        {
            var armour = new ComponentHealth(10);
            armour.Damage(10);
            Assert.AreEqual(50f, DamageMath.AbsorbArmour(50, armour, 0.1f));
        }

        [Test]
        public void AbsorbShield_RespectsPerImpactCapAndHealth()
        {
            var shield = new ComponentHealth(100);
            Assert.AreEqual(30f, DamageMath.AbsorbShield(50, shield, 20));
            Assert.AreEqual(80f, shield.Current);

            var weak = new ComponentHealth(10);
            Assert.AreEqual(40f, DamageMath.AbsorbShield(50, weak, 20));
            Assert.AreEqual(0f, weak.Current);
        }

        [Test]
        public void ProximityWeight_FallsOffWithDistanceToPoint()
        {
            var point = new Vector2(10, 0);
            Assert.AreEqual(1f, DamageMath.ProximityWeight(new Vector2(10, 0), point, 5), 1e-5f);
            Assert.AreEqual(0.5f, DamageMath.ProximityWeight(new Vector2(12.5f, 0), point, 5), 1e-5f);
            Assert.AreEqual(0f, DamageMath.ProximityWeight(new Vector2(5, 20), point, 5));
        }

        [Test]
        public void ProximityWeight_ZeroWithoutPoint()
        {
            Assert.AreEqual(0f, DamageMath.ProximityWeight(Vector2.zero, null, 5));
        }
    }


    public class ComponentHealthTests
    {
        [Test]
        public void SetMax_PreservesFractionOnBoostAndRemoval()
        {
            var shield = new ComponentHealth(100f);
            shield.Damage(40f);
            shield.SetMax(200f);
            Assert.AreEqual(200f, shield.Max);
            Assert.AreEqual(120f, shield.Current, 1e-5f);
            shield.Damage(20f);
            shield.SetMax(100f);
            Assert.AreEqual(50f, shield.Current, 1e-5f);
            for (int cycle = 0; cycle < 10; cycle++)
            {
                shield.SetMax(200f);
                shield.SetMax(100f);
            }
            Assert.AreEqual(50f, shield.Current, 1e-5f);
        }

        [Test]
        public void SetMax_DoesNotRestoreDepletedShield()
        {
            var shield = new ComponentHealth(100f);
            shield.Damage(100f);
            shield.SetMax(200f);
            Assert.AreEqual(0f, shield.Current);
        }

        [Test]
        public void Damage_ClampsAtZeroAndFlagsDestroyed()
        {
            var health = new ComponentHealth(10);
            health.Damage(25);
            Assert.AreEqual(0f, health.Current);
            Assert.IsTrue(health.IsDestroyed);
        }

        [Test]
        public void Repair_ClampsAtMax()
        {
            var health = new ComponentHealth(10);
            health.Damage(4);
            health.Repair(100);
            Assert.AreEqual(10f, health.Current);
            Assert.AreEqual(1f, health.Fraction);
        }
    }

    public class PowerBankTests
    {
        [Test]
        public void NewBank_StartsFullyCharged()
        {
            var bank = new PowerBank(50);
            Assert.AreEqual(50f, bank.Stored);
        }

        [Test]
        public void Charge_ClampsAtMax()
        {
            var bank = new PowerBank(50);
            bank.Draw(40);
            bank.Charge(80);
            Assert.AreEqual(50f, bank.Stored);
        }

        [Test]
        public void TryDraw_IsAllOrNothing()
        {
            var bank = new PowerBank(50);
            bank.Draw(40);
            Assert.IsFalse(bank.TryDraw(15));
            Assert.AreEqual(10f, bank.Stored);
            Assert.IsTrue(bank.TryDraw(10));
            Assert.AreEqual(0f, bank.Stored);
        }

        [Test]
        public void Draw_TakesWhatIsAvailable()
        {
            var bank = new PowerBank(50);
            bank.Draw(40);
            Assert.AreEqual(10f, bank.Draw(25));
            Assert.AreEqual(0f, bank.Stored);
        }
    }

    public class OrbitMathTests
    {
        [Test]
        public void Position_StartsAtPeriapsisWithZeroPhase()
        {
            Vector2 position = OrbitMath.Position(100, 300, 0, 0, 60, 0);
            Assert.AreEqual(100f, position.x, 1e-3f);
            Assert.AreEqual(0f, position.y, 1e-3f);
        }

        [Test]
        public void Position_ReachesApoapsisHalfwayThroughPeriod()
        {
            Vector2 position = OrbitMath.Position(100, 300, 0, 0, 60, 30);
            Assert.AreEqual(-300f, position.x, 1e-2f);
        }

        [Test]
        public void Position_ReturnsAfterOnePeriod()
        {
            Vector2 start = OrbitMath.Position(100, 300, 1f, 0, 60, 0);
            Vector2 end = OrbitMath.Position(100, 300, 1f, 0, 60, 60);
            Assert.AreEqual(0f, (start - end).magnitude, 1e-2f);
        }

        [Test]
        public void Position_StaysWithinAltitudeBounds()
        {
            for (float t = 0; t < 60; t += 1.5f)
            {
                float distance = OrbitMath.Position(100, 300, 0.7f, 0.1f, 60, t).magnitude;
                Assert.GreaterOrEqual(distance, 99.99f);
                Assert.LessOrEqual(distance, 300.01f);
            }
        }

        [Test]
        public void Position_PrecessionRotatesTheEllipse()
        {
            Vector2 position = OrbitMath.Position(100, 100, 0, Mathf.PI / 2, 60, 1);
            // A circle at t=1: orbit advanced 6 degrees, ellipse rotated 90 degrees.
            Assert.AreEqual(100f, position.magnitude, 1e-2f);
            float angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
            Assert.AreEqual(96f, angle, 1e-2f);
        }
    }

    public class AsteroidMathTests
    {
        [Test]
        public void Density_IsWithinZeroToOneAndDeterministic()
        {
            for (int i = 0; i < 100; i++)
            {
                var position = new Vector2(i * 37f, i * 91f);
                float density = AsteroidMath.Density(position, 300, 5, 2);
                Assert.GreaterOrEqual(density, 0f);
                Assert.LessOrEqual(density, 1f);
                Assert.AreEqual(density, AsteroidMath.Density(position, 300, 5, 2));
            }
        }

        [Test]
        public void Density_HigherSharpnessNeverIncreasesDensity()
        {
            var position = new Vector2(123f, 456f);
            Assert.LessOrEqual(AsteroidMath.Density(position, 300, 5, 3), AsteroidMath.Density(position, 300, 5, 1));
        }
    }

    public class FireMathTests
    {
        [Test]
        public void Spread_IsEvenlySpacedIncludingEndpoints()
        {
            float[] angles = FireMath.Angles(FirePattern.Spread, 5, -20, 20, 0, new Random(1));
            CollectionAssert.AreEqual(new[] { -20f, -10f, 0f, 10f, 20f }, angles);
        }

        [Test]
        public void Spread_SingleBulletFiresDownTheMiddle()
        {
            Assert.AreEqual(5f, FireMath.Angles(FirePattern.Spread, 1, 0, 10, 0, new Random(1))[0]);
        }

        [Test]
        public void Shotgun_StaysWithinRange()
        {
            float[] angles = FireMath.Angles(FirePattern.Shotgun, 200, -15, 25, 0, new Random(3));
            Assert.IsTrue(angles.All(a => a >= -15f && a <= 25f));
        }

        [Test]
        public void Sinewave_SweepsBetweenLimits()
        {
            Assert.AreEqual(20f, FireMath.Angles(FirePattern.Sinewave, 1, -20, 20, Mathf.PI / 2, new Random(1))[0], 1e-4f);
            Assert.AreEqual(-20f, FireMath.Angles(FirePattern.Sinewave, 1, -20, 20, -Mathf.PI / 2, new Random(1))[0], 1e-4f);
            Assert.AreEqual(0f, FireMath.Angles(FirePattern.Sinewave, 1, -20, 20, 0, new Random(1))[0], 1e-4f);
        }
    }

    public class ThrusterMathTests
    {
        [Test]
        public void Ramp_ReachesFullThrottleInRampUpTime()
        {
            float throttle = 0;
            for (int i = 0; i < 10; i++) throttle = ThrusterMath.Ramp(throttle, 1, 1f, 2f, 0.1f);
            Assert.AreEqual(1f, throttle, 1e-4f);
        }

        [Test]
        public void Ramp_FallsAtRampDownRate()
        {
            Assert.AreEqual(0.95f, ThrusterMath.Ramp(1f, 0, 1f, 2f, 0.1f), 1e-5f);
        }

        [Test]
        public void Ramp_ZeroTimeIsInstant()
        {
            Assert.AreEqual(1f, ThrusterMath.Ramp(0f, 1, 0f, 0f, 0.02f));
            Assert.AreEqual(0f, ThrusterMath.Ramp(1f, 0, 0f, 0f, 0.02f));
        }
    }

    public class LobbyReadinessTests
    {
        [TestCase(0, 0, false)]
        [TestCase(1, 0, false)]
        [TestCase(1, 1, true)]
        [TestCase(7, 6, false)]
        [TestCase(7, 7, true)]
        [TestCase(16, 15, false)]
        [TestCase(16, 16, true)]
        [TestCase(17, 17, false)]
        [TestCase(2, 3, false)]
        public void CanStart_RequiresEveryPlayerWithinTheCapacity(int players, int ready, bool expected)
        {
            Assert.AreEqual(expected, LobbyReadiness.CanStart(players, ready));
        }
    }

    public class LoadoutMathTests
    {
        [Test]
        public void Cycle_WrapsAtBothEnds()
        {
            Assert.AreEqual(0, LoadoutMath.Cycle(2, 3, 1, false));
            Assert.AreEqual(2, LoadoutMath.Cycle(0, 3, -1, false));
        }

        [Test]
        public void Cycle_IncludesNoneWhenAllowed()
        {
            Assert.AreEqual(-1, LoadoutMath.Cycle(2, 3, 1, true));
            Assert.AreEqual(0, LoadoutMath.Cycle(-1, 3, 1, true));
            Assert.AreEqual(2, LoadoutMath.Cycle(-1, 3, -1, true));
        }

        [Test]
        public void CycleAvailable_SkipsUnavailableEntriesAndStaysPutWhenNoneFit()
        {
            Assert.AreEqual(2, LoadoutMath.CycleAvailable(0, 4, 1, false, i => i == 2));
            Assert.AreEqual(3, LoadoutMath.CycleAvailable(0, 4, -1, false, i => i >= 2));
            Assert.AreEqual(1, LoadoutMath.CycleAvailable(1, 4, 1, false, i => false));
        }

        [Test]
        public void Cycle_EmptyListStaysAtFirst()
        {
            Assert.AreEqual(0, LoadoutMath.Cycle(0, 0, 1, false));
            Assert.AreEqual(-1, LoadoutMath.Cycle(-1, 0, 1, true));
        }

        [Test]
        public void InRange_RejectsOutOfRangeAndNoneUnlessAllowed()
        {
            Assert.IsTrue(LoadoutMath.InRange(0, 3, false));
            Assert.IsFalse(LoadoutMath.InRange(3, 3, false));
            Assert.IsFalse(LoadoutMath.InRange(-1, 3, false));
            Assert.IsTrue(LoadoutMath.InRange(-1, 3, true));
            Assert.IsFalse(LoadoutMath.InRange(-2, 3, true));
        }

        [Test]
        public void MountAssignments_CopyOnWriteAndSupportMoreThanFour()
        {
            ShipLoadout original = ShipLoadout.Empty;
            original.MountComponents = new[] { -1, -1, -1, -1, -1, -1 };
            ShipLoadout edited = original;
            edited.SetMountComponent(5, 3);
            Assert.AreEqual(-1, original.MountComponents[5]);
            Assert.AreEqual(3, edited.MountComponents[5]);
            Assert.Throws<ArgumentOutOfRangeException>(() => edited.SetMountComponent(6, 0));
        }
    }

    public class PredictionMathTests
    {
        [Test]
        public void Offset_IsVelocityTimesLookAheadTimesBlend()
        {
            Vector2 offset = PredictionMath.Offset(new Vector2(10, 0), 0.1f, 0.5f, 1f);
            Assert.AreEqual(0.5f, offset.x, 1e-5f);
        }

        [Test]
        public void Offset_CapsLookAheadAndIsZeroWithoutBlend()
        {
            Assert.AreEqual(2f, PredictionMath.Offset(new Vector2(10, 0), 5f, 1f, 0.2f).x, 1e-5f);
            Assert.AreEqual(Vector2.zero, PredictionMath.Offset(new Vector2(10, 0), 0.1f, 0f, 1f));
        }
    }

    public class RotationMathTests
    {
        [Test]
        public void Acceleration_TurnsTowardTargetAtMaximumFromRest()
        {
            Assert.AreEqual(360f, RotationMath.Acceleration(90, 0, 360, 0.02f));
            Assert.AreEqual(-360f, RotationMath.Acceleration(-90, 0, 360, 0.02f));
        }

        [Test]
        public void Acceleration_BrakesWhenCloseAndMovingFast()
        {
            Assert.AreEqual(-360f, RotationMath.Acceleration(1, 100, 360, 0.02f));
        }

        [Test]
        public void Acceleration_IsZeroOnTargetAtRestAndStopsSpin()
        {
            Assert.AreEqual(0f, RotationMath.Acceleration(0, 0, 360, 0.02f));
            Assert.AreEqual(-360f, RotationMath.Acceleration(0, 10, 360, 0.02f));
        }
    }

    public class DustMathTests
    {
        [Test]
        public void Fade_RampsInAndOutAndIsZeroOutsideLife()
        {
            Assert.AreEqual(0f, DustMath.Fade(0f, 4f, 1f));
            Assert.AreEqual(0.5f, DustMath.Fade(0.5f, 4f, 1f), 1e-5f);
            Assert.AreEqual(1f, DustMath.Fade(2f, 4f, 1f));
            Assert.AreEqual(0.5f, DustMath.Fade(3.5f, 4f, 1f), 1e-5f);
            Assert.AreEqual(0f, DustMath.Fade(4f, 4f, 1f));
            Assert.AreEqual(0f, DustMath.Fade(5f, 4f, 1f));
        }

        [Test]
        public void Fade_ShortLifetimeNeverReachesFull()
        {
            Assert.AreEqual(0.5f, DustMath.Fade(0.5f, 1f, 1f), 1e-5f);
        }

        [Test]
        public void StreakAmount_StartsAtFractionAndSaturates()
        {
            Assert.AreEqual(0f, DustMath.StreakAmount(30f, 100f, 0.5f));
            Assert.AreEqual(0f, DustMath.StreakAmount(50f, 100f, 0.5f));
            Assert.AreEqual(0.5f, DustMath.StreakAmount(75f, 100f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, DustMath.StreakAmount(100f, 100f, 0.5f));
            Assert.AreEqual(1f, DustMath.StreakAmount(300f, 100f, 0.5f));
            Assert.AreEqual(0f, DustMath.StreakAmount(300f, 0f, 0.5f));
        }

        [Test]
        public void Wrap_KeepsValuesInRange()
        {
            Assert.AreEqual(0.5f, DustMath.Wrap(0.5f, 1.2f), 1e-5f);
            Assert.AreEqual(-1.1f, DustMath.Wrap(1.3f, 1.2f), 1e-5f);
            Assert.AreEqual(1.1f, DustMath.Wrap(-1.3f, 1.2f), 1e-5f);
            Assert.AreEqual(0.2f, DustMath.Wrap(5f, 1.2f), 1e-4f);
        }
    }

    public class CameraZoomMathTests
    {
        [TestCase(0f, 0f)]
        [TestCase(25f, 0f)]
        [TestCase(-25f, 0f)]
        [TestCase(0f, 15f)]
        public void TargetSize_VisibleFootprintEnforcesMinimum(float x, float y)
        {
            Assert.AreEqual(40f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(x, y), 5f, 40f, 50f));
        }

        [Test]
        public void TargetSize_NearbyBlendsAndDistantObjectsHaveNoEffect()
        {
            Assert.AreEqual(25f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(50f, 0f), 5f, 40f, 50f));
            Assert.AreEqual(10f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(75f, 0f), 5f, 40f, 50f));
            Assert.AreEqual(10f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(100f, 0f), 5f, 40f, 50f));
        }

        [Test]
        public void TargetSize_CornerDistanceUsesCircularFootprint()
        {
            Assert.AreEqual(40f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(23f, 14f), 5f, 40f, 0f));
            Assert.AreEqual(10f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(24f, 14f), 5f, 40f, 0f));
        }

        [Test]
        public void TargetSize_NeverReducesSpeedZoomAndCanExceedSpeedMaximum()
        {
            Assert.AreEqual(20f, CameraZoomMath.TargetSize(20f, 1f, Vector2.zero, 5f, 5f, 50f));
            Assert.AreEqual(80f, CameraZoomMath.TargetSize(50f, 1f, Vector2.zero, 5f, 80f, 50f));
        }

        [Test]
        public void TargetSize_AspectControlsHorizontalReach()
        {
            Assert.AreEqual(40f, CameraZoomMath.TargetSize(10f, 2f, new Vector2(20f, 0f), 0f, 40f, 0f));
            Assert.AreEqual(10f, CameraZoomMath.TargetSize(10f, 0.5f, new Vector2(20f, 0f), 0f, 40f, 0f));
        }

        [Test]
        public void TargetSize_BaselineViewDoesNotLatchToExpandedZoom()
        {
            Vector2 offset = new Vector2(80f, 0f);
            Assert.AreEqual(10f, CameraZoomMath.TargetSize(10f, 1f, offset, 5f, 80f, 50f));
            Assert.AreEqual(80f, CameraZoomMath.TargetSize(80f, 1f, offset, 5f, 80f, 50f));
        }
    }

    public class SpeedMathTests
    {
        [Test]
        public void Blend_GoesFromRestToFullAndClamps()
        {
            Assert.AreEqual(10f, SpeedMath.Blend(0, 10, 30, 100));
            Assert.AreEqual(20f, SpeedMath.Blend(50, 10, 30, 100), 1e-5f);
            Assert.AreEqual(30f, SpeedMath.Blend(500, 10, 30, 100));
        }
    }

    public class ActivationTests
    {
        private static PowerBank Charged(float amount)
        {
            var bank = new PowerBank(1000);
            bank.Draw(bank.Max - amount);
            return bank;
        }

        [Test]
        public void Passive_IsAlwaysActive()
        {
            Assert.IsTrue(new Activation(ActivationMode.Passive, 0, 0, 0).IsActive);
        }

        [Test]
        public void Toggle_DrawsPowerWhileOnAndStopsWhenPressedAgain()
        {
            var activation = new Activation(ActivationMode.Toggle, 0, 0, 10);
            PowerBank power = Charged(100);

            activation.Press();
            activation.Step(1f, power);
            Assert.IsTrue(activation.IsActive);
            Assert.AreEqual(90f, power.Stored);

            activation.Press();
            activation.Step(1f, power);
            Assert.IsFalse(activation.IsActive);
            Assert.AreEqual(90f, power.Stored);
        }

        [Test]
        public void Toggle_DoesNotSwitchOnWithoutPower()
        {
            var activation = new Activation(ActivationMode.Toggle, 0, 0, 10);
            activation.Press();
            activation.Step(1f, Charged(5));
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void Toggle_SwitchesOffWhenPowerRunsOutAndStaysOff()
        {
            var activation = new Activation(ActivationMode.Toggle, 0, 0, 10);
            PowerBank power = Charged(15);
            activation.Press();
            activation.Step(1f, power);
            activation.Step(1f, power);
            Assert.IsFalse(activation.IsActive);

            power.Charge(100);
            activation.Step(1f, power);
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void Triggered_RunsForDurationThenCoolsDown()
        {
            var activation = new Activation(ActivationMode.Triggered, 2f, 5f, 1);
            PowerBank power = Charged(100);

            activation.Press();
            activation.Step(1f, power);
            Assert.IsTrue(activation.IsActive);
            activation.Step(1f, power);
            Assert.IsFalse(activation.IsActive);
            Assert.AreEqual(5f, activation.CooldownRemaining);

            activation.Press();
            activation.Step(1f, power);
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void Triggered_CanBeUsedAgainAfterCooldown()
        {
            var activation = new Activation(ActivationMode.Triggered, 2f, 2f, 1);
            PowerBank power = Charged(100);
            activation.Press();
            for (int i = 0; i < 4; i++) activation.Step(1f, power);

            activation.Press();
            activation.Step(1f, power);
            Assert.IsTrue(activation.IsActive);
        }

        [Test]
        public void Triggered_PowerFailureEndsEarlyWithCooldown()
        {
            var activation = new Activation(ActivationMode.Triggered, 10f, 5f, 10);
            PowerBank power = Charged(15);
            activation.Press();
            activation.Step(1f, power);
            activation.Step(1f, power);
            Assert.IsFalse(activation.IsActive);
            Assert.AreEqual(5f, activation.CooldownRemaining);
        }

        [Test]
        public void NegativeDrain_GeneratesPowerWhileActive()
        {
            var activation = new Activation(ActivationMode.Toggle, 0, 0, -4);
            PowerBank power = Charged(0);
            activation.Press();
            activation.Step(1f, power);
            Assert.IsTrue(activation.IsActive);
            Assert.AreEqual(4f, power.Stored);
        }

        [Test]
        public void Stop_SwitchesOffImmediately()
        {
            var activation = new Activation(ActivationMode.Toggle, 0, 0, 1);
            activation.Press();
            activation.Step(1f, Charged(10));
            activation.Stop();
            Assert.IsFalse(activation.IsActive);
        }

        [Test]
        public void Level_DrainsWhileTriggeredRunsThenRefillsThroughCooldown()
        {
            var activation = new Activation(ActivationMode.Triggered, 4f, 4f, 0);
            PowerBank power = Charged(100);
            Assert.AreEqual(1f, activation.Level);

            activation.Press();
            activation.Step(1f, power);
            Assert.AreEqual(0.75f, activation.Level, 1e-5f);

            for (int i = 0; i < 3; i++) activation.Step(1f, power);
            Assert.IsTrue(activation.IsCoolingDown);
            Assert.AreEqual(0f, activation.Level, 1e-5f);

            activation.Step(1f, power);
            activation.Step(1f, power);
            Assert.AreEqual(0.5f, activation.Level, 1e-5f);
        }

        [Test]
        public void Level_ToggleIsOneWhenOnAndZeroWhenOff()
        {
            var activation = new Activation(ActivationMode.Toggle, 0, 0, 0);
            Assert.AreEqual(0f, activation.Level);
            activation.Press();
            activation.Step(1f, Charged(10));
            Assert.AreEqual(1f, activation.Level);
        }
    }

    public class PlayerPaletteTests
    {
        [Test]
        public void Palette_HasSixtyFourDistinctOpaqueColours()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < PlayerPalette.Count; i++)
            {
                Color32 colour = PlayerPalette.Colour(i);
                Assert.AreEqual(255, colour.a);
                Assert.IsTrue(seen.Add((colour.r << 16) | (colour.g << 8) | colour.b), $"swatch {i} repeats a colour");
            }
            Assert.AreEqual(64, seen.Count);
        }

        [Test]
        public void Palette_DoesNotOfferExactShaderKeyColours()
        {
            var greenKey = new Color32(0, 255, 0, 255);
            var magentaKey = new Color32(255, 0, 255, 255);
            for (int i = 0; i < PlayerPalette.Count; i++)
            {
                Color32 colour = PlayerPalette.Colour(i);
                Assert.AreNotEqual(greenKey, colour, $"swatch {i} matches the green key");
                Assert.AreNotEqual(magentaKey, colour, $"swatch {i} matches the magenta key");
            }
            Assert.IsFalse(PlayerPalette.IsAvailable(greenKey));
            Assert.IsFalse(PlayerPalette.IsAvailable(magentaKey));
        }

        [Test]
        public void IndexOf_FindsPaletteColoursAndRejectsOthers()
        {
            Assert.AreEqual(17, PlayerPalette.IndexOf(PlayerPalette.Colour(17)));
            Assert.AreEqual(-1, PlayerPalette.IndexOf(new Color32(1, 2, 3, 255)));
        }

        [Test]
        public void EmptyLoadout_UsesDefaultColours()
        {
            ShipLoadout loadout = ShipLoadout.Empty;
            Assert.AreEqual(PlayerPalette.DefaultPrimary, loadout.Colour0);
            Assert.AreEqual(PlayerPalette.DefaultSecondary, loadout.Colour1);
        }

        [Test]
        public void Palette_FreeColoursAreBrightHuesAndGreys()
        {
            for (int i = 0; i < PlayerPalette.Count; i++)
            {
                Assert.AreEqual(i < 16 ? 0 : 100, PlayerPalette.Price(i));
                Assert.AreEqual(i < 16, PlayerPalette.IsAvailable(PlayerPalette.Colour(i)));
                if (i >= 8 && i < 16)
                {
                    Color32 grey = PlayerPalette.Colour(i);
                    Assert.AreEqual(grey.r, grey.g);
                    Assert.AreEqual(grey.r, grey.b);
                }
            }
            Assert.AreEqual(new Color32(255, 0, 0, 255), PlayerPalette.DefaultPrimary);
            Assert.AreEqual(new Color32(255, 255, 255, 255), PlayerPalette.DefaultSecondary);
            Assert.IsFalse(PlayerPalette.IsAvailable(new Color32(1, 2, 3, 255)));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPalette.Price(-1));
        }
    }

    public class VitalsMathTests
    {
        [TestCase(220f, 1f, 1f, 1f)]
        [TestCase(190f, 1f, 0.7f, 1f)]
        [TestCase(100f, 1f, 0f, 0.5f)]
        [TestCase(40f, 0.5f, 0f, 0f)]
        [TestCase(0f, 0f, 0f, 0f)]
        public void PowerSegments_DrainInOrderAndRechargeInReverse(
            float stored, float hull, float first, float second)
        {
            Assert.AreEqual(hull, VitalsMath.PowerSegmentFraction(stored, 0f, 80f), 1e-6f);
            Assert.AreEqual(first, VitalsMath.PowerSegmentFraction(stored, 120f, 100f), 1e-6f);
            Assert.AreEqual(second, VitalsMath.PowerSegmentFraction(stored, 80f, 40f), 1e-6f);
        }

        [Test]
        public void PowerSegment_EmptyCapacityHasNoFill()
        {
            Assert.AreEqual(0f, VitalsMath.PowerSegmentFraction(100f, 0f, 0f));
        }

        [Test]
        public void Byte_RoundTripsWithinQuantisationAndClamps()
        {
            Assert.AreEqual(0.5f, VitalsMath.FromByte(VitalsMath.ToByte(0.5f)), 1f / 255f);
            Assert.AreEqual(255, VitalsMath.ToByte(2f));
            Assert.AreEqual(0, VitalsMath.ToByte(-1f));
        }

        [Test]
        public void SetFraction_ScalesHealthAndPower()
        {
            var health = new ComponentHealth(40);
            health.SetFraction(0.25f);
            Assert.AreEqual(10f, health.Current);

            var bank = new PowerBank(200);
            bank.SetFraction(0.5f);
            Assert.AreEqual(100f, bank.Stored);
            Assert.AreEqual(0.5f, bank.Fraction);
        }
    }
}
