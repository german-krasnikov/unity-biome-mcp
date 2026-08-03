// Real-world scenario tests combining VAL/VAR/INCLUDE in realistic scripts.
// All pure parser tests (no Unity API).
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasRealWorldTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // B1 — Full combat scenario: VAL + VAR + WAIT_UNTIL + INVOKE
        [Test]
        public void Parse_RealWorld_CombatScript_ProducesCorrectStepSequence()
        {
            var script = @"
VAL $player /Player/Character
VAL $enemy /Enemies/Boss
VAR $player_hp @$player|Health|current
VAR $enemy_hp @$enemy|Health|current
ASSERT $player_hp == 100
INVOKE $player Sword Attack
WAIT_UNTIL $enemy_hp < 80 TIMEOUT 5
ASSERT $enemy_hp >= 0";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(4, result.Count);

            // Step 0: ASSERT — $player_hp is a VAR, stored as $player_hp at parse time
            Assert.AreEqual(StepType.Assert, result[0].Type);
            Assert.AreEqual("$player_hp", result[0].Query);

            // Step 1: INVOKE — $player VAL-expanded to /Player/Character
            Assert.AreEqual(StepType.Invoke, result[1].Type);
            Assert.AreEqual("/Player/Character", result[1].Path);

            // Step 2: WAIT_UNTIL — $enemy_hp is a VAR, stored as token at parse time
            Assert.AreEqual(StepType.WaitUntil, result[2].Type);
            Assert.AreEqual("$enemy_hp", result[2].Query);

            // VAR defs collected with VAL expansion applied to @-query
            Assert.IsTrue(result.VarDefs.ContainsKey("player_hp"));
            Assert.IsTrue(result.VarDefs.ContainsKey("enemy_hp"));
            Assert.AreEqual("@/Player/Character|Health|current", result.VarDefs["player_hp"]);
        }

        // B2 — Multi-object: player + enemy + collectible (5 aliases)
        [Test]
        public void Parse_RealWorld_MultiObjectAliases_AllResolveIndependently()
        {
            var script = @"
VAL $player  /World/Player
VAL $enemy   /World/Enemy/Boss
VAL $item    /World/Items/GoldCoin
VAL $respawn 0,1,0
VAL $hp_max  100
ASSERT $player|Health|hp == $hp_max
ASSERT $enemy|Health|hp > 0
ASSERT $item|Collectible|collected == False
MOVE $player TO $respawn";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("/World/Player|Health|hp", result[0].Query);
            Assert.AreEqual("100", result[0].Value);
            Assert.AreEqual("/World/Enemy/Boss|Health|hp", result[1].Query);
            Assert.AreEqual("/World/Items/GoldCoin|Collectible|collected", result[2].Query);
            // MOVE: $player expanded in path, $respawn expanded in position
            Assert.AreEqual("/World/Player", result[3].Path);
            Assert.That(result[3].Position.y, Is.EqualTo(1f).Within(0.001f));
        }

        // B3 — Config-driven: expected values from VAL constants
        [Test]
        public void Parse_RealWorld_ConfigConstants_UsedInAssertValues()
        {
            var script = @"
VAL $boss          /Enemies/Dragon
VAL $expected_hp   500
VAL $expected_atk  75
VAR $actual_hp     @$boss|DragonHealth|current
VAR $actual_atk    @$boss|DragonCombat|attackPower
ASSERT $actual_hp == $expected_hp
ASSERT $actual_atk == $expected_atk";
            var result = PlaytestParser.Parse(script);
            // Parse-time: VAL expanded in Value field
            Assert.AreEqual("500", result[0].Value);
            Assert.AreEqual("75", result[1].Value);
            // Runtime: VAR still collected
            Assert.IsTrue(result.VarDefs.ContainsKey("actual_hp"));
            Assert.IsTrue(result.VarDefs.ContainsKey("actual_atk"));
        }

        // B4 — UI test: button → dialog → text verification
        [Test]
        public void Parse_RealWorld_UIFlow_ClickWaitAssert()
        {
            var script = @"
VAL $menu_button /UI/MainMenu/StartButton
VAL $dialog      /UI/Dialogs/ConfirmDialog
VAL $dialog_text /UI/Dialogs/ConfirmDialog/TextLabel
CLICK $menu_button
WAIT_UNTIL $dialog|CanvasGroup|alpha == 1 TIMEOUT 3
ASSERT $dialog_text|TMP_Text|text contains Ready";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(StepType.Click,     result[0].Type);
            Assert.AreEqual(StepType.WaitUntil, result[1].Type);
            Assert.AreEqual(StepType.Assert,    result[2].Type);
            Assert.AreEqual("/UI/MainMenu/StartButton", result[0].Path);
            Assert.AreEqual("/UI/Dialogs/ConfirmDialog|CanvasGroup|alpha", result[1].Query);
            StringAssert.Contains("/UI/Dialogs/ConfirmDialog/TextLabel", result[2].Query);
            Assert.AreEqual("contains", result[2].Op);
        }
    }
}
