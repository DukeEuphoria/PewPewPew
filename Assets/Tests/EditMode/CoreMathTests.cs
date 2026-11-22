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
                MethodInfo setHud = systemType.GetMethod("SetHud");
                setHud.Invoke(systemA, new object[] { 1f, HudState.Active });
                setHud.Invoke(systemB, new object[] { 1f, HudState.Active });
                var systems = (IList)Get(ship, "m_SubSystems");
                systems.Add(systemA);
                systems.Add(systemB);
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
            Type callbackType = typeof(Action<,>).MakeGenericType(GameType("PewPewPew.World.SpaceObject"), typeof(double));
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
                SetDefinition(definition, "m_HalfAngle", halfAngle);
                SetDefinition(definition, "m_FadeStartAngle", halfAngle * 0.75f);
                SetDefinition(definition, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), meshType));
                SetDefinition(definition, "m_GeodesicResolution", 3);
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
                SetDefinition(definition, "m_HalfAngle", halfAngle);
                SetDefinition(definition, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), meshType));
                SetDefinition(definition, "m_FadeStartAngle", halfAngle * 0.75f);
                SetDefinition(definition, "m_MaxAlpha", 0.8f);
                SetDefinition(definition, "m_EdgeAlpha", 0.2f);
                ship.transform.rotation = Quaternion.Euler(0f, 0f, 37f);
                mount.transform.SetParent(ship.transform, false);
                mount.transform.localRotation = Quaternion.Euler(0f, 0f, mountAngle);
                GameObject visualObject = UnityEngine.Object.Instantiate(prefab, ship.transform);
                Component visual = visualObject.GetComponent(visualType);
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
                        Mathf.Clamp01((Mathf.Abs(angle) - halfAngle * 0.75f) / (halfAngle * 0.25f)));
                    Assert.That(colors[i].a, Is.EqualTo(expectedAlpha).Within(0.002f));
                    if (Mathf.Abs(Mathf.Abs(angle) - halfAngle * 0.75f) < 0.01f) sawFadeStart = true;
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
                SetDefinition(definition, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), 0));
                SetDefinition(definition, "m_HalfAngle", 180f);
                fit.Invoke(visual, new object[] { bounds, mount.transform, definition });
                Assert.AreSame(original, filter.sharedMesh);
                Assert.IsTrue(mesh == null, "Replacing a generated mesh releases it");
                var properties = new MaterialPropertyBlock();
                visualObject.GetComponent<Renderer>().GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_ShieldAlpha"), Is.EqualTo(0.8f).Within(1e-6f));
                Assert.AreEqual(0f, properties.GetFloat("_UseVertexColors"));
                SetDefinition(definition, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), 1));
                fit.Invoke(visual, new object[] { bounds, mount.transform, definition });
                Mesh fullGeodesic = filter.sharedMesh;
                Assert.AreNotSame(original, fullGeodesic);
                Assert.IsTrue(fullGeodesic.colors.All(color => Mathf.Abs(color.a - 0.8f) < 1e-6f));
                SetDefinition(definition, "m_MeshType", Enum.ToObject(definitionType.Assembly.GetType("PewPewPew.Ships.ShieldMeshType"), 0));
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
        public void ShieldDefinition_ConstrainsFadeSettings()
        {
            Type type = Type.GetType("PewPewPew.Ships.ShieldDef, Assembly-CSharp");
            ScriptableObject definition = ScriptableObject.CreateInstance(type);
            try
            {
                SetDefinition(definition, "m_HalfAngle", 45f);
                SetDefinition(definition, "m_FadeStartAngle", 45f);
                SetDefinition(definition, "m_MaxAlpha", 0.3f);
                SetDefinition(definition, "m_EdgeAlpha", 0.9f);
                Assert.Less((float)type.GetProperty("FadeStartAngle").GetValue(definition), 45f);
                Assert.That((float)type.GetProperty("EdgeAlpha").GetValue(definition), Is.EqualTo(0.3f).Within(1e-6f));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
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

        private static void SetDefinition(ScriptableObject definition, string field, object value) =>
            definition.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, value);
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
        public void ProximityWeight_FallsOffWithDistanceToNearestPoint()
        {
            var points = new[] { new Vector2(0, 0), new Vector2(10, 0) };
            Assert.AreEqual(1f, DamageMath.ProximityWeight(new Vector2(10, 0), points, 5), 1e-5f);
            Assert.AreEqual(0.5f, DamageMath.ProximityWeight(new Vector2(12.5f, 0), points, 5), 1e-5f);
            Assert.AreEqual(0f, DamageMath.ProximityWeight(new Vector2(5, 20), points, 5));
        }

        [Test]
        public void ProximityWeight_ZeroWithoutEmissionPoints()
        {
            Assert.AreEqual(0f, DamageMath.ProximityWeight(Vector2.zero, new Vector2[0], 5));
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
        public void SpinSpeed_SmallerSpinsFaster()
        {
            Assert.AreEqual(120f, AsteroidMath.SpinSpeed(1, 120f));
            Assert.AreEqual(30f, AsteroidMath.SpinSpeed(4, 120f));
            Assert.Greater(AsteroidMath.SpinSpeed(2, 120f), AsteroidMath.SpinSpeed(10, 120f));
            Assert.AreEqual(120f, AsteroidMath.SpinSpeed(0, 120f));
        }

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

        [Test]
        public void Split_SizeOneDoesNotSplit()
        {
            Assert.IsEmpty(AsteroidMath.Split(1, new Random(1)));
        }

        [Test]
        public void Split_PiecesSumToSizeAndAreValid()
        {
            var random = new Random(42);
            for (int size = 2; size <= 10; size++)
            {
                for (int run = 0; run < 50; run++)
                {
                    int[] pieces = AsteroidMath.Split(size, random);
                    Assert.AreEqual(size, pieces.Sum());
                    Assert.GreaterOrEqual(pieces.Length, 2);
                    Assert.IsTrue(pieces.All(p => p >= 1 && p < size));
                }
            }
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

        [Test]
        public void EmissionPoints_RotateSequentiallyOneAtATime()
        {
            var sequence = Enumerable.Range(0, 6).Select(b => FireMath.EmissionPoints(b, 1, 4)[0]);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 0, 1 }, sequence);
        }

        [Test]
        public void EmissionPoints_UsesGroupsWhenFiringFromSeveral()
        {
            CollectionAssert.AreEqual(new[] { 0, 1 }, FireMath.EmissionPoints(0, 2, 4));
            CollectionAssert.AreEqual(new[] { 2, 3 }, FireMath.EmissionPoints(1, 2, 4));
            CollectionAssert.AreEqual(new[] { 0, 1 }, FireMath.EmissionPoints(2, 2, 4));
        }

        [Test]
        public void BulletsAtPoint_DividesEvenlyWithRemainderFirst()
        {
            int[] counts = Enumerable.Range(0, 3).Select(p => FireMath.BulletsAtPoint(7, 3, p)).ToArray();
            CollectionAssert.AreEqual(new[] { 3, 2, 2 }, counts);
        }

        [Test]
        public void Burst_SequentialSinglePointRotatesThroughConsideredPoints()
        {
            var random = new Random(1);
            var points = Enumerable.Range(0, 5)
                .Select(burst => FireMath.Burst(FirePattern.Spread, 1, 0, 0, 0, burst, 1, 4, random)[0].PointIndex);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 0 }, points);
        }

        [Test]
        public void Burst_SplitsBulletsEvenlyAcrossPointsAndUsesEachAngleOnce()
        {
            Shot[] shots = FireMath.Burst(FirePattern.Spread, 4, -30, 30, 0, 0, 2, 4, new Random(1));
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, shots.Select(s => s.PointIndex));
            CollectionAssert.AreEqual(new[] { -30f, -10f, 10f, 30f }, shots.Select(s => s.Angle));
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
