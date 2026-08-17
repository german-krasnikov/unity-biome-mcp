using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPBatchAtomicTests : UnityMcpTestBase
    {
        private List<GameObject> _toDestroy = new List<GameObject>();
        private Func<bool> _origIsReadOnly;

        [SetUp]
        public void SetUp()
        {
            _origIsReadOnly = CommandRouter.IsReadOnly;
            CommandRouter.IsReadOnly = () => false;
            CommandRouter.IsCompiling = () => false;
            BatchHelper.IsCompiling = () => false;
        }

        [TearDown]
        public void TearDown()
        {
            CommandRouter.IsReadOnly = _origIsReadOnly;
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
            foreach (var go in _toDestroy)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        // ── 1. All succeed — objects present, no rollback ─────────────────────
        [Test]
        public void Atomic_AllSucceed_AllObjectsPresent()
        {
            string result = BatchHelper.Execute(
                "create_object name=AtomicA\ncreate_object name=AtomicB",
                "continue", 25000, atomic: true);

            Assert.IsFalse(result.Contains("ATOMIC_ROLLBACK"), "No rollback expected on success");
            Assert.IsTrue(result.Contains("ok:2"), "Both ops should succeed");

            var a = GameObject.Find("AtomicA");
            var b = GameObject.Find("AtomicB");
            _toDestroy.Add(a);
            _toDestroy.Add(b);
            Assert.IsNotNull(a, "AtomicA should exist");
            Assert.IsNotNull(b, "AtomicB should exist");
        }

        // ── 2. Op1 succeeds, Op2 fails — everything reverted ─────────────────
        [Test]
        public void Atomic_Op2Fails_AllReverted_SceneUnchanged()
        {
            string result = BatchHelper.Execute(
                "create_object name=AtomicC\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: true);

            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Rollback expected");
            Assert.IsTrue(result.Contains("err:1"), "Should report 1 error");

            // Should be null after rollback, but track for safety
            var c = GameObject.Find("AtomicC");
            if (c != null) _toDestroy.Add(c);
            Assert.IsNull(c, "AtomicC should be reverted (not present in scene)");
        }

        // ── 3. Non-atomic — partial apply on failure ──────────────────────────
        [Test]
        public void NonAtomic_Op2Fails_PartialApplied()
        {
            string result = BatchHelper.Execute(
                "create_object name=AtomicD\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: false);

            Assert.IsFalse(result.Contains("ATOMIC_ROLLBACK"), "No rollback in non-atomic mode");

            var d = GameObject.Find("AtomicD");
            Assert.IsNotNull(d, "AtomicD should remain (partial apply in non-atomic mode)");
            _toDestroy.Add(d);
        }

        // ── 4. Nested batch: outer atomic reverts inner's work too ────────────
        [Test]
        public void Atomic_NestedBatch_OuterReverts()
        {
            string innerBatch = "create_object name=AtomicE";
            string result = BatchHelper.Execute(
                $"batch commands=\"{innerBatch}\"\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: true);

            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Outer rollback expected");

            var e = GameObject.Find("AtomicE");
            if (e != null) _toDestroy.Add(e);
            Assert.IsNull(e, "AtomicE (created by nested batch) should be reverted by outer");
        }

        // ── 5. Read-only atomic batch — no exception, no spurious undo entry ──
        [Test]
        public void Atomic_ReadOnlyBatch_NoOp()
        {
            // ping is a read command — should succeed cleanly with no rollback
            Assert.DoesNotThrow(() =>
            {
                string result = BatchHelper.Execute(
                    "ping\nping",
                    "continue", 25000, atomic: true);
                Assert.IsFalse(result.Contains("ATOMIC_ROLLBACK"), "No rollback for read-only batch");
                Assert.IsTrue(result.Contains("ok:2"), "Both pings should succeed");
            });
        }

        // ── 6. Op0 fails in atomic mode — message must not say "0..-1" ──────────
        [Test]
        public void Atomic_FirstOpFails_NothingToRevert()
        {
            string result = BatchHelper.Execute(
                "set_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: true);

            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Rollback message expected even for op 0 failure");
            Assert.IsFalse(result.Contains("0..-1"), "Must not emit misleading '0..-1' range");
            Assert.IsTrue(result.Contains("nothing to revert"), "Must clarify nothing was applied");
        }

        // ── Strategy C: ReadOnly verification ────────────────────────────────

        [Test]
        public void Atomic_CreateObject_WhenReadOnly_IsBlocked()
        {
            // Temporarily override seam to simulate RO worker
            CommandRouter.IsReadOnly = () => true;
            try
            {
                var result = BatchHelper.Execute(
                    "create_object name=ROVerifyObj", "stop", 5000, atomic: true);
                StringAssert.Contains("READ_ONLY_BLOCKED", result);
            }
            finally
            {
                CommandRouter.IsReadOnly = () => false;
            }
        }

        // ── 7. atomic=true overrides on_error=continue (still stops+reverts) ──
        [Test]
        public void Atomic_OverridesOnErrorContinue()
        {
            // With on_error=continue normally we'd keep going, but atomic overrides
            string result = BatchHelper.Execute(
                "create_object name=AtomicF\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)\ncreate_object name=AtomicG",
                "continue", 25000, atomic: true);

            // AtomicG (op2) must NOT have been created — atomic stopped at op1 failure
            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Rollback expected");
            Assert.IsFalse(result.Contains("[2]"), "Op2 should not have been executed");

            var f = GameObject.Find("AtomicF");
            var g = GameObject.Find("AtomicG");
            if (f != null) _toDestroy.Add(f);
            if (g != null) _toDestroy.Add(g);
            Assert.IsNull(f, "AtomicF should be reverted");
            Assert.IsNull(g, "AtomicG should never have been created");
        }

        [TestCase("err: rejected")]
        [TestCase("ERROR: rejected")]
        [TestCase("  blocked: rejected")]
        [TestCase("\twarn: context\n  TIMEOUT: rejected")]
        [TestCase("DRY-RUN RESOLVE ERROR: target not found")]
        public void HandlerFailurePrefixes_AreClassifiedAcrossAllLines(string result)
        {
            Assert.IsTrue(BatchHelper.IsFailureResult(result), result);
        }

        [Test]
        public void NonAtomic_HandlerReturnedError_Stop_DoesNotCountOkOrRunNext()
        {
            CommandRegistry.Register("test_returned_error", _ => "error: rejected",
                required: "", alwaysAllowed: true);

            var result = BatchHelper.Execute(
                "test_returned_error\ncreate_object name=ReturnedErrorNext",
                "stop", 25000, atomic: false);

            StringAssert.Contains("error: rejected", result, result);
            StringAssert.Contains("ok:0 err:1", result, result);
            Assert.IsTrue(BatchHelper.HasErrors(result), result);
            var next = GameObject.Find("ReturnedErrorNext");
            if (next != null) _toDestroy.Add(next);
            Assert.IsNull(next, "on_error=stop must not execute the next operation");
        }

        [Test]
        public void Atomic_WarningThenReturnedError_RollsBackPriorMutationAndStops()
        {
            CommandRegistry.Register("test_returned_multiline_error",
                _ => "warn: validation context\n  ErR: rejected after warning",
                required: "", alwaysAllowed: true);

            var result = BatchHelper.Execute(
                "create_object name=ReturnedErrorBefore\n" +
                "test_returned_multiline_error\n" +
                "create_object name=ReturnedErrorAfter",
                "continue", 25000, atomic: true);

            StringAssert.Contains("ATOMIC_ROLLBACK", result, result);
            StringAssert.Contains("ok:1 err:1", result, result);
            Assert.IsTrue(BatchHelper.HasErrors(result), result);

            var before = GameObject.Find("ReturnedErrorBefore");
            var after = GameObject.Find("ReturnedErrorAfter");
            if (before != null) _toDestroy.Add(before);
            if (after != null) _toDestroy.Add(after);
            Assert.IsNull(before, "the prior mutation must be reverted");
            Assert.IsNull(after, "the operation after the returned error must not run");
        }

        [TestCase("This is an error: shown as ordinary prose")]
        [TestCase("note: DRY-RUN RESOLVE ERROR: appears in documentation")]
        [TestCase("DRY-RUN: value contains ERROR: but operation succeeded")]
        public void OrdinaryProseContainingError_IsNotFailureProtocol(string result)
        {
            Assert.IsFalse(BatchHelper.IsFailureResult(result), result);
        }

        [Test]
        public void Atomic_DryRunResolveError_RollsBackPriorMutationAndStops()
        {
            CommandRegistry.Register("test_dry_run_resolve_error",
                _ => "DRY-RUN RESOLVE ERROR: target not found: /Missing",
                required: "", alwaysAllowed: true);

            var result = BatchHelper.Execute(
                "create_object name=DryRunResolveBefore\n" +
                "test_dry_run_resolve_error\n" +
                "create_object name=DryRunResolveAfter",
                "continue", 25000, atomic: true);

            StringAssert.Contains("DRY-RUN RESOLVE ERROR:", result, result);
            StringAssert.Contains("ATOMIC_ROLLBACK", result, result);
            StringAssert.Contains("ok:1 err:1", result, result);

            var before = GameObject.Find("DryRunResolveBefore");
            var after = GameObject.Find("DryRunResolveAfter");
            if (before != null) _toDestroy.Add(before);
            if (after != null) _toDestroy.Add(after);
            Assert.IsNull(before, "the prior mutation must be reverted");
            Assert.IsNull(after, "the operation after the protocol error must not run");
        }

        [Test]
        public void Atomic_ActualBulkDryRunResolveError_RollsBackAndStops()
        {
            var result = BatchHelper.Execute(
                "create_object name=BulkResolveBefore components=Rigidbody2D\n" +
                "set_property find_type=Rigidbody2D component=Rigidbody2D " +
                "prop=m_ConnectedBody value=/DefinitelyMissingBulkTarget dry_run=true\n" +
                "create_object name=BulkResolveAfter",
                "continue", 25000, atomic: true);

            StringAssert.Contains("DRY-RUN BULK ERROR:", result, result);
            StringAssert.Contains("ATOMIC_ROLLBACK", result, result);
            StringAssert.Contains("ok:1 err:1", result, result);

            var before = GameObject.Find("BulkResolveBefore");
            var after = GameObject.Find("BulkResolveAfter");
            if (before != null) _toDestroy.Add(before);
            if (after != null) _toDestroy.Add(after);
            Assert.IsNull(before, "the created Rigidbody2D object must be reverted");
            Assert.IsNull(after, "the operation after the bulk failure must not run");
        }

        [Test]
        public void NonAtomic_DeadlineTimeout_IsAnErrorEvenWhenErrCountIsZero()
        {
            var elapsed = new Queue<long>(new[] { 0L, 1001L });

            var result = BatchHelper.Execute(
                "ping\nping", "continue", timeoutMs: 1000,
                elapsedMilliseconds: () => elapsed.Dequeue());

            StringAssert.Contains("TIMEOUT: batch deadline", result, result);
            StringAssert.Contains("ok:1 timeout:1", result, result);
            Assert.IsTrue(BatchHelper.HasErrors(result), result);
        }
    }
}
