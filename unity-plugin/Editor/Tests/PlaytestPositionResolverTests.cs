// TDD: PlaytestPositionResolver — pure-logic + parser integration tests, EditMode safe.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestPositionResolverTests : SceneTestBase
    {
        GameObject _stub;

        [SetUp]
        public void SetUp()
        {
            _stub = new GameObject("TestObj");
            _stub.transform.position = new Vector3(10f, 5f, 3f);
            PlaytestPositionResolver._findOverride = _ => _stub;
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestPositionResolver._findOverride = null;
            if (_stub != null) UnityEngine.Object.DestroyImmediate(_stub);
        }

        // ── T1: literal passthrough ───────────────────────────────────────────────

        [Test]
        public void Resolve_Literal_ReturnsVector()
        {
            PlaytestPositionResolver._findOverride = null; // not needed
            var v = PlaytestPositionResolver.Resolve("3,1.5,0");
            Assert.AreEqual(new Vector3(3f, 1.5f, 0f), v);
        }

        // ── T2: @-path, object found ──────────────────────────────────────────────

        [Test]
        public void Resolve_AtPath_ReturnsWorldPosition()
        {
            var v = PlaytestPositionResolver.Resolve("@/Player.position");
            Assert.AreEqual(_stub.transform.position, v);
        }

        // ── T3: positive offset ───────────────────────────────────────────────────

        [Test]
        public void Resolve_AtPath_PlusOffset()
        {
            var v = PlaytestPositionResolver.Resolve("@/P.position + (1,0,0)");
            Assert.AreEqual(_stub.transform.position + new Vector3(1f, 0f, 0f), v);
        }

        // ── T4: negative offset ───────────────────────────────────────────────────

        [Test]
        public void Resolve_AtPath_MinusOffset()
        {
            var v = PlaytestPositionResolver.Resolve("@/P.position - (0,2,0)");
            Assert.AreEqual(_stub.transform.position - new Vector3(0f, 2f, 0f), v);
        }

        // ── T5: offset without parens ─────────────────────────────────────────────

        [Test]
        public void Resolve_AtPath_OffsetNoParen()
        {
            var v = PlaytestPositionResolver.Resolve("@/P.position + 1,0,0");
            Assert.AreEqual(_stub.transform.position + new Vector3(1f, 0f, 0f), v);
        }

        // ── T6: object not found → ArgumentException ──────────────────────────────

        [Test]
        public void Resolve_ObjectNotFound_Throws()
        {
            PlaytestPositionResolver._findOverride = _ => null;
            var ex = Assert.Throws<ArgumentException>(() => PlaytestPositionResolver.Resolve("@/Missing.position"));
            StringAssert.Contains("not found", ex.Message);
        }

        // ── T7: missing .position ─────────────────────────────────────────────────

        [Test]
        public void Resolve_MissingDotPosition_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => PlaytestPositionResolver.Resolve("@/Player"));
            StringAssert.Contains(".position", ex.Message);
        }

        // ── T8: empty string ──────────────────────────────────────────────────────

        [Test]
        public void Resolve_EmptyString_Throws()
        {
            PlaytestPositionResolver._findOverride = null;
            Assert.Throws<ArgumentException>(() => PlaytestPositionResolver.Resolve(""));
        }

        // ── T9: bad offset floats ─────────────────────────────────────────────────

        [Test]
        public void Resolve_BadOffsetFloat_Throws()
        {
            Assert.Throws<ArgumentException>(() => PlaytestPositionResolver.Resolve("@/P.position + (a,b,c)"));
        }

        // ── T10: case-insensitive .position ──────────────────────────────────────

        [Test]
        public void Resolve_UppercasePosition_Works()
        {
            var v = PlaytestPositionResolver.Resolve("@/P.POSITION");
            Assert.AreEqual(_stub.transform.position, v);
        }

        // ── T11: unknown path → null from override → throws ───────────────────────

        [Test]
        public void Resolve_PathWithSpaces_ThrowsNotFound()
        {
            PlaytestPositionResolver._findOverride = path => path.Contains(" ") ? null : _stub;
            var ex = Assert.Throws<ArgumentException>(() =>
                PlaytestPositionResolver.Resolve("@/Object With Spaces.position"));
            StringAssert.Contains("not found", ex.Message);
        }

        // ── T12: parser — MOVE literal → Position set, RawPosition null ──────────

        [Test]
        public void Parse_MOVE_Literal_PositionSet_RawNull()
        {
            var steps = PlaytestParser.Parse("MOVE TO 1,2,3");
            Assert.AreEqual(1, steps.Count);
            Assert.IsNull(steps[0].RawPosition);
            Assert.AreEqual(1f, steps[0].Position.x);
            Assert.AreEqual(2f, steps[0].Position.y);
            Assert.AreEqual(3f, steps[0].Position.z);
        }

        // ── T13: parser — MOVE @-expr → RawPosition set, Position default ─────────

        [Test]
        public void Parse_MOVE_AtExpr_RawPositionSet()
        {
            var steps = PlaytestParser.Parse("MOVE TO @/P.position");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("@/P.position", steps[0].RawPosition);
            Assert.AreEqual(0f, steps[0].Position.x);
            Assert.AreEqual(0f, steps[0].Position.y);
            Assert.AreEqual(0f, steps[0].Position.z);
        }

        // ── T14: parser — TELEPORT @-expr → RawPosition set ──────────────────────

        [Test]
        public void Parse_TELEPORT_AtExpr_RawPositionSet()
        {
            var steps = PlaytestParser.Parse("TELEPORT /Player @/Spawn.position");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("@/Spawn.position", steps[0].RawPosition);
        }

        // ── T15: parser — MOVE_PATH mixed literal + ref ───────────────────────────

        [Test]
        public void Parse_MOVEPATH_Mixed_EachStepCorrect()
        {
            var steps = PlaytestParser.Parse("MOVE_PATH 0,0,0 > @/A.position > 5,0,5");
            Assert.AreEqual(3, steps.Count);
            // first: literal
            Assert.IsNull(steps[0].RawPosition);
            Assert.AreEqual(0f, steps[0].Position.x);
            Assert.AreEqual(0f, steps[0].Position.y);
            Assert.AreEqual(0f, steps[0].Position.z);
            // second: @-ref
            Assert.AreEqual("@/A.position", steps[1].RawPosition);
            // third: literal
            Assert.IsNull(steps[2].RawPosition);
            Assert.AreEqual(5f, steps[2].Position.x);
            Assert.AreEqual(0f, steps[2].Position.y);
            Assert.AreEqual(5f, steps[2].Position.z);
        }
    }
}
