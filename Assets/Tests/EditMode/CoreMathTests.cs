using System;
using System.Linq;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEngine;
using Random = System.Random;

namespace PewPewPew.Tests
{
    public class GravityMathTests
    {
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
    }

    public class DamageMathTests
    {
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
        public void Charge_ClampsAtMax()
        {
            var bank = new PowerBank(50);
            bank.Charge(80);
            Assert.AreEqual(50f, bank.Stored);
        }

        [Test]
        public void TryDraw_IsAllOrNothing()
        {
            var bank = new PowerBank(50);
            bank.Charge(10);
            Assert.IsFalse(bank.TryDraw(15));
            Assert.AreEqual(10f, bank.Stored);
            Assert.IsTrue(bank.TryDraw(10));
            Assert.AreEqual(0f, bank.Stored);
        }

        [Test]
        public void Draw_TakesWhatIsAvailable()
        {
            var bank = new PowerBank(50);
            bank.Charge(10);
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
        public void SubSystemSlots_RoundTripAndEmptyDefaultsToNone()
        {
            ShipLoadout loadout = ShipLoadout.Empty;
            Assert.AreEqual(-1, loadout.GetSubSystem(2));
            loadout.SetSubSystem(2, 5);
            Assert.AreEqual(5, loadout.GetSubSystem(2));
            Assert.AreEqual(-1, loadout.GetSubSystem(9));
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
            bank.Charge(amount);
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
    }

    public class VitalsMathTests
    {
        [Test]
        public void Byte_RoundTripsWithinQuantisationAndClamps()
        {
            Assert.AreEqual(0.5f, VitalsMath.FromByte(VitalsMath.ToByte(0.5f)), 1f / 255f);
            Assert.AreEqual(255, VitalsMath.ToByte(2f));
            Assert.AreEqual(0, VitalsMath.ToByte(-1f));
        }

        [Test]
        public void SubSystemSlots_StoreLevelAndState()
        {
            var vitals = new ShipVitals();
            vitals.SetSubSystem(2, 1f, HudState.Cooling);
            Assert.AreEqual(255, vitals.GetLevel(2));
            Assert.AreEqual(HudState.Cooling, vitals.GetState(2));
            Assert.AreEqual(HudState.Off, vitals.GetState(0));
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
