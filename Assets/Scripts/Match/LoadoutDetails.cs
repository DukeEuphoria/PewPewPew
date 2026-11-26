using System;
using System.Text;
using PewPewPew.Core;
using PewPewPew.Ships;
using PewPewPew.Weapons;
using UnityEngine;

namespace PewPewPew.Match
{
    public static class LoadoutDetails
    {
        public static string Hull(ShipCatalog catalog, ShipLoadout loadout)
        {
            if (catalog == null || !catalog.IsValid(loadout))
                throw new ArgumentException("Hull details require a valid loadout.", nameof(loadout));

            HullDef hull = catalog.Hulls[loadout.Hull];
            float mass = hull.Mass;
            float storage = hull.MaxPowerStorage;
            float generation = Mathf.Max(0, -hull.PassivePowerDrain);
            float consumption = Mathf.Max(0, hull.PassivePowerDrain);
            int cargoCapacity = 0;
            HullPoint[] points = hull.HullPoints.Points;
            for (int i = 0; i < loadout.MountComponents.Length; i++)
                if (loadout.MountComponents[i] < 0) cargoCapacity += Ship.CargoCapacity(points[i].Size);
            foreach (int index in loadout.MountComponents)
            {
                if (index < 0) continue;
                ShipComponentDef component = catalog.Components[index];
                mass += component.Mass;
                generation += Mathf.Max(0, -component.PassivePowerDrain);
                consumption += Mathf.Max(0, component.PassivePowerDrain);
                if (component is SubSystemDef subSystem) storage += subSystem.ExtraPowerStorage;
            }

            var text = new StringBuilder();
            text.AppendLine(hull.DisplayName);
            if (!string.IsNullOrWhiteSpace(hull.Description)) text.AppendLine(hull.Description);
            text.AppendLine($"Hull health: {hull.MaxHealth:0.##}");
            text.AppendLine($"Armour: {hull.Armour:0.##}  Repair: {hull.ArmourRepairRate:0.##}/s");
            text.AppendLine($"Energy generation: {generation:0.##}/s  Passive use: {consumption:0.##}/s");
            text.AppendLine($"Net energy: {generation - consumption:0.##}/s  Storage: {storage:0.##}");
            text.AppendLine($"Cargo capacity: {cargoCapacity}");
            text.Append($"Mass: {mass:0.##}");
            if (hull.IsLocked) text.Append($"\nLocked - price: {hull.Price}");
            return text.ToString();
        }

        public static string Slot(HullPoint point, ShipComponentDef fitted)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            if (fitted != null) return Component(fitted);
            return $"{point.name}\n{point.Description}\nMaximum size: {point.Size}\nAccepts: {point.Accepts}";
        }

        public static string Component(ShipComponentDef component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            var text = new StringBuilder();
            text.AppendLine($"{component.DisplayName} ({component.Size} {component.Type})");
            if (!string.IsNullOrWhiteSpace(component.Description)) text.AppendLine(component.Description);
            text.AppendLine($"Mass: {component.Mass:0.##}  Health: {component.MaxHealth:0.##}");
            string activeUnit = component is GunDef ? "/burst" : "/s";
            text.AppendLine($"Passive power use: {component.PassivePowerDrain:0.##}/s (negative generates)");
            text.AppendLine($"Active power use: {component.ActivePowerDrain:0.##}{activeUnit}");
            text.AppendLine($"Self repair: {component.AutoRepairRate:0.##}/s");
            if (component is GunDef gun)
            {
                text.AppendLine($"Fire rate: {gun.FireRate:0.##}/s  Burst: {gun.BulletsPerBurst}");
                text.AppendLine($"Projectile speed: {gun.LaunchSpeed:0.##}");
                if (gun.BulletPrefab != null)
                {
                    var damage = gun.BulletPrefab.Damage;
                    text.AppendLine($"Projectile damage - asteroid: {damage.Asteroid:0.##}  Shield: {damage.Shield:0.##}");
                    text.AppendLine($"Armour: {damage.Armour:0.##}  Hull/components: {damage.HullComponents:0.##}");
                }
                text.AppendLine($"Pattern: {gun.Pattern}  Angles: {gun.MinLaunchAngle:0.##} to {gun.MaxLaunchAngle:0.##} degrees");
            }
            else if (component is ShieldDef shield)
            {
                text.AppendLine($"Capacity: {shield.ShieldCapacity:0.##}  Impact limit: {shield.MaxSingleImpact:0.##}");
                text.AppendLine($"Recharge: {shield.RechargeRate:0.##}/s  Coverage: +/-{shield.HalfAngle:0.##} degrees");
            }
            else if (component is ThrusterDef thruster)
            {
                text.AppendLine($"Force: {thruster.Force:0.##}");
                text.AppendLine($"Ramp up: {thruster.RampUpTime:0.##}s  Ramp down: {thruster.RampDownTime:0.##}s");
            }
            else if (component is SubSystemDef subSystem)
            {
                text.AppendLine($"Activation: {subSystem.Mode}  Duration: {subSystem.Duration:0.##}s  Cooldown: {subSystem.Cooldown:0.##}s");
                if (subSystem.ExtraPowerStorage != 0) text.AppendLine($"Extra storage: {subSystem.ExtraPowerStorage:0.##}");
                if (subSystem.HullRepairRate != 0) text.AppendLine($"Hull repair: {subSystem.HullRepairRate:0.##}/s while active");
                if (subSystem.FireRateMultiplier != 1) text.AppendLine($"Fire rate multiplier: {subSystem.FireRateMultiplier:0.##} while active");
                if (subSystem.ThrustForceMultiplier != 1) text.AppendLine($"Thrust multiplier: {subSystem.ThrustForceMultiplier:0.##} while active");
                if (subSystem.ShieldHealthMultiplier != 1) text.AppendLine($"Shield multiplier: {subSystem.ShieldHealthMultiplier:0.##} while active");
                if (subSystem.ArmourStrengthMultiplier != 1) text.AppendLine($"Armour multiplier: {subSystem.ArmourStrengthMultiplier:0.##} while active");
                if (subSystem.Radar != null)
                {
                    var radar = subSystem.Radar;
                    text.AppendLine($"Radar: {radar.Mode}  Range: {radar.Range:0.##}  Coverage: +/-{radar.HalfAngle:0.##} degrees");
                }
                if (subSystem.TractorRange > 0f)
                    text.AppendLine($"Tractor: range {subSystem.TractorRange:0.##}  Cone: {subSystem.TractorHalfAngle * 2f:0.##} degrees  Force: {subSystem.TractorForce:0.##}");
            }
            if (component.IsLocked) text.AppendLine($"Locked - price: {component.Price}");
            return text.ToString().TrimEnd();
        }
    }
}
