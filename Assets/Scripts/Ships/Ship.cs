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
        private const byte ComponentEnabled = 1;
        private const byte ComponentDestroyed = 2;
        private const byte ComponentPowered = 4;
        private const float CargoDropClearance = 0.1f;
        private const float CargoDropSpeed = 1f;
        private const float TractorStartWidth = 0.08f;
        private const float TractorStartWidthFraction = 0.5f;
        private const float DiameterPerRadius = 2f;
        private const float TractorDepthOffset = -0.5f;
        private const string TractorMaterialPath = "Treasure/TractorBeam";
        private static Material s_TractorMaterial;
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
        private ShipSystem[] m_MountSystems;
        private readonly Dictionary<ShipSystem, int> m_StorageSegments = new Dictionary<ShipSystem, int>();
        private readonly List<SubSystem> m_SubSystems = new List<SubSystem>();
        private readonly List<MountedGun> m_Guns = new List<MountedGun>();
        private readonly List<MountedThruster> m_Thrusters = new List<MountedThruster>();
        private readonly List<MountedShield> m_Shields = new List<MountedShield>();
        private readonly Dictionary<int, SubSystem> m_Tractors = new Dictionary<int, SubSystem>();
        private readonly Dictionary<int, LineRenderer> m_TractorLines = new Dictionary<int, LineRenderer>();
        private readonly List<HudGauge> m_ShieldGauges = new List<HudGauge>();
        private readonly List<Color> m_ShieldGaugeColours = new List<Color>();
        private readonly List<Transform> m_Visuals = new List<Transform>();
        private readonly List<IHudElement> m_HudElements = new List<IHudElement>();
        private readonly List<GameObject> m_HudObjects = new List<GameObject>();
        private Vector2 m_ShieldCenter;
        private readonly System.Random m_Random = new System.Random();
        private ShipSystem m_Hull;
        private HullDef m_HullDef;
        private ComponentHealth m_Armour;
        private float m_ArmourStrengthMultiplier = 1f;
        private PowerBank m_Power;
        private ShipCargo m_Cargo;
        private byte[] m_AppliedCargo;
        private bool m_CargoVisualDirty;
        private float m_HullPowerLevel;
        private ShipHudPaperdoll m_Paperdoll;
        private ShipControls m_Controls;
        private ThrusterEffects m_ThrusterEffects;
        private ShipHudSync m_HudSync;
        private bool m_Applied;
        private bool m_Exploded;
        private PlayerState m_LastAttacker;
        private float m_LastAttackTime;

        [SyncVar] private ShipLoadout m_Loadout;
        [SyncVar] private byte[] m_Throttles;
        [SyncVar(hook = nameof(OnHeldShieldLevelsChanged))] private byte[] m_HeldShieldLevels;
        [SyncVar] private uint[] m_TractorTargets;

        public PlayerState Owner { get; private set; }
        public event Action<Ship> Destroyed;
        public IReadOnlyList<Transform> Visuals => m_Visuals;
        public IReadOnlyList<SubSystem> SubSystems => m_SubSystems;
        public bool HasExploded => m_Exploded;
        public int FreeCargo => m_Cargo == null ? 0 : m_Cargo.Free;

        public static int CargoCapacity(ComponentSize size) => size switch
        {
            ComponentSize.Small => ShipCargo.SmallCapacity,
            ComponentSize.Medium => ShipCargo.MediumCapacity,
            ComponentSize.Large => ShipCargo.LargeCapacity,
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };

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
            BuildTractorLines();
            ApplyHeldShieldLevels(m_HeldShieldLevels);
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
            m_ShieldCenter = hullBounds.center;
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.offset = hullBounds.center;
            collider.radius = Mathf.Max(hullBounds.size.x, hullBounds.size.y) * 0.6f;
            m_Hull = new ShipSystem(m_HullDef);
            m_Systems.Add(m_Hull);
            ShipComponentDef[] definitions = catalog.Components;
            m_MountSystems = new ShipSystem[hull.Points.Length];
            var cargoCapacities = new int[hull.Points.Length];
            for (int i = 0; i < hull.Points.Length; i++)
            {
                int component = m_Loadout.MountComponents[i];
                if (component < 0)
                {
                    cargoCapacities[i] = CargoCapacity(hull.Points[i].Size);
                    continue;
                }
                ShipComponentDef definition = definitions[component];
                HullPoint point = hull.Points[i];
                if (definition is GunDef gunDef)
                {
                    var gun = new Gun(gunDef, new ShipSystem(gunDef, point.transform, transform), this);
                    m_Guns.Add(new MountedGun { Gun = gun, Role = point.GunRole });
                    m_Systems.Add(gun.System);
                }
                else if (definition is ThrusterDef thrusterDef)
                {
                    var thruster = new Thruster(thrusterDef, new ShipSystem(thrusterDef, point.transform, transform));
                    m_Thrusters.Add(new MountedThruster
                    {
                        Thruster = thruster,
                        Direction = GetThrusterDirection(point.transform.up, transform.up, transform.right),
                        EffectGroup = m_ThrusterEffects.Build(thrusterDef, new[] { point.transform })
                    });
                    m_Systems.Add(thruster.System);
                }
                else if (definition is ShieldDef shieldDef)
                {
                    var generator = new ShipSystem(shieldDef, point.transform, transform);
                    ShieldVisual visual = Instantiate(shieldDef.Visual, transform);
                    visual.Fit(hullBounds, point.transform, shieldDef);
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
                    ValidateTractor(subSystemDef);
                    if (subSystemDef.TractorRange > 0f) m_Tractors.Add(i, subSystem);
                }
                else throw new InvalidOperationException($"Unsupported mounted component {definition.name}.");
                m_MountSystems[i] = m_Systems[m_Systems.Count - 1];
            }
            m_Cargo = new ShipCargo(cargoCapacities);
            m_Armour = new ComponentHealth(m_HullDef.Armour);
            var capacities = new List<float> { m_HullDef.MaxPowerStorage };
            for (int i = m_SubSystems.Count - 1; i >= 0; i--)
            {
                SubSystem subSystem = m_SubSystems[i];
                if (subSystem.Def.ExtraPowerStorage <= 0f) continue;
                m_StorageSegments.Add(subSystem.System, capacities.Count);
                capacities.Add(subSystem.Def.ExtraPowerStorage);
            }
            m_Power = PowerBank.CreateSegmented(capacities.ToArray());
            m_HullPowerLevel = m_Power.GetSegmentFraction(0);
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
            if (!float.IsFinite(m_HullDef.CargoDropFraction) || m_HullDef.CargoDropFraction < 0f || m_HullDef.CargoDropFraction > 1f)
                throw new InvalidOperationException($"{m_HullDef.name} has an invalid cargo drop fraction.");
            m_HudSync.PublishCargo(m_Cargo.Snapshot());
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
            PublishHeldShieldLevels();
            StepRepairs(deltaTime);
            StepSubSystems(deltaTime);
            StepTractors(deltaTime);
            SyncStorageAvailability();
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

        private void PublishHeldShieldLevels()
        {
            if (!m_Controls.HoldShields)
            {
                if (m_HeldShieldLevels != null) m_HeldShieldLevels = null;
                return;
            }
            byte[] previous = m_HeldShieldLevels;
            byte[] updated = previous == null || previous.Length != m_Shields.Count ? new byte[m_Shields.Count] : null;
            for (int i = 0; i < m_Shields.Count; i++)
            {
                MountedShield shield = m_Shields[i];
                byte level = shield.Generator.Health.IsDestroyed ? (byte)0 :
                    (byte)(1 + Mathf.RoundToInt(Mathf.Clamp01(shield.Charge.Fraction) * 254f));
                if (updated == null && level != previous[i]) updated = (byte[])previous.Clone();
                if (updated != null) updated[i] = level;
            }
            if (updated != null) m_HeldShieldLevels = updated;
        }

        private void OnHeldShieldLevelsChanged(byte[] previous, byte[] current) => ApplyHeldShieldLevels(current);

        private void ApplyHeldShieldLevels(byte[] levels)
        {
            if (!m_Applied) return;
            if (levels != null && levels.Length != m_Shields.Count)
            {
                Debug.LogError("Shield visual state does not match the ship loadout.", this);
                return;
            }
            for (int i = 0; i < m_Shields.Count; i++)
            {
                byte level = levels == null ? (byte)0 : levels[i];
                m_Shields[i].Visual.SetHeld(level != 0, level == 0 ? 0f : (level - 1) / 254f);
            }
        }

        private void PublishVitals()
        {
            SyncStorageAvailability();
            m_HullPowerLevel = m_Power.GetSegmentFraction(0);
            var vitals = new ShipVitals
            {
                Armour = VitalsMath.ToByte(m_Armour.Fraction),
                Power = VitalsMath.ToByte(m_Power.UsableFraction),
                HullPower = VitalsMath.ToByte(m_HullPowerLevel),
                Shields = new byte[m_Shields.Count],
                ShieldStates = new byte[m_Shields.Count],
                SubSystemLevels = new byte[m_SubSystems.Count],
                SubSystemStates = new byte[m_SubSystems.Count],
                ComponentStates = new byte[m_MountSystems.Length]
            };
            for (int i = 0; i < m_MountSystems.Length; i++)
            {
                ShipSystem system = m_MountSystems[i];
                if (system != null)
                    vitals.ComponentStates[i] = (byte)((system.Enabled ? ComponentEnabled : 0) |
                        (system.Health.IsDestroyed ? ComponentDestroyed : 0) | (IsPowered(system) ? ComponentPowered : 0));
            }
            for (int i = 0; i < m_Shields.Count; i++)
            {
                MountedShield shield = m_Shields[i];
                shield.HudState = shield.Generator.Health.IsDestroyed ? (byte)3 :
                    shield.Toggled ? (byte)1 : m_Controls.HoldShields ? (byte)2 : (byte)0;
                vitals.Shields[i] = VitalsMath.ToByte(shield.Charge.Fraction);
                vitals.ShieldStates[i] = shield.HudState;
            }
            for (int i = m_SubSystems.Count - 1; i >= 0; i--)
            {
                SubSystem subSystem = m_SubSystems[i];
                subSystem.RefreshHud();
                if (m_StorageSegments.TryGetValue(subSystem.System, out int segment))
                    subSystem.SetHud(m_Power.GetSegmentFraction(segment), subSystem.HudState);
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
            if (isClient) RenderTractors();
            if (isOwned && m_Cargo != null && m_HudSync.Cargo != null && !ReferenceEquals(m_AppliedCargo, m_HudSync.Cargo))
            {
                m_Cargo.ApplySnapshot(m_HudSync.Cargo);
                m_AppliedCargo = m_HudSync.Cargo;
                m_CargoVisualDirty = true;
            }
            RenderPaperdoll();
            for (int i = 0; i < m_ShieldGauges.Count; i++)
                if (m_ShieldGauges[i] != null)
                    m_ShieldGauges[i].SetColor(m_Shields[i].HudState == 0 || m_Shields[i].HudState == 3
                        ? m_ShieldGaugeColours[i] * 0.4f : m_ShieldGaugeColours[i]);
        }

        private void ApplyVitals(ShipVitals vitals)
        {
            if (vitals.Shields == null || vitals.ShieldStates == null ||
                vitals.SubSystemLevels == null || vitals.SubSystemStates == null || vitals.ComponentStates == null) return;
            if (vitals.Shields.Length != m_Shields.Count || vitals.ShieldStates.Length != m_Shields.Count ||
                vitals.SubSystemLevels.Length != m_SubSystems.Count || vitals.SubSystemStates.Length != m_SubSystems.Count ||
                vitals.ComponentStates.Length != m_MountSystems.Length)
            {
                Debug.LogError("HUD state does not match the ship loadout.", this);
                return;
            }
            m_Armour.SetFraction(VitalsMath.FromByte(vitals.Armour));
            m_HullPowerLevel = VitalsMath.FromByte(vitals.HullPower);
            for (int i = 0; i < m_Shields.Count; i++)
            {
                m_Shields[i].Charge.SetFraction(VitalsMath.FromByte(vitals.Shields[i]));
                m_Shields[i].HudState = vitals.ShieldStates[i];
            }
            for (int i = 0; i < m_SubSystems.Count; i++)
                m_SubSystems[i].SetHud(VitalsMath.FromByte(vitals.SubSystemLevels[i]), (HudState)vitals.SubSystemStates[i]);
            for (int i = 0; i < m_MountSystems.Length; i++)
                if (m_MountSystems[i] != null)
                    m_MountSystems[i].Enabled = (vitals.ComponentStates[i] & ComponentEnabled) != 0;
        }

        private void RenderPaperdoll()
        {
            if (m_Paperdoll == null) return;
            ShipVitals vitals = m_HudSync.Vitals;
            if (m_CargoVisualDirty)
            {
                m_Paperdoll.SetCargo(m_Cargo);
                m_CargoVisualDirty = false;
            }
            if (vitals.ComponentStates == null || vitals.ComponentStates.Length != m_MountSystems.Length) return;
            for (int i = 0; i < m_MountSystems.Length; i++)
            {
                if (m_MountSystems[i] == null) continue;
                byte state = vitals.ComponentStates[i];
                m_Paperdoll.SetMountState(i, (state & ComponentPowered) != 0, (state & ComponentDestroyed) != 0);
            }
        }

        private void BuildHud()
        {
            ShipHudRoot root = ShipHudRoot.Instance;
            if (root == null || m_HudElements.Count > 0) return;
            var paperdollObject = new GameObject("Ship paperdoll", typeof(RectTransform), typeof(ShipHudPaperdoll));
            m_HudObjects.Add(paperdollObject);
            root.PlacePaperdoll((RectTransform)paperdollObject.transform);
            m_Paperdoll = paperdollObject.GetComponent<ShipHudPaperdoll>();
            m_Paperdoll.Initialize(m_Loadout, CmdToggleComponent, root.PaperdollSize,
                1f - root.PaperdollBrightness, root.PaperdollMarkerScale);
            m_CargoVisualDirty = true;
            foreach (SubSystem subSystem in m_SubSystems)
            {
                int mount = Array.IndexOf(m_MountSystems, subSystem.System);
                subSystem.Gauge = m_Paperdoll.GetMountGauge(mount);
            }
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
            foreach (SubSystem subSystem in m_SubSystems)
                m_HudElements.Add(subSystem);
        }

        private HudGauge AddBar(ShipHudRoot root, HudGauge prefab, int row, int bars, Func<float> fraction)
        {
            if (prefab == null) return null;
            HudGauge gauge = Instantiate(prefab);
            root.PlaceBar(gauge.Rect, row, bars);
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
            if (m_Paperdoll != null) m_Paperdoll.Dispose();
            m_Paperdoll = null;
            foreach (SubSystem subSystem in m_SubSystems) subSystem.Gauge = null;
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
            PublishHeldShieldLevels();
        }

        [Command]
        public void CmdPressSubSystem(int index)
        {
            if (index >= 0 && index < m_SubSystems.Count) ToggleSystem(m_SubSystems[index].System);
        }

        public void PressComponentShortcut(int shortcut)
        {
            int mount = LoadoutMath.OccupiedMountAt(m_Loadout.MountComponents, shortcut);
            if (mount >= 0) CmdToggleComponent(mount);
        }

        [Command]
        public void CmdToggleComponent(int mount)
        {
            if (m_MountSystems == null || mount < 0 || mount >= m_MountSystems.Length ||
                m_MountSystems[mount] == null)
            {
                Debug.LogWarning($"Rejected invalid component mount {mount}.", this);
                return;
            }
            ToggleSystem(m_MountSystems[mount]);
        }

        private void ToggleSystem(ShipSystem system)
        {
            if (system.Health.IsDestroyed) return;
            foreach (MountedShield shield in m_Shields)
            {
                if (shield.Generator != system) continue;
                shield.Toggled = !shield.Toggled;
                system.Enabled = shield.Toggled;
                return;
            }
            foreach (SubSystem subSystem in m_SubSystems)
            {
                if (subSystem.System != system) continue;
                subSystem.TogglePower();
                SyncStorageAvailability();
                return;
            }
            system.Enabled = !system.Enabled;
        }

        [Command]
        public void CmdToggleShield(int index)
        {
            if (index >= 0 && index < m_Shields.Count) ToggleSystem(m_Shields[index].Generator);
        }

        [Command]
        public void CmdToggleShields()
        {
            bool turnOn = m_Shields.Any(shield => !shield.Generator.Health.IsDestroyed && !shield.Toggled);
            foreach (MountedShield shield in m_Shields)
            {
                if (shield.Generator.Health.IsDestroyed) continue;
                shield.Toggled = turnOn;
                shield.Generator.Enabled = turnOn;
            }
        }

        public HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker)
        {
            if (!isServer || amount <= 0f || m_Hull.Health.IsDestroyed) return HitResult.NoDamage;
            AttributeDamage(attacker);
            float remaining = AbsorbShields(amount, point);
            if (remaining <= 0f) return HitResult.ShieldAbsorbed;
            float leftover = DamageMath.AbsorbArmour(remaining, m_Armour, m_HullDef.ArmourWearRatio, m_ArmourStrengthMultiplier);
            if (leftover <= 0f) return HitResult.NoDamage;
            DamageSystems(leftover, point);
            return HitResult.Damaged;
        }

        public HitResult TakeProjectileDamage(ProjectileDamage damage, Vector2 point, PlayerState attacker)
        {
            if (!isServer || m_Hull.Health.IsDestroyed) return HitResult.NoDamage;
            if (!damage.IsValid || !float.IsFinite(point.x) || !float.IsFinite(point.y))
            {
                Debug.LogError("Projectile damage requires finite nonnegative ratings and finite impact coordinates.", this);
                return HitResult.NoDamage;
            }
            float fraction = 1f;
            bool changed = false;
            if (damage.Shield > 0f)
            {
                float residual = AbsorbShields(damage.Shield, point);
                fraction = residual / damage.Shield;
                changed = residual < damage.Shield;
                if (fraction <= 0f)
                {
                    if (changed) AttributeDamage(attacker);
                    return HitResult.ShieldAbsorbed;
                }
            }
            if (damage.Armour > 0f)
            {
                float incoming = damage.Armour * fraction;
                float residual = DamageMath.AbsorbProjectileArmour(incoming, m_Armour, m_ArmourStrengthMultiplier);
                changed |= residual < incoming;
                fraction = residual / damage.Armour;
            }
            if (damage.HullComponents * fraction > 0f)
                changed |= DamageSystems(damage.HullComponents * fraction, point, attacker);
            if (!changed) return HitResult.NoDamage;
            AttributeDamage(attacker);
            return HitResult.Damaged;
        }

        private void AttributeDamage(PlayerState attacker)
        {
            if (attacker != null && attacker != Owner)
            {
                m_LastAttacker = attacker;
                m_LastAttackTime = Time.time;
            }
        }

        private float AbsorbShields(float remaining, Vector2 point)
        {
            Vector2 shieldOffset = point - (Vector2)transform.TransformPoint(m_ShieldCenter);
            for (int i = 0; i < m_Shields.Count && remaining > 0f; i++)
            {
                MountedShield shield = m_Shields[i];
                if (shield.Generator.Health.IsDestroyed || (!shield.Toggled && !m_Controls.HoldShields)) continue;
                if (!DamageMath.ShieldCovers(shield.Generator.Point.up, shieldOffset, shield.Def.HalfAngle)) continue;
                float after = DamageMath.AbsorbShield(remaining, shield.Charge, shield.Def.MaxSingleImpact);
                if (after < remaining) RpcShieldHit(i, shield.Charge.Fraction);
                remaining = after;
            }
            return remaining;
        }

        private bool DamageSystems(float amount, Vector2 point, PlayerState attacker = null)
        {
            bool changed = false;
            Vector2 localPoint = transform.InverseTransformPoint(point);
            foreach (ShipSystem system in m_Systems)
            {
                float weight = system.Def.TakesFullDamage ? 1f : DamageMath.ProximityWeight(localPoint, system.PointPosition, m_SystemHitRadius);
                float before = system.Health.Current;
                system.Health.Damage(amount * weight);
                changed |= system.Health.Current < before;
            }
            if (changed) AttributeDamage(attacker);
            CheckHull();
            return changed;
        }

        [ServerCallback]
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.GetComponentInParent<TreasurePickup>() != null) return;
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
            Transform point = gun.System.Point;
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
                subSystem.Step(deltaTime, m_Power);
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

        private static void ValidateTractor(SubSystemDef def)
        {
            if (!float.IsFinite(def.TractorRange) || def.TractorRange < 0f ||
                !float.IsFinite(def.TractorHalfAngle) || def.TractorHalfAngle < 0f || def.TractorHalfAngle > 180f ||
                !float.IsFinite(def.TractorForce) || def.TractorForce < 0f ||
                (def.TractorRange > 0f && (def.TractorForce <= 0f || def.Mode != ActivationMode.Passive ||
                    !float.IsFinite(def.ActivePowerDrain) || def.ActivePowerDrain < 0f)))
                throw new InvalidOperationException($"{def.name} has invalid automatic tractor settings.");
        }

        private void StepTractors(float deltaTime)
        {
            if (m_Tractors.Count == 0) return;
            var targets = new uint[m_MountSystems.Length];
            foreach (KeyValuePair<int, SubSystem> entry in m_Tractors)
            {
                SubSystem system = entry.Value;
                if (!system.IsRunning) continue;
                TreasurePickup target = FindTractorTarget(system);
                if (target == null || !m_Power.TryDraw(system.Def.ActivePowerDrain * deltaTime)) continue;
                Vector2 pull = (Vector2)system.System.Point.position - (Vector2)target.transform.position;
                if (pull.sqrMagnitude <= 0f) continue;
                target.Pull(pull.normalized * system.Def.TractorForce);
                targets[entry.Key] = target.netId;
            }
            if (m_TractorTargets == null || !m_TractorTargets.SequenceEqual(targets)) m_TractorTargets = targets;
        }

        private TreasurePickup FindTractorTarget(SubSystem system)
        {
            Transform mount = system.System.Point;
            Vector2 origin = mount.position;
            Vector2 forward = mount.up;
            float rangeSquared = system.Def.TractorRange * system.Def.TractorRange;
            float minimumDot = Mathf.Cos(system.Def.TractorHalfAngle * Mathf.Deg2Rad);
            int free = FreeCargo;
            if (free == 0) return null;
            float nearest = float.PositiveInfinity;
            TreasurePickup best = null;
            foreach (SpaceObject spaceObject in SpaceObject.ActiveObjects)
            {
                if (!(spaceObject is TreasurePickup pickup) || !pickup.isServer || !pickup.IsAvailable ||
                    ShipCargo.Units(pickup.Size) > free) continue;
                Vector2 delta = (Vector2)pickup.transform.position - origin;
                float distance = delta.sqrMagnitude;
                if (distance <= 0f || distance > rangeSquared || distance > nearest ||
                    Vector2.Dot(forward, delta.normalized) < minimumDot) continue;
                if (distance == nearest && best != null && pickup.netId >= best.netId) continue;
                if (TractorBlocked(origin, delta, Mathf.Sqrt(distance))) continue;
                nearest = distance;
                best = pickup;
            }
            return best;
        }

        private bool TractorBlocked(Vector2 origin, Vector2 delta, float distance)
        {
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, delta / distance, distance))
            {
                Collider2D collider = hit.collider;
                if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform) ||
                    collider.GetComponentInParent<TreasurePickup>() != null) continue;
                Ship ship = collider.GetComponentInParent<Ship>();
                if ((ship != null && ship != this) || collider.GetComponentInParent<Asteroid>() != null ||
                    collider.GetComponentInParent<OrbitalBody>() != null) return true;
            }
            return false;
        }

        private void BuildTractorLines()
        {
            if (m_Tractors.Count == 0 || m_TractorLines.Count > 0) return;
            if (s_TractorMaterial == null)
            {
                s_TractorMaterial = Resources.Load<Material>(TractorMaterialPath);
                if (s_TractorMaterial == null) throw new InvalidOperationException($"Missing tractor material at Resources/{TractorMaterialPath}.");
            }
            foreach (KeyValuePair<int, SubSystem> entry in m_Tractors)
            {
                var lineObject = new GameObject("Tractor beam " + entry.Key);
                lineObject.transform.SetParent(transform, false);
                lineObject.layer = gameObject.layer;
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = s_TractorMaterial;
                line.renderingLayerMask = TreasureVisual.RenderingLayers;
                line.startWidth = TractorStartWidth;
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.enabled = false;
                m_TractorLines.Add(entry.Key, line);
            }
        }

        private void RenderTractors()
        {
            foreach (KeyValuePair<int, LineRenderer> entry in m_TractorLines)
            {
                LineRenderer line = entry.Value;
                uint target = m_TractorTargets != null && entry.Key < m_TractorTargets.Length ? m_TractorTargets[entry.Key] : 0;
                if (target == 0 || !NetworkClient.spawned.TryGetValue(target, out NetworkIdentity identity) || identity == null)
                {
                    line.enabled = false;
                    continue;
                }
                if (!identity.TryGetComponent(out TreasurePickup pickup))
                {
                    line.enabled = false;
                    Debug.LogError("Tractor target is not a treasure pickup.", identity);
                    continue;
                }
                Vector3 scale = pickup.transform.lossyScale;
                float diameter = pickup.Radius * DiameterPerRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                line.startWidth = Mathf.Min(TractorStartWidth, diameter * TractorStartWidthFraction);
                line.endWidth = diameter;
                Vector3 start = m_Tractors[entry.Key].System.Point.position;
                Vector3 end = identity.transform.position;
                start.z += TractorDepthOffset;
                end.z += TractorDepthOffset;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
                line.enabled = true;
            }
        }

        private void StepWeapons(float deltaTime)
        {
            for (int i = 0; i < m_Guns.Count; i++)
            {
                MountedGun mounted = m_Guns[i];
                Gun gun = mounted.Gun;
                bool fire = mounted.Role == GunControlRole.Main ? m_Controls.FireMain : m_Controls.FireSecondary;
                if (gun.Step(deltaTime, gun.System.Point, m_Body.linearVelocity, m_Power, fire, m_Random))
                    RpcFired(i);
            }
        }

        private void StepPower(float deltaTime)
        {
            SyncStorageAvailability();
            foreach (ShipSystem system in m_Systems)
                if (IsPowered(system) && system.Def.PassivePowerDrain < 0f)
                    m_Power.Charge(-system.Def.PassivePowerDrain * deltaTime);
            foreach (ShipSystem system in m_Systems)
            {
                if (!IsPowered(system) || system.Def.PassivePowerDrain <= 0f) continue;
                float needed = system.Def.PassivePowerDrain * deltaTime;
                system.Health.Damage((needed - m_Power.Draw(needed)) * m_UnpoweredDamagePerPower);
            }
        }

        private void SyncStorageAvailability()
        {
            foreach (KeyValuePair<ShipSystem, int> storage in m_StorageSegments)
                m_Power.SetSegmentEnabled(storage.Value, storage.Key.Enabled && !storage.Key.Health.IsDestroyed);
        }

        private bool IsPowered(ShipSystem system)
        {
            if (system.Health.IsDestroyed) return false;
            if (system.Enabled) return true;
            foreach (MountedShield shield in m_Shields)
                if (shield.Generator == system) return m_Controls.HoldShields;
            return false;
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
                if (!IsPowered(shield.Generator)) continue;
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
            if (m_Tractors.Count > 0) m_TractorTargets = new uint[m_MountSystems.Length];
            DropCargo();
            m_Body.simulated = false;
            Destroyed?.Invoke(this);
            RpcExploded();
            Invoke(nameof(Despawn), m_DespawnDelay);
        }

        [Server]
        internal bool TryCollect(TreasurePickup pickup)
        {
            if (pickup == null) throw new ArgumentNullException(nameof(pickup));
            if (m_Exploded || m_Hull == null || m_Hull.Health.IsDestroyed || !pickup.IsAvailable) return false;
            if (!m_Cargo.TryAdd(pickup.Material, pickup.Size)) return false;
            m_HudSync.PublishCargo(m_Cargo.Snapshot());
            return true;
        }

        private void DropCargo()
        {
            var collider = GetComponent<CircleCollider2D>();
            if (collider == null) throw new InvalidOperationException("Ship requires a circle collider to drop cargo.");
            Vector2 centre = transform.TransformPoint(collider.offset);
            for (int material = 0; material < ShipCargo.MaterialCount; material++)
            {
                var kind = (TreasureMaterial)material;
                int remaining = ShipCargo.DropUnits(m_Cargo.Total(kind), m_HullDef.CargoDropFraction);
                while (remaining > 0)
                {
                    int units = m_Random.Next(1, Math.Min(remaining, ShipCargo.Units(TreasureSize.Large)) + 1);
                    TreasureSize size = (TreasureSize)(units - 1);
                    float radius = AsteroidTreasure.Radius(size);
                    float angle = (float)m_Random.NextDouble() * Mathf.PI * 2f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 position = centre + direction * (collider.radius + radius + CargoDropClearance);
                    var deposit = new TreasureDeposit { Material = kind, Size = size, Radius = radius };
                    TreasurePickup.Spawn(deposit, position, transform.rotation,
                        m_Body.GetPointVelocity(position) + direction * CargoDropSpeed, m_Body.angularVelocity);
                    remaining -= units;
                }
            }
            m_Cargo.Clear();
            m_HudSync.PublishCargo(m_Cargo.Snapshot());
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
