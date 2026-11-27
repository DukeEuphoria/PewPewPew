using System;
using NUnit.Framework;
using PewPewPew.Core;

namespace PewPewPew.Tests
{
    public class ShipCargoTests
    {
        [Test]
        public void Collection_SplitsAcrossStoresAndRejectsWholePickupWhenFull()
        {
            var cargo = new ShipCargo(new[] { ShipCargo.SmallCapacity, 0, ShipCargo.MediumCapacity });
            for (int i = 0; i < 5; i++) Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Large));
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.RedCrystal, TreasureSize.Large));
            Assert.AreEqual(1, cargo.Amount(0, TreasureMaterial.RedCrystal));
            Assert.AreEqual(2, cargo.Amount(2, TreasureMaterial.RedCrystal));
            Assert.AreEqual(0, cargo.Stored(1));
            for (int i = 0; i < 11; i++) Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Silver, TreasureSize.Medium));
            byte[] before = cargo.Snapshot();
            Assert.AreEqual(1, cargo.Free);
            Assert.IsFalse(cargo.TryAdd(TreasureMaterial.PurpleCrystal, TreasureSize.Medium));
            CollectionAssert.AreEqual(before, cargo.Snapshot());
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.PurpleCrystal, TreasureSize.Small));
            Assert.AreEqual(0, cargo.Free);
        }

        [Test]
        public void Snapshot_ValidatesBeforeChangingCargo()
        {
            var cargo = new ShipCargo(new[] { ShipCargo.SmallCapacity, 0 });
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.CyanCrystal, TreasureSize.Large));
            byte[] before = cargo.Snapshot();
            var invalid = cargo.Snapshot();
            invalid[ShipCargo.MaterialCount] = 1;
            Assert.Throws<ArgumentException>(() => cargo.ApplySnapshot(invalid));
            Assert.Throws<ArgumentException>(() => cargo.ApplySnapshot(new byte[1]));
            CollectionAssert.AreEqual(before, cargo.Snapshot());
            cargo.Clear();
            cargo.ApplySnapshot(before);
            Assert.AreEqual(3, cargo.Total(TreasureMaterial.CyanCrystal));
        }

        [Test]
        public void NoEmptyPoints_CannotCollect()
        {
            var cargo = new ShipCargo(new[] { 0, 0 });
            Assert.AreEqual(0, cargo.Free);
            Assert.IsFalse(cargo.TryAdd(TreasureMaterial.Gold, TreasureSize.Small));
            Assert.AreEqual(0, cargo.Total(TreasureMaterial.Gold));
        }

        [Test]
        public void Collection_UsesAuthoredUnitsAndRejectsInvalidAmounts()
        {
            var cargo = new ShipCargo(new[] { 2, 3 });
            Assert.IsTrue(cargo.TryAdd(TreasureMaterial.Gold, 4));
            Assert.AreEqual(4, cargo.Total(TreasureMaterial.Gold));
            byte[] before = cargo.Snapshot();
            Assert.IsFalse(cargo.TryAdd(TreasureMaterial.Gold, 2));
            CollectionAssert.AreEqual(before, cargo.Snapshot());
            Assert.Throws<ArgumentOutOfRangeException>(() => cargo.TryAdd(TreasureMaterial.Gold, 0));
        }

        [Test]
        public void DeathDrops_RoundPerMaterialToNearestAwayFromZero()
        {
            Assert.AreEqual(2, ShipCargo.DropUnits(5, 0.3f));
            Assert.AreEqual(1, ShipCargo.DropUnits(1, 0.5f));
            Assert.AreEqual(4, ShipCargo.DropUnits(5, 0.7f));
            Assert.AreEqual(0, ShipCargo.DropUnits(1, 0.3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShipCargo.DropUnits(1, float.NaN));
        }
    }
}
