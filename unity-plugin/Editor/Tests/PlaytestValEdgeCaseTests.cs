// Error recovery and edge case tests for VAL/VAR/INCLUDE system.
// Pure parser tests — no Unity API.
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestValEdgeCaseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // C1 — Typo in $name: unknown sigil left as-is, no throw
        [Test]
        public void Parse_Val_TypoInSigil_UnknownLeftAsIs_NoThrow()
        {
            var script = "VAL $player /Player\nASSERT $playr|Health|hp > 0";
            ParseResult result = null;
            Assert.DoesNotThrow(() => result = PlaytestParser.Parse(script));
            Assert.AreEqual(1, result.Count);
            // $playr is unknown (typo) — left raw
            StringAssert.Contains("$playr", result[0].Query);
            // $player IS defined but NOT expanded here (different name)
            StringAssert.DoesNotContain("/Player", result[0].Query);
        }

        // C2 — VAL $name matching a DSL keyword: command parsing unaffected
        [Test]
        public void Parse_Val_NameMatchesKeyword_CommandParsingUnaffected()
        {
            var script = "VAL $ASSERT harmless_value\nASSERT /X|C|f == 1";
            ParseResult result = null;
            Assert.DoesNotThrow(() => result = PlaytestParser.Parse(script));
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Assert, result[0].Type);
            Assert.AreEqual("/X|C|f", result[0].Query);
        }

        // C3 — Comment lines: $name NOT expanded (line skipped entirely)
        [Test]
        public void Parse_Val_SigilInComment_NotExpanded_LineSkipped()
        {
            var script = "VAL $secret /PrivatePath\n# $secret should stay raw\nASSERT /X|C|f == 1";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Assert, result[0].Type);
        }

        // C4 — VAL value with spaces (captured via split-3)
        [Test]
        public void Parse_Val_ValueWithSpaces_CapturedAndExpanded()
        {
            var script = "VAL $msg Hello World\nLOG $msg";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(StepType.Log, result[0].Type);
            Assert.AreEqual("Hello World", result[0].Message);
        }

        // C5 — VAL with only 2 tokens (no value): ArgumentException with helpful message
        [Test]
        public void Parse_Val_MissingValue_ThrowsWithHelpfulMessage()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("VAL $name"));
            StringAssert.Contains("VAL", ex.Message);
            // Must NOT be a generic index-out-of-range
            Assert.AreNotEqual("IndexOutOfRangeException", ex.GetType().Name);
        }

        // C6 — VAL and VAR same $name: VAL expands parse-time value, VAR still collected
        [Test]
        public void Parse_Val_AndVar_SameName_VarDefsCollected_ValAlreadyExpanded()
        {
            var script = @"
VAL $hp 100
VAR $hp @/Player|Health|hp
ASSERT $hp == $hp";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            // At parse time: $hp → "100" (VAL expansion)
            Assert.AreEqual("100", result[0].Value);
            // VAR still collected for runtime
            Assert.IsTrue(result.VarDefs.ContainsKey("hp"));
        }

        // C7 — Indirect VAL cycle depth 3 (A→B, B→C, C→A)
        [Test]
        public void Parse_Val_IndirectCycle_Depth3_ThrowsCycleDetected()
        {
            var script = @"
VAL $a $b
VAL $b $c
VAL $c $a
LOG $a";
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse(script));
            StringAssert.Contains("cycle", ex.Message.ToLower());
        }

        // C8 — Diamond dependency (A→B, A→C, B→D, C→D): NOT a cycle, resolves correctly
        [Test]
        public void Parse_Val_DiamondDependency_NoCycleThrown_ResolvesCorrectly()
        {
            var script = @"
VAL $d   /Root
VAL $b   $d/BranchB
VAL $c   $d/BranchC
VAL $a   $b+$c
LOG $a";
            ParseResult result = null;
            Assert.DoesNotThrow(() => result = PlaytestParser.Parse(script));
            Assert.AreEqual(StepType.Log, result[0].Type);
            StringAssert.Contains("/Root/BranchB", result[0].Message);
            StringAssert.Contains("/Root/BranchC", result[0].Message);
        }

        // C9 — Unicode $name not matched by ASCII regex (left as-is)
        [Test]
        public void Parse_Val_UnicodeSigil_NotMatchedByRegex_LeftAsIs()
        {
            // Note: CollectVals WILL store key "игрок" but SigilRegex won't match $игрок
            var script = "VAL $игрок /Player\nLOG $игрок";
            ParseResult result = null;
            Assert.DoesNotThrow(() => result = PlaytestParser.Parse(script));
            // $игрок not expanded (regex is ASCII-only)
            StringAssert.Contains("$игрок", result[0].Message);
        }

        // C10 — VAR without @ prefix → ArgumentException mentioning @
        [Test]
        public void Parse_Var_MissingAtPrefix_ThrowsWithExpectedAtMessage()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("VAR $hp /Player|Health|hp"));
            StringAssert.Contains("@", ex.Message);
        }

        // C11 — INCLUDE file not found → ArgumentException with filename
        [Test]
        public void Parse_Include_FileNotFound_ThrowsArgumentExceptionWithFilename()
        {
            IncludeResolver notFound = filename => throw new System.IO.FileNotFoundException();
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("INCLUDE missing.defs\nLOG ok", notFound));
            StringAssert.Contains("missing.defs", ex.Message);
        }

        // C12 — INCLUDE empty file: no aliases injected, script parses fine
        [Test]
        public void Parse_Include_EmptyFile_NoAliasesInjected_ScriptParsesOk()
        {
            var resolver = AliasHelpers.FileMap(new Dictionary<string, string> {
                ["empty.defs"] = ""
            });
            var result = PlaytestParser.Parse("INCLUDE empty.defs\nLOG ok", resolver);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Log, result[0].Type);
        }

        // C13 — VarRegistry.ExpandVars: unregistered $name left as-is
        [Test]
        public void VarRegistry_ExpandVars_UnregisteredName_LeftAsIs()
        {
            var registry = new PlaytestVarRegistry((p, c, f) => "42");
            // $hp not registered
            var expanded = registry.ExpandVars("$hp > 0");
            Assert.AreEqual("$hp > 0", expanded);
        }

        // C14 — VarRegistry.ExpandVars: ReadValue throws → exception contains $name
        [Test]
        public void VarRegistry_Resolve_ReadValueThrows_ExceptionContainsVarName()
        {
            var registry = new PlaytestVarRegistry(
                (p, c, f) => throw new ArgumentException("Object not found"));
            registry.Register("hp", "@/DestroyedEnemy|Health|current");
            var ex = Assert.Throws<ArgumentException>(() => registry.ExpandVars("$hp"));
            StringAssert.Contains("hp", ex.Message);
        }

        // C15 — VAL value starts with DSL keyword → ArgumentException mentioning "DSL keyword"
        [Test]
        public void Parse_Val_ValueStartsWithKeyword_ThrowsArgumentException()
        {
            var script = "VAL $cmd INVOKE\nASSERT /X|C|f == 1";
            var ex = Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
            StringAssert.Contains("DSL keyword", ex.Message);
        }

        // C16 — Typo sigil with VAL defined → warning contains the typo name
        [Test]
        public void Parse_Val_TypoSigil_ProducesWarning()
        {
            var script = "VAL $player /Player\nASSERT $playr|H|hp > 0";
            var result = PlaytestParser.Parse(script);
            Assert.IsNotNull(result.Warnings);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("playr")));
        }

        // C17 — Correct sigil (no typo) → no warnings
        [Test]
        public void Parse_Val_NoTypo_NoWarnings()
        {
            var script = "VAL $player /Player\nASSERT $player|H|hp > 0";
            var result = PlaytestParser.Parse(script);
            Assert.IsTrue(result.Warnings == null || result.Warnings.Count == 0);
        }
    }
}
