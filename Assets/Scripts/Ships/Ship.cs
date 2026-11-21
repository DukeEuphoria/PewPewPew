using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using PewPewPew.Core;
using PewPewPew.GameSystems;
using PewPewPew.Match;
using PewPewPew.Weapons;
using PewPewPew.World;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Ships
{
    [RequireComponent(typeof(ThrusterEffects), typeof(ShipHudSync))]
    public class Ship : SpaceObject, IDamageable
    {
        private const float MinAimDistanceSqr = 1e-4f;
        private const int BoxCornerCount = 8;
        [SerializeField] private GameObject m_ExplosionPrefab;
        [SerializeField, Tooltip("Keeps the object alive briefly after the hull fails so clients receive the explosion.")] private float m_DespawnDelay = 0.3f;
        [SerializeField] private float m_SystemHitRadius = 2f;
        [SerializeField, Tooltip("Damage taken per unit of passive power a system could not get.")] private float m_UnpoweredDamagePerPower = 1f;
        [SerializeField] private float m_CollisionDamageMultiplier = 1f;
        [SerializeField] private float m_OrbitalBodyDamageMultiplier = 10f;

        private sealed class MountedGun
        {
            public Gun Gun;
            public GunControlRole Role;
        }

        private enum ThrusterDirection { Forward, Reverse, Left, Right }

        private sealed class MountedThruster
        {
            public Thruster Thruster;
            public ThrusterDirection Direction;
            public int EffectGroup;
        }

        private sealed class MountedShield
        {
            public ShieldDef Def;
            public ShipSystem Generator;
            public ComponentHealth Charge;
            public ShieldVisual Visual;
            public bool Toggled = true;
            public byte HudState = 1;
        }

        private readonly List<ShipSystem> m_Systems = new List<ShipSystem>();
        private readonly List<SubSystem> m_SubSystems = new List<SubSystem>();
        private readonly List<MountedGun> m_Guns = new List<MountedGun>();
        private readonly List<MountedThruster> m_Thrusters = new List<MountedThruster>();
        private readonly List<MountedShield> m_Shields = new List<MountedShield>();
        private readonly List<HudGauge> m_ShieldGauges = new List<HudGauge>();
        private readonly List<Color> m_ShieldGaugeColours = new List<Color>();
        private readonly List<Transform> m_Visuals = new List<Transform>();
        private readonly List<IHudElement> m_HudElements = new List<IHudElement>();
        private readonly List<GameObject> m_HudObjects = new List<GameObject>();
        private readonly System.Random m_Random = new System.Random();
        private ShipSystem m_Hull;
        private HullDef m_HullDef;
        private ComponentHealth m_Armour;
        private float m_ArmourStrengthMultiplier = 1f;
        private PowerBank m_Power;
        private float m_HullPowerLevel;
        private ShipControls m_Controls;
        private ThrusterEffects m_ThrusterEffects;
        private ShipHudSync m_HudSync;
        private bool m_Applied;
        private bool m_Exploded;
        private PlayerState m_LastAttacker;
        private float m_LastAttackTime;

        [SyncVar] private ShipLoadout m_Loadout;
        [SyncVar] private byte[] m_Throttles;

        public PlayerState Owner { get; private set; }
        public event Action<Ship> Destroyed;
        public IReadOnlyList<Transform> Visuals => m_Visuals;
        public IReadOnlyList<SubSystem> SubSystems => m_SubSystems;
        public bool HasExploded => m_Exploded;

        public void Initialize(ShipLoadout loadout, PlayerState owner)
        {
            m_Loadout = loadout;
            Owner = owner;
        }

        public PlayerState KillCredit(float windowSeconds) =>
            m_LastAttacker != null && Time.time - m_LastAttackTime <= windowSeconds ? m_LastAttacker : null;

        protected override void Awake()
        {
            base.Awake();
            m_ThrusterEffects = GetComponent<ThrusterEffects>();
            m_HudSync = GetComponent<ShipHudSync>();
        }

        private void OnDestroy() => DestroyHud();
        public override void OnStartAuthority() { if (m_Applied) BuildHud(); }
        public override void OnStopAuthority() => DestroyHud();
        public override void OnStartClient()
        {
            ApplyLoadout();
            if (isOwned) BuildHud();
        }

        private void ApplyLoadout()
        {
            if (m_Applied) return;
            ShipCatalog catalog = ShipCatalog.Instance;
            if (catalog == null || !catalog.IsValid(m_Loadout))
                throw new InvalidOperationException("Cannot construct a ship from an invalid loadout.");
            m_Applied = true;
            m_HullDef = catalog.Hulls[m_Loadout.Hull];
            HullPoints hull = Instantiate(m_HullDef.HullPoints, transform);
            ShipColours.Apply(hull.gameObject, m_Loadout);
            m_Visuals.Add(hull.transform);
            Bounds hullBounds = LocalBounds(hull.transform);
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.offset = hullBounds.center;
            collider.radius = Mathf.Max(hullBounds.size.x, hullBounds.size.y) * 0.6f;
            m_Hull = new ShipSystem(m_HullDef, Array.Empty<Transform>());
            m_Systems.Add(m_Hull);
            ShipComponentDef[] definitions = catalog.Components;
            for (int i = 0; i < hull.Points.Length; i++)
            {
                int component = m_Loadout.MountComponents[i];
                if (component < 0) continue;
                ShipComponentDef definition = definitions[component];
                HullPoint point = hull.Points[i];
                Transform[] mount = { point.transform };
                if (definition is GunDef gunDef)
                {
                    var gun = new Gun(gunDef, new ShipSystem(gunDef, mount, transform));
                    m_Guns.Add(new MountedGun { Gun = gun, Role = point.GunRole });
                    m_Systems.Add(gun.System);
                }
                else if (definition is ThrusterDef thrusterDef)
                {
                    var thruster = new Thruster(thrusterDef, new ShipSystem(thrusterDef, mount, transform));
                    m_Thrusters.Add(new MountedThruster
                    {
                        Thruster = thruster,
                        Direction = GetThrusterDirection(point.transform.up, transform.up, transform.right),
                        EffectGroup = m_ThrusterEffects.Build(thrusterDef, mount)
                    });
                    m_Systems.Add(thruster.System);
                }
                else if (definition is ShieldDef shieldDef)
                {
                    var generator = new ShipSystem(shieldDef, mount, transform);
                    ShieldVisual visual = Instantiate(shieldDef.Visual, transform);
                    visual.Fit(hullBounds);
                    foreach (Collider2D shieldCollider in visual.GetComponentsInChildren<Collider2D>())
                        shieldCollider.enabled = false;
                    m_Visuals.Add(visual.transform);
                    m_Shields.Add(new MountedShield
                    {
                        Def = shieldDef,
                        Generator = generator,
                        Charge = new ComponentHealth(shieldDef.ShieldCapacity),
                        Visual = visual
                    });
                    m_Systems.Add(generator);
                }
                else if (definition is SubSystemDef subSystemDef)
                {
                    var subSystem = new SubSystem(subSystemDef, point.transform, transform);
                    m_SubSystems.Add(subSystem);
                    m_Systems.Add(subSystem.System);
                }
                else throw new InvalidOperationException($"Unsupported mounted component {definition.name}.");
            }
            m_Armour = new ComponentHealth(m_HullDef.Armour);
            m_Power = new PowerBank(m_HullDef.MaxPowerStorage + m_SubSystems.Sum(s => s.Def.ExtraPowerStorage));
            m_HullPowerLevel = VitalsMath.PowerSegmentFraction(m_Power.Stored, 0f, m_HullDef.MaxPowerStorage);
            ApplyThrottles();
        }

        private static ThrusterDirection GetThrusterDirection(Vector3 pointUp, Vector3 forward, Vector3 right)
        {
            Vector3 thrust = -pointUp;
            float forwardDot = Vector3.Dot(thrust, forward);
            float reverseDot = Vector3.Dot(thrust, -forward);
            float leftDot = Vector3.Dot(thrust, -right);
            float rightDot = Vector3.Dot(thrust, right);
            float greatest = Mathf.Max(forwardDot, reverseDot, leftDot, rightDot);
            if (greatest < 1e-4f)
                throw new InvalidOperationException("A thruster hull point must have an Up direction in the ship's movement plane.");
            if (forwardDot == greatest) return ThrusterDirection.Forward;
            if (reverseDot == greatest) return ThrusterDirection.Reverse;
            if (leftDot == greatest) return ThrusterDirection.Left;
            return ThrusterDirection.Right;
        }

        private Bounds LocalBounds(Transform root)
        {
            Matrix4x4 toShip = transform.worldToLocalMatrix;
            Bounds result = default;
            bool first = true;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Matrix4x4 matrix = toShip * renderer.localToWorldMatrix;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < BoxCornerCount; corner++)
                {
                    Vector3 offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                    if (first) result = new Bounds(point, Vector3.zero);
                    else result.Encapsulate(point);
                    first = false;
                }
            }
            return result;
        }

        public override void OnStartServer()
        {
            ApplyLoadout();
            m_Body.mass = m_Systems.Sum(system => system.Def.Mass);
            m_Body.angularDamping = m_HullDef.AngularDamping;
            m_Controls.AimPoint = m_Body.position + (Vector2)transform.up;
        }

        protected override void OnPhysicsStep()
        {
            base.OnPhysicsStep();
            if (!isServer || m_Hull.Health.IsDestroyed) return;
            float deltaTime = Time.fixedDeltaTime;
            float frictionScale = GravityMath.FrictionScale(GravityForce / m_Body.mass, GameWorld.Instance.FrictionGravityThreshold);
            m_Body.AddForce(GravityMath.Drag(m_Body.linearVelocity, m_HullDef.Friction * frictionScale));
            StepRotation(deltaTime);
            StepPower(deltaTime);
            StepShields(deltaTime);
            StepRepairs(deltaTime);
            StepSubSystems(deltaTime);
            StepWeapons(deltaTime);
            StepThrusters(deltaTime);
            PublishVitals();
            CheckHull();
        }

        private void StepThrusters(float deltaTime)
        {
            var throttles = new byte[m_Thrusters.Count];
            bool changed = m_Throttles == null || m_Throttles.Length != throttles.Length;
            for (int i = 0; i < m_Thrusters.Count; i++)
            {
                MountedThruster mounted = m_Thrusters[i];
                float demand;
                switch (mounted.Direction)
                {
                    case ThrusterDirection.Forward: demand = m_Controls.Thrust; break;
                    case ThrusterDirection.Reverse: demand = m_Controls.ReverseThrust; break;
                    case ThrusterDirection.Left: demand = Mathf.Max(0f, -m_Controls.SideThrust); break;
                    case ThrusterDirection.Right: demand = Mathf.Max(0f, m_Controls.SideThrust); break;
                    default: throw new InvalidOperationException("Invalid thruster control role.");
                }
                mounted.Thruster.Step(deltaTime, demand, m_Power);
                mounted.Thruster.Apply(m_Body);
                throttles[i] = VitalsMath.ToByte(mounted.Thruster.Throttle);
                if (!changed && throttles[i] != m_Throttles[i]) changed = true;
            }
            if (changed) m_Throttles = throttles;
            if (isClient && changed) ApplyThrottles();
        }

        private void ApplyThrottles()
        {
            if (m_Throttles == null) return;
            if (m_Throttles.Length != m_Thrusters.Count)
            {
                Debug.LogError("Thruster effect state does not match the ship loadout.", this);
                return;
            }
            for (int i = 0; i < m_Thrusters.Count; i++)
                m_ThrusterEffects.SetThrottle(m_Thrusters[i].EffectGroup, VitalsMath.FromByte(m_Throttles[i]));
        }

        private void PublishVitals()
        {
            m_HullPowerLevel = VitalsMath.PowerSegmentFraction(m_Power.Stored, 0f, m_HullDef.MaxPowerStorage);
            var vitals = new ShipVitals
            {
                Armour = VitalsMath.ToByte(m_Armour.Fraction),
                Power = VitalsMath.ToByte(m_Power.Fraction),
                HullPower = VitalsMath.ToByte(m_HullPowerLevel),
                Shields = new byte[m_Shields.Count],
                ShieldStates = new byte[m_Shields.Count],
                SubSystemLevels = new byte[m_SubSystems.Count],
                SubSystemStates = new byte[m_SubSystems.Count]
            };
            for (int i = 0; i < m_Shields.Count; i++)
            {
                MountedShield shield = m_Shields[i];
                shield.HudState = shield.Generator.Health.IsDestroyed ? (byte)3 :
                    shield.Toggled ? (byte)1 : m_Controls.HoldShields ? (byte)2 : (byte)0;
                vitals.Shields[i] = VitalsMath.ToByte(shield.Charge.Fraction);
                vitals.ShieldStates[i] = shield.HudState;
            }
            float lowerCapacity = m_HullDef.MaxPowerStorage;
            for (int i = m_SubSystems.Count - 1; i >= 0; i--)
            {
                SubSystem subSystem = m_SubSystems[i];
                subSystem.RefreshHud();
                float capacity = subSystem.Def.ExtraPowerStorage;
                if (capacity > 0f)
                {
                    subSystem.SetHud(VitalsMath.PowerSegmentFraction(m_Power.Stored, lowerCapacity, capacity), subSystem.HudState);
                    lowerCapacity += capacity;
                }
                vitals.SubSystemLevels[i] = VitalsMath.ToByte(subSystem.HudLevel);
                vitals.SubSystemStates[i] = (byte)subSystem.HudState;
            }
            m_HudSync.Publish(vitals);
        }

        private void Update()
        {
            if (m_Applied && !isServer)
            {
                ApplyThrottles();
                if (isOwned) ApplyVitals(m_HudSync.Vitals);
            }
            foreach (IHudElement element in m_HudElements) element.Render();
            for (int i = 0; i < m_ShieldGauges.Count; i++)
                if (m_ShieldGauges[i] != null)
                    m_ShieldGauges[i].SetColor(m_Shields[i].HudState == 0 || m_Shields[i].HudState == 3
                        ? m_ShieldGaugeColours[i] * 0.4f : m_ShieldGaugeColours[i]);
        }

        private void ApplyVitals(ShipVitals vitals)
        {
            if (vitals.Shields == null || vitals.ShieldStates == null ||
                vitals.SubSystemLevels == null || vitals.SubSystemStates == null) return;
            if (vitals.Shields.Length != m_Shields.Count || vitals.ShieldStates.Length != m_Shields.Count ||
                vitals.SubSystemLevels.Length != m_SubSystems.Count || vitals.SubSystemStates.Length != m_SubSystems.Count)
            {
                Debug.LogError("HUD state does not match the ship loadout.", this);
                return;
            }
            m_Armour.SetFraction(VitalsMath.FromByte(vitals.Armour));
            m_Power.SetFraction(VitalsMath.FromByte(vitals.Power));
            m_HullPowerLevel = VitalsMath.FromByte(vitals.HullPower);
            for (int i = 0; i < m_Shields.Count; i++)
            {
                m_Shields[i].Charge.SetFraction(VitalsMath.FromByte(vitals.Shields[i]));
                m_Shields[i].HudState = vitals.ShieldStates[i];
            }
            for (int i = 0; i < m_SubSystems.Count; i++)
                m_SubSystems[i].SetHud(VitalsMath.FromByte(vitals.SubSystemLevels[i]), (HudState)vitals.SubSystemStates[i]);
        }

        private void BuildHud()
        {
            ShipHudRoot root = ShipHudRoot.Instance;
            if (root == null || m_HudElements.Count > 0) return;
            int barCount = m_Shields.Count + 2;
            AddBar(root, m_HullDef.ArmourBar, 0, barCount, () => m_Armour.Fraction);
            for (int i = 0; i < m_Shields.Count; i++)
            {
                int index = i;
                HudGauge gauge = AddBar(root, m_Shields[i].Def.Bar, i + 1, barCount, () => m_Shields[index].Charge.Fraction);
                m_ShieldGauges.Add(gauge);
                m_ShieldGaugeColours.Add(gauge != null ? gauge.FillColor : Color.white);
                if (gauge != null) AddClickTarget(gauge, () => CmdToggleShield(index));
            }
            AddBar(root, m_HullDef.PowerBar, barCount - 1, barCount, () => m_HullPowerLevel);
            for (int i = 0; i < m_SubSystems.Count; i++)
            {
                int index = i;
                HudGauge prefab = m_SubSystems[i].Def.Gauge;
                if (prefab != null)
                {
                    HudGauge gauge = Instantiate(prefab);
                    root.PlaceSubSystem(gauge.Rect, i);
                    m_HudObjects.Add(gauge.gameObject);
                    m_SubSystems[i].Gauge = gauge;
                    AddClickTarget(gauge, () => CmdPressSubSystem(index));
                }
                m_HudElements.Add(m_SubSystems[i]);
            }
        }

        private HudGauge AddBar(ShipHudRoot root, HudGauge prefab, int row, int bars, Func<float> fraction)
        {
            if (prefab == null) return null;
            HudGauge gauge = Instantiate(prefab);
            root.PlaceBar(gauge.Rect, row, bars, m_SubSystems.Count);
            m_HudObjects.Add(gauge.gameObject);
            m_HudElements.Add(new BarElement(gauge, fraction));
            return gauge;
        }

        private static void AddClickTarget(HudGauge gauge, Action action)
        {
            var target = new GameObject("Activation", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)target.transform;
            rect.SetParent(gauge.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = target.GetComponent<Image>();
            image.color = Color.clear;
            Button button = target.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
        }

        private void DestroyHud()
        {
            foreach (GameObject hudObject in m_HudObjects) if (hudObject != null) Destroy(hudObject);
            m_HudObjects.Clear();
            m_HudElements.Clear();
            m_ShieldGauges.Clear();
            m_ShieldGaugeColours.Clear();
        }

        [Command]
        public void CmdSetControls(ShipControls controls)
        {
            if (!float.IsFinite(controls.AimPoint.x) || !float.IsFinite(controls.AimPoint.y) ||
                !float.IsFinite(controls.Thrust) || !float.IsFinite(controls.ReverseThrust) ||
                !float.IsFinite(controls.SideThrust))
            {
                Debug.LogWarning("Rejected non-finite ship controls.", this);
                return;
            }
            controls.Thrust = Mathf.Clamp01(controls.Thrust);
            controls.ReverseThrust = Mathf.Clamp01(controls.ReverseThrust);
            controls.SideThrust = Mathf.Clamp(controls.SideThrust, -1f, 1f);
            m_Controls = controls;
        }

        [Command]
        public void CmdPressSubSystem(int index)
        {
            if (index >= 0 && index < m_SubSystems.Count) m_SubSystems[index].Activation.Press();
        }

        [Command]
        public void CmdToggleShield(int index)
        {
            if (index >= 0 && index < m_Shields.Count) m_Shields[index].Toggled = !m_Shields[index].Toggled;
        }

        [Command]
        public void CmdToggleShields()
        {
            bool turnOn = m_Shields.Any(shield => !shield.Toggled);
            foreach (MountedShield shield in m_Shields) shield.Toggled = turnOn;
        }

        public HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker)
        {
            if (!isServer || amount <= 0f || m_Hull.Health.IsDestroyed) return HitResult.NoDamage;
            if (attacker != null && attacker != Owner)
            {
                m_LastAttacker = attacker;
                m_LastAttackTime = Time.time;
            }
            float remaining = amount;
            for (int i = 0; i < m_Shields.Count && remaining > 0f; i++)
            {
                MountedShield shield = m_Shields[i];
                if (shield.Generator.Health.IsDestroyed || (!shield.Toggled && !m_Controls.HoldShields)) continue;
                float after = DamageMath.AbsorbShield(remaining, shield.Charge, shield.Def.MaxSingleImpact);
                if (after < remaining) RpcShieldHit(i, shield.Charge.Fraction);
                remaining = after;
            }
            if (remaining <= 0f) return HitResult.ShieldAbsorbed;
            float leftover = DamageMath.AbsorbArmour(remaining, m_Armour, m_HullDef.ArmourWearRatio, m_ArmourStrengthMultiplier);
            if (leftover <= 0f) return HitResult.NoDamage;
            Vector2 localPoint = transform.InverseTransformPoint(point);
            foreach (ShipSystem system in m_Systems)
            {
                float weight = system.Def.TakesFullDamage ? 1f : DamageMath.ProximityWeight(localPoint, system.PointPositions, m_SystemHitRadius);
                system.Health.Damage(leftover * weight);
            }
            CheckHull();
            return HitResult.Damaged;
        }

        [ServerCallback]
        private void OnCollisionEnter2D(Collision2D collision)
        {
            bool isOrbitalBody = collision.collider.GetComponentInParent<OrbitalBody>() != null;
            float multiplier = isOrbitalBody ? m_OrbitalBodyDamageMultiplier : m_CollisionDamageMultiplier;
            ContactPoint2D contact = collision.GetContact(0);
            Ship otherShip = collision.collider.GetComponentInParent<Ship>();
            TakeDamage(DamageMath.Impact(collision.relativeVelocity, contact.normal, multiplier), contact.point, otherShip != null ? otherShip.Owner : null);
        }

        [ClientRpc]
        private void RpcShieldHit(int index, float fraction)
        {
            if (index >= 0 && index < m_Shields.Count) m_Shields[index].Visual.Flash(fraction);
        }

        [ClientRpc]
        private void RpcFired(int index)
        {
            if (index < 0 || index >= m_Guns.Count) return;
            Gun gun = m_Guns[index].Gun;
            Transform point = gun.System.Points[0];
            if (gun.Def.LaunchEffect != null) Instantiate(gun.Def.LaunchEffect, point.position, point.rotation);
            if (gun.Def.LaunchSound != null) AudioSource.PlayClipAtPoint(gun.Def.LaunchSound, point.position);
        }

        private void StepRotation(float deltaTime)
        {
            Vector2 toAim = m_Controls.AimPoint - m_Body.position;
            if (toAim.sqrMagnitude < MinAimDistanceSqr) return;
            float targetAngle = Mathf.Atan2(toAim.y, toAim.x) * Mathf.Rad2Deg - 90f;
            float error = Mathf.DeltaAngle(m_Body.rotation, targetAngle);
            float acceleration = RotationMath.Acceleration(error, m_Body.angularVelocity, m_HullDef.AngularAcceleration, deltaTime);
            m_Body.AddTorque(acceleration * Mathf.Deg2Rad * m_Body.inertia);
        }

        private void StepSubSystems(float deltaTime)
        {
            float fireRateMultiplier = 1f;
            float thrustForceMultiplier = 1f;
            float shieldHealthMultiplier = 1f;
            float armourStrengthMultiplier = 1f;
            foreach (SubSystem subSystem in m_SubSystems)
            {
                if (subSystem.System.Health.IsDestroyed) subSystem.Activation.Stop();
                else subSystem.Activation.Step(deltaTime, m_Power);
                if (!subSystem.IsRunning) continue;
                m_Hull.Health.Repair(subSystem.Def.HullRepairRate * deltaTime);
                fireRateMultiplier *= subSystem.Def.FireRateMultiplier;
                thrustForceMultiplier *= subSystem.Def.ThrustForceMultiplier;
                shieldHealthMultiplier *= subSystem.Def.ShieldHealthMultiplier;
                armourStrengthMultiplier *= subSystem.Def.ArmourStrengthMultiplier;
            }
            foreach (MountedGun gun in m_Guns) gun.Gun.RateMultiplier = fireRateMultiplier;
            foreach (MountedThruster thruster in m_Thrusters) thruster.Thruster.ForceMultiplier = thrustForceMultiplier;
            foreach (MountedShield shield in m_Shields) shield.Charge.SetMax(shield.Def.ShieldCapacity * shieldHealthMultiplier);
            m_ArmourStrengthMultiplier = armourStrengthMultiplier;
        }

        private void StepWeapons(float deltaTime)
        {
            for (int i = 0; i < m_Guns.Count; i++)
            {
                MountedGun mounted = m_Guns[i];
                Gun gun = mounted.Gun;
                gun.Tick(deltaTime);
                if (mounted.Role == GunControlRole.Main ? !m_Controls.FireMain : !m_Controls.FireSecondary) continue;
                Shot[] shots = gun.TryFire(Time.time, m_Power, m_Random);
                if (shots == null) continue;
                foreach (Shot shot in shots)
                {
                    Transform point = gun.System.Points[shot.PointIndex];
                    float angle = Vector2.SignedAngle(Vector2.up, point.up) + shot.Angle;
                    Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                    Bullet bullet = Instantiate(gun.Def.BulletPrefab, point.position, Quaternion.Euler(0f, 0f, angle));
                    bullet.Launch(m_Body.linearVelocity + direction * gun.Def.LaunchSpeed, this);
                    NetworkServer.Spawn(bullet.gameObject);
                }
                RpcFired(i);
            }
        }

        private void StepPower(float deltaTime)
        {
            foreach (ShipSystem system in m_Systems)
                if (!system.Health.IsDestroyed && system.Def.PassivePowerDrain < 0f)
                    m_Power.Charge(-system.Def.PassivePowerDrain * deltaTime);
            foreach (ShipSystem system in m_Systems)
            {
                if (system.Health.IsDestroyed || system.Def.PassivePowerDrain <= 0f) continue;
                float needed = system.Def.PassivePowerDrain * deltaTime;
                system.Health.Damage((needed - m_Power.Draw(needed)) * m_UnpoweredDamagePerPower);
            }
        }

        private void StepShields(float deltaTime)
        {
            foreach (MountedShield shield in m_Shields)
            {
                if (shield.Generator.Health.IsDestroyed)
                {
                    shield.Charge.Damage(shield.Charge.Current);
                    continue;
                }
                if (shield.Charge.Current < shield.Charge.Max && m_Power.TryDraw(shield.Def.ActivePowerDrain * deltaTime))
                    shield.Charge.Repair(shield.Def.RechargeRate * deltaTime);
            }
        }

        private void StepRepairs(float deltaTime)
        {
            m_Armour.Repair(m_HullDef.ArmourRepairRate * deltaTime);
            foreach (ShipSystem system in m_Systems)
                if (!system.Health.IsDestroyed) system.Health.Repair(system.Def.AutoRepairRate * deltaTime);
        }

        private void CheckHull()
        {
            if (m_Exploded || !m_Hull.Health.IsDestroyed) return;
            m_Exploded = true;
            m_Body.simulated = false;
            Destroyed?.Invoke(this);
            RpcExploded();
            Invoke(nameof(Despawn), m_DespawnDelay);
        }

        private void Despawn() => NetworkServer.Destroy(gameObject);

        [ClientRpc]
        private void RpcExploded()
        {
            m_Exploded = true;
            DestroyHud();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (m_ExplosionPrefab != null) Instantiate(m_ExplosionPrefab, transform.position, Quaternion.identity);
        }
    }
}
