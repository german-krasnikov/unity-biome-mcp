using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class GdSnapshotSerializerTests
    {
        // ── ToAliasLines: point ───────────────────────────────────────────────

        [Test]
        public void ToAliasLines_Point_EmitsOneAlias()
        {
            var snap = RegionSnapshot.CreatePoint("a1b2c3d4", new Vector2(1.5f, 2.5f), Array.Empty<string>(), "S", "spawn");
            var lines = GdSnapshotSerializer.ToAliasLines(snap);
            Assert.AreEqual(1, lines.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            StringAssert.StartsWith("ALIAS @spawn", lines);
            StringAssert.Contains("1.50", lines);
            StringAssert.Contains("0.00", lines); // PlaneY
            StringAssert.Contains("2.50", lines);
        }

        // ── ToAliasLines: polyline ────────────────────────────────────────────

        [Test]
        public void ToAliasLines_Polyline_EmitsNumberedAliases()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(5f, 5f) };
            var snap = RegionSnapshot.CreatePolyline("abcd1234", pts, Array.Empty<string>(), "S", "path");
            var lines = GdSnapshotSerializer.ToAliasLines(snap).Split(new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(3, lines.Length);
            StringAssert.StartsWith("ALIAS @path_0", lines[0]);
            StringAssert.StartsWith("ALIAS @path_1", lines[1]);
            StringAssert.StartsWith("ALIAS @path_2", lines[2]);
        }

        // ── ToAliasLines: measurement ─────────────────────────────────────────

        [Test]
        public void ToAliasLines_Measurement_EmitsStartEnd()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1m2m3m4", Vector2.zero, new Vector2(10f, 0f), "S", "gap");
            var lines = GdSnapshotSerializer.ToAliasLines(snap).Split(new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, lines.Length);
            StringAssert.StartsWith("ALIAS @gap_start", lines[0]);
            StringAssert.StartsWith("ALIAS @gap_end", lines[1]);
        }

        // ── ToAliasLines: region ──────────────────────────────────────────────

        [Test]
        public void ToAliasLines_Region_EmitsCenterAlias()
        {
            var snap = new RegionSnapshot
            {
                Id = "r1r2r3r4",
                AnnotationType = "region",
                Label = "zone",
                CenterX = 3f,
                CenterZ = 7f,
                PlaneY = 0f,
            };
            var lines = GdSnapshotSerializer.ToAliasLines(snap);
            StringAssert.StartsWith("ALIAS @zone", lines);
            StringAssert.Contains("3.00", lines);
            StringAssert.Contains("7.00", lines);
        }

        // ── Label sanitization ────────────────────────────────────────────────

        [Test]
        public void ToAliasLines_EmptyLabel_UsesGdPrefix()
        {
            var snap = RegionSnapshot.CreatePoint("deadbeef", new Vector2(1f, 2f), Array.Empty<string>(), "S");
            var lines = GdSnapshotSerializer.ToAliasLines(snap);
            StringAssert.StartsWith("ALIAS @gd_deadbeef", lines);
        }

        [Test]
        public void ToAliasLines_LabelWithSpaces_Sanitized()
        {
            var snap = RegionSnapshot.CreatePoint("id000001", new Vector2(0f, 0f), Array.Empty<string>(), "S", "My Spawn Point");
            var lines = GdSnapshotSerializer.ToAliasLines(snap);
            StringAssert.StartsWith("ALIAS @my_spawn_point", lines);
        }

        // ── Null/corrupt VerticesFlat guards ─────────────────────────────────

        [Test]
        public void ToAliasLines_MeasurementNullVertices_ReturnsNoVerticesComment()
        {
            var snap = new RegionSnapshot
            {
                Id = "m9m9m9m9",
                AnnotationType = "measurement",
                Label = "gap",
                VerticesFlat = null,
            };
            var result = GdSnapshotSerializer.ToAliasLines(snap);
            StringAssert.StartsWith("# @gap —", result);
            StringAssert.Contains("no vertices", result);
        }

        [Test]
        public void ToAliasLines_MeasurementTooFewVertices_ReturnsNoVerticesComment()
        {
            var snap = new RegionSnapshot
            {
                Id = "m8m8m8m8",
                AnnotationType = "measurement",
                Label = "short",
                VerticesFlat = new[] { 1f, 2f }, // only 2 elements, need 4
            };
            var result = GdSnapshotSerializer.ToAliasLines(snap);
            StringAssert.Contains("no vertices", result);
        }

        [Test]
        public void ToAliasLines_UnknownType_ReturnsUnknownTypeComment()
        {
            var snap = new RegionSnapshot
            {
                Id = "u1u1u1u1",
                AnnotationType = "circle",
                Label = "circ",
            };
            var result = GdSnapshotSerializer.ToAliasLines(snap);
            StringAssert.StartsWith("# @circ —", result);
            StringAssert.Contains("unknown type", result);
            StringAssert.Contains("circle", result);
        }

        // ── ToPlaytestPreamble ────────────────────────────────────────────────

        [Test]
        public void ToPlaytestPreamble_MultipleSnapshots_AllIncluded()
        {
            var snaps = new List<RegionSnapshot>
            {
                RegionSnapshot.CreatePoint("aa000000", new Vector2(1f, 2f), Array.Empty<string>(), "S", "alpha"),
                RegionSnapshot.CreatePoint("bb000000", new Vector2(3f, 4f), Array.Empty<string>(), "S", "beta"),
            };
            var preamble = GdSnapshotSerializer.ToPlaytestPreamble(snaps);
            StringAssert.Contains("ALIAS @alpha", preamble);
            StringAssert.Contains("ALIAS @beta", preamble);
        }
    }
}
