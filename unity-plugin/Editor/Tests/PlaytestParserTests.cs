// TDD: PlaytestParser pure-logic tests — no Unity API, EditMode safe.
// Compare drives every ASSERT in playtests; a bug silently passes all assertions.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestParserTests
    {
        // ── Compare: numeric equality ────────────────────────────────────────────

        [Test]
        public void Compare_FieldEquals_ReturnsPassed()
        {
            Assert.IsTrue(PlaytestParser.Compare("42", "==", "42"));
        }

        [Test]
        public void Compare_FieldEquals_WrongValue_ReturnsFailed()
        {
            Assert.IsFalse(PlaytestParser.Compare("42", "==", "99"));
        }

        [Test]
        public void Compare_NumericEquals_FloatTolerance()
        {
            // Within 0.001 tolerance
            Assert.IsTrue(PlaytestParser.Compare("1.0", "==", "1.0009"));
        }

        [Test]
        public void Compare_NumericNotEquals_ReturnsFailed_WhenEqual()
        {
            Assert.IsFalse(PlaytestParser.Compare("10", "!=", "10"));
        }

        [Test]
        public void Compare_NumericGreater_ReturnsTrue_WhenActualLarger()
        {
            Assert.IsTrue(PlaytestParser.Compare("5", ">", "3"));
        }

        [Test]
        public void Compare_NumericGreater_ReturnsFalse_WhenActualSmaller()
        {
            Assert.IsFalse(PlaytestParser.Compare("2", ">", "3"));
        }

        [Test]
        public void Compare_NumericGreaterOrEqual_ReturnsTrue_WhenEqual()
        {
            Assert.IsTrue(PlaytestParser.Compare("3", ">=", "3"));
        }

        [Test]
        public void Compare_NumericLess_ReturnsTrue_WhenActualSmaller()
        {
            Assert.IsTrue(PlaytestParser.Compare("1", "<", "2"));
        }

        [Test]
        public void Compare_NumericLessOrEqual_ReturnsTrue_WhenEqual()
        {
            Assert.IsTrue(PlaytestParser.Compare("5", "<=", "5"));
        }

        // ── Compare: string equality ─────────────────────────────────────────────

        [Test]
        public void Compare_StringEquals_CaseInsensitive_ReturnsTrue()
        {
            Assert.IsTrue(PlaytestParser.Compare("True", "==", "true"));
        }

        [Test]
        public void Compare_StringEquals_Mismatch_ReturnsFalse()
        {
            Assert.IsFalse(PlaytestParser.Compare("False", "==", "True"));
        }

        [Test]
        public void Compare_StringNotEquals_DifferentValues_ReturnsTrue()
        {
            Assert.IsTrue(PlaytestParser.Compare("Idle", "!=", "Running"));
        }

        // ── Compare: contains ────────────────────────────────────────────────────

        [Test]
        public void Compare_FieldContains_Substring_Passes()
        {
            Assert.IsTrue(PlaytestParser.Compare("Hello World", "contains", "World"));
        }

        [Test]
        public void Compare_FieldContains_MissingSubstring_Fails()
        {
            Assert.IsFalse(PlaytestParser.Compare("Hello World", "contains", "xyz"));
        }

        // ── ResolveQuery: pipe notation ──────────────────────────────────────────

        [Test]
        public void ResolveQuery_DotNotation_FindsNestedField()
        {
            // pipe notation: path|component|field
            var (path, comp, field) = PlaytestParser.ResolveQuery("/Player|Health|value", null);
            Assert.AreEqual("/Player", path);
            Assert.AreEqual("Health", comp);
            Assert.AreEqual("value", field);
        }

        [Test]
        public void ResolveQuery_TwoParts_ReturnsPathAndComp()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/Enemy|Rigidbody", null);
            Assert.AreEqual("/Enemy", path);
            Assert.AreEqual("Rigidbody", comp);
            Assert.AreEqual("", field);
        }

        [Test]
        public void ResolveQuery_NoPipe_ReturnsQueryAsPath()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/SomeObject", null);
            Assert.AreEqual("/SomeObject", path);
            Assert.AreEqual("", comp);
            Assert.AreEqual("", field);
        }

        [Test]
        public void ResolveQuery_WithConfig_UsesAliasWhenFound()
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            try
            {
                config.aliases.Add(new QueryAlias
                {
                    alias = "hp",
                    path = "/Player",
                    component = "Health",
                    field = "current"
                });

                var (path, comp, field) = PlaytestParser.ResolveQuery("hp", config);
                Assert.AreEqual("/Player", path);
                Assert.AreEqual("Health", comp);
                Assert.AreEqual("current", field);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ResolveQuery_WithConfig_FallsBackToPipeWhenAliasNotFound()
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            try
            {
                var (path, comp, field) = PlaytestParser.ResolveQuery("/X|Y|Z", config);
                Assert.AreEqual("/X", path);
                Assert.AreEqual("Y", comp);
                Assert.AreEqual("Z", field);
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ── Parse: ASSERT line ───────────────────────────────────────────────────

        [Test]
        public void Parse_AssertLine_ExtractsPathAndCondition()
        {
            var steps = PlaytestParser.Parse("ASSERT /Player|Health|hp == 100");
            Assert.AreEqual(1, steps.Count);
            var s = steps[0];
            Assert.AreEqual(StepType.Assert, s.Type);
            Assert.AreEqual("/Player|Health|hp", s.Query);
            Assert.AreEqual("==", s.Op);
            Assert.AreEqual("100", s.Value);
        }

        [Test]
        public void Parse_WaitLine_ExtractsDelay()
        {
            var steps = PlaytestParser.Parse("WAIT 2.5");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Wait, steps[0].Type);
            Assert.AreEqual(2.5f, steps[0].Delay, 0.001f);
        }

        [Test]
        public void Parse_CommentLine_IsSkipped()
        {
            var steps = PlaytestParser.Parse("# this is a comment\nASSERT /X|C|f == 1");
            Assert.AreEqual(1, steps.Count);
        }

        [Test]
        public void Parse_EmptyScript_ReturnsEmptyList()
        {
            var steps = PlaytestParser.Parse("");
            Assert.AreEqual(0, steps.Count);
        }

        [Test]
        public void Parse_AliasSubstitution_AppliedBeforeParsing()
        {
            var script = "ALIAS hp /Player|Health|current\nASSERT hp == 100";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("/Player|Health|current", steps[0].Query);
        }

        [Test]
        public void Parse_AssertConsoleLine_ExtractsType()
        {
            var steps = PlaytestParser.Parse("ASSERT_CONSOLE_CLEAN");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.AssertConsoleClean, steps[0].Type);
        }

        [Test]
        public void Parse_WaitUntil_ExtractsQueryOpValue()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|C|f == 1");
            Assert.AreEqual(1, steps.Count);
            var s = steps[0];
            Assert.AreEqual(StepType.WaitUntil, s.Type);
            Assert.AreEqual("/P|C|f", s.Query);
            Assert.AreEqual("==", s.Op);
            Assert.AreEqual("1", s.Value);
        }
    }
}
