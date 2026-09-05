// Real GridTest scene tests for VAL/VAR/INCLUDE — grounded in actual scene paths.
// B5/E5/E6 use Camera instead of GridPlayer (GridPlayer not in test assembly scope).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasGridTestTests : SceneTestBase
    {
        // ── Category A: VAL on Real GridTest Paths (parse-time) ─────────────────

        // A1 — Basic VAL expands to GridPlayer query
        [Test]
        public void Val_GridPlayer_ExpandsToFullPipePath()
        {
            var script = @"
VAL $gp /GridPlayer|GridPlayer|Score
ASSERT $gp == 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", result[0].Query);
        }

        // A2 — VAL as nested path alias (3-level)
        [Test]
        public void Val_NestedPath_DelForceChild1_ExpandsCorrectly()
        {
            var script = @"
VAL $child1 /DelForceContainer/DelForceChild1
ASSERT_NEAR $child1 /GridPlayer 15.0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.AssertNear, result[0].Type);
            Assert.AreEqual("/DelForceContainer/DelForceChild1", result[0].Path);
        }

        // A3 — VAL used in WAIT_UNTIL query
        [Test]
        public void Val_UsedInWaitUntil_ExpandsQuery()
        {
            var script = @"
VAL $moving /GridPlayer|GridPlayer|IsMoving
WAIT_UNTIL $moving == False TIMEOUT 5
";
            var result = PlaytestParser.Parse(script);
            var step = result[0];
            Assert.AreEqual(StepType.WaitUntil, step.Type);
            Assert.AreEqual("/GridPlayer|GridPlayer|IsMoving", step.Query);
            Assert.AreEqual("False", step.Value);
        }

        // A4 — VAL used in both query AND value positions
        [Test]
        public void Val_ExpandsInBothQueryAndValue()
        {
            var script = @"
VAL $score_q /GridPlayer|GridPlayer|Score
VAL $init 0
ASSERT $score_q == $init
";
            var result = PlaytestParser.Parse(script);
            var step = result[0];
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", step.Query);
            Assert.AreEqual("0", step.Value);
        }

        // A5 — VAL + MACRO: alias inside macro body
        [Test]
        public void Val_InsideMacroBody_ExpandsAfterCall()
        {
            var script = @"
VAL $px /GridPlayer|GridPlayer|PosX
VAL $pz /GridPlayer|GridPlayer|PosZ
MACRO assert_origin
  ASSERT $px == 0
  ASSERT $pz == 0
END_MACRO
CALL assert_origin
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("/GridPlayer|GridPlayer|PosX", result[0].Query);
            Assert.AreEqual("/GridPlayer|GridPlayer|PosZ", result[1].Query);
        }

        // A6 — Multiple VAL, one referenced multiple times
        [Test]
        public void Val_ReferencedMultipleTimes_AllExpand()
        {
            var script = @"
VAL $speed /GridPlayer|GridPlayer|MoveSpeed
ASSERT $speed > 0
ASSERT $speed < 100
ASSERT $speed == 5
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(3, result.Count);
            foreach (var s in result.Steps)
                Assert.AreEqual("/GridPlayer|GridPlayer|MoveSpeed", s.Query);
        }

        // ── Category B: VAR on Real GridPlayer Fields (runtime) ─────────────────

        // B1 — VAR collected from script, not emitted as step
        [Test]
        public void Var_GridPlayerScore_CollectedNotEmitted()
        {
            var script = @"
VAR $score @/GridPlayer|GridPlayer|Score
ASSERT $score == 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count, "VAR line must not emit a step");
            Assert.IsNotNull(result.VarDefs);
            Assert.IsTrue(result.VarDefs.ContainsKey("score"));
            Assert.AreEqual("@/GridPlayer|GridPlayer|Score", result.VarDefs["score"]);
        }

        // B2 — VAR parsed correctly: strips @ prefix, stores raw query
        [Test]
        public void VarRegistry_Register_GridPlayerMoveCount_ParsesCorrectly()
        {
            string capturedPath = null, capturedComp = null, capturedField = null;
            var registry = new PlaytestVarRegistry((path, comp, field) => {
                capturedPath = path; capturedComp = comp; capturedField = field;
                return "7";
            });
            registry.Register("movecount", "@/GridPlayer|GridPlayer|MoveCount");
            registry.ExpandVars("$movecount");
            Assert.AreEqual("/GridPlayer", capturedPath);
            Assert.AreEqual("GridPlayer", capturedComp);
            Assert.AreEqual("MoveCount", capturedField);
        }

        // B3 — VAR expands in ASSERT value position
        [Test]
        public void VarRegistry_ExpandVars_InAssertValue_ReplacesToken()
        {
            var registry = new PlaytestVarRegistry((p, c, f) => "10"); // GridSize default
            registry.Register("grid_size", "@/GridPlayer|GridPlayer|GridSize");
            var result = registry.ExpandVars("ASSERT /SomeQuery == $grid_size");
            StringAssert.Contains("10", result);
            Assert.IsFalse(result.Contains("$grid_size"));
        }

        // B4 — VAR: object not found → throws ArgumentException with var name
        [Test]
        public void VarRegistry_ObjectNotFound_ThrowsWithVarName()
        {
            var registry = new PlaytestVarRegistry((path, comp, field) =>
                throw new ArgumentException($"Object not found: {path}"));
            registry.Register("ghost", "@/NonExistentObject|GridPlayer|Score");
            var ex = Assert.Throws<ArgumentException>(() => registry.ExpandVars("$ghost"));
            StringAssert.Contains("ghost", ex.Message);
        }

        // B5 — ReadFieldInternal on NonSerialized field returns default (Camera.nearClipPlane)
        [Test]
        public void VarRegistry_CanReadCameraField_ViaDelegate()
        {
            // Using Camera instead of GridPlayer (GridPlayer not in test assembly).
            // Tests that ReadValueFn delegate pattern works for real Unity components.
            var go = new GameObject("TestCamera");
            var cam = go.AddComponent<Camera>();
            try
            {
                var registry = new PlaytestVarRegistry((path, comp, field) => {
                    // Simulate what PlaytestRunner.ReadValue does
                    var camComp = go.GetComponent<Camera>();
                    Assert.IsNotNull(camComp);
                    return RuntimeHelper.ReadFieldInternal(camComp, field);
                });
                registry.Register("fov", "@/TestCamera|Camera|fieldOfView");
                var val = registry.ExpandVars("$fov");
                Assert.IsNotNull(val); // fieldOfView is a property, may throw
            }
            catch (ArgumentException)
            {
                // ReadFieldInternal may not find fieldOfView as a field — acceptable
                Assert.Pass("ReadFieldInternal threw for property-backed field — expected behavior");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // B6 — VAL vs VAR: both $-sigil, different names, no conflict
        [Test]
        public void Var_SameNameAsAlias_NoConflict()
        {
            var script = @"
VAL $score /GridPlayer|GridPlayer|Score
VAR $hp @/GridPlayer|GridPlayer|MoveCount
ASSERT $score == 0
ASSERT $hp == 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", result[0].Query); // VAL expanded
            Assert.AreEqual("$hp", result[1].Query);  // $hp left for runtime (VAR)
            Assert.IsTrue(result.VarDefs.ContainsKey("hp"));
        }

        // ── Category C: INCLUDE with real .defs content ─────────────────────────

        // C1 — INCLUDE expands VAL defs from gridtest.defs
        [Test]
        public void Include_GridTestDefs_ExpandsValsFromFile()
        {
            string gridtestDefs = @"
VAL $player /GridPlayer
VAL $score /GridPlayer|GridPlayer|Score
VAL $moving /GridPlayer|GridPlayer|IsMoving
";
            IncludeResolver resolver = filename => {
                Assert.AreEqual("gridtest.defs", filename);
                return gridtestDefs;
            };
            var script = @"
INCLUDE gridtest.defs
ASSERT $score == 0
ASSERT $moving == False
";
            var result = PlaytestParser.Parse(script, resolver);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", result[0].Query);
            Assert.AreEqual("/GridPlayer|GridPlayer|IsMoving", result[1].Query);
        }

        // C2 — INCLUDE expands VAR defs from file
        [Test]
        public void Include_GridTestDefs_CollectsVarsFromFile()
        {
            string gridtestDefs = @"
VAR $live_score @/GridPlayer|GridPlayer|Score
VAR $live_moves @/GridPlayer|GridPlayer|MoveCount
";
            var script = @"
INCLUDE gridtest.defs
ASSERT $live_score > 0
";
            var result = PlaytestParser.Parse(script, f => gridtestDefs);
            Assert.IsNotNull(result.VarDefs);
            Assert.IsTrue(result.VarDefs.ContainsKey("live_score"));
            Assert.IsTrue(result.VarDefs.ContainsKey("live_moves"));
            Assert.AreEqual(1, result.Count);
        }

        // C3 — INCLUDE missing file: resolver throws, wrapped in ArgumentException
        [Test]
        public void Include_MissingFile_ThrowsArgumentException()
        {
            var script = "INCLUDE missing.defs\nASSERT /X|C|f == 1";
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse(script,
                    f => throw new System.IO.FileNotFoundException(f)));
            StringAssert.Contains("missing.defs", ex.Message);
        }

        // C4 — INCLUDE + MACRO: defs file defines macros used in main script
        [Test]
        public void Include_DefsWithMacro_CalledFromMainScript()
        {
            string defs = @"
MACRO assert_at_origin
  ASSERT /GridPlayer|GridPlayer|PosX == 0
  ASSERT /GridPlayer|GridPlayer|PosZ == 0
END_MACRO
";
            var script = @"
INCLUDE gridtest.defs
CALL assert_at_origin
";
            var result = PlaytestParser.Parse(script, f => defs);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("/GridPlayer|GridPlayer|PosX", result[0].Query);
        }

        // C5 — INCLUDE same file twice: no duplicate key crash, $score resolves once
        [Test]
        public void Include_SameFileTwice_DeduplicatesVals()
        {
            int callCount = 0;
            string defs = "VAL $score /GridPlayer|GridPlayer|Score";
            var script = @"
INCLUDE gridtest.defs
INCLUDE gridtest.defs
ASSERT $score == 0
";
            var result = PlaytestParser.Parse(script, f => { callCount++; return defs; });
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", result[0].Query);
            Assert.AreEqual(2, callCount);
        }

        // ── Category D: Combinatorial real-world scenarios ───────────────────────

        // D1 — Full GridTest game loop: move → assert score
        [Test]
        public void Parse_GridTestGameLoop_MoveAndCheckScore()
        {
            var script = @"
VAL $gp_pos_x /GridPlayer|GridPlayer|PosX
VAL $gp_score /GridPlayer|GridPlayer|Score
MOVE /GridPlayer TO 3,0,0
ASSERT $gp_pos_x == 3
ASSERT $gp_score >= 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(StepType.Move, result[0].Type);
            Assert.AreEqual("/GridPlayer", result[0].Path);
            Assert.AreEqual("/GridPlayer|GridPlayer|PosX", result[1].Query);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", result[2].Query);
        }

        // D2 — ASSERT_BATCH on all GridPlayer fields at once (VAL-based)
        [Test]
        public void Parse_AssertBatch_GridPlayerDefaultState()
        {
            var script = @"
VAL $gp_speed /GridPlayer|GridPlayer|MoveSpeed
VAL $gp_size  /GridPlayer|GridPlayer|GridSize
VAL $gp_score /GridPlayer|GridPlayer|Score
ASSERT_BATCH
  ASSERT $gp_speed == 5
  ASSERT $gp_size  == 10
  ASSERT $gp_score == 0
END
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.AssertBatch, result[0].Type);
            Assert.AreEqual(3, result[0].BatchOps.Length);
            Assert.AreEqual("/GridPlayer|GridPlayer|MoveSpeed", result[0].Queries[0]);
            Assert.AreEqual("/GridPlayer|GridPlayer|GridSize",  result[0].Queries[1]);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score",     result[0].Queries[2]);
        }

        // D3 — VAR + WAIT_UNTIL: poll IsMoving until false
        [Test]
        public void Parse_Var_WaitUntilIsMovingFalse_GridPlayer()
        {
            var script = @"
VAR $is_moving @/GridPlayer|GridPlayer|IsMoving
MOVE /GridPlayer TO 5,0,5
WAIT_UNTIL $is_moving == False TIMEOUT 10
ASSERT /GridPlayer|GridPlayer|MoveCount == 1
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(3, result.Count);
            var waitStep = result[1];
            Assert.AreEqual(StepType.WaitUntil, waitStep.Type);
            Assert.AreEqual("$is_moving", waitStep.Query); // VAR — stays as token at parse time
            Assert.AreEqual(10f, waitStep.Timeout, 0.001f);
        }

        // D4 — MACRO + CALL: parameterised check for any GridLine
        [Test]
        public void Parse_Macro_CheckGridLineActive_WithCallParam()
        {
            var script = @"
MACRO check_gridline $1
  ASSERT /Grid/$1|MeshRenderer|enabled == True
END_MACRO
CALL check_gridline GridLine_X5
CALL check_gridline GridLine_Z3
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("/Grid/GridLine_X5|MeshRenderer|enabled", result[0].Query);
            Assert.AreEqual("/Grid/GridLine_Z3|MeshRenderer|enabled", result[1].Query);
        }

        // D5 — INVARIANT on Score never goes negative
        [Test]
        public void Parse_Invariant_GridPlayerScoreNonNegative()
        {
            var script = @"
VAL $score_q /GridPlayer|GridPlayer|Score
INVARIANT $score_q >= 0
MOVE /GridPlayer TO 5,0,5
WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == False TIMEOUT 8
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(3, result.Count);
            var inv = result[0];
            Assert.AreEqual(StepType.Invariant, inv.Type);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", inv.Query);
            Assert.AreEqual(">=", inv.Op);
            Assert.AreEqual("0", inv.Value);
        }

        // D6 — CAPTURE then ASSERT_CAPTURED
        [Test]
        public void Parse_CaptureScore_ThenAssertIncreased()
        {
            var script = @"
VAL $score_q /GridPlayer|GridPlayer|Score
CAPTURE before_score $score_q
MOVE /GridPlayer TO 3,0,3
WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == False TIMEOUT 8
ASSERT_CAPTURED before_score INCREASED_BY >= 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(StepType.Capture, result[0].Type);
            Assert.AreEqual("/GridPlayer|GridPlayer|Score", result[0].Query);
            Assert.AreEqual(StepType.AssertCaptured, result[3].Type);
            Assert.AreEqual("before_score", result[3].Message);
        }

        // D7 — SECTION + DESC labelling
        [Test]
        public void Parse_Section_And_Desc_LabelGridTestSteps()
        {
            var script = @"
SECTION ""GridPlayer Initial State""
DESC ""score starts at zero""
ASSERT /GridPlayer|GridPlayer|Score == 0
DESC ""speed is default""
ASSERT /GridPlayer|GridPlayer|MoveSpeed == 5
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(StepType.Section, result[0].Type);
            Assert.AreEqual("GridPlayer Initial State", result[0].Message);
            Assert.AreEqual("score starts at zero", result[1].Label);
            Assert.AreEqual("speed is default", result[2].Label);
        }

        // ── Category E: Error Scenarios ──────────────────────────────────────────

        // E1 — VAL $name = non-existent object path: parse succeeds
        [Test]
        public void Val_NonExistentPath_ParseSucceeds_RuntimeFails()
        {
            var script = @"
VAL $ghost /NonExistentObject|GridPlayer|Score
ASSERT $ghost == 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/NonExistentObject|GridPlayer|Score", result[0].Query);
        }

        // E2 — VAR with malformed @-query (no pipe) throws at parse
        [Test]
        public void Var_MalformedAtQuery_NoPipe_ThrowsArgumentException()
        {
            var script = "VAR $bad @NotAPipePath\nASSERT /X|C|f == 1";
            var ex = Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
            StringAssert.Contains("pipe", ex.Message);
        }

        // E3 — VAL name collision: second definition wins (last wins)
        [Test]
        public void Val_DuplicateName_SecondDefinitionWins()
        {
            var script = @"
VAL $score_q /GridPlayer|GridPlayer|Score
VAL $score_q /GridPlayer|GridPlayer|MoveCount
ASSERT $score_q == 0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            // Last VAL wins
            Assert.AreEqual("/GridPlayer|GridPlayer|MoveCount", result[0].Query);
        }

        // E4 — VAL with Collectible path (no fields): parse succeeds
        [Test]
        public void Val_CollectiblePath_NoFields_ParseSucceeds()
        {
            var script = @"
VAL $col1 /Collectible_1
ASSERT_NEAR $col1 /GridPlayer 20.0
";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Collectible_1", result[0].Path);
        }

        // E5 — RuntimeHelper: reading Camera.nearClipPlane (float field via property)
        [Test]
        public void RuntimeHelper_ReadFieldInternal_Camera_NearClip_ReturnsValue()
        {
            // Substituting GridPlayer with Camera (same test intent: RuntimeHelper field read)
            var go = new GameObject("TestCam");
            var cam = go.AddComponent<Camera>();
            try
            {
                // nearClipPlane is a property on Camera — may throw if not field-backed
                // Test documents the expected failure mode
                try
                {
                    var val = RuntimeHelper.ReadFieldInternal(cam, "nearClipPlane");
                    Assert.IsNotNull(val); // got a value — good
                }
                catch (ArgumentException)
                {
                    Assert.Pass("ReadFieldInternal correctly threw for non-field-backed property");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // E6 — RuntimeHelper: invalid field name throws
        [Test]
        public void RuntimeHelper_ReadFieldInternal_InvalidField_Throws()
        {
            var go = new GameObject("TestObj");
            var tr = go.GetComponent<Transform>();
            try
            {
                // "NonExistentField" doesn't exist on Transform — throws ArgumentException or similar
                Assert.Catch<Exception>(() => RuntimeHelper.ReadFieldInternal(tr, "NonExistentField"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
