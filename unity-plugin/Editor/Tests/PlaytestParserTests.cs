// TDD: PlaytestParser pure-logic tests — no Unity API, EditMode safe.
// Compare drives every ASSERT in playtests; a bug silently passes all assertions.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
            finally { UnityEngine.Object.DestroyImmediate(config); }
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
            finally { UnityEngine.Object.DestroyImmediate(config); }
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
            var script = "VAL $hp /Player|Health|current\nASSERT $hp == 100";
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

        // ── #3: WAIT_UNTIL bool shorthand ────────────────────────────────────────

        [Test]
        public void WaitUntil_BoolSugar_Positive()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL $door_open");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitUntil, steps[0].Type);
            Assert.AreEqual("$door_open", steps[0].Query);
            Assert.AreEqual("==", steps[0].Op);
            Assert.AreEqual("True", steps[0].Value);
        }

        [Test]
        public void WaitUntil_BoolSugar_Negated()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL !$door_open");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitUntil, steps[0].Type);
            Assert.AreEqual("$door_open", steps[0].Query);
            Assert.AreEqual("==", steps[0].Op);
            Assert.AreEqual("False", steps[0].Value);
        }

        [Test]
        public void WaitUntil_StandardForm_UnchangedByFix()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL /Player|Health|value > 0 TIMEOUT 5");
            var s = steps[0];
            Assert.AreEqual(StepType.WaitUntil, s.Type);
            Assert.AreEqual("/Player|Health|value", s.Query);
            Assert.AreEqual(">", s.Op);
            Assert.AreEqual("0", s.Value);
        }

        [Test]
        public void WaitUntil_BoolSugar_RawPath_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("WAIT_UNTIL /Player|Health|ready"));
        }

        // ── #1: Unconditional $sigil warning ─────────────────────────────────────

        [Test]
        public void Parse_UnresolvedSigil_WarnedEvenWithNoDefs()
        {
            var result = PlaytestParser.Parse("ASSERT $health == 100");
            Assert.IsNotNull(result.Warnings, "expected warning for $health");
            StringAssert.Contains("$health", result.Warnings[0]);
        }

        [Test]
        public void Parse_ResolvedSigil_NoWarning()
        {
            var result = PlaytestParser.Parse("VAL $health /Player|Health|value\nASSERT $health == 100");
            Assert.IsNull(result.Warnings);
        }

        // ── #5: Levenshtein "Did you mean?" ─────────────────────────────────────

        [Test]
        public void Parse_UnresolvedSigil_SuggestsClosestMatch()
        {
            var result = PlaytestParser.Parse(
                "VAL $health /Player|Health|value\nASSERT $healt == 100");
            Assert.IsNotNull(result.Warnings);
            StringAssert.Contains("Did you mean $health", result.Warnings[0]);
        }

        [Test]
        public void Parse_UnresolvedSigil_NoSuggestion_FallsBackToHint()
        {
            var result = PlaytestParser.Parse(
                "VAL $health /Player|Health|value\nASSERT $xyz == 1");
            Assert.IsNotNull(result.Warnings);
            StringAssert.Contains("typo in VAL/VAR name", result.Warnings[0]);
            StringAssert.DoesNotContain("Did you mean", result.Warnings[0]);
        }

        [Test]
        public void Parse_UnresolvedSigil_NoDefs_NoSuggestion()
        {
            var result = PlaytestParser.Parse("ASSERT $health == 100");
            Assert.IsNotNull(result.Warnings);
            StringAssert.Contains("$health", result.Warnings[0]);
            StringAssert.DoesNotContain("Did you mean", result.Warnings[0]);
        }

        // ── #7 SET_DEFAULT_TIMEOUT ───────────────────────────────────────────────

        [Test]
        public void Parse_SetDefaultTimeout_SetsParseResultDefaultTimeout()
        {
            var r = PlaytestParser.Parse("SET_DEFAULT_TIMEOUT 3\nWAIT_UNTIL /Player|Health|hp > 0");
            Assert.AreEqual(3f, r.DefaultTimeout, 0.001f);
        }

        [Test]
        public void Parse_NoSetDefaultTimeout_DefaultTimeoutIsZero()
        {
            var r = PlaytestParser.Parse("ASSERT /Player|Health|hp == 100");
            Assert.AreEqual(0f, r.DefaultTimeout, 0.001f);
        }

        [Test]
        public void Parse_SetDefaultTimeout_MissingValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse("SET_DEFAULT_TIMEOUT"));
        }

        [Test]
        public void Parse_WaitUntilWithTimeout_HasExplicitTimeoutTrue()
        {
            var r = PlaytestParser.Parse("WAIT_UNTIL /Enemy|Health|hp == 0 TIMEOUT 7");
            Assert.IsTrue(r[0].HasExplicitTimeout);
            Assert.AreEqual(7f, r[0].Timeout, 0.001f);
        }

        [Test]
        public void Parse_WaitUntilWithoutTimeout_HasExplicitTimeoutFalse()
        {
            var r = PlaytestParser.Parse("WAIT_UNTIL /Enemy|Health|hp == 0");
            Assert.IsFalse(r[0].HasExplicitTimeout);
        }

        // ── #6 ASSERT TIMEOUT ────────────────────────────────────────────────────

        [Test]
        public void Parse_AssertWithTimeout_HasExplicitTimeoutTrueAndTimeoutSet()
        {
            var r = PlaytestParser.Parse("ASSERT /Player|Health|hp > 0 TIMEOUT 5");
            Assert.IsTrue(r[0].HasExplicitTimeout);
            Assert.AreEqual(5f, r[0].Timeout, 0.001f);
            Assert.AreEqual(StepType.Assert, r[0].Type);
        }

        [Test]
        public void Parse_AssertWithoutTimeout_HasExplicitTimeoutFalse()
        {
            var r = PlaytestParser.Parse("ASSERT /Player|Health|hp > 0");
            Assert.IsFalse(r[0].HasExplicitTimeout);
            Assert.AreEqual(StepType.Assert, r[0].Type);
        }

        [Test]
        public void Parse_AssertWithTimeoutAndAs_BothParsed()
        {
            var r = PlaytestParser.Parse("ASSERT /Enemy|Health|hp == 0 TIMEOUT 3 AS enemy dead");
            Assert.IsTrue(r[0].HasExplicitTimeout);
            Assert.AreEqual(3f, r[0].Timeout, 0.001f);
            Assert.AreEqual("enemy dead", r[0].Message);
        }

        // ── #8 ASSERT_ONE_ACTIVE ─────────────────────────────────────────────────

        [Test]
        public void Parse_AssertOneActive_ParsesQueryArray()
        {
            var r = PlaytestParser.Parse("ASSERT_ONE_ACTIVE /Cam_Intro /Cam_Menu /Cam_Game");
            Assert.AreEqual(StepType.AssertOneActive, r[0].Type);
            CollectionAssert.AreEqual(new[] { "/Cam_Intro", "/Cam_Menu", "/Cam_Game" }, r[0].Queries);
        }

        [Test]
        public void Parse_AssertOneActive_OnePath_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("ASSERT_ONE_ACTIVE /Cam_Intro"));
        }

        [Test]
        public void Parse_AssertOneActive_ZeroPaths_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("ASSERT_ONE_ACTIVE"));
        }

        [Test]
        public void Assert_AsLabel_WithTimeout_LabelClean()
        {
            var r = PlaytestParser.Parse("ASSERT /Obj|Comp|f == 1 AS my_label TIMEOUT 3");
            Assert.AreEqual("my_label", r.Steps[0].Message);
            Assert.AreEqual(3f, r.Steps[0].Timeout);
        }

        // ── G2: INVOKE / INVOKE_REPEAT multi-arg parsing ──────────────────────

        [Test]
        public void Parse_Invoke_MultipleArgs_AllArgsParsed()
        {
            // G2: "INVOKE /Player Health TakeDamage 42 true" — second arg "true" was dropped.
            var r = PlaytestParser.Parse("INVOKE /Player Health TakeDamage 42 true");
            var step = r.Steps[0];
            Assert.AreEqual(StepType.Invoke, step.Type);
            Assert.AreEqual("42 true", step.Args,
                "G2: INVOKE must capture all args after method name, space-joined");
        }

        [Test]
        public void Parse_InvokeRepeat_MultipleArgs_AllArgsParsed()
        {
            // G2: INVOKE_REPEAT expands to N Invoke steps; each must carry all args.
            var r = PlaytestParser.Parse("INVOKE_REPEAT 2 /Player Health TakeDamage 42 true");
            var first = r.Steps[0];
            Assert.AreEqual(StepType.Invoke, first.Type);
            Assert.AreEqual("42 true", first.Args,
                "G2: INVOKE_REPEAT must capture all args after method name, space-joined");
        }
    }
}
