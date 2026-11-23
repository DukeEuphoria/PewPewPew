using System;
using NUnit.Framework;
using PewPewPew.Core;

namespace PewPewPew.Tests
{
    public class PowerBankSegmentTests
    {
        [Test]
        public void ChargeAndDraw_UseHullFirstAndReverseSegmentOrder()
        {
            PowerBank bank = PowerBank.CreateSegmented(10f, 20f, 30f);
            bank.SetFraction(0f);

            bank.Charge(35f);

            Assert.AreEqual(10f, bank.GetSegmentStored(0));
            Assert.AreEqual(20f, bank.GetSegmentStored(1));
            Assert.AreEqual(5f, bank.GetSegmentStored(2));
            Assert.AreEqual(7f, bank.Draw(7f));
            Assert.AreEqual(10f, bank.GetSegmentStored(0));
            Assert.AreEqual(18f, bank.GetSegmentStored(1));
            Assert.AreEqual(0f, bank.GetSegmentStored(2));
        }

        [Test]
        public void DisabledSegment_PreservesChargeAndIsExcludedFromChargeAndDraw()
        {
            PowerBank bank = PowerBank.CreateSegmented(10f, 20f, 30f);
            bank.SetFraction(0.5f);
            bank.SetSegmentEnabled(1, false);

            Assert.AreEqual(40f, bank.UsableMax);
            Assert.AreEqual(10f, bank.UsableStored);
            bank.Charge(25f);
            Assert.AreEqual(20f, bank.GetSegmentStored(1));
            Assert.AreEqual(25f, bank.GetSegmentStored(2));
            Assert.AreEqual(20f, bank.Draw(20f));
            Assert.AreEqual(20f, bank.GetSegmentStored(1));
            Assert.AreEqual(5f, bank.GetSegmentStored(2));
            bank.SetFraction(0f);
            Assert.AreEqual(20f, bank.GetSegmentStored(1));
            bank.Charge(15f);

            bank.SetSegmentEnabled(1, true);

            Assert.AreEqual(60f, bank.UsableMax);
            Assert.AreEqual(35f, bank.UsableStored);
            Assert.AreEqual(1f, bank.GetSegmentFraction(1));
        }

        [Test]
        public void EmptySegmentedBankAndInvalidAmounts_DoNotCreateNegativeCharge()
        {
            PowerBank bank = PowerBank.CreateSegmented();

            Assert.AreEqual(0, bank.SegmentCount);
            Assert.AreEqual(0f, bank.Max);
            Assert.AreEqual(0f, bank.Stored);
            Assert.AreEqual(0f, bank.UsableMax);
            Assert.AreEqual(0f, bank.Draw(10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => bank.TryDraw(-1f));

            PowerBank segmented = PowerBank.CreateSegmented(10f, 20f);
            segmented.SetFraction(0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => segmented.Charge(-3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => segmented.Draw(-2f));
            Assert.AreEqual(0f, segmented.Stored);
        }

        [Test]
        public void SingleBankConstructorAndSetFraction_RetainSinglePoolSemantics()
        {
            var bank = new PowerBank(50f);
            Assert.AreEqual(50f, bank.Stored);
            Assert.AreEqual(50f, bank.Max);

            bank.SetFraction(0.25f);

            Assert.AreEqual(12.5f, bank.Stored);
            Assert.AreEqual(0.25f, bank.Fraction);
            Assert.AreEqual(12.5f, bank.Draw(20f));
            Assert.AreEqual(0f, bank.Stored);
        }
    }
}
