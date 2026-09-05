// TDD: PlaytestDslExporter — pure static, no Unity API, EditMode safe.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestDslExporterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── StepToDsl: Move ────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_Move_WithPath_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Move, path = "/Player", position = new Vector3(1f, 0f, 3f) };
            Assert.AreEqual("MOVE /Player TO 1,0,3", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Move_NoPath_OmitsPath()
        {
            var s = new VisualStep { type = StepType.Move, position = new Vector3(5f, 2f, 0f) };
            Assert.AreEqual("MOVE TO 5,2,0", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Teleport_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Teleport, path = "/Enemy", position = new Vector3(-1f, 0f, 2f) };
            Assert.AreEqual("TELEPORT /Enemy -1,0,2", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Wait_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Wait, delay = 2.5f };
            Assert.AreEqual("WAIT 2.5", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_WaitUntil_Basic_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.WaitUntil, query = "q", op = "==", value = "1", timeout = 5f };
            Assert.AreEqual("WAIT_UNTIL q == 1 TIMEOUT 5", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_WaitUntil_WithAbort_AppendsAbort()
        {
            var s = new VisualStep { type = StepType.WaitUntil, query = "q", op = "==", value = "1", timeout = 5f, abortOnFail = true };
            Assert.AreEqual("WAIT_UNTIL q == 1 TIMEOUT 5 ABORT", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Assert_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Assert, query = "hp", op = ">", value = "0" };
            Assert.AreEqual("ASSERT hp > 0", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_AssertConsoleClean_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.AssertConsoleClean };
            Assert.AreEqual("ASSERT_CONSOLE_CLEAN", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Section_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Section, message = "Phase 1" };
            Assert.AreEqual("SECTION \"Phase 1\"", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Log_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Log, message = "hello world" };
            Assert.AreEqual("LOG hello world", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Invoke_WithArgs_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Invoke, path = "/P", component = "Health", method = "Take", args = "10" };
            Assert.AreEqual("INVOKE /P Health Take 10", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Invoke_NoArgs_OmitsArgs()
        {
            var s = new VisualStep { type = StepType.Invoke, path = "/P", component = "Health", method = "Reset", args = "" };
            Assert.AreEqual("INVOKE /P Health Reset", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_TimeScale_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.TimeScale, delay = 0.5f };
            Assert.AreEqual("TIMESCALE 0.5", PlaytestDslExporter.StepToDsl(s));
        }

        // ── Description prefix ─────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_WithDescription_PrependsDESCLine()
        {
            var s = new VisualStep { type = StepType.Wait, delay = 1f, description = "Wait a bit" };
            var dsl = PlaytestDslExporter.StepToDsl(s);
            StringAssert.StartsWith("DESC Wait a bit\n", dsl);
        }

        // ── Export header ──────────────────────────────────────────────────────────
        [Test]
        public void Export_WithGlobalAbort_PrependsAbortOnFail()
        {
            var steps = new List<VisualStep> { new VisualStep { type = StepType.Wait, delay = 1f } };
            StringAssert.StartsWith("ABORT_ON_FAIL", PlaytestDslExporter.Export(steps, true));
        }

        [Test]
        public void Export_WithoutGlobalAbort_NoAbortOnFail()
        {
            var steps = new List<VisualStep> { new VisualStep { type = StepType.Wait, delay = 1f } };
            StringAssert.DoesNotContain("ABORT_ON_FAIL", PlaytestDslExporter.Export(steps, false));
        }

        // ── Roundtrip: Export → Parse → count + types ──────────────────────────────
        [Test]
        public void Roundtrip_BasicSteps_PreservesCountAndTypes()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Wait, delay = 1f },
                new VisualStep { type = StepType.Assert, query = "hp", op = "==", value = "100" },
                new VisualStep { type = StepType.AssertConsoleClean },
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(3, parsed.Count);
            Assert.AreEqual(StepType.Wait, parsed[0].Type);
            Assert.AreEqual(StepType.Assert, parsed[1].Type);
            Assert.AreEqual(StepType.AssertConsoleClean, parsed[2].Type);
        }

        [Test]
        public void Roundtrip_Move_PreservesPositionAndPath()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Move, path = "/Player", position = new Vector3(1f, 2f, 3f) }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.Move, parsed[0].Type);
            Assert.AreEqual("/Player", parsed[0].Path);
            Assert.That(parsed[0].Position.x, Is.EqualTo(1f).Within(0.01f));
        }

        // D04: proves Float3 survives the full Composer round trip — VisualStep.position
        // (Vector3 setter, converts to Float3) → PlaytestDslExporter.Export (Vector3 getter,
        // converts back) → DSL text → PlaytestParser.Parse (Float3 via SetPosition).
        [Test]
        public void Roundtrip_Teleport_PreservesPositionAndPath()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Teleport, path = "/Enemy", position = new Vector3(-1f, 4f, 2f) }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.Teleport, parsed[0].Type);
            Assert.AreEqual("/Enemy", parsed[0].Path);
            Assert.That(parsed[0].Position.x, Is.EqualTo(-1f).Within(0.01f));
            Assert.That(parsed[0].Position.y, Is.EqualTo(4f).Within(0.01f));
            Assert.That(parsed[0].Position.z, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void Roundtrip_WaitUntil_PreservesAll()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.WaitUntil, query = "alive", op = "==", value = "true", timeout = 8f, abortOnFail = true }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.WaitUntil, parsed[0].Type);
            Assert.AreEqual("alive", parsed[0].Query);
            Assert.AreEqual(8f, parsed[0].Timeout, 0.001f);
            Assert.IsTrue(parsed[0].AbortOnFail);
        }

        [Test]
        public void Roundtrip_WaitUntil_PreservesOpAndValue()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.WaitUntil, query = "hp", op = ">", value = "50", timeout = 5f }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(">", parsed[0].Op);
            Assert.AreEqual("50", parsed[0].Value);
        }

        [Test]
        public void Roundtrip_Description_PreservesLabel()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Wait, delay = 1f, description = "Wait phase" }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual("Wait phase", parsed[0].Label);
        }

        [Test]
        public void Export_EmptyList_ReturnsEmpty()
        {
            Assert.AreEqual("", PlaytestDslExporter.Export(new List<VisualStep>(), false));
        }

        // ── FromParsed: field preservation ─────────────────────────────────────────
        [Test]
        public void FromParsed_Assert_PreservesFields()
        {
            var p = new PlaytestStep { Type = StepType.Assert, Query = "hp", Op = "==", Value = "100", Label = "Check health" };
            var v = PlaytestDslExporter.FromParsed(p);
            Assert.AreEqual(StepType.Assert, v.type);
            Assert.AreEqual("hp", v.query);
            Assert.AreEqual("==", v.op);
            Assert.AreEqual("100", v.value);
            Assert.AreEqual("Check health", v.description);
        }

        [Test]
        public void FromParsed_WaitUntil_PreservesAbortAndTimeout()
        {
            var p = new PlaytestStep { Type = StepType.WaitUntil, Query = "alive", Op = "==", Value = "true", Timeout = 10f, AbortOnFail = true };
            var v = PlaytestDslExporter.FromParsed(p);
            Assert.AreEqual(10f, v.timeout, 0.001f);
            Assert.IsTrue(v.abortOnFail);
        }

        [Test]
        public void FromParsed_Invoke_PreservesAllFields()
        {
            var p = new PlaytestStep { Type = StepType.Invoke, Path = "/E", Component = "AI", Method = "Alert", Args = "5" };
            var v = PlaytestDslExporter.FromParsed(p);
            Assert.AreEqual("/E", v.path);
            Assert.AreEqual("AI", v.component);
            Assert.AreEqual("Alert", v.method);
            Assert.AreEqual("5", v.args);
        }

        [Test]
        public void FromParsed_UnknownType_PreservesRawLine()
        {
            var p = new PlaytestStep { Type = StepType.Snapshot, RawLine = "SNAPSHOT hp,pos", Message = null };
            var v = PlaytestDslExporter.FromParsed(p);
            Assert.AreEqual(StepType.Snapshot, v.type);
            Assert.AreEqual("SNAPSHOT hp,pos", v.rawLine);
            Assert.AreEqual("SNAPSHOT hp,pos", PlaytestDslExporter.Export(
                new List<VisualStep> { v }, false));
        }

        [Test]
        public void Roundtrip_LoadedUnsupportedStep_PreservesRawLineVerbatim()
        {
            const string raw = "  SNAPSHOT hp,pos  ";
            var parsed = PlaytestParser.Parse(raw);
            var visual = PlaytestDslExporter.FromParsed(parsed[0]);

            Assert.AreEqual(StepType.Snapshot, visual.type);
            Assert.AreEqual(raw, visual.rawLine);
            Assert.AreEqual(raw, PlaytestDslExporter.Export(
                new List<VisualStep> { visual }, false));
        }

        [Test]
        public void Roundtrip_LoadedUnsupportedStep_ExportsResolvedSelfContainedLine()
        {
            const string dsl = "VAL $target hp,pos\nSNAPSHOT $target";
            var parsed = PlaytestParser.Parse(dsl);
            var visual = PlaytestDslExporter.FromParsed(parsed[0]);
            var exported = PlaytestDslExporter.Export(
                new List<VisualStep> { visual }, false);
            var reparsed = PlaytestParser.Parse(exported);

            Assert.AreEqual("SNAPSHOT hp,pos", visual.rawLine);
            Assert.AreEqual("SNAPSHOT hp,pos", exported);
            Assert.IsTrue(reparsed.Warnings == null || reparsed.Warnings.Count == 0);
            CollectionAssert.AreEqual(parsed[0].Queries, reparsed[0].Queries);
        }

        [Test]
        public void Roundtrip_LoadedUnsupportedStep_PreservesPathPrefixSemantics()
        {
            const string dsl =
                "PATH_PREFIX /Level\nVAL $target /Door|DoorState|open\nSNAPSHOT $target";
            var parsed = PlaytestParser.Parse(dsl);
            var visual = PlaytestDslExporter.FromParsed(parsed[0]);
            var exported = PlaytestDslExporter.Export(
                new List<VisualStep> { visual }, false);
            var reparsed = PlaytestParser.Parse(exported);

            Assert.AreEqual("SNAPSHOT /Level/Door|DoorState|open", exported);
            Assert.IsTrue(reparsed.Warnings == null || reparsed.Warnings.Count == 0);
            CollectionAssert.AreEqual(parsed[0].Queries, reparsed[0].Queries);
        }

        [Test]
        public void SelectableTypes_AllValidateAndRoundtrip()
        {
            foreach (var type in PlaytestDslExporter.SelectableTypes)
            {
                var visual = ValidStep(type);
                Assert.IsNull(PlaytestStepValidator.GetValidationError(visual), type.ToString());

                var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(
                    new List<VisualStep> { visual }, false));
                Assert.AreEqual(1, parsed.Count, type.ToString());
                Assert.AreEqual(type, parsed[0].Type, type.ToString());
            }
        }

        private static VisualStep ValidStep(StepType type) => type switch
        {
            StepType.Move => new VisualStep { type = type, position = Vector3.one },
            StepType.Teleport => new VisualStep { type = type, path = "/Target", position = Vector3.one },
            StepType.Wait => new VisualStep { type = type, delay = 1f },
            StepType.WaitUntil => new VisualStep { type = type, query = "q", op = "==", value = "1", timeout = 5f },
            StepType.Assert => new VisualStep { type = type, query = "q", op = "==", value = "1" },
            StepType.AssertConsoleClean => new VisualStep { type = type },
            StepType.Log => new VisualStep { type = type, message = "message" },
            StepType.Section => new VisualStep { type = type, message = "section" },
            StepType.Invoke => new VisualStep { type = type, path = "/Target", component = "Health", method = "Reset" },
            StepType.TimeScale => new VisualStep { type = type, delay = 1f },
            StepType.Monitor => new VisualStep { type = type, query = "health" },
            StepType.Set => new VisualStep { type = type, path = "/Target", component = "Health", method = "value", args = "1" },
            StepType.Click => new VisualStep { type = type, path = "/UI/Button" },
            StepType.Invariant => new VisualStep { type = type, query = "q", op = ">=", value = "0" },
            StepType.Capture => new VisualStep { type = type, message = "capture", query = "q" },
            StepType.AssertCaptured => new VisualStep { type = type, message = "capture", op = "DELTA" },
            StepType.AssertNear => new VisualStep { type = type, path = "/A", value = "/B", delay = 1f },
            _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
        };

        // ── Value quoting ──────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_Assert_ValueWithSpaces_QuotesValue()
        {
            var s = new VisualStep { type = StepType.Assert, query = "q", op = "==", value = "hello world" };
            Assert.AreEqual("ASSERT q == \"hello world\"", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_WaitUntil_ValueWithSpaces_QuotesValue()
        {
            var s = new VisualStep { type = StepType.WaitUntil, query = "q", op = "==", value = "hello world", timeout = 5f };
            Assert.AreEqual("WAIT_UNTIL q == \"hello world\" TIMEOUT 5", PlaytestDslExporter.StepToDsl(s));
        }

        // ── Skip unsupported empty steps ───────────────────────────────────────────
        [Test]
        public void Export_SkipsUnsupportedEmptySteps()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Snapshot, message = "" },
                new VisualStep { type = StepType.Wait, delay = 2f },
            };
            var result = PlaytestDslExporter.Export(steps, false);
            StringAssert.DoesNotContain("SNAPSHOT", result);
            StringAssert.Contains("WAIT 2", result);
        }

        // ── Set ────────────────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_Set_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Set, path = "/Player", component = "Health", method = "hp", args = "100" };
            Assert.AreEqual("SET /Player Health hp 100", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void Roundtrip_Set_PreservesAllFields()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Set, path = "/Player", component = "Health", method = "hp", args = "100" }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.Set, parsed[0].Type);
            Assert.AreEqual("/Player", parsed[0].Path);
            Assert.AreEqual("Health",  parsed[0].Component);
            Assert.AreEqual("hp",      parsed[0].Method);
            Assert.AreEqual("100",     parsed[0].Args);
        }

        // ── Click ──────────────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_Click_NoWait_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Click, path = "/UI/StartBtn" };
            Assert.AreEqual("CLICK /UI/StartBtn", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_Click_WithWait_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Click, path = "/UI/StartBtn", delay = 0.5f };
            Assert.AreEqual("CLICK /UI/StartBtn WAIT 0.5", PlaytestDslExporter.StepToDsl(s));
        }

        // ── Invariant ──────────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_Invariant_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Invariant, query = "/P|Health|hp", op = ">=", value = "0" };
            Assert.AreEqual("INVARIANT /P|Health|hp >= 0", PlaytestDslExporter.StepToDsl(s));
        }

        // ── Capture ────────────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_Capture_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.Capture, message = "label1", query = "/P|Health|hp" };
            Assert.AreEqual("CAPTURE label1 /P|Health|hp", PlaytestDslExporter.StepToDsl(s));
        }

        // ── AssertCaptured ─────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_AssertCaptured_BasicMode_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.AssertCaptured, message = "label1", op = "DELTA" };
            Assert.AreEqual("ASSERT_CAPTURED label1 DELTA", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_AssertCaptured_WithArgsAndValue_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.AssertCaptured, message = "label1", op = "DELTA", args = "<", value = "5" };
            Assert.AreEqual("ASSERT_CAPTURED label1 DELTA < 5", PlaytestDslExporter.StepToDsl(s));
        }

        // ── AssertNear ─────────────────────────────────────────────────────────────
        [Test]
        public void StepToDsl_AssertNear_ProducesCorrectLine()
        {
            var s = new VisualStep { type = StepType.AssertNear, path = "/A", value = "/B", delay = 0.5f };
            Assert.AreEqual("ASSERT_NEAR /A /B 0.5", PlaytestDslExporter.StepToDsl(s));
        }

        // ── AssertConsoleClean with IGNORE ─────────────────────────────────────────
        [Test]
        public void StepToDsl_AssertConsoleClean_WithIgnore_EmitsIgnoreClause()
        {
            var s = new VisualStep { type = StepType.AssertConsoleClean, message = "NullRef" };
            Assert.AreEqual("ASSERT_CONSOLE_CLEAN IGNORE \"NullRef\"", PlaytestDslExporter.StepToDsl(s));
        }

        [Test]
        public void StepToDsl_AssertConsoleClean_EmptyMessage_NoIgnore()
        {
            var s = new VisualStep { type = StepType.AssertConsoleClean, message = "" };
            Assert.AreEqual("ASSERT_CONSOLE_CLEAN", PlaytestDslExporter.StepToDsl(s));
        }

        // ── Roundtrip: Capture / AssertCaptured / AssertNear ──────────────────────
        [Test]
        public void Roundtrip_Capture_PreservesLabelAndQuery()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Capture, message = "hp_snap", query = "/Player|Health|hp" }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.Capture, parsed[0].Type);
            Assert.AreEqual("hp_snap", parsed[0].Message);
            Assert.AreEqual("/Player|Health|hp", parsed[0].Query);
        }

        [Test]
        public void Roundtrip_AssertCaptured_BasicMode_PreservesFields()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.AssertCaptured, message = "hp_snap", op = "DELTA" }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.AssertCaptured, parsed[0].Type);
            Assert.AreEqual("hp_snap", parsed[0].Message);
            Assert.AreEqual("DELTA", parsed[0].Op);
        }

        [Test]
        public void Roundtrip_AssertNear_PreservesPathsAndThreshold()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.AssertNear, path = "/A", value = "/B", delay = 1.5f }
            };
            var parsed = PlaytestParser.Parse(PlaytestDslExporter.Export(steps, false));
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(StepType.AssertNear, parsed[0].Type);
            Assert.AreEqual("/A", parsed[0].Path);
            Assert.AreEqual("/B", parsed[0].Value);
            Assert.AreEqual(1.5f, parsed[0].Delay, 0.001f);
        }
    }
}
