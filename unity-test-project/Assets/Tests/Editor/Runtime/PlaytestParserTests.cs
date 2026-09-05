using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class PlaytestParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ---------- Parse Tests ----------

        [Test]
        public void Parse_EmptyScript_ReturnsEmpty()
        {
            Assert.That(PlaytestParser.Parse("").Count, Is.EqualTo(0));
            Assert.That(PlaytestParser.Parse("   \n  \n").Count, Is.EqualTo(0));
            Assert.Throws<NullReferenceException>(() => PlaytestParser.Parse(null));
        }

        [Test]
        public void Parse_CommentsAndBlankLines_Skipped()
        {
            var steps = PlaytestParser.Parse("# comment\n\n# another");
            Assert.That(steps.Count, Is.EqualTo(0));
        }

        [Test]
        public void Parse_MoveWithPath()
        {
            var steps = PlaytestParser.Parse("MOVE /GridPlayer TO 1,2,3");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Move));
            Assert.That(steps[0].Path, Is.EqualTo("/GridPlayer"));
            Assert.That(steps[0].Position.x, Is.EqualTo(1f));
            Assert.That(steps[0].Position.y, Is.EqualTo(2f));
            Assert.That(steps[0].Position.z, Is.EqualTo(3f));
        }

        [Test]
        public void Parse_MoveWithoutPath()
        {
            var steps = PlaytestParser.Parse("MOVE TO 1,2,3");
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Move));
            Assert.That(steps[0].Path, Is.Null);
            Assert.That(steps[0].Position.x, Is.EqualTo(1f));
            Assert.That(steps[0].Position.y, Is.EqualTo(2f));
            Assert.That(steps[0].Position.z, Is.EqualTo(3f));
        }

        [Test]
        public void Parse_Wait()
        {
            var steps = PlaytestParser.Parse("WAIT 2.5");
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Wait));
            Assert.That(steps[0].Delay, Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void Parse_WaitUntil_WithTimeout()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL Currency == 200 TIMEOUT 10");
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.WaitUntil));
            Assert.That(s.Query, Is.EqualTo("Currency"));
            Assert.That(s.Op, Is.EqualTo("=="));
            Assert.That(s.Value, Is.EqualTo("200"));
            Assert.That(s.Timeout, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void Parse_WaitUntil_DefaultTimeout()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL Currency == 200");
            Assert.That(steps[0].Timeout, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void Parse_Assert()
        {
            var steps = PlaytestParser.Parse("ASSERT Currency > 100");
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.Assert));
            Assert.That(s.Query, Is.EqualTo("Currency"));
            Assert.That(s.Op, Is.EqualTo(">"));
            Assert.That(s.Value, Is.EqualTo("100"));
        }

        [Test]
        public void Parse_AssertConsoleClean()
        {
            var steps = PlaytestParser.Parse("ASSERT_CONSOLE_CLEAN");
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }

        [Test]
        public void Parse_Snapshot()
        {
            var steps = PlaytestParser.Parse("SNAPSHOT Currency, GridPlayer.Corn, GridPlayer.Eggs");
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.Snapshot));
            Assert.That(s.Queries, Has.Length.EqualTo(3));
            Assert.That(s.Queries[0].Trim(), Is.EqualTo("Currency"));
        }

        [Test]
        public void Parse_Invoke()
        {
            var steps = PlaytestParser.Parse("INVOKE /Player Health TakeDamage 50");
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.Invoke));
            Assert.That(s.Path, Is.EqualTo("/Player"));
            Assert.That(s.Component, Is.EqualTo("Health"));
            Assert.That(s.Method, Is.EqualTo("TakeDamage"));
            Assert.That(s.Args, Is.EqualTo("50"));
        }

        [Test]
        public void Parse_Set()
        {
            // SET path comp field value → Path=tokens[1], Comp=tokens[2], Method=tokens[3](field), Args=tokens[4](value)
            var steps = PlaytestParser.Parse("SET /Player Health hp 100");
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.Set));
            Assert.That(s.Path, Is.EqualTo("/Player"));
            Assert.That(s.Component, Is.EqualTo("Health"));
            Assert.That(s.Method, Is.EqualTo("hp"));
            Assert.That(s.Args, Is.EqualTo("100"));
        }

        [Test]
        public void Parse_Log()
        {
            var steps = PlaytestParser.Parse("LOG Test started");
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Log));
            Assert.That(steps[0].Message, Is.EqualTo("Test started"));
        }

        [Test]
        public void Parse_TimeScale()
        {
            var steps = PlaytestParser.Parse("TIMESCALE 3.0");
            Assert.That(steps[0].Type, Is.EqualTo(StepType.TimeScale));
            Assert.That(steps[0].Delay, Is.EqualTo(3.0f));
        }

        [Test]
        public void Parse_InvalidCommand_Throws()
        {
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse("BLAH foo"));
        }

        // ---------- Resolver Tests ----------

        [Test]
        public void ResolveQuery_AliasFound()
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            config.aliases.Add(new QueryAlias { alias = "Currency", path = "/Money", component = "CurrencyComp", field = "Value" });
            var (path, comp, field) = PlaytestParser.ResolveQuery("Currency", config);
            Assert.That(path, Is.EqualTo("/Money"));
            Assert.That(comp, Is.EqualTo("CurrencyComp"));
            Assert.That(field, Is.EqualTo("Value"));
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ResolveQuery_PipeSeparated()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/GridPlayer|Health|hp", null);
            Assert.That(path, Is.EqualTo("/GridPlayer"));
            Assert.That(comp, Is.EqualTo("Health"));
            Assert.That(field, Is.EqualTo("hp"));
        }

        [Test]
        public void ResolveQuery_NoMatch()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("rawQuery", null);
            Assert.That(path, Is.EqualTo("rawQuery"));
            Assert.That(comp, Is.EqualTo(""));
            Assert.That(field, Is.EqualTo(""));
        }

        [Test]
        public void ResolveQuery_NullConfig_PipeFormatWorks()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/P|C|F", null);
            Assert.That(path, Is.EqualTo("/P"));
            Assert.That(comp, Is.EqualTo("C"));
            Assert.That(field, Is.EqualTo("F"));
        }

        // ---------- Compare Tests ----------

        [Test]
        public void Compare_NumericGreater()
        {
            Assert.That(PlaytestParser.Compare("10", ">", "5"), Is.True);
            Assert.That(PlaytestParser.Compare("3", ">", "5"), Is.False);
        }

        [Test]
        public void Compare_NumericEqual()
        {
            Assert.That(PlaytestParser.Compare("5.0", "==", "5"), Is.True);
            Assert.That(PlaytestParser.Compare("5.1", "==", "5"), Is.False);
        }

        [Test]
        public void Compare_StringEqual_CaseInsensitive()
        {
            Assert.That(PlaytestParser.Compare("True", "==", "true"), Is.True);
            Assert.That(PlaytestParser.Compare("Hello", "==", "hello"), Is.True);
        }

        [Test]
        public void Compare_Contains()
        {
            Assert.That(PlaytestParser.Compare("hello world", "contains", "world"), Is.True);
            Assert.That(PlaytestParser.Compare("hello world", "contains", "xyz"), Is.False);
        }

        [Test]
        public void Compare_InvalidOperator_Throws()
        {
            Assert.Throws<ArgumentException>(() => PlaytestParser.Compare("5", "??", "3"));
        }

        // ---------- VAL (alias) Tests ----------

        [Test]
        public void Parse_Alias_ReplacesInSubsequentLines()
        {
            var steps = PlaytestParser.Parse("VAL $money /Money|TestPlayableAPI|money\nASSERT $money >= 100");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Assert));
            Assert.That(steps[0].Query, Is.EqualTo("/Money|TestPlayableAPI|money"));
        }

        [Test]
        public void Parse_Alias_MultipleAliases()
        {
            var steps = PlaytestParser.Parse(
                "VAL $hp /Player|Health|hp\nVAL $gold /Shop|Currency|gold\nASSERT $hp > 0\nASSERT $gold == 0");
            Assert.That(steps.Count, Is.EqualTo(2));
            Assert.That(steps[0].Query, Is.EqualTo("/Player|Health|hp"));
            Assert.That(steps[1].Query, Is.EqualTo("/Shop|Currency|gold"));
        }

        // ---------- TELEPORT Tests ----------

        [Test]
        public void Parse_Teleport_WithPath()
        {
            var steps = PlaytestParser.Parse("TELEPORT /GridPlayer 5,0,3");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Teleport));
            Assert.That(steps[0].Path, Is.EqualTo("/GridPlayer"));
            Assert.That(steps[0].Position.x, Is.EqualTo(5f));
            Assert.That(steps[0].Position.y, Is.EqualTo(0f));
            Assert.That(steps[0].Position.z, Is.EqualTo(3f));
        }

        // ---------- ASSERT_BATCH Tests ----------

        [Test]
        public void Parse_AssertBatch_CollectsAll()
        {
            var script = "ASSERT_BATCH\nASSERT hp > 0\nASSERT gold >= 100\nASSERT alive == True\nEND";
            var steps = PlaytestParser.Parse(script);
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertBatch));
            Assert.That(steps[0].BatchOps, Is.Not.Null);
            Assert.That(steps[0].BatchOps.Length, Is.EqualTo(3));
            Assert.That(steps[0].BatchOps[0], Is.EqualTo(">"));
            Assert.That(steps[0].BatchValues[0], Is.EqualTo("0"));
        }

        [Test]
        public void Parse_AssertBatch_SkipsComments()
        {
            var script = "ASSERT_BATCH\n# comment\nASSERT hp > 0\n# another\nASSERT gold >= 100\nEND";
            var steps = PlaytestParser.Parse(script);
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].BatchOps.Length, Is.EqualTo(2));
        }

        // ---------- ASSERT_NEAR Tests ----------

        [Test]
        public void Parse_AssertNear_Basic()
        {
            var steps = PlaytestParser.Parse("ASSERT_NEAR /A /B 2.0");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertNear));
            Assert.That(steps[0].Path, Is.EqualTo("/A"));
            Assert.That(steps[0].Value, Is.EqualTo("/B"));
            Assert.That(steps[0].Delay, Is.EqualTo(2.0f).Within(0.001f));
        }

        // ---------- ASSERT_CONSOLE_CLEAN IGNORE Tests ----------

        [Test]
        public void Parse_AssertConsoleClean_WithIgnore()
        {
            var steps = PlaytestParser.Parse("ASSERT_CONSOLE_CLEAN IGNORE \"DOTween warning\", \"shader\"");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
            Assert.That(steps[0].Queries, Is.Not.Null);
            Assert.That(steps[0].Queries.Length, Is.EqualTo(2));
            Assert.That(steps[0].Queries[0], Is.EqualTo("DOTween warning"));
            Assert.That(steps[0].Queries[1], Is.EqualTo("shader"));
        }

        [Test]
        public void Parse_AssertConsoleClean_WithoutIgnore_StillWorks()
        {
            var steps = PlaytestParser.Parse("ASSERT_CONSOLE_CLEAN");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
            Assert.That(steps[0].Queries, Is.Null);
        }

        // ---------- CAPTURE / ASSERT_CAPTURED / INVARIANT / ASSERT_CONSERVED Tests ----------

        [Test]
        public void Parse_Capture()
        {
            var steps = PlaytestParser.Parse("CAPTURE money /Money|CurrencyComp|Value");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Capture));
            Assert.That(steps[0].Message, Is.EqualTo("money"));
            Assert.That(steps[0].Query, Is.EqualTo("/Money|CurrencyComp|Value"));
        }

        [Test]
        public void Parse_AssertCaptured_Simple()
        {
            var steps = PlaytestParser.Parse("ASSERT_CAPTURED money INCREASED");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertCaptured));
            Assert.That(steps[0].Message, Is.EqualTo("money"));
            Assert.That(steps[0].Op, Is.EqualTo("INCREASED"));
            Assert.That(steps[0].Args, Is.Null.Or.Empty);
        }

        [Test]
        public void Parse_AssertCaptured_WithBy()
        {
            var steps = PlaytestParser.Parse("ASSERT_CAPTURED money INCREASED_BY >= 50");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertCaptured));
            Assert.That(steps[0].Message, Is.EqualTo("money"));
            Assert.That(steps[0].Op, Is.EqualTo("INCREASED_BY"));
            Assert.That(steps[0].Args, Is.EqualTo(">="));
            Assert.That(steps[0].Value, Is.EqualTo("50"));
        }

        [Test]
        public void Parse_Invariant()
        {
            var steps = PlaytestParser.Parse("INVARIANT /Money|CurrencyComp|Value >= 0");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Invariant));
            Assert.That(steps[0].Query, Is.EqualTo("/Money|CurrencyComp|Value"));
            Assert.That(steps[0].Op, Is.EqualTo(">="));
            Assert.That(steps[0].Value, Is.EqualTo("0"));
        }

        [Test]
        public void Parse_AssertConserved()
        {
            var steps = PlaytestParser.Parse("ASSERT_CONSERVED SUM item_a + item_b == CONSTANT OVER 3.0");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConserved));
            Assert.That(steps[0].Queries, Is.Not.Null);
            Assert.That(steps[0].Queries.Length, Is.EqualTo(2));
            Assert.That(steps[0].Queries[0], Is.EqualTo("item_a"));
            Assert.That(steps[0].Queries[1], Is.EqualTo("item_b"));
            Assert.That(steps[0].Delay, Is.EqualTo(3.0f).Within(0.001f));
        }

        // ---------- SIMULATE Tests ----------

        [Test]
        public void Parse_Simulate_Basic()
        {
            var steps = PlaytestParser.Parse("SIMULATE random_walk DURATION 60");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Simulate));
            Assert.That(steps[0].SimulatorName, Is.EqualTo("random_walk"));
            Assert.That(steps[0].Timeout, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void Parse_Simulate_AllKeywords()
        {
            var steps = PlaytestParser.Parse("SIMULATE random_walk DURATION 60 TIMESCALE 2 TARGET \"/Player\" FREQUENCY 5");
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.Simulate));
            Assert.That(s.SimulatorName, Is.EqualTo("random_walk"));
            Assert.That(s.Timeout, Is.EqualTo(60f).Within(0.001f));
            Assert.That(s.Delay, Is.EqualTo(2f).Within(0.001f));
            Assert.That(s.Path, Is.EqualTo("/Player"));
            Assert.That(s.Value, Is.EqualTo("5"));
        }

        // ---------- MONITOR Tests ----------

        [Test]
        public void Parse_Monitor_Start()
        {
            var steps = PlaytestParser.Parse("MONITOR economy");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Monitor));
            Assert.That(steps[0].Query, Is.EqualTo("economy"));
        }

        [Test]
        public void Parse_Monitor_Stop()
        {
            var steps = PlaytestParser.Parse("MONITOR STOP");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Monitor));
            Assert.That(steps[0].Query, Is.Null.Or.Empty);
        }

        // ---------- TRACE_FLOW Tests ----------

        [Test]
        public void Parse_TraceFlow()
        {
            var steps = PlaytestParser.Parse("TRACE_FLOW FROM /A TO /B FIELD Count TIMEOUT 10");
            Assert.That(steps.Count, Is.EqualTo(1));
            var s = steps[0];
            Assert.That(s.Type, Is.EqualTo(StepType.TraceFlow));
            Assert.That(s.Path, Is.EqualTo("/A"));
            Assert.That(s.Query, Is.EqualTo("/B"));
            Assert.That(s.Method, Is.EqualTo("Count"));
            Assert.That(s.Timeout, Is.EqualTo(10f).Within(0.001f));
        }

        // ---------- ASSERT_CTA Tests ----------

        [Test]
        public void Parse_AssertCta_Visible()
        {
            var steps = PlaytestParser.Parse("ASSERT_CTA VISIBLE");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertCta));
            Assert.That(steps[0].Op, Is.EqualTo("VISIBLE"));
        }

        [Test]
        public void Parse_AssertCta_Clickable()
        {
            var steps = PlaytestParser.Parse("ASSERT_CTA CLICKABLE");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertCta));
            Assert.That(steps[0].Op, Is.EqualTo("CLICKABLE"));
        }

        // ---------- FOR loop tests ----------

        [Test]
        public void For_ExtremeRange_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("FOR $i IN -2147483648..2147483647\nWAIT 1\nEND_FOR"));
            StringAssert.Contains("max 10000", ex.Message);
        }

        [Test]
        public void For_IterVar_NotReplacedInsideWord()
        {
            // $i must not mangle $items → 0tems
            var steps = PlaytestParser.Parse("FOR $i IN 0..1\nASSERT /$items|Comp|field == val\nEND_FOR");
            // $items should survive whole-word guard (not be replaced by iter value)
            Assert.That(steps[0].Query, Does.Contain("$items"));
        }
    }
}
