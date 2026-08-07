// Tests for Polygon2D: construction, PIP (winding number), metrics, CSV round-trip, simplification.
// EditMode only — no Unity scene objects needed. Pure math tests.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    public class Polygon2DTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Shared test data ──────────────────────────────────────────────────
        // All polygons are CCW (positive area in standard math / XZ plane)

        static readonly Vector2[] UnitSquare    = { V(0,0), V(1,0), V(1,1), V(0,1) };
        static readonly Vector2[] UnitTriangle  = { V(0,0), V(1,0), V(0.5f,1) };
        static readonly Vector2[] LShape        = { V(0,0), V(2,0), V(2,1), V(1,1), V(1,2), V(0,2) };
        static readonly Vector2[] Figure8       = { V(0,0), V(1,1), V(0,1), V(1,0) };
        static readonly Vector2[] LargeCoords   = { V(50000,50000), V(50001,50000), V(50001,50001), V(50000,50001) };
        static readonly Vector2[] Diamond       = { V(0,-1), V(1,0), V(0,1), V(-1,0) };
        static readonly Vector2[] CWSquare      = { V(0,0), V(0,1), V(1,1), V(1,0) };
        static readonly Vector2[] ColinearStrip = { V(0,0), V(1,0), V(2,0), V(2,1), V(0,1) };

        static Vector2 V(float x, float y) => new Vector2(x, y);

        // ── Construction ──────────────────────────────────────────────────────

        [Test]
        public void Ctor_Triangle_CountIs3()
            => Assert.AreEqual(3, new Polygon2D(UnitTriangle).Count);

        [Test]
        public void Ctor_Square_CountIs4()
            => Assert.AreEqual(4, new Polygon2D(UnitSquare).Count);

        [Test]
        public void Ctor_MakesDefensiveCopy()
        {
            var verts = (Vector2[])UnitSquare.Clone();
            var p = new Polygon2D(verts);
            verts[0] = new Vector2(999, 999);
            Assert.AreNotEqual(999f, p.Vertices[0].x);
        }

        [Test]
        public void Ctor_ClosingDuplicate_Stripped()
        {
            // 5 verts where last == first → stored as 4
            var verts = new[] { V(0,0), V(1,0), V(1,1), V(0,1), V(0,0) };
            Assert.AreEqual(4, new Polygon2D(verts).Count);
        }

        [Test]
        public void Ctor_NearClosingDuplicate_NotStripped()
        {
            // Last vertex close but dist > 1e-5 — kept
            var verts = new[] { V(0,0), V(1,0), V(1,1), V(0.001f, 0) };
            Assert.AreEqual(4, new Polygon2D(verts).Count);
        }

        [Test]
        public void Ctor_Null_ThrowsArgumentException()
            => Assert.Throws<System.ArgumentException>(() => new Polygon2D((Vector2[])null));

        [Test]
        public void Ctor_TwoVertices_ThrowsArgumentException()
            => Assert.Throws<System.ArgumentException>(() => new Polygon2D(new[] { V(0,0), V(1,0) }));

        [Test]
        public void Ctor_Empty_ThrowsArgumentException()
            => Assert.Throws<System.ArgumentException>(() => new Polygon2D(new Vector2[0]));

        [Test]
        public void ProjectXZ_DropsYCoordinate()
        {
            var pts = new[] { new Vector3(1, 100, 2), new Vector3(3, -50, 4), new Vector3(5, 0, 6) };
            var result = Polygon2D.ProjectXZ(pts);
            Assert.AreEqual(new Vector2(1, 2), result[0]);
            Assert.AreEqual(new Vector2(3, 4), result[1]);
        }

        // ── PIP: Unit Square ──────────────────────────────────────────────────

        [Test]
        public void Square_Center_Inside()
            => Assert.IsTrue(new Polygon2D(UnitSquare).Contains(V(0.5f, 0.5f)));

        [Test]
        public void Square_OutsideRight_NotInside()
            => Assert.IsFalse(new Polygon2D(UnitSquare).Contains(V(2, 0.5f)));

        [Test]
        public void Square_OutsideAbove_NotInside()
            => Assert.IsFalse(new Polygon2D(UnitSquare).Contains(V(0.5f, 2)));

        [Test]
        public void Square_OutsideBelow_NotInside()
            => Assert.IsFalse(new Polygon2D(UnitSquare).Contains(V(0.5f, -1)));

        [Test]
        public void Square_Corner_DoesNotThrow()
            => Assert.DoesNotThrow(() => new Polygon2D(UnitSquare).Contains(V(0, 0)));

        [Test]
        public void Square_EdgePoint_DoesNotThrow()
            => Assert.DoesNotThrow(() => new Polygon2D(UnitSquare).Contains(V(0.5f, 0)));

        // ── PIP: Triangle ─────────────────────────────────────────────────────

        [Test]
        public void Triangle_Centroid_Inside()
            => Assert.IsTrue(new Polygon2D(UnitTriangle).Contains(V(0.5f, 0.33f)));

        [Test]
        public void Triangle_FarOutside_NotInside()
            => Assert.IsFalse(new Polygon2D(UnitTriangle).Contains(V(5, 5)));

        [Test]
        public void Triangle_JustOutside_NotInside()
            => Assert.IsFalse(new Polygon2D(UnitTriangle).Contains(V(0.9f, 0.9f)));

        // ── PIP: Concave (L-Shape) ────────────────────────────────────────────

        [Test]
        public void LShape_InMainBody_Inside()
            => Assert.IsTrue(new Polygon2D(LShape).Contains(V(0.5f, 0.5f)));

        [Test]
        public void LShape_InExtension_Inside()
            => Assert.IsTrue(new Polygon2D(LShape).Contains(V(0.5f, 1.5f)));

        [Test]
        public void LShape_InConcavePocket_NotInside()
            => Assert.IsFalse(new Polygon2D(LShape).Contains(V(1.5f, 1.5f)));

        // ── PIP: CW winding ───────────────────────────────────────────────────

        [Test]
        public void CWSquare_Center_Inside()
        {
            // Winding number works regardless of CW/CCW winding direction
            Assert.IsTrue(new Polygon2D(CWSquare).Contains(V(0.5f, 0.5f)));
        }

        // ── PIP: Large coordinates ────────────────────────────────────────────

        [Test]
        public void LargeCoords_Center_Inside()
            => Assert.IsTrue(new Polygon2D(LargeCoords).Contains(V(50000.5f, 50000.5f)));

        [Test]
        public void LargeCoords_Outside_NotInside()
            => Assert.IsFalse(new Polygon2D(LargeCoords).Contains(V(49999, 50000.5f)));

        // ── PIP: Self-intersecting figure-8 ──────────────────────────────────

        [Test]
        public void Figure8_BelowDiagonal_Inside()
            // For Figure8={V(0,0),V(1,1),V(0,1),V(1,0)}, bottom-left region wn=-1 (inside)
            => Assert.IsTrue(new Polygon2D(Figure8).Contains(V(0.25f, 0.25f)));

        [Test]
        public void Figure8_AboveDiagonal_Outside()
            // For Figure8 with this winding, top-right point has wn=0 (outside, NonZero rule)
            => Assert.IsFalse(new Polygon2D(Figure8).Contains(V(0.75f, 0.75f)));

        // ── Metrics ───────────────────────────────────────────────────────────

        [Test]
        public void Area_UnitSquare_Is1()
            => Assert.AreEqual(1f, new Polygon2D(UnitSquare).Area(), 0.001f);

        [Test]
        public void Area_Triangle_IsHalf()
            => Assert.AreEqual(0.5f, new Polygon2D(UnitTriangle).Area(), 0.001f);

        [Test]
        public void Centroid_UnitSquare_Is05_05()
        {
            var c = new Polygon2D(UnitSquare).Centroid();
            Assert.AreEqual(0.5f, c.x, 0.001f);
            Assert.AreEqual(0.5f, c.y, 0.001f);
        }

        [Test]
        public void Centroid_Diamond_IsOrigin()
        {
            var c = new Polygon2D(Diamond).Centroid();
            Assert.AreEqual(0f, c.x, 0.001f);
            Assert.AreEqual(0f, c.y, 0.001f);
        }

        // ── CSV round-trip ────────────────────────────────────────────────────

        [Test]
        public void RoundTrip_UnitSquare_PreservesVertices()
        {
            var poly = new Polygon2D(UnitSquare);
            var csv = poly.ToCsv();
            var restored = Polygon2D.FromCsv(csv);
            Assert.AreEqual(poly.Count, restored.Count);
            for (int i = 0; i < poly.Count; i++)
            {
                Assert.AreEqual(poly.Vertices[i].x, restored.Vertices[i].x, 0.01f);
                Assert.AreEqual(poly.Vertices[i].y, restored.Vertices[i].y, 0.01f);
            }
        }

        [Test]
        public void RoundTrip_LargeCoords_WithinTolerance()
        {
            var poly = new Polygon2D(LargeCoords);
            var csv = poly.ToCsv();
            var restored = Polygon2D.FromCsv(csv);
            Assert.AreEqual(poly.Vertices[0].x, restored.Vertices[0].x, 0.01f);
        }

        [Test]
        public void ToCsv_UsesInvariantCulture()
        {
            // Verify decimal point is '.' not ',' (de-DE locale guard)
            var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");
                var poly = new Polygon2D(new[] { V(1.5f, 2.3f), V(3.7f, 4.1f), V(5.9f, 6.8f) });
                var csv = poly.ToCsv();
                StringAssert.Contains("1.50", csv);
                StringAssert.DoesNotContain("1,50", csv);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void FromCsv_TooFewVertices_Throws()
            => Assert.Throws<System.ArgumentException>(() => Polygon2D.FromCsv("0,0;1,0"));

        [Test]
        public void FromCsv_BadFormat_Throws()
            => Assert.Throws<System.ArgumentException>(() => Polygon2D.FromCsv("0,0;bad;1,1"));

        [Test]
        public void FromCsv_EmptyString_Throws()
            => Assert.Throws<System.ArgumentException>(() => Polygon2D.FromCsv(""));

        [Test]
        public void FromCsv_ExtremeCoordinates_Throws()
            => Assert.Throws<System.ArgumentException>(() => Polygon2D.FromCsv("200000,0;0,200000;1,1"));

        // ── Simplification ────────────────────────────────────────────────────

        [Test]
        public void Simplify_Triangle_Unchanged()
        {
            var poly = new Polygon2D(UnitTriangle);
            Assert.AreEqual(3, poly.Simplify(0.1f).Count);
        }

        [Test]
        public void Simplify_NeverReducesBelowThree()
        {
            // Aggressive epsilon should still keep at least 3 vertices
            var poly = new Polygon2D(UnitSquare);
            Assert.GreaterOrEqual(poly.Simplify(100f).Count, 3);
        }

        [Test]
        public void Simplify_ColinearMiddle_Removed()
        {
            // Square with extra point in middle of bottom edge
            var verts = new[] { V(0,0), V(0.5f,0), V(1,0), V(1,1), V(0,1) };
            var poly = new Polygon2D(verts);
            var result = poly.Simplify(0.01f);
            Assert.Less(result.Count, 5);
        }

        [Test]
        public void Simplify_ColinearVertexNearClosingEdge_Removed()
        {
            // V(0,0.5f) is collinear on the closing edge (0,1)→(0,0)
            var verts = new[] { V(0,0), V(1,0), V(1,1), V(0.5f,1), V(0,1), V(0,0.5f) };
            var result = new Polygon2D(verts).Simplify(0.01f);
            Assert.Less(result.Count, 6);
        }

        // ── Bounds ────────────────────────────────────────────────────────────

        [Test]
        public void ComputeBounds_UnitSquare_CorrectMinMax()
        {
            var b = new Polygon2D(UnitSquare).ComputeBounds();
            Assert.AreEqual(0f, b.xMin, 0.001f);
            Assert.AreEqual(0f, b.yMin, 0.001f);
            Assert.AreEqual(1f, b.xMax, 0.001f);
            Assert.AreEqual(1f, b.yMax, 0.001f);
        }

        [Test]
        public void ComputeBounds_Diamond_CorrectMinMax()
        {
            var b = new Polygon2D(Diamond).ComputeBounds();
            Assert.AreEqual(-1f, b.xMin, 0.001f);
            Assert.AreEqual(-1f, b.yMin, 0.001f);
            Assert.AreEqual(1f,  b.xMax, 0.001f);
            Assert.AreEqual(1f,  b.yMax, 0.001f);
        }

        // ── ContainsBatch ─────────────────────────────────────────────────────

        [Test]
        public void ContainsBatch_MixedPoints_ReturnsCorrectIndices()
        {
            var poly = new Polygon2D(UnitSquare);
            var pts = new[] { V(0.5f, 0.5f), V(2, 2), V(0.1f, 0.1f), V(-1, -1) };
            var inside = poly.ContainsBatch(pts);
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, inside);
        }

        [Test]
        public void ContainsBatch_AllOutside_EmptyList()
        {
            var poly = new Polygon2D(UnitSquare);
            var pts = new[] { V(2, 2), V(-1, -1) };
            Assert.IsEmpty(poly.ContainsBatch(pts));
        }
    }
}
