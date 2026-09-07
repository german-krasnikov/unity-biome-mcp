// PR-04R Step B3: SourcePatchModePolicy's disable path now goes through a narrow
// ISourcePatchReloadPort instead of calling SyncHelper.TriggerSync directly. The
// legacy SyncHelperReloadPort adapter reproduces today's exact call, so the
// epoch-based assertions in SourcePatchMutationModeTests.cs stay green unchanged.
// This file proves the port itself: a fake port receives the call, and the real
// SyncHelper epoch is untouched when the fake is installed — the call site has no
// hidden second path to SyncHelper.
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchModePolicyReloadPortTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class RecordingReloadPort : ISourcePatchReloadPort
        {
            public int CallCount;
            public int? ReceivedEpoch;
            public System.Exception Failure;
            public ReloadPortOutcome Outcome = ReloadPortOutcome.Accepted;
            public ReloadPortOutcome RequestReloadVerification(int expectedEpochAfter)
            {
                CallCount++;
                ReceivedEpoch = expectedEpochAfter;
                if (Failure != null) throw Failure;
                return Outcome;
            }
        }

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(() => SourcePatchModePolicy.ReloadPort = new SyncHelperReloadPort());
            SourcePatchHost.ResetForTests();
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
        }

        [Test]
        public void RequestDisable_FromOnReady_CallsInjectedPortExactlyOnce()
        {
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var fakePort = new RecordingReloadPort();
            SourcePatchModePolicy.ReloadPort = fakePort;
            var epochBefore = SyncHelper.CurrentEpoch;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("requested", result);
            Assert.AreEqual(1, fakePort.CallCount,
                "RequestDisable must call the injected port exactly once");
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch,
                "installing a fake port must remove the hidden direct SyncHelper.TriggerSync call");
        }

        [Test]
        public void RequestDisable_FromRecovery_CallsInjectedPortExactlyOnce()
        {
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;
            var fakePort = new RecordingReloadPort();
            SourcePatchModePolicy.ReloadPort = fakePort;
            var epochBefore = SyncHelper.CurrentEpoch;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("requested", result);
            Assert.AreEqual(1, fakePort.CallCount);
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch);
        }

        [Test]
        public void RequestDisable_NoOpPort_EntersRecoveryNotDisabling()
        {
            // N2a.3 GAP: a no-op ACK (will_compile=false — no domain reload
            // will happen) must not leave the policy silently stuck reporting
            // "requested" forever in Disabling, and must not claim Off. It
            // must land on the one state that means "bounded, explicit,
            // retryable" — Recovery — with the receipt retained.
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var fakePort = new RecordingReloadPort { Outcome = ReloadPortOutcome.AcceptedNoOp };
            SourcePatchModePolicy.ReloadPort = fakePort;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual(SourcePatchModePolicy.NoOpRecoveryResult, result);
            Assert.AreEqual(1, fakePort.CallCount);
            Assert.AreEqual(SourcePatchState.Recovery, SourcePatchHost.CurrentState,
                "a no-op reload must not stay in Disabling and must not become Off");
            Assert.That(SourcePatchReceiptStore.TryRead(out var receipt), Is.True,
                "receipt must be retained for explicit recovery after a no-op outcome");
            Assert.AreEqual(receipt.ExpectedEpochAfter, fakePort.ReceivedEpoch,
                "the port must receive the receipt's Reload-owned epoch, not a locally recomputed value");
        }

        [Test]
        public void RequestDisable_RejectedOutcome_EntersRecoveryAndThrows()
        {
            // Distinct from the exception-throwing fake below: this port returns
            // a structured ReloadPortOutcome.Rejected value (no exception of its
            // own). RequestDisable must still fail closed — it throws (per the
            // catch block's re-throw semantics) and lands the same place a throwing
            // port would: Recovery, with the receipt retained.
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var fakePort = new RecordingReloadPort { Outcome = ReloadPortOutcome.Rejected };
            SourcePatchModePolicy.ReloadPort = fakePort;
            var epochBefore = SyncHelper.CurrentEpoch;

            Assert.Throws<System.InvalidOperationException>(() => SourcePatchModePolicy.SetMutationIntent(false));

            Assert.That(fakePort.CallCount, Is.EqualTo(1));
            Assert.That(SourcePatchHost.CurrentState, Is.EqualTo(SourcePatchState.Recovery));
            Assert.That(SourcePatchReceiptStore.TryRead(out var receipt), Is.True,
                "receipt must be retained after a Rejected outcome, same as after a thrown exception");
            Assert.That(receipt.ExpectedEpochAfter, Is.EqualTo(epochBefore + 1));
            Assert.That(SyncHelper.CurrentEpoch, Is.EqualTo(epochBefore));
        }

        [Test]
        public void RequestDisable_RejectedReloadEntersRecoveryAndRetainsReceipt()
        {
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var fakePort = new RecordingReloadPort
            {
                Failure = new System.InvalidOperationException("wedged|epoch=20")
            };
            SourcePatchModePolicy.ReloadPort = fakePort;
            var epochBefore = SyncHelper.CurrentEpoch;

            Assert.Throws<System.InvalidOperationException>(() => SourcePatchModePolicy.SetMutationIntent(false));

            Assert.That(fakePort.CallCount, Is.EqualTo(1));
            Assert.That(SourcePatchHost.CurrentState, Is.EqualTo(SourcePatchState.Recovery));
            Assert.That(SourcePatchReceiptStore.TryRead(out var receipt), Is.True);
            Assert.That(receipt.ExpectedEpochAfter, Is.EqualTo(epochBefore + 1));
            Assert.That(SyncHelper.CurrentEpoch, Is.EqualTo(epochBefore));
        }

        // V5 (Plans/N2-reload-sourcepatch-contract.md): a fake port that re-enters
        // the disable route (e.g. some other editor callback firing while the
        // reload port call is in flight) while CurrentState is already Disabling.
        // RequestDisable's own Disabling short-circuit (L144-145 above) must
        // absorb the reentry idempotently, WITHOUT redispatching to the port and
        // WITHOUT touching the receipt written before the port was first called.
        private sealed class ReentrantDuringPortPort : ISourcePatchReloadPort
        {
            public int CallCount;
            public string ReentrantResult;
            public SourcePatchDisableReceipt ReceiptSeenDuringCall;

            public ReloadPortOutcome RequestReloadVerification(int expectedEpochAfter)
            {
                CallCount++;
                SourcePatchReceiptStore.TryRead(out var receipt);
                ReceiptSeenDuringCall = receipt;
                // Simulated reentrant event, firing on the same call stack.
                ReentrantResult = SourcePatchModePolicy.SetMutationIntent(false);
                return ReloadPortOutcome.Accepted;
            }
        }

        [Test]
        public void ReentrantDisable_DuringPort_DoesNotOverwriteReceipt()
        {
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var fakePort = new ReentrantDuringPortPort();
            SourcePatchModePolicy.ReloadPort = fakePort;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("requested", result);
            Assert.AreEqual(1, fakePort.CallCount,
                "the port is invoked exactly once — a reentrant disable must short-circuit on Disabling, never redispatch");
            Assert.AreEqual("requested", fakePort.ReentrantResult,
                "the reentrant disable observes the same idempotent Disabling short-circuit as any other caller");
            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState);
            Assert.IsTrue(SourcePatchReceiptStore.TryRead(out var receiptAfter));
            Assert.AreEqual(fakePort.ReceiptSeenDuringCall.OpId, receiptAfter.OpId,
                "reentry must never overwrite the original receipt's correlation id (OpId)");
            Assert.AreEqual(fakePort.ReceiptSeenDuringCall.ExpectedEpochAfter, receiptAfter.ExpectedEpochAfter,
                "reentry must never recompute/overwrite the original receipt's expected epoch");
        }
    }
}
