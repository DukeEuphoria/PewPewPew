using System;
using System.Collections.Generic;
using NUnit.Framework;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Tests
{
    public class TreasureGeometryTests
    {
        private static readonly List<Vector2[]> Square = new List<Vector2[]>
        {
            new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }
        };

        [Test]
        public void Footprint_RequiresPositiveRockOverlap()
        {
            Assert.IsTrue(TreasureGeometry.Overlaps(Square, new Vector2(0.5f, 0.5f), 0.1f));
            Assert.IsTrue(TreasureGeometry.Overlaps(Square, new Vector2(1.1f, 0.5f), 0.2f));
            Assert.IsFalse(TreasureGeometry.Overlaps(Square, new Vector2(1.2f, 0.5f), 0.2f));
            Assert.IsFalse(TreasureGeometry.Overlaps(Square, new Vector2(2f, 0.5f), 0.2f));
        }

        [Test]
        public void Footprint_RejectsInvalidInputs()
        {
            Assert.Throws<ArgumentNullException>(() => TreasureGeometry.Overlaps(null, Vector2.zero, 1f));
            Assert.Throws<ArgumentException>(() => TreasureGeometry.Overlaps(Square, Vector2.zero, 0f));
            Assert.Throws<ArgumentException>(() => TreasureGeometry.Overlaps(Square, new Vector2(float.NaN, 0f), 1f));
            Assert.Throws<ArgumentException>(() => TreasureGeometry.SupportingComponent(
                Array.Empty<List<Vector2[]>>(), Array.Empty<bool>(), Vector2.zero, 0f));
        }

        [Test]
        public void Footprint_TracksErodedMarchingSquares()
        {
            AsteroidGeometry field = AsteroidGeometryTests.Solid();
            Vector2 centre = Vector2.one * 4f;
            Assert.IsTrue(TreasureGeometry.Overlaps(field.Polygons(), centre, 0.25f));
            field.ErodeImpact(centre, Vector2.zero, 255f, false);
            Assert.IsFalse(TreasureGeometry.Overlaps(field.Polygons(), centre, 0.25f));
            Assert.IsTrue(TreasureGeometry.Overlaps(field.Polygons(), centre, 0.75f));
        }

        [Test]
        public void SupportingComponent_ReleasesOnSplitOrDiscard()
        {
            var other = new List<Vector2[]>
            {
                new[] { Vector2.right * 2f, Vector2.right * 3f, new Vector2(3f, 1f), new Vector2(2f, 1f) }
            };
            var components = new[] { Square, other };
            var retained = new[] { true, true };
            Assert.AreEqual(0, TreasureGeometry.SupportingComponent(components, retained, new Vector2(0.5f, 0.5f), 0.25f));
            Assert.AreEqual(-1, TreasureGeometry.SupportingComponent(components, retained, new Vector2(1.5f, 0.5f), 0.6f));
            Assert.AreEqual(-1, TreasureGeometry.SupportingComponent(components, new[] { true, false },
                new Vector2(1.5f, 0.5f), 0.6f));
            Assert.AreEqual(-1, TreasureGeometry.SupportingComponent(components, new[] { false, true },
                new Vector2(0.5f, 0.5f), 0.25f));
            Assert.AreEqual(-1, TreasureGeometry.SupportingComponent(components, retained, new Vector2(4f, 0.5f), 0.25f));
        }
    }
}
