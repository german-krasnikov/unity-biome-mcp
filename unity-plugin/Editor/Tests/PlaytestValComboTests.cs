// Combinatorial tests: cross-feature interactions between VAL/VAR/INCLUDE/MACRO/ALIAS.
// All pure parser — no Unity API.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestValComboTests
    {
        // A1 — VAL used inside MACRO body: expands after CALL phase
        [Test]
        public void Parse_Val_UsedInsideMacroBody_ExpandsAfterCallPhase()
        {
            var script = @"
VAL $base /Player/Character
MACRO check $1
    ASSERT $base|Health|$1 > 0
END_MACRO
CALL check hp";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Player/Character|Health|hp", result[0].Query);
        }

        // A2 — MACRO param shadows VAL with same name: CALL arg wins
        [Test]
        public void Parse_Val_MacroParamSameNameAsVal_CallArgWins()
        {
            // CALL substitutes $player → /Enemy first (phase 0.5).
            // VAL expansion (phase 0.7) finds $player already replaced → no effect.
            var script = @"
VAL $player /DefaultPlayer
MACRO attack $player
    ASSERT $player|Health|hp < 100
END_MACRO
CALL attack /Enemy";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual("/Enemy|Health|hp", result[0].Query);
        }

        // A3 — VAL expanded inside ASSERT_BATCH block body
        [Test]
        public void Parse_Val_ExpandedInsideAssertBatchBlock()
        {
            var script = @"
VAL $enemy /Enemies/Boss
ASSERT_BATCH
    ASSERT $enemy|Health|hp > 0
    ASSERT $enemy|Rigidbody|velocity == 0
END";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.AssertBatch, result[0].Type);
            Assert.AreEqual("/Enemies/Boss|Health|hp", result[0].Queries[0]);
            Assert.AreEqual("/Enemies/Boss|Rigidbody|velocity", result[0].Queries[1]);
        }

        // A4 — VAL expanded in compound WAIT_UNTIL AND condition
        [Test]
        public void Parse_Val_ExpandedInWaitUntilAndCondition()
        {
            var script = @"
VAL $hp /Player|Health|hp
VAL $mp /Player|Mana|mp
WAIT_UNTIL $hp > 50 AND $mp > 10 TIMEOUT 5";
            var result = PlaytestParser.Parse(script);
            var step = result[0];
            Assert.AreEqual(StepType.WaitUntil, step.Type);
            Assert.AreEqual("/Player|Health|hp", step.Query);
            Assert.IsNotNull(step.Queries);
            Assert.AreEqual("/Player|Mana|mp", step.Queries[0]);
        }

        // A5 — VAL chain (depth 3) used in MOVE position
        [Test]
        public void Parse_Val_PositionChain_MoveParsesVector3()
        {
            var script = @"
VAL $x 10
VAL $yz 0,5
VAL $pos $x,$yz
MOVE TO $pos";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Move, result[0].Type);
            Assert.That(result[0].Position.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result[0].Position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result[0].Position.z, Is.EqualTo(5f).Within(0.001f));
        }

        // A6 — VAR @query with $sigil: VAL expanded BEFORE VAR collection (phase ordering)
        [Test]
        public void Parse_Var_AtQueryContainsVal_ValExpandedBeforeVarCollect()
        {
            var script = @"
VAL $base /Player/Char
VAR $hp @$base|Health|current";
            var result = PlaytestParser.Parse(script);
            Assert.IsTrue(result.VarDefs.ContainsKey("hp"));
            Assert.AreEqual("@/Player/Char|Health|current", result.VarDefs["hp"]);
        }

        // A7 — INCLUDE imports VAL defs; all expand in main script
        [Test]
        public void Parse_Include_ValDefsImported_ExpandedInMainScript()
        {
            var resolver = AliasHelpers.FileMap(new Dictionary<string, string> {
                ["combat.defs"] = "VAL $player /Player/Hero\nVAL $max_hp 100"
            });
            var script = "INCLUDE combat.defs\nASSERT $player|Health|hp == $max_hp";
            var result = PlaytestParser.Parse(script, resolver);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Player/Hero|Health|hp", result[0].Query);
            Assert.AreEqual("100", result[0].Value);
        }

        // A8 — Inline VAL overrides same-name imported VAL (last definition wins)
        [Test]
        public void Parse_Include_InlineValOverridesImported()
        {
            var resolver = AliasHelpers.FileMap(new Dictionary<string, string> {
                ["base.defs"] = "VAL $target /Enemy"
            });
            var script = "INCLUDE base.defs\nVAL $target /FriendlyHero\nASSERT $target|H|hp > 0";
            var result = PlaytestParser.Parse(script, resolver);
            Assert.AreEqual("/FriendlyHero|H|hp", result[0].Query);
        }

        // A9 — $name expanded in LOG message (entire raw line expansion)
        [Test]
        public void Parse_Val_ExpandedInLogMessage()
        {
            var script = "VAL $player /Player\nLOG Testing path $player done";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(StepType.Log, result[0].Type);
            StringAssert.Contains("/Player", result[0].Message);
            StringAssert.DoesNotContain("$player", result[0].Message);
        }

        // A10 — VAL and ALIAS coexist; no double-expansion or collision
        [Test]
        public void Parse_Val_AndAlias_BothApply_NoDoubleExpansion()
        {
            var script = @"
VAL $vpath /ValPath
ALIAS apath /AliasPath
ASSERT $vpath|C|f == 1
ASSERT apath|C|f == 1";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("/ValPath|C|f", result[0].Query);
            Assert.AreEqual("/AliasPath|C|f", result[1].Query);
        }
    }
}
