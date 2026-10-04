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
    public class Ship : SpaceObject, IDamageable
    {
        [SerializeField] private ShieldVisual m_ShieldVisual;
        [SerializeField] private GameObject m_ExplosionPrefab;
        [SerializeField, Tooltip("Keeps the object alive briefly after the hull fails so clients receive the explosion.")] private float m_DespawnDelay = 0.3f;
        [SerializeField] private ThrusterEffects m_ThrusterEffects;
        [SerializeField, Tooltip("The ship body's mesh filter (not the shield's); receives the hull's mesh.")] private MeshFilter m_HullMeshFilter;
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
        private bool m_Applied;
        private bool m_Exploded;
        private PlayerState m_LastAttacker;
        private float m_LastAttackTime;

        [SyncVar] private ShipLoadout m_Loadout;
        [SyncVar(hook = nameof(OnThrustingChanged))] private bool m_Thrusting;

        /// Server only. The player this ship belongs to.
        public PlayerState Owner { get; private set; }

        /// Server only. Raised once when the hull reaches zero health.
        public event Action<Ship> Destroyed;

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
                if (index >= 0) m_SubSystemDefs.Add(catalog.SubSystems[index]);
            }

            if (m_HullDef.Mesh != null) m_HullMeshFilter.sharedMesh = m_HullDef.Mesh;
            m_ShieldVisual.transform.localScale = new Vector3(m_HullDef.Size.x, m_HullDef.Size.y, 1f);
            m_ThrusterEffects.Build(m_ThrusterDef, m_HullDef.ThrusterPoints);
        }

        public override void OnStartServer()
        {
            ApplyLoadout();
            m_Hull = new ShipSystem(m_HullDef, Array.Empty<EmissionPoint>());
            m_Systems.Add(m_Hull);
            m_ShieldGenerator = new ShipSystem(m_ShieldDef, Array.Empty<EmissionPoint>());
            m_Systems.Add(m_ShieldGenerator);
            m_MainGun = new Gun(m_MainGunDef, new ShipSystem(m_MainGunDef, m_HullDef.MainWeaponPoints));
            m_SecondaryGun = new Gun(m_SecondaryGunDef, new ShipSystem(m_SecondaryGunDef, m_HullDef.SecondaryWeaponPoints));
            m_Thruster = new Thruster(m_ThrusterDef, new ShipSystem(m_ThrusterDef, m_HullDef.ThrusterPoints));
            m_Systems.AddRange(new[] { m_MainGun.System, m_SecondaryGun.System, m_Thruster.System });

            for (int i = 0; i < m_SubSystemDefs.Count; i++)
            {
                var subSystem = new SubSystem(m_SubSystemDefs[i]);
                m_SubSystems.Add(subSystem);
                m_Systems.Add(subSystem.System);
            }

            m_Shield = new ComponentHealth(m_ShieldDef.ShieldCapacity);
            m_Armour = new ComponentHealth(m_HullDef.Armour);
            m_Power = new PowerBank(m_HullDef.MaxPowerStorage + m_SubSystems.Sum(s => s.Def.ExtraPowerStorage));

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
            SetThrusting(m_Thruster.IsFiring);
            CheckHull();
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
            if (index >= 0 && index < m_SubSystems.Count) m_SubSystems[index].Activation.Press();
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

            float leftover = DamageMath.AbsorbArmour(afterShield, m_Armour, m_HullDef.ArmourWearRatio);
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
            EmissionPoint[] points = secondary ? m_HullDef.SecondaryWeaponPoints : m_HullDef.MainWeaponPoints;
            for (int i = 0; i < points.Length; i++)
            {
                if ((pointMask & (1 << i)) == 0) continue;

                Vector3 position = transform.TransformPoint(points[i].Position);
                if (def.LaunchEffect != null) Instantiate(def.LaunchEffect, position, transform.rotation * Quaternion.Euler(0f, 0f, points[i].Angle));
                if (def.LaunchSound != null) AudioSource.PlayClipAtPoint(def.LaunchSound, position);
            }
        }

        // The hook only runs on remote clients, so the host updates its own effects directly.
        private void SetThrusting(bool thrusting)
        {
            if (m_Thrusting == thrusting) return;

            m_Thrusting = thrusting;
            if (isClient) m_ThrusterEffects.SetActive(thrusting);
        }

        private void OnThrustingChanged(bool oldValue, bool newValue) => m_ThrusterEffects.SetActive(newValue);

        private void StepRotation(float deltaTime)
        {
            Vector2 toAim = m_Controls.AimPoint - m_Body.position;
            if (toAim.sqrMagnitude < 1e-4f) return;

            float targetAngle = Mathf.Atan2(toAim.y, toAim.x) * Mathf.Rad2Deg - 90f; // Forward is +Y.
            float error = Mathf.DeltaAngle(m_Body.rotation, targetAngle);
            float acceleration = RotationMath.Acceleration(error, m_Body.angularVelocity, m_HullDef.AngularAcceleration, deltaTime);
            m_Body.AddTorque(acceleration * Mathf.Deg2Rad * m_Body.inertia);
        }

        private void StepSubSystems(float deltaTime)
        {
            float fireRateMultiplier = 1f;
            foreach (SubSystem subSystem in m_SubSystems)
            {
                if (subSystem.System.Health.IsDestroyed) subSystem.Activation.Stop();
                else subSystem.Activation.Step(deltaTime, m_Power);

                if (!subSystem.IsRunning) continue;
                m_Hull.Health.Repair(subSystem.Def.HullRepairRate * deltaTime);
                fireRateMultiplier *= subSystem.Def.FireRateMultiplier;
            }
            m_MainGun.RateMultiplier = fireRateMultiplier;
            m_SecondaryGun.RateMultiplier = fireRateMultiplier;
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
                EmissionPoint point = gun.System.Points[shot.PointIndex];
                float angle = m_Body.rotation + point.Angle + shot.Angle;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;

                Bullet bullet = Instantiate(gun.Def.BulletPrefab, transform.TransformPoint(point.Position), Quaternion.Euler(0f, 0f, angle));
                bullet.Launch(m_Body.linearVelocity + direction * gun.Def.LaunchSpeed, this);
                NetworkServer.Spawn(bullet.gameObject);
                pointMask |= (byte)(1 << shot.PointIndex);
            }
            RpcFired(secondary, pointMask);
        }

        private void StepPower(float deltaTime)
        {
            foreach (ShipSystem system in m_Systems.Where(s => !s.Health.IsDestroyed && s.Def.PassivePowerDrain < 0f))
            {
                m_Power.Charge(-system.Def.PassivePowerDrain * deltaTime);
            }

            foreach (ShipSystem system in m_Systems.Where(s => !s.Health.IsDestroyed && s.Def.PassivePowerDrain > 0f))
            {
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
            foreach (ShipSystem system in m_Systems.Where(s => !s.Health.IsDestroyed))
            {
                system.Health.Repair(system.Def.AutoRepairRate * deltaTime);
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
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (m_ExplosionPrefab != null) Instantiate(m_ExplosionPrefab, transform.position, Quaternion.identity);
        }
    }
}
