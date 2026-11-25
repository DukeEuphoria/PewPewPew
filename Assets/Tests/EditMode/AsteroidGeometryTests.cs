using System;
using System.Collections.Generic;
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
                Assert.AreEqual(256, field.Samples.Length);
                Assert.AreEqual(1, field.Components().Count, $"Seed {seed}");
                Assert.Greater(field.Area(), 2f);
                Assert.IsTrue(field.Samples.Any(value => value > 0 && value < 255));
                for (int i = 0; i < 16; i++)
                {
                    Assert.AreEqual(0, field.Samples[i]);
                    Assert.AreEqual(0, field.Samples[240 + i]);
                    Assert.AreEqual(0, field.Samples[i * 16]);
                    Assert.AreEqual(0, field.Samples[i * 16 + 15]);
                }
            }
            CollectionAssert.AreNotEqual(AsteroidGeometry.Generate(1).Samples, AsteroidGeometry.Generate(2).Samples);
        }

        [Test]
        public void VertexOffsets_AreSeededBoundedAndKeepCellsConvex()
        {
            var field = new AsteroidGeometry(16, 16, Enumerable.Repeat((byte)255, 256).ToArray());
            var copy = new AsteroidGeometry(16, 16, field.Samples);
            field.ConfigurePositions(123, 0f, Vector2Int.zero);
            Assert.AreEqual(new Vector2(4, 5), field.Position(4, 5));
            field.ConfigurePositions(123, 1f, Vector2Int.zero);
            copy.ConfigurePositions(123, 1f, Vector2Int.zero);
            float smallest = float.PositiveInfinity, largest = 0f;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                Vector2 position = field.Position(x, y);
                Assert.AreEqual(position, copy.Position(x, y));
                float displacement = Vector2.Distance(position, new Vector2(x, y));
                Assert.LessOrEqual(displacement, 1.000001f);
                smallest = Mathf.Min(smallest, displacement);
                largest = Mathf.Max(largest, displacement);
            }
            Assert.Less(smallest, 0.3f);
            Assert.Greater(largest, 0.9f);
            foreach (Vector2[] polygon in field.Polygons())
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 a = polygon[(i + 1) % polygon.Length] - polygon[i];
                    Vector2 b = polygon[(i + 2) % polygon.Length] - polygon[(i + 1) % polygon.Length];
                    Assert.Greater(a.x * b.y - a.y * b.x, 0f);
                }
            copy.ConfigurePositions(124, 1f, Vector2Int.zero);
            Assert.AreNotEqual(field.Position(4, 5), copy.Position(4, 5));
            copy.ConfigurePositions(123, 0.5f, Vector2Int.zero);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                Vector2 grid = new Vector2(x, y);
                Assert.Less(Vector2.Distance(copy.Position(x, y) - grid,
                    (field.Position(x, y) - grid) * 0.5f), 0.000002f);
            }
            foreach (Vector2[] polygon in copy.Polygons())
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector2 a = polygon[(i + 1) % polygon.Length] - polygon[i];
                    Vector2 b = polygon[(i + 2) % polygon.Length] - polygon[(i + 1) % polygon.Length];
                    Assert.Greater(a.x * b.y - a.y * b.x, 0f);
                }
            Assert.Throws<ArgumentOutOfRangeException>(() => field.ConfigurePositions(0, 1.01f, Vector2Int.zero));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void PolygonWeights_FollowDensityAndIsosurfaceForEveryMarchingCase(float offset)
        {
            const int size = 2;
            byte[] solid = { 160, 200, 230, 255 };
            for (int code = 1; code < 16; code++)
            {
                var samples = new byte[size * size];
                for (int i = 0; i < samples.Length; i++)
                    if ((code & (1 << i)) != 0) samples[i] = solid[i];
                var field = new AsteroidGeometry(size, size, samples);
                field.ConfigurePositions(123, offset, Vector2Int.zero);
                var weights = new List<float[]>();
                var polygons = field.Polygons(weights);
                var original = field.Polygons();
                Assert.AreEqual(original.Count, polygons.Count);
                Assert.AreEqual(polygons.Count, weights.Count);
                for (int p = 0; p < polygons.Count; p++)
                {
                    CollectionAssert.AreEqual(original[p], polygons[p]);
                    Assert.AreEqual(polygons[p].Length, weights[p].Length);
                    for (int v = 0; v < polygons[p].Length; v++)
                    {
                        float expected = 0f;
                        for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            if (polygons[p][v].Equals(field.Position(x, y)))
                                expected = Mathf.Clamp01((samples[y * size + x] - AsteroidGeometry.IsoLevel) /
                                    (byte.MaxValue - AsteroidGeometry.IsoLevel));
                        Assert.AreEqual(expected, weights[p][v], 0.000001f, $"Case {code}, vertex {v}");
                    }
                }
            }
        }

        [Test]
        public void VertexOffsets_SurviveComponentExtractionAndRecentering()
        {
            for (uint seed = 0; seed < 100; seed++)
            {
                var field = AsteroidGeometry.Generate(seed, 6.5f);
                field.ConfigurePositions(seed ^ 99, 1f, Vector2Int.zero);
                field.Crack(new Vector2(1f, 7f), seed + 5);
                foreach (AsteroidGeometry component in field.Components())
                {
                    var child = component.Recenter(out Vector2 offset);
                    Assert.AreEqual(component.Area(), child.Area(), 0.0001f, $"Seed {seed}");
                    var before = component.Polygons().SelectMany(p => p).ToArray();
                    var after = child.Polygons().SelectMany(p => p).ToArray();
                    Assert.AreEqual(before.Length, after.Length, $"Seed {seed}");
                    Vector2 translation = offset + Vector2.one *
                        ((component.Width - 1) * 0.5f - (child.Width - 1) * 0.5f);
                    for (int i = 0; i < before.Length; i++)
                        Assert.Less(Vector2.Distance(before[i], after[i] + translation), 0.00001f, $"Seed {seed}");
                }
            }
        }

        [Test]
        public void Components_KeepTheirPositionsWhenParentOrSiblingIsReconfigured()
        {
            var field = Solid();
            field.ConfigurePositions(123, 1f, Vector2Int.zero);
            for (int y = 0; y < field.Height; y++) field.Samples[y * field.Width + 4] = 0;
            var components = field.Components();
            Assert.AreEqual(2, components.Count);
            Vector2 original = field.Position(2, 2);
            field.ConfigurePositions(123, 0f, Vector2Int.zero);
            components[1].ConfigurePositions(124, 1f, Vector2Int.zero);
            Assert.AreEqual(original, components[0].Position(2, 2));
            var child = components[0].Recenter(out Vector2 offset);
            Vector2 translation = offset + Vector2.one * ((field.Width - child.Width) * 0.5f);
            Vector2 childGrid = new Vector2(2, 2) - translation;
            Assert.Less(Vector2.Distance(original, child.Position((int)childGrid.x, (int)childGrid.y) + translation), 0.00001f);
        }

        [Test]
        public void Excavation_UsesOffsetSamplePositions()
        {
            var field = Solid();
            field.ConfigurePositions(123, 1f, Vector2Int.zero);
            Vector2 point = field.Position(4, 4);
            const float radius = 0.1f;
            const float strength = 10f;
            field.Erode(point, radius, strength);
            Assert.AreEqual(245, field.Samples[4 * field.Width + 4]);
            Assert.AreEqual(255, field.Samples[2 * field.Width + 2]);
        }

        [Test]
        public void Explosion_UsesOffsetSamplePositions()
        {
            var field = Solid();
            field.ConfigurePositions(123, 1f, Vector2Int.zero);
            Vector2 point = field.Position(4, 4);
            field.ErodeDamage(point, Vector2.zero, Vector2.right, Vector2.up, 0.6f, 10f, false);
            Assert.AreEqual(245, field.Samples[40]);
        }

        [Test]
        public void PointImpact_UsesNearestOffsetSample()
        {
            var field = Solid();
            field.ConfigurePositions(123, 1f, Vector2Int.zero);
            Vector2 point = field.Position(4, 4);
            field.ErodeDamage(point, Vector2.right, Vector2.right, Vector2.up, 0.5f, 256f, true);
            Assert.AreEqual(0, field.Samples[40]);
            Assert.AreEqual(254, field.Samples[41]);
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
            Vector2 point = Vector2.one * 7.5f - offset;
            Assert.AreEqual(0.75f, field.ErodeImpact(point, Vector2.zero, 0.75f, false));
            Assert.AreEqual(254, field.Samples[7 * 16 + 7]);
            for (int i = 0; i < 1016; i++) field.ErodeImpact(point, Vector2.zero, 0.25f, false);
            Assert.AreEqual(127, field.Samples[7 * 16 + 7]);
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
            Vector2 hole = Vector2.one * 7.5f - offset;
            Assert.IsFalse(AsteroidGeometry.Contains(fitted.Polygons(), hole));
            Assert.AreEqual(1, fitted.Components().Count);
            Assert.AreEqual(16, fitted.Width);
            Assert.AreEqual(16, fitted.Height);
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
                Assert.AreEqual(256, fit.Samples.Length);
                Assert.AreEqual(1, fit.Components().Count);
                Assert.AreEqual(piece.Area(), fit.Area(), 0.00001f);
                Assert.AreEqual(Solidness(piece), Solidness(fit));
            }
        }

        [Test]
        public void InvalidFieldsAndExcavation_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AsteroidGeometry(17, 16, new byte[272]));
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
                var large = AsteroidGeometry.Generate(seed, 6.5f);
                Assert.AreEqual(256, small.Samples.Length);
                Assert.AreEqual(256, large.Samples.Length);
                Assert.Greater(large.Area(), small.Area() * 5f);
                for (int i = 0; i < small.Samples.Length; i++) Assert.GreaterOrEqual(large.Samples[i], small.Samples[i]);
                Assert.That(AsteroidGeometry.InitialRadius(seed, 2f, 6.5f), Is.InRange(2f, 6.5f));
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

        [Test]
        public void Surface_UsesDisplacedVerticesAndPreservesPlanesAndWalls()
        {
            const uint seed = 123;
            var field = new AsteroidGeometry(16, 16, Enumerable.Repeat((byte)255, 256).ToArray());
            field.ConfigurePositions(seed, 1f, Vector2Int.zero);
            var surface = new AsteroidSurface(seed, Vector2.one * 7.5f, Vector2.one, 1f, 0.8f, 5f, 1f);
            Vector2[] cell = {
                surface.Local(field.Position(7, 7)), surface.Local(field.Position(8, 7)),
                surface.Local(field.Position(8, 8)), surface.Local(field.Position(7, 8))
            };
            var facets = surface.Facets(cell);
            Assert.AreEqual(AsteroidGeometry.PolygonArea(cell), facets.Sum(AsteroidGeometry.PolygonArea), 0.00001f);
            foreach (Vector2 corner in cell)
                Assert.IsTrue(facets.SelectMany(facet => facet).Any(point => Vector2.Distance(point, corner) < 0.00001f));
            Assert.IsFalse(facets.SelectMany(facet => facet)
                .Any(point => Vector2.Distance(surface.Source(point), new Vector2(7, 7)) < 0.00001f),
                "The roof must not introduce the old undisplaced grid vertex.");
            foreach (Vector2[] facet in facets)
            for (int i = 1; i < facet.Length - 1; i++)
            {
                Vector2 centre = (facet[0] + facet[i] + facet[i + 1]) / 3f;
                float height = (surface.Front(facet[0]) + surface.Front(facet[i]) + surface.Front(facet[i + 1])) / 3f;
                Assert.AreEqual(height, surface.Front(centre), 0.0001f);
            }
            Vector2 start = surface.Local(field.Position(5, 6));
            Vector2 end = surface.Local(field.Position(10, 9));
            var cuts = surface.WallCuts(start, end);
            for (int i = 1; i < cuts.Count; i++)
            {
                Vector2 a = Vector2.Lerp(start, end, cuts[i - 1]);
                Vector2 b = Vector2.Lerp(start, end, cuts[i]);
                Assert.AreEqual((surface.Front(a) + surface.Front(b)) * 0.5f,
                    surface.Front((a + b) * 0.5f), 0.0002f);
            }
            Vector2 offset = new Vector2(2f, -1f);
            var child = surface.Child(offset);
            foreach (Vector2 corner in cell)
                Assert.AreEqual(surface.Front(corner), child.Front(corner - offset), 0.00001f);
        }

        [TestCase(2f, 1f)]
        [TestCase(6f, 2f)]
        public void Surface_DomeRisesTowardTheOriginalCentreAndScalesWithTheRock(float radius, float spacing)
        {
            var domed = new AsteroidSurface(123, Vector2.one * 7.5f, Vector2.one / spacing, 1f, 0.4f, radius);
            var flat = new AsteroidSurface(123, domed.Offset, domed.Scale, domed.Depth, domed.Relief, 0f);
            Vector2 middle = Vector2.right * (radius * spacing * 0.5f);
            Vector2 edge = Vector2.right * (radius * spacing);
            float rise = flat.Front(Vector2.zero) - domed.Front(Vector2.zero);
            float middleRise = flat.Front(middle) - domed.Front(middle);
            Assert.AreEqual(spacing * Mathf.Sqrt(radius * radius - 0.5f), rise, 0.00001f);
            float inner = radius * 0.5f - 0.5f, outer = radius * 0.5f + 0.5f;
            Assert.AreEqual(spacing * 0.5f * (Mathf.Sqrt(radius * radius - inner * inner - 0.25f) +
                Mathf.Sqrt(radius * radius - outer * outer - 0.25f)), middleRise, 0.00001f);
            Assert.GreaterOrEqual(flat.Front(edge), domed.Front(edge));
            Assert.Greater(rise, middleRise);
            Assert.Throws<ArgumentException>(() => new AsteroidSurface(0, Vector2.zero, Vector2.one, 1f, 0f, float.NaN));
            Assert.Throws<ArgumentException>(() => new AsteroidSurface(0, Vector2.zero, Vector2.one, 1f, 0f, -1f));
        }
    }
}
