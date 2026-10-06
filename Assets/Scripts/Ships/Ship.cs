using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using PewPewPew.Core;
using PewPewPew.Match;
using PewPewPew.Weapons;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Server-authoritative ship: mass, power, shield/armour/hull damage, weapons, thrust and collisions.
    /// Ships feel gravity but create none. Ship forward is local +Y. Components come from the synced loadout.
    [RequireComponent(typeof(ThrusterEffects), typeof(ShipHudSync))]
    public class Ship : SpaceObject, IDamageable
    {
        // Aim points closer than this (squared) to the ship are ignored so the ship doesn't jitter.
        private const float MinAimDistanceSqr = 1e-4f;
        private const int BoxCornerCount = 8;
        [SerializeField] private GameObject m_ExplosionPrefab;
        [SerializeField, Tooltip("Keeps the object alive briefly after the hull fails so clients receive the explosion.")] private float m_DespawnDelay = 0.3f;
        [SerializeField] private float m_SystemHitRadius = 2f;
        [SerializeField, Tooltip("Damage taken per unit of passive power a system could not get.")] private float m_UnpoweredDamagePerPower = 1f;
        [SerializeField] private float m_CollisionDamageMultiplier = 1f;
        [SerializeField] private float m_OrbitalBodyDamageMultiplier = 10f;

        private readonly List<ShipSystem> m_Systems = new List<ShipSystem>();
        private readonly List<SubSystem> m_SubSystems = new List<SubSystem>();
        private ShipSystem m_Hull;
        private ShipSystem m_ShieldGenerator;
        private ComponentHealth m_Shield;
        private ComponentHealth m_Armour;
        private float m_ArmourStrengthMultiplier = 1f;
        private PowerBank m_Power;
        private Gun m_MainGun;
        private Gun m_SecondaryGun;
        private Thruster m_Thruster;
        private ShipControls m_Controls;
        private readonly System.Random m_Random = new System.Random();

        private HullDef m_HullDef;
        private ShieldDef m_ShieldDef;
        private GunDef m_MainGunDef;
        private GunDef m_SecondaryGunDef;
        private ThrusterDef m_ThrusterDef;
        private readonly List<SubSystemDef> m_SubSystemDefs = new List<SubSystemDef>();
        private readonly List<int> m_SubSystemSlots = new List<int>();
        private Transform[] m_SubSystemPoints = Array.Empty<Transform>();
        private bool m_Applied;
        private readonly List<Transform> m_Visuals = new List<Transform>();
        private ShieldVisual m_ShieldVisual;
        private ThrusterEffects m_ThrusterEffects;
        private ShipHudSync m_HudSync;
        private readonly List<IHudElement> m_HudElements = new List<IHudElement>();
        private readonly List<GameObject> m_HudObjects = new List<GameObject>();
        private Transform[] m_MainPoints;
        private Transform[] m_SecondaryPoints;
        private Transform[] m_ThrusterPoints;
        private bool m_Exploded;
        private PlayerState m_LastAttacker;
        private float m_LastAttackTime;

        [SyncVar] private ShipLoadout m_Loadout;
        [SyncVar(hook = nameof(OnThrottleChanged))] private byte m_Throttle;

        /// Server only. The player this ship belongs to.
        public PlayerState Owner { get; private set; }

        /// Server only. Raised once when the hull reaches zero health.
        public event Action<Ship> Destroyed;

        /// Instantiated visuals (hull first, then shield); empty until the loadout is applied.
        public IReadOnlyList<Transform> Visuals => m_Visuals;
        public IReadOnlyList<SubSystem> SubSystems => m_SubSystems;
        public bool HasExploded => m_Exploded;

        /// Server only, before the ship is spawned. The loadout must already be validated.
        public void Initialize(ShipLoadout loadout, PlayerState owner)
        {
            m_Loadout = loadout;
            Owner = owner;
        }

        /// Server only. The player who last damaged this ship within the window, if any.
        public PlayerState KillCredit(float windowSeconds)
        {
            return m_LastAttacker != null && Time.time - m_LastAttackTime <= windowSeconds ? m_LastAttacker : null;
        }

        protected override void Awake()
        {
            base.Awake();
            m_ThrusterEffects = GetComponent<ThrusterEffects>();
            m_HudSync = GetComponent<ShipHudSync>();
        }

        private void OnDestroy() => DestroyHud();

        public override void OnStartAuthority() => BuildHud();

        public override void OnStopAuthority() => DestroyHud();

        public override void OnStartClient() => ApplyLoadout();

        // Runs on the server and every client: resolves the loadout's definitions and the visuals that depend on them.
        private void ApplyLoadout()
        {
            if (m_Applied) return;
            m_Applied = true;

            ShipCatalog catalog = ShipCatalog.Instance;
            m_HullDef = catalog.Hulls[m_Loadout.Hull];
            m_ShieldDef = catalog.Shields[m_Loadout.Shield];
            m_MainGunDef = catalog.MainGuns[m_Loadout.MainGun];
            m_SecondaryGunDef = catalog.SecondaryGuns[m_Loadout.SecondaryGun];
            m_ThrusterDef = catalog.Thrusters[m_Loadout.Thruster];
            for (int slot = 0; slot < Mathf.Min(ShipLoadout.SubSystemSlots, m_HullDef.SubSystemSlots); slot++)
            {
                int index = m_Loadout.GetSubSystem(slot);
                if (index >= 0)
                {
                    m_SubSystemDefs.Add(catalog.SubSystems[index]);
                    m_SubSystemSlots.Add(slot);
                }
            }

            Bounds hullBounds = default;
            if (m_HullDef.HullPoints != null)
            {
                HullPoints hull = Instantiate(m_HullDef.HullPoints, transform);
                ShipColours.Apply(hull.gameObject, m_Loadout);
                m_MainPoints = BindPoints(m_HullDef.MainWeaponPoints, m_MainGunDef.MaxEmissionPointsConsidered, hull.transform);
                m_SecondaryPoints = BindPoints(m_HullDef.SecondaryWeaponPoints, m_SecondaryGunDef.MaxEmissionPointsConsidered, hull.transform);
                m_ThrusterPoints = BindPoints(m_HullDef.ThrusterPoints, m_ThrusterDef.EmissionPointsUsed, hull.transform);
                Transform[] mounts = m_HullDef.HullPoints.SubSystems;
                m_SubSystemPoints = new Transform[m_SubSystemDefs.Count];
                for (int index = 0; index < m_SubSystemPoints.Length; index++)
                {
                    int slot = m_SubSystemSlots[index];
                    if (mounts == null || slot >= mounts.Length || mounts[slot] == null) continue;
                    Transform[] bound = m_HullDef.Bind(new[] { mounts[slot] }, hull.transform);
                    if (bound.Length > 0) m_SubSystemPoints[index] = bound[0];
                }
                hullBounds = LocalBounds(hull.transform);
                m_Visuals.Add(hull.transform);
            }
            else
            {
                m_MainPoints = m_SecondaryPoints = m_ThrusterPoints = Array.Empty<Transform>();
            }
            m_ShieldVisual = Instantiate(m_ShieldDef.Visual, transform);
            m_ShieldVisual.Fit(hullBounds);
            m_Visuals.Add(m_ShieldVisual.transform);
            m_ThrusterEffects.Build(m_ThrusterDef, m_ThrusterPoints);
            m_ThrusterEffects.SetThrottle(VitalsMath.FromByte(m_Throttle));
            BuildSystems();
        }

        // Built on every client too so the owner's HUD can mirror the synced state; only the server steps them.
        private void BuildSystems()
        {
            m_Hull = new ShipSystem(m_HullDef, Array.Empty<Transform>());
            m_Systems.Add(m_Hull);
            m_ShieldGenerator = new ShipSystem(m_ShieldDef, Array.Empty<Transform>());
            m_Systems.Add(m_ShieldGenerator);
            m_MainGun = new Gun(m_MainGunDef, new ShipSystem(m_MainGunDef, m_MainPoints, transform));
            m_SecondaryGun = new Gun(m_SecondaryGunDef, new ShipSystem(m_SecondaryGunDef, m_SecondaryPoints, transform));
            m_Thruster = new Thruster(m_ThrusterDef, new ShipSystem(m_ThrusterDef, m_ThrusterPoints, transform));
            m_Systems.AddRange(new[] { m_MainGun.System, m_SecondaryGun.System, m_Thruster.System });

            for (int i = 0; i < m_SubSystemDefs.Count; i++)
            {
                Transform mount = i < m_SubSystemPoints.Length ? m_SubSystemPoints[i] : null;
                var subSystem = new SubSystem(m_SubSystemDefs[i], mount, transform);
                m_SubSystems.Add(subSystem);
                m_Systems.Add(subSystem.System);
            }

            m_Shield = new ComponentHealth(m_ShieldDef.ShieldCapacity);
            m_Armour = new ComponentHealth(m_HullDef.Armour);
            m_Power = new PowerBank(m_HullDef.MaxPowerStorage + m_SubSystems.Sum(s => s.Def.ExtraPowerStorage));
        }

        // The hull's points for a component, resolved to this ship's own hull instance.
        private Transform[] BindPoints(Transform[] prefabPoints, int needed, Transform hullRoot)
        {
            Transform[] selected = m_HullDef.SelectPoints(prefabPoints, needed);
            return selected == null ? Array.Empty<Transform>() : m_HullDef.Bind(selected, hullRoot);
        }

        // Combined bounds of every renderer under root, in this ship's local space.
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
                    Vector3 offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
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
            m_Body.AddForce(GravityMath.Drag(m_Body.linearVelocity, m_HullDef.Friction));
            StepRotation(deltaTime);
            StepPower(deltaTime);
            StepShield(deltaTime);
            StepRepairs(deltaTime);
            StepSubSystems(deltaTime);
            StepWeapons(deltaTime);
            m_Thruster.Step(deltaTime, m_Controls.Thrust, m_Power);
            m_Thruster.Apply(m_Body);
            SetThrottle(m_Thruster.Throttle);
            PublishVitals();
            CheckHull();
        }

        private void PublishVitals()
        {
            var vitals = new ShipVitals
            {
                Armour = VitalsMath.ToByte(m_Armour.Fraction),
                Shield = VitalsMath.ToByte(m_Shield.Fraction),
                Power = VitalsMath.ToByte(m_Power.Fraction),
            };
            for (int i = 0; i < m_SubSystems.Count; i++)
            {
                m_SubSystems[i].RefreshHud();
                vitals.SetSubSystem(i, m_SubSystems[i].HudLevel, m_SubSystems[i].HudState);
            }
            m_HudSync.Publish(vitals);
        }

        private void Update()
        {
            if (m_Applied && !isServer && isOwned) ApplyVitals(m_HudSync.Vitals);
            if (m_HudElements.Count == 0) return;

            foreach (IHudElement element in m_HudElements) element.Render();
        }

        private void ApplyVitals(ShipVitals vitals)
        {
            m_Armour.SetFraction(VitalsMath.FromByte(vitals.Armour));
            m_Shield.SetFraction(VitalsMath.FromByte(vitals.Shield));
            m_Power.SetFraction(VitalsMath.FromByte(vitals.Power));
            for (int i = 0; i < m_SubSystems.Count; i++)
            {
                m_SubSystems[i].SetHud(VitalsMath.FromByte(vitals.GetLevel(i)), vitals.GetState(i));
            }
        }

        // Rows top to bottom: armour, shield, power, weapons, sub systems.
        private void BuildHud()
        {
            ShipHudRoot root = ShipHudRoot.Instance;
            if (root == null || m_HudElements.Count > 0) return;

            AddBar(root, m_HullDef.ArmourBar, 0, () => m_Armour.Fraction);
            AddBar(root, m_ShieldDef.Bar, 1, () => m_Shield.Fraction);
            AddBar(root, m_HullDef.PowerBar, 2, () => m_Power.Fraction);
            m_HudElements.Add(m_MainGun);
            m_HudElements.Add(m_SecondaryGun);
            for (int i = 0; i < m_SubSystems.Count; i++)
            {
                m_SubSystems[i].Gauge = Spawn(root, m_SubSystems[i].Def.Gauge, ShipHudRoot.SubSystemRow, m_SubSystemSlots[i], ShipLoadout.SubSystemSlots);
                m_HudElements.Add(m_SubSystems[i]);
            }
        }

        private void AddBar(ShipHudRoot root, HudGauge prefab, int row, Func<float> fraction)
        {
            HudGauge gauge = Spawn(root, prefab, row, 0, 1);
            if (gauge != null) m_HudElements.Add(new BarElement(gauge, fraction));
        }

        private HudGauge Spawn(ShipHudRoot root, HudGauge prefab, int row, int column, int columns)
        {
            if (prefab == null) return null;

            HudGauge gauge = Instantiate(prefab);
            root.Place(gauge.Rect, row, column, columns);
            m_HudObjects.Add(gauge.gameObject);
            return gauge;
        }

        private void DestroyHud()
        {
            foreach (GameObject hudObject in m_HudObjects)
            {
                if (hudObject != null) Destroy(hudObject);
            }
            m_HudObjects.Clear();
            m_HudElements.Clear();
        }

        [Command]
        public void CmdSetControls(ShipControls controls)
        {
            if (!float.IsFinite(controls.AimPoint.x) || !float.IsFinite(controls.AimPoint.y)) return;

            controls.Thrust = Mathf.Clamp01(controls.Thrust);
            m_Controls = controls;
        }

        [Command]
        public void CmdPressSubSystem(int index)
        {
            int installed = m_SubSystemSlots.IndexOf(index);
            if (installed >= 0) m_SubSystems[installed].Activation.Press();
        }

        public HitResult TakeDamage(float amount, Vector2 point, PlayerState attacker)
        {
            if (!isServer || amount <= 0f || m_Hull.Health.IsDestroyed) return HitResult.NoDamage;

            if (attacker != null && attacker != Owner)
            {
                m_LastAttacker = attacker;
                m_LastAttackTime = Time.time;
            }

            float afterShield = DamageMath.AbsorbShield(amount, m_Shield, m_ShieldDef.MaxSingleImpact);
            if (afterShield < amount) RpcShieldHit(m_Shield.Fraction);
            if (afterShield <= 0f) return HitResult.ShieldAbsorbed;

            float leftover = DamageMath.AbsorbArmour(afterShield, m_Armour, m_HullDef.ArmourWearRatio, m_ArmourStrengthMultiplier);
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
        private void RpcShieldHit(float healthFraction) => m_ShieldVisual.Flash(healthFraction);

        [ClientRpc]
        private void RpcFired(bool secondary, byte pointMask)
        {
            GunDef def = secondary ? m_SecondaryGunDef : m_MainGunDef;
            Transform[] points = secondary ? m_SecondaryPoints : m_MainPoints;
            for (int i = 0; i < points.Length; i++)
            {
                if ((pointMask & (1 << i)) == 0) continue;

                Vector3 position = points[i].position;
                if (def.LaunchEffect != null) Instantiate(def.LaunchEffect, position, points[i].rotation);
                if (def.LaunchSound != null) AudioSource.PlayClipAtPoint(def.LaunchSound, position);
            }
        }

        // The hook only runs on remote clients, so the host updates its own effects directly.
        private void SetThrottle(float throttle)
        {
            byte quantised = VitalsMath.ToByte(throttle);
            if (m_Throttle == quantised) return;

            m_Throttle = quantised;
            if (isClient) m_ThrusterEffects.SetThrottle(VitalsMath.FromByte(quantised));
        }

        private void OnThrottleChanged(byte oldValue, byte newValue) => m_ThrusterEffects.SetThrottle(VitalsMath.FromByte(newValue));

        private void StepRotation(float deltaTime)
        {
            Vector2 toAim = m_Controls.AimPoint - m_Body.position;
            if (toAim.sqrMagnitude < MinAimDistanceSqr) return;

            float targetAngle = Mathf.Atan2(toAim.y, toAim.x) * Mathf.Rad2Deg - 90f; // Forward is +Y.
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
            m_MainGun.RateMultiplier = fireRateMultiplier;
            m_SecondaryGun.RateMultiplier = fireRateMultiplier;
            m_Thruster.ForceMultiplier = thrustForceMultiplier;
            m_Shield.SetMax(m_ShieldDef.ShieldCapacity * shieldHealthMultiplier);
            m_ArmourStrengthMultiplier = armourStrengthMultiplier;
        }

        private void StepWeapons(float deltaTime)
        {
            m_MainGun.Tick(deltaTime);
            m_SecondaryGun.Tick(deltaTime);
            if (m_Controls.FireMain) Fire(m_MainGun, false);
            if (m_Controls.FireSecondary) Fire(m_SecondaryGun, true);
        }

        private void Fire(Gun gun, bool secondary)
        {
            Shot[] shots = gun.TryFire(Time.time, m_Power, m_Random);
            if (shots == null) return;

            byte pointMask = 0;
            foreach (Shot shot in shots)
            {
                Transform point = gun.System.Points[shot.PointIndex];
                float angle = Vector2.SignedAngle(Vector2.up, point.up) + shot.Angle;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;

                Bullet bullet = Instantiate(gun.Def.BulletPrefab, point.position, Quaternion.Euler(0f, 0f, angle));
                bullet.Launch(m_Body.linearVelocity + direction * gun.Def.LaunchSpeed, this);
                NetworkServer.Spawn(bullet.gameObject);
                pointMask |= (byte)(1 << shot.PointIndex);
            }
            RpcFired(secondary, pointMask);
        }

        private void StepPower(float deltaTime)
        {
            for (int i = 0; i < m_Systems.Count; i++)
            {
                ShipSystem system = m_Systems[i];
                if (!system.Health.IsDestroyed && system.Def.PassivePowerDrain < 0f) m_Power.Charge(-system.Def.PassivePowerDrain * deltaTime);
            }

            for (int i = 0; i < m_Systems.Count; i++)
            {
                ShipSystem system = m_Systems[i];
                if (system.Health.IsDestroyed || system.Def.PassivePowerDrain <= 0f) continue;
                float needed = system.Def.PassivePowerDrain * deltaTime;
                float shortfall = needed - m_Power.Draw(needed);
                system.Health.Damage(shortfall * m_UnpoweredDamagePerPower);
            }
        }

        // The generator's passive drain is handled with the other systems; a destroyed generator collapses the shield.
        private void StepShield(float deltaTime)
        {
            if (m_ShieldGenerator.Health.IsDestroyed)
            {
                m_Shield.Damage(m_Shield.Current);
                return;
            }

            if (m_Shield.Current >= m_Shield.Max) return;
            if (m_Power.TryDraw(m_ShieldDef.ActivePowerDrain * deltaTime)) m_Shield.Repair(m_ShieldDef.RechargeRate * deltaTime);
        }

        private void StepRepairs(float deltaTime)
        {
            m_Armour.Repair(m_HullDef.ArmourRepairRate * deltaTime);
            for (int i = 0; i < m_Systems.Count; i++)
            {
                ShipSystem system = m_Systems[i];
                if (!system.Health.IsDestroyed) system.Health.Repair(system.Def.AutoRepairRate * deltaTime);
            }
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
