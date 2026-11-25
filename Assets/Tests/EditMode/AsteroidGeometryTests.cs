using System;
using System.Linq;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Tests
{
    public class AsteroidGeometryTests
    {
        private static int Solidness(AsteroidGeometry field) => field.Samples.Sum(value => (int)value);
        internal static AsteroidGeometry Solid()
        {
            var bytes = new byte[81];
            for (int y = 1; y < 8; y++)
            for (int x = 1; x < 8; x++) bytes[y * 9 + x] = 255;
            return new AsteroidGeometry(9, 9, bytes);
        }

        [Test]
        public void SeededRocks_AreConnectedIrregularAndBoundedAcrossPopulation()
        {
            for (uint seed = 0; seed < 250; seed++)
            {
                AsteroidGeometry field = AsteroidGeometry.Generate(seed);
                CollectionAssert.AreEqual(field.Samples, AsteroidGeometry.Generate(seed).Samples);
                Assert.AreEqual(289, field.Samples.Length);
                Assert.AreEqual(1, field.Components().Count, $"Seed {seed}");
                Assert.Greater(field.Area(), 2f);
                Assert.IsTrue(field.Samples.Any(value => value > 0 && value < 255));
                for (int i = 0; i < 17; i++)
                {
                    Assert.AreEqual(0, field.Samples[i]);
                    Assert.AreEqual(0, field.Samples[272 + i]);
                    Assert.AreEqual(0, field.Samples[i * 17]);
                    Assert.AreEqual(0, field.Samples[i * 17 + 16]);
                }
            }
            CollectionAssert.AreNotEqual(AsteroidGeometry.Generate(1).Samples, AsteroidGeometry.Generate(2).Samples);
        }

        [Test]
        public void Excavation_OpensAHoleThenSplitsAChannel()
        {
            AsteroidGeometry field = Solid();
            float before = field.Area();
            field.Erode(new Vector2(4, 4), 1.6f, 255f);
            Assert.IsFalse(AsteroidGeometry.Contains(field.Polygons(), new Vector2(4, 4)));
            Assert.IsTrue(AsteroidGeometry.Contains(field.Polygons(), new Vector2(2, 4)));
            Assert.Less(field.Area(), before);
            for (int y = 0; y < 9; y++) field.Erode(new Vector2(4, y), 0.6f, 255f);
            var components = field.Components();
            Assert.AreEqual(2, components.Count);
            Assert.AreEqual(field.Area(), components.Sum(piece => piece.Area()), 0.0001f);
        }

        [Test]
        public void FractionalImpact_AccumulatesExactlyAcrossComponentsAndRecentering()
        {
            AsteroidGeometry field = Solid();
            Assert.AreEqual(0.25f, field.ErodeImpact(new Vector2(4, 4), Vector2.zero, 0.25f, false));
            Assert.AreEqual(255, field.Samples[40]);
            field = field.Components().Single().Recenter(out Vector2 offset);
            Vector2 point = Vector2.one * 8f - offset;
            Assert.AreEqual(0.75f, field.ErodeImpact(point, Vector2.zero, 0.75f, false));
            Assert.AreEqual(254, field.Samples[8 * 17 + 8]);
            for (int i = 0; i < 1016; i++) field.ErodeImpact(point, Vector2.zero, 0.25f, false);
            Assert.AreEqual(127, field.Samples[8 * 17 + 8]);
            Assert.IsFalse(AsteroidGeometry.Contains(field.Polygons(), point));
        }

        [TestCase(255, 0, 0, 255)]
        [TestCase(0, 255, 255, 0)]
        public void DiagonalCorners_DoNotConnect(int a, int b, int c, int d)
        {
            var field = new AsteroidGeometry(2, 2, new[] { (byte)a, (byte)b, (byte)c, (byte)d });
            Assert.AreEqual(2, field.Components().Count);
            Assert.AreEqual(2, field.Polygons().Count);
            Assert.IsFalse(AsteroidGeometry.Contains(field.Polygons(), Vector2.one * 0.5f));
        }

        [Test]
        public void Recentering_PreservesAnEmptyHoleAndExactMaterial()
        {
            AsteroidGeometry source = Solid();
            source.Erode(new Vector2(4, 4), 1.6f, 255f);
            AsteroidGeometry fitted = source.Recenter(out Vector2 offset);
            Vector2 hole = new Vector2(4, 4) + Vector2.one * 4f - offset;
            Assert.IsFalse(AsteroidGeometry.Contains(fitted.Polygons(), hole));
            Assert.AreEqual(1, fitted.Components().Count);
            Assert.AreEqual(17, fitted.Width);
            Assert.AreEqual(17, fitted.Height);
            Assert.AreEqual(source.Area(), fitted.Area(), 0.00001f);
            Assert.AreEqual(Solidness(source), Solidness(fitted));
        }

        [Test]
        public void ShatteringAndRepeatedRecentering_PreserveRemainingMaterial()
        {
            AsteroidGeometry parent = Solid();
            int solidness = Solidness(parent);
            parent.Crack(new Vector2(0f, 4f), 1);
            var pieces = parent.Components();
            Assert.That(pieces.Count, Is.InRange(2, 4));
            Assert.Less(Solidness(parent), solidness);
            Assert.AreEqual(Solidness(parent), pieces.Sum(Solidness));
            foreach (AsteroidGeometry piece in pieces)
            {
                AsteroidGeometry fit = piece.Recenter(out _).Recenter(out _);
                Assert.AreEqual(289, fit.Samples.Length);
                Assert.AreEqual(1, fit.Components().Count);
                Assert.AreEqual(piece.Area(), fit.Area(), 0.00001f);
                Assert.AreEqual(Solidness(piece), Solidness(fit));
            }
        }

        [Test]
        public void InvalidFieldsAndExcavation_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AsteroidGeometry(18, 17, new byte[306]));
            Assert.Throws<ArgumentException>(() => Solid().Erode(Vector2.zero, 1f, float.NaN));
            Assert.Throws<ArgumentException>(() => Solid().ErodeImpact(Vector2.zero, Vector2.zero, 1f, true));
            Assert.Throws<ArgumentException>(() => Solid().ErodeImpact(Vector2.zero, Vector2.right, float.NaN, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => AsteroidGeometry.Generate(0, 8f));
        }

        [Test]
        public void OrdinaryImpact_LowersOnlyTheNearestCornerByExactDamage()
        {
            AsteroidGeometry field = Solid();
            byte[] before = (byte[])field.Samples.Clone();
            float area = field.Area();
            Assert.AreEqual(3f, field.ErodeImpact(new Vector2(3.6f, 4.1f), Vector2.zero, 3f, false));
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(i == 40 ? 3 : 0, before[i] - field.Samples[i]);
            Assert.AreEqual(area, field.Area(), 0.00001f);
            Assert.AreEqual(0f, field.ErodeImpact(new Vector2(-100f, 0f), Vector2.zero, 4f, false));
            Assert.AreEqual(252f, field.ErodeImpact(new Vector2(4, 4), Vector2.zero, 1000f, false));
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(255, field.Samples[41], "Collisions must not propagate overflow.");
        }

        [TestCase(1f, 0f, 1, 0)]
        [TestCase(-1f, 0f, -1, 0)]
        [TestCase(0f, 1f, 0, 1)]
        [TestCase(0f, -1f, 0, -1)]
        [TestCase(1f, 1f, 1, 1)]
        [TestCase(-1f, -1f, -1, -1)]
        [TestCase(1f, 0.4f, 1, 0)]
        public void ProjectileOverflow_FollowsTheImpactVectorAndStopsAtNonSolid(float dx, float dy, int stepX, int stepY)
        {
            var field = Solid();
            byte[] before = (byte[])field.Samples.Clone();
            Assert.AreEqual(256f, field.ErodeImpact(new Vector2(4, 4), new Vector2(dx, dy), 256f, true));
            int next = (4 + stepY) * 9 + 4 + stepX;
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(i == 40 ? 0 : i == next ? 254 : before[i], field.Samples[i]);
        }

        [Test]
        public void ProjectileOverflow_SkipsEmptyGapsAndClipsAtGridBounds()
        {
            var field = Solid();
            field.Samples[41] = 0;
            Assert.AreEqual(256f, field.ErodeImpact(new Vector2(4, 4), Vector2.right, 256f, true));
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(0, field.Samples[41]);
            Assert.AreEqual(254, field.Samples[42]);
            Assert.AreEqual(255, field.Samples[39]);
            Assert.AreEqual(0f, field.ErodeImpact(new Vector2(-10, 4), Vector2.left, 1000f, true));
            Assert.AreEqual(1f, field.ErodeImpact(new Vector2(-10, 4), Vector2.right, 1f, true));
            Assert.AreEqual(254, field.Samples[37]);
            Assert.AreEqual(0f, field.ErodeImpact(new Vector2(-10, -10), Vector2.right, 1f, true));
            Assert.AreEqual(509f, field.ErodeImpact(new Vector2(6, 4), Vector2.right, 1000f, true));
            Assert.AreEqual(0, field.Samples[42]);
            Assert.AreEqual(0, field.Samples[43]);
            Assert.AreEqual(0, field.Samples[44]);
        }

        [Test]
        public void FractionalOverflow_ConservesTheBudgetAtTheNonSolidCutoff()
        {
            var field = Solid();
            field.ErodeImpact(new Vector2(4, 4), Vector2.right, 0.5f, true);
            Assert.AreEqual(255, field.Samples[40]);
            field.ErodeImpact(new Vector2(4, 4), Vector2.right, 254.75f, true);
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(255, field.Samples[41]);
            field.ErodeImpact(new Vector2(4, 4), Vector2.right, 0.75f, true);
            Assert.AreEqual(254, field.Samples[41]);
            Assert.AreEqual(255, field.Samples[42]);
        }

        [TestCase(128)]
        [TestCase(255)]
        public void SolidCorner_ConsumesItsRemainingByteBeforeOverflowing(int solidness)
        {
            var bytes = new byte[9];
            bytes[4] = (byte)solidness;
            var isolated = new AsteroidGeometry(3, 3, bytes);
            Assert.AreEqual(1, isolated.Components().Count);
            Assert.IsTrue(AsteroidGeometry.Contains(isolated.Polygons(), Vector2.one));
            Assert.AreEqual(solidness, isolated.ErodeImpact(Vector2.one, Vector2.zero, solidness, false));
            Assert.IsEmpty(isolated.Components());
            Assert.IsEmpty(isolated.Polygons());

            var field = Solid();
            field.Samples[40] = (byte)solidness;
            if (solidness > 128)
                field.ErodeImpact(new Vector2(4, 4), Vector2.right, solidness - 128f, true);
            Assert.AreEqual(128, field.Samples[40]);
            Assert.AreEqual(255, field.Samples[41]);
            Assert.AreEqual(129f, field.ErodeImpact(new Vector2(4, 4), Vector2.right, 129f, true));
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(254, field.Samples[41]);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(127)]
        public void BelowThresholdCorners_AreNotSolidAndDoNotConsumeProjectileDamage(int solidness)
        {
            var bytes = new byte[9];
            bytes[4] = (byte)solidness;
            var isolated = new AsteroidGeometry(3, 3, bytes);
            Assert.IsEmpty(isolated.Components());
            Assert.IsEmpty(isolated.Polygons());
            var field = Solid();
            field.Samples[40] = (byte)solidness;
            Assert.AreEqual(1f, field.ErodeImpact(new Vector2(4, 4), Vector2.right, 1f, true));
            Assert.AreEqual(solidness, field.Samples[40]);
            Assert.AreEqual(254, field.Samples[41]);
        }

        [Test]
        public void MidpointContour_InterpolatesEdgesAndSurvivesComponentExtraction()
        {
            var field = new AsteroidGeometry(2, 2, new byte[] { 255, 0, 0, 0 });
            Assert.Contains(new Vector2(0.5f, 0f), field.Polygons().Single());
            field.Samples[0] = 200;
            field.Samples[1] = 100;
            Assert.Contains(new Vector2(0.725f, 0f), field.Polygons().Single());
            var component = field.Components().Single();
            CollectionAssert.AreEqual(field.Samples, component.Samples);
            CollectionAssert.AreEqual(field.Polygons().Single(), component.Polygons().Single());
        }

        [Test]
        public void OccupiedRadius_ChangesMaterialCoverageNotGridResolution()
        {
            for (uint seed = 0; seed < 100; seed++)
            {
                var small = AsteroidGeometry.Generate(seed, 2f);
                var large = AsteroidGeometry.Generate(seed, 7f);
                Assert.AreEqual(289, small.Samples.Length);
                Assert.AreEqual(289, large.Samples.Length);
                Assert.Greater(large.Area(), small.Area() * 5f);
                for (int i = 0; i < small.Samples.Length; i++) Assert.GreaterOrEqual(large.Samples[i], small.Samples[i]);
                Assert.That(AsteroidGeometry.InitialRadius(seed, 2f, 7f), Is.InRange(2f, 7f));
            }
        }

        [Test]
        public void Components_PreserveLocalSolidnessWithoutRedistribution()
        {
            AsteroidGeometry field = Solid();
            for (int i = 0; i < 30; i++) field.ErodeImpact(new Vector2(2f, 2f), Vector2.zero, 3f, false);
            for (int y = 0; y < 9; y++) field.Samples[y * 9 + 4] = 0;
            var parts = field.Components();
            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual(Solidness(field), parts.Sum(Solidness));
            Assert.Contains((byte)165, parts[0].Samples);
            Assert.IsTrue(parts[1].Samples.Where(value => value > 0).All(value => value == 255));
            foreach (AsteroidGeometry part in parts)
            {
                var fit = part.Recenter(out _);
                Assert.AreEqual(Solidness(part), Solidness(fit));
            }
        }

        [Test]
        public void Recentering_DoesNotRefillWeakenedSolidness()
        {
            var source = Solid();
            for (int i = 0; i < source.Samples.Length; i++)
                if (source.Samples[i] > 200) source.Samples[i] = 200;
            var fitted = source.Recenter(out _);
            Assert.AreEqual(Solidness(source), Solidness(fitted));
            Assert.LessOrEqual(fitted.Samples.Max(), 200);
        }

        [Test]
        public void Crack_IsDeterministicAndThroughGoing()
        {
            AsteroidGeometry a = Solid(), b = Solid();
            a.Crack(new Vector2(0.5f, 4f), 123);
            b.Crack(new Vector2(0.5f, 4f), 123);
            CollectionAssert.AreEqual(a.Samples, b.Samples);
            Assert.AreEqual(2, a.Components().Count);
            Assert.IsFalse(AsteroidGeometry.Contains(a.Polygons(), new Vector2(0.5f, 4f)));
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        public void SmallRadius_RemainsAPointImpactWithProjectileOverflow(float radius)
        {
            var field = Solid();
            field.ErodeDamage(new Vector2(4.1f, 4.1f), Vector2.right, Vector2.right, Vector2.up, radius, 256f, true);
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(254, field.Samples[41]);
            Assert.AreEqual(255, field.Samples[49]);
        }

        [Test]
        public void Explosion_DamagesACircleWithLinearFalloffAndNoForwardOverflow()
        {
            var field = Solid();
            field.ErodeDamage(new Vector2(4, 4), Vector2.right, Vector2.right, Vector2.up, 2f, 100f, true);
            Assert.AreEqual(155, field.Samples[40]);
            foreach (int index in new[] { 39, 41, 31, 49 }) Assert.AreEqual(205, field.Samples[index]);
            Assert.AreEqual(226, field.Samples[50]);
            Assert.AreEqual(255, field.Samples[42], "The edge of the radius receives zero damage.");
            field = Solid();
            field.ErodeDamage(new Vector2(4, 4), Vector2.right, Vector2.right, Vector2.up, 1.1f, 1000f, true);
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(165, field.Samples[41], "The centre's excess must not propagate into its neighbour.");
            Assert.AreEqual(255, field.Samples[42]);
        }

        [Test]
        public void Explosion_UsesWorldDistancesAndDoesNotSnapToAnOutsideCorner()
        {
            var field = Solid();
            field.ErodeDamage(new Vector2(4, 4), Vector2.zero, Vector2.right * 2f, Vector2.up * 0.5f, 1.5f, 100f, false);
            Assert.AreEqual(255, field.Samples[41]);
            Assert.AreEqual(189, field.Samples[49]);
            Assert.AreEqual(222, field.Samples[58]);
            var missed = Solid();
            Assert.AreEqual(0f, missed.ErodeDamage(new Vector2(4.5f, 4.5f), Vector2.zero,
                Vector2.right, Vector2.up, 0.6f, 100f, false));
            Assert.AreEqual(255 * 49, Solidness(missed));
            Assert.Throws<ArgumentException>(() => field.ErodeDamage(Vector2.zero, Vector2.zero,
                Vector2.right, Vector2.up, -1f, 1f, false));
        }

        [Test]
        public void Surface_RetainsExactOriginalPlanesAcrossChildFrames()
        {
            var surface = new AsteroidSurface(123, Vector2.one * 8f, new Vector2(2f, 3f), 2f, 0.7f, 4f);
            var offset = new Vector2(0.33f, -0.21f);
            var child = surface.Child(offset);
            var grandchild = child.Child(new Vector2(-0.15f, 0.1f));
            Vector2 point = new Vector2(0.13f, 0.19f);
            Assert.AreEqual(surface.Front(point + offset), child.Front(point), 0.000001f);
            Assert.AreEqual(surface.Front(point + offset + new Vector2(-0.15f, 0.1f)),
                grandchild.Front(point), 0.000001f);
            Vector2[] square = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            foreach (Vector2[] facet in surface.Facets(square))
            for (int i = 1; i < facet.Length - 1; i++)
            {
                Vector2 centre = (facet[0] + facet[i] + facet[i + 1]) / 3f;
                float plane = (surface.Front(facet[0]) + surface.Front(facet[i]) + surface.Front(facet[i + 1])) / 3f;
                Assert.AreEqual(plane, surface.Front(centre), 0.000003f);
                Assert.That(surface.Front(centre), Is.InRange(-3.7f, -1f));
            }
            Assert.Throws<ArgumentException>(() => new AsteroidSurface(0, Vector2.zero, Vector2.one, 1f, 2f, 4f));
        }

        [TestCase(2f, 1f)]
        [TestCase(6f, 2f)]
        public void Surface_DomeRisesTowardTheOriginalCentreAndScalesWithTheRock(float radius, float spacing)
        {
            var domed = new AsteroidSurface(123, Vector2.one * 8f, Vector2.one / spacing, 1f, 0.4f, radius);
            var flat = new AsteroidSurface(123, domed.Offset, domed.Scale, domed.Depth, domed.Relief, 0f);
            Vector2 middle = Vector2.right * (radius * spacing * 0.5f);
            Vector2 edge = Vector2.right * (radius * spacing);
            float rise = flat.Front(Vector2.zero) - domed.Front(Vector2.zero);
            float middleRise = flat.Front(middle) - domed.Front(middle);
            Assert.AreEqual(radius * spacing, rise, 0.00001f);
            Assert.AreEqual(radius * spacing * Mathf.Sqrt(0.75f), middleRise, 0.00001f);
            Assert.AreEqual(flat.Front(edge), domed.Front(edge), 0.00001f);
            Assert.Greater(rise, middleRise);
            Assert.Throws<ArgumentException>(() => new AsteroidSurface(0, Vector2.zero, Vector2.one, 1f, 0f, float.NaN));
            Assert.Throws<ArgumentException>(() => new AsteroidSurface(0, Vector2.zero, Vector2.one, 1f, 0f, -1f));
        }
    }
}
