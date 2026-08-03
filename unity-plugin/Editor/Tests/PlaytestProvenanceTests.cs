// TDD: P1.2 Macro Stack Provenance — pure-logic tests, EditMode safe.
// Covers: SourcedLine struct, PlaytestStep provenance fields, FormatProvenance, failure report integration.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestProvenanceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── 1. Direct lines have null SourceFile and MacroStack ──────────────────

        [Test]
        public void Parse_DirectLine_HasNullSourceFileAndMacroStack()
        {
            var result = PlaytestParser.Parse("ASSERT /A|B|C == 1");
            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result[0].SourceFile);
            Assert.IsNull(result[0].MacroStack);
        }

        // ── 2. SourceLine tracks 0-based index in inline script ──────────────────

        [Test]
        public void Parse_MultipleDirectLines_SourceLineIsLineIndex()
        {
            var result = PlaytestParser.Parse("LOG first\nLOG second\nLOG third");
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(0, result[0].SourceLine);
            Assert.AreEqual(1, result[1].SourceLine);
            Assert.AreEqual(2, result[2].SourceLine);
        }

        // ── 3. SECTION sets SectionContext on subsequent steps ────────────────────

        [Test]
        public void Parse_SectionLabel_SetsContextOnSubsequentSteps()
        {
            var result = PlaytestParser.Parse("SECTION Zone\nLOG hello");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Zone", result[1].SectionContext);
        }

        // ── 4. SECTION step itself gets its own label as SectionContext ───────────

        [Test]
        public void Parse_SectionStep_SectionContextIsOwnLabel()
        {
            var result = PlaytestParser.Parse("SECTION Zone phase");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Zone phase", result[0].SectionContext);
        }

        // ── 5. Steps before any SECTION have null SectionContext ──────────────────

        [Test]
        public void Parse_BeforeAnySection_SectionContextIsNull()
        {
            var result = PlaytestParser.Parse("LOG before\nSECTION Phase1\nLOG after");
            Assert.AreEqual(3, result.Count);
            Assert.IsNull(result[0].SectionContext);
            Assert.AreEqual("Phase1", result[2].SectionContext);
        }

        // ── 6. CALL macro → expanded steps have MacroStack ───────────────────────

        [Test]
        public void Parse_CallMacro_StepsHaveMacroStack()
        {
            var script = "MACRO clear_zone\n  LOG clear\nEND_MACRO\nCALL clear_zone";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0].MacroStack);
            Assert.AreEqual(1, result[0].MacroStack.Length);
            Assert.AreEqual("clear_zone", result[0].MacroStack[0]);
        }

        // ── 7. Direct steps (not from CALL) have null MacroStack ─────────────────

        [Test]
        public void Parse_DirectStep_HasNullMacroStack()
        {
            var result = PlaytestParser.Parse("LOG direct");
            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result[0].MacroStack);
        }

        // ── 8. Nested CALL → MacroStack preserves full call chain ────────────────

        [Test]
        public void Parse_NestedCallMacro_FullCallStack()
        {
            var script =
                "MACRO clear_zone\n" +
                "  LOG clearing\n" +
                "END_MACRO\n" +
                "MACRO clear_and_build\n" +
                "  CALL clear_zone\n" +
                "END_MACRO\n" +
                "CALL clear_and_build";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0].MacroStack);
            Assert.AreEqual(2, result[0].MacroStack.Length);
            Assert.AreEqual("clear_and_build", result[0].MacroStack[0]);
            Assert.AreEqual("clear_zone", result[0].MacroStack[1]);
        }

        // ── 9. ShallowClone copies provenance fields ──────────────────────────────

        [Test]
        public void ShallowClone_CopiesProvenanceFields()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Log,
                SourceFile = "test.playtest",
                SourceLine = 3,
                MacroStack = new[] { "outer", "inner" },
                SectionContext = "Phase1"
            };
            var clone = step.ShallowClone();
            Assert.AreEqual("test.playtest", clone.SourceFile);
            Assert.AreEqual(3, clone.SourceLine);
            Assert.AreEqual(new[] { "outer", "inner" }, clone.MacroStack);
            Assert.AreEqual("Phase1", clone.SectionContext);
        }

        // ── 10. FormatProvenance: null fields → empty string ─────────────────────

        [Test]
        public void FormatProvenance_NullFields_ReturnsEmpty()
        {
            var step = new PlaytestStep { Type = StepType.Log };
            Assert.AreEqual("", PlaytestRunner.FormatProvenance(step));
        }

        // ── 11. FormatProvenance: MacroStack only ─────────────────────────────────

        [Test]
        public void FormatProvenance_MacroStackOnly_ReturnsMacroLine()
        {
            var step = new PlaytestStep { MacroStack = new[] { "clear_zone" } };
            var result = PlaytestRunner.FormatProvenance(step);
            StringAssert.Contains("macro: clear_zone", result);
        }

        // ── 12. FormatProvenance: nested MacroStack uses arrow separator ──────────

        [Test]
        public void FormatProvenance_NestedMacroStack_UsesArrowSeparator()
        {
            var step = new PlaytestStep { MacroStack = new[] { "outer", "inner" } };
            var result = PlaytestRunner.FormatProvenance(step);
            StringAssert.Contains("macro: outer -> inner", result);
        }

        // ── 13. FormatProvenance: all fields → multiline block ────────────────────

        [Test]
        public void FormatProvenance_AllFields_ReturnsMultilineBlock()
        {
            var step = new PlaytestStep
            {
                SourceFile = "Playtests/game.playtest",
                SourceLine = 4,   // 0-based → displayed as line 5
                MacroStack = new[] { "clear_and_build", "clear_zone" },
                SectionContext = "Zone phase"
            };
            var result = PlaytestRunner.FormatProvenance(step);
            StringAssert.Contains("source: Playtests/game.playtest:5", result);
            StringAssert.Contains("macro: clear_and_build -> clear_zone", result);
            StringAssert.Contains("section: Zone phase", result);
        }

        // ── 14. ExecuteSyncStep Assert ERR includes provenance in result ──────────

        [Test]
        public void ExecuteSyncStep_AssertErr_IncludesProvenanceInResult()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/NonExistent__Provenance__|SomeComp|field",
                Op = "==",
                Value = "True",
                MacroStack = new[] { "clear_zone" },
                SectionContext = "Build phase",
                RawLine = "ASSERT /NonExistent__Provenance__|SomeComp|field == True"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
            Assert.AreEqual(1, results.Count);
            StringAssert.Contains("ERR", results[0]);
            StringAssert.Contains("macro: clear_zone", results[0]);
            StringAssert.Contains("section: Build phase", results[0]);
        }

        // ── 15. ExecuteSyncStep Assert PASS does NOT include provenance ───────────

        // Covered by: FormatProvenance test 10 (null fields → empty) +
        // Steps.cs only calls FormatProvenance when !ok (FAIL) or in catch (ERR).
        // A PASS never goes through those paths.
    }
}
