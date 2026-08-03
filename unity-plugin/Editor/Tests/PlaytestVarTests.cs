// Baseline TDD tests for VAL/VAR/INCLUDE/VarRegistry (from ARCH1 plan).
// All tests are pure-parser (no Unity API) or VarRegistry-unit tests.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestVarTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── VAL: static parse-time sigil expansion ──────────────────────────────

        [Test]
        public void Parse_Val_ExpandsSigilInAssert()
        {
            var script = "VAL $hp /Player|Health|health\nASSERT $hp == 100";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Player|Health|health", result[0].Query);
        }

        [Test]
        public void Parse_Val_ExpandsInWaitUntil()
        {
            var script = "VAL $p /P|H|h\nWAIT_UNTIL $p == 5 TIMEOUT 10";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual("/P|H|h", result[0].Query);
        }

        [Test]
        public void Parse_Val_ExpandsNestedVals()
        {
            var script = "VAL $base /Player\nVAL $hp $base|Health|health\nASSERT $hp == 0";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual("/Player|Health|health", result[0].Query);
        }

        [Test]
        public void Parse_Val_CycleAB_ThrowsCycleDetected()
        {
            var script = "VAL $a $b\nVAL $b $a\nASSERT $a == 0";
            var ex = Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
            StringAssert.Contains("cycle", ex.Message.ToLower());
        }

        [Test]
        public void Parse_Val_SelfCycle_ThrowsCycleDetected()
        {
            var script = "VAL $x $x\nASSERT $x == 0";
            var ex = Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
            StringAssert.Contains("cycle", ex.Message.ToLower());
        }

        [Test]
        public void Parse_Val_UnknownSigil_LeftIntact()
        {
            var script = "ASSERT $unknown == 0";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual("$unknown", result[0].Query);
        }

        [Test]
        public void Parse_Val_DoesNotExpandInsideComment()
        {
            var script = "VAL $hp 100\n# $hp comment\nASSERT real == 0";
            var result = PlaytestParser.Parse(script);
            // Comment line is skipped; only 1 step (ASSERT)
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("real", result[0].Query);
        }

        [Test]
        public void Parse_Val_ExpandsInMacroBody()
        {
            var script = "VAL $p /P\nMACRO m\n  ASSERT $p|H|h == 0\nEND_MACRO\nCALL m";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/P|H|h", result[0].Query);
        }

        // ── INCLUDE: file injection ─────────────────────────────────────────────

        [Test]
        public void Parse_Include_InjectsFileContent()
        {
            IncludeResolver resolver = filename => {
                Assert.AreEqual("shared.defs", filename);
                return "VAL $hp /P|H|h";
            };
            var script = "INCLUDE shared.defs\nASSERT $hp == 0";
            var result = PlaytestParser.Parse(script, resolver);
            Assert.AreEqual("/P|H|h", result[0].Query);
        }

        [Test]
        public void Parse_Include_MaxDepth_ThrowsDepthExceeded()
        {
            // Circular: every file includes itself
            IncludeResolver resolver = filename => "INCLUDE a.defs";
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("INCLUDE a.defs\nLOG ok", resolver));
            StringAssert.Contains("depth", ex.Message.ToLower());
        }

        [Test]
        public void Parse_Include_FileNotFound_ThrowsWithFilename()
        {
            IncludeResolver resolver = filename => throw new System.IO.FileNotFoundException("not found");
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("INCLUDE missing.defs\nLOG ok", resolver));
            StringAssert.Contains("missing.defs", ex.Message);
        }

        // ── VAR: binding collection (parse-time, no Unity API) ─────────────────

        [Test]
        public void Parse_Var_CollectedInVarDefs()
        {
            var script = "VAR $hp @/P|Health|health\nASSERT /P|Health|health == 0";
            var result = PlaytestParser.Parse(script);
            Assert.IsNotNull(result.VarDefs);
            Assert.IsTrue(result.VarDefs.ContainsKey("hp"));
            Assert.AreEqual("@/P|Health|health", result.VarDefs["hp"]);
        }

        [Test]
        public void Parse_Var_SigilStrippedFromKey()
        {
            var script = "VAR $hero @/Player|Transform|position\nLOG ok";
            var result = PlaytestParser.Parse(script);
            Assert.IsTrue(result.VarDefs.ContainsKey("hero"));
        }

        [Test]
        public void Parse_Var_UnknownSigilLeftIntactInStep()
        {
            // $hero in ASSERT query is a VAR reference — left intact at parse time
            var script = "VAR $hero @/Player|Health|hp\nASSERT $hero == 0";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual("$hero", result[0].Query);
        }

        // ── PlaytestVarRegistry: unit tests with mocked ReadValueFn ────────────

        [Test]
        public void VarRegistry_ExpandVars_ReplacesKnownSigil()
        {
            var registry = new PlaytestVarRegistry((path, comp, field) => "42");
            registry.Register("hp", "@/P|Health|hp");
            Assert.AreEqual("42", registry.ExpandVars("$hp"));
        }

        [Test]
        public void VarRegistry_ExpandVars_LeavesUnknownSigil()
        {
            var registry = new PlaytestVarRegistry((p, c, f) => "x");
            // $unknown not registered
            Assert.AreEqual("$unknown", registry.ExpandVars("$unknown"));
        }

        [Test]
        public void VarRegistry_ExpandStep_ClonesAndExpandsAllFields()
        {
            var registry = new PlaytestVarRegistry((p, c, f) => "MyPath");
            registry.Register("obj", "@/MyPath|Transform|x");
            var step = new PlaytestStep { Path = "$obj", Query = "$obj|Comp|f" };
            var expanded = registry.ExpandStep(step);
            Assert.AreEqual("MyPath", expanded.Path);
            Assert.AreEqual("MyPath|Comp|f", expanded.Query);
            // Original not mutated
            Assert.AreEqual("$obj", step.Path);
        }

        [Test]
        public void VarRegistry_HasAny_FalseWhenEmpty()
        {
            var registry = new PlaytestVarRegistry();
            Assert.IsFalse(registry.HasAny);
        }

        [Test]
        public void VarRegistry_HasAny_TrueAfterRegister()
        {
            var registry = new PlaytestVarRegistry();
            registry.Register("hp", "@/P|H|hp");
            Assert.IsTrue(registry.HasAny);
        }

        // ── PlaytestStep.ShallowClone ──────────────────────────────────────────

        [Test]
        public void ShallowClone_DoesNotMutateOriginal()
        {
            var step = new PlaytestStep { Query = "original", Value = "1" };
            var clone = step.ShallowClone();
            clone.Query = "changed";
            Assert.AreEqual("original", step.Query);
        }

        [Test]
        public void ShallowClone_ArraysAreSharedReferences()
        {
            // ShallowClone: array references shared (not deep-copied by design)
            var step = new PlaytestStep { Queries = new[] { "a", "b" } };
            var clone = step.ShallowClone();
            Assert.AreSame(step.Queries, clone.Queries);
        }

        // ── ExpandStep: arrays ────────────────────────────────────────────────────

        [Test]
        public void VarRegistry_ExpandStep_ExpandsQueriesArray_WithVarSigil()
        {
            var registry = new PlaytestVarRegistry((p, c, f) => "resolvedQ");
            registry.Register("q", "@/Obj|Comp|field");
            var step = new PlaytestStep { Queries = new[] { "$q", "plain" } };
            var expanded = registry.ExpandStep(step);
            Assert.AreEqual("resolvedQ", expanded.Queries[0]);
            Assert.AreEqual("plain", expanded.Queries[1]);
            // original untouched
            Assert.AreEqual("$q", step.Queries[0]);
        }

        [Test]
        public void VarRegistry_ExpandStep_ExpandsBatchValuesArray_WithVarSigil()
        {
            var registry = new PlaytestVarRegistry((p, c, f) => "42");
            registry.Register("v", "@/Obj|Comp|val");
            var step = new PlaytestStep { BatchValues = new[] { "$v", "static" } };
            var expanded = registry.ExpandStep(step);
            Assert.AreEqual("42", expanded.BatchValues[0]);
            Assert.AreEqual("static", expanded.BatchValues[1]);
            // original untouched
            Assert.AreEqual("$v", step.BatchValues[0]);
        }

        // ── INCLUDE path traversal ────────────────────────────────────────────────

        [Test]
        public void Parse_Include_PathTraversal_Throws()
        {
            IncludeResolver resolver = _ => "content";
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("INCLUDE ../../etc/passwd\nLOG ok", resolver));
            StringAssert.Contains("traversal", ex.Message.ToLower());
        }

        [Test]
        public void Parse_Include_RootedPath_Throws()
        {
            IncludeResolver resolver = _ => "content";
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("INCLUDE /etc/passwd\nLOG ok", resolver));
            StringAssert.Contains("traversal", ex.Message.ToLower());
        }

        // ── ParseResult: implicit cast + backward compat ───────────────────────

        [Test]
        public void ParseResult_ImplicitCast_ToList()
        {
            var parseResult = PlaytestParser.Parse("LOG ok");
            List<PlaytestStep> steps = parseResult; // implicit cast
            Assert.IsNotNull(steps);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].Type);
        }

        [Test]
        public void ParseResult_VarDefs_NullWhenNoVarsDeclared()
        {
            var result = PlaytestParser.Parse("ASSERT /P|H|h == 0");
            Assert.IsNull(result.VarDefs);
        }

        // ── VAR improved error messages (4.5 + 4.6) ──────────────────────────────

        [Test]
        public void Parse_Var_MissingAt_ErrorMentionsPipeFormat()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("VAR $hp /Player|Health|hp"));
            StringAssert.Contains("@", ex.Message);
        }

        [Test]
        public void Parse_Var_TooFewTokens_ErrorContainsExample()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("VAR $hp"));
            StringAssert.Contains("Example", ex.Message);
        }

        // ── Wave 6.1: ExpandStep field coverage ──────────────────────────────────

        PlaytestVarRegistry MakeReg(string name = "hp", string value = "100")
        {
            var reg = new PlaytestVarRegistry((_p, _c, _f) => value);
            reg.Register(name, "@/P|C|f");
            return reg;
        }

        [Test]
        public void ExpandStep_Component_Expanded()
        {
            var reg = MakeReg();
            var step = new PlaytestStep { Component = "$hp" };
            Assert.AreEqual("100", reg.ExpandStep(step).Component);
        }

        [Test]
        public void ExpandStep_Method_Expanded()
        {
            var reg = MakeReg();
            var step = new PlaytestStep { Method = "$hp" };
            Assert.AreEqual("100", reg.ExpandStep(step).Method);
        }

        [Test]
        public void ExpandStep_Args_Expanded()
        {
            var reg = MakeReg();
            var step = new PlaytestStep { Args = "$hp" };
            Assert.AreEqual("100", reg.ExpandStep(step).Args);
        }

        [Test]
        public void ExpandStep_Message_Expanded()
        {
            var reg = MakeReg();
            var step = new PlaytestStep { Message = "HP is $hp" };
            Assert.AreEqual("HP is 100", reg.ExpandStep(step).Message);
        }

        [Test]
        public void ExpandStep_RawPosition_Expanded()
        {
            var reg = MakeReg("pos", "1,2,3");
            var step = new PlaytestStep { RawPosition = "$pos" };
            Assert.AreEqual("1,2,3", reg.ExpandStep(step).RawPosition);
        }

        [Test]
        public void ExpandStep_NoVars_ReturnsSameInstance()
        {
            // HasAny=false → ExpandStep returns the SAME object (no clone)
            var reg = new PlaytestVarRegistry((_p, _c, _f) => "x");
            var step = new PlaytestStep { Component = "$hp" };
            Assert.AreSame(step, reg.ExpandStep(step));
        }

        // ── Wave 6.3: VAL in TELEPORT / SET / INVOKE ─────────────────────────────

        [Test]
        public void Parse_Val_ExpandsInTeleport()
        {
            var script = "VAL $dest 5,0,-3\nTELEPORT /Player $dest";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Teleport, result[0].Type);
            Assert.AreEqual(5f,  result[0].Position.x, 0.001f);
            Assert.AreEqual(0f,  result[0].Position.y, 0.001f);
            Assert.AreEqual(-3f, result[0].Position.z, 0.001f);
        }

        [Test]
        public void Parse_Val_ExpandsInSet()
        {
            var script = "VAL $field currentHp\nSET /Player Health $field 100";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Set, result[0].Type);
            Assert.AreEqual("currentHp", result[0].Method);
        }

        [Test]
        public void Parse_Val_ExpandsInInvoke()
        {
            var script = "VAL $comp PlayerController\nINVOKE /Player $comp Fire";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(StepType.Invoke, result[0].Type);
            Assert.AreEqual("PlayerController", result[0].Component);
        }

    }
}
