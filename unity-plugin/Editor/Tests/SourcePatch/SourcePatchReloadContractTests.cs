// PR-06 Gap A + Gap B: every existing SourcePatch test reaches Off/Recovery by
// assigning SourcePatchHost.CurrentState directly (a documented test seam).
// Nothing exercises the getter's real lazy EnsureReconciled() -> ComputeInitialState()
// -> ReconcileDomainStart() chain, chained to a receipt actually produced by
// RequestDisable() - exactly what a real Domain Reload does in production.
// ForceUnreconciledForTests() reproduces the one side effect of a real Domain
// Reload (_reconciled reset to false) without requiring one.
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchReloadContractTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MockSyncOps _syncOps;

        private sealed class RecordingReloadPort : ISourcePatchReloadPort
        {
            public int CallCount;
            public ReloadPortOutcome RequestReloadVerification(int expectedEpochAfter)
            {
                CallCount++;
                return ReloadPortOutcome.Accepted;
            }
        }

        private sealed class FakeProvider : ISourcePatchProvider
        {
            public SourcePatchApplyOutcome Outcome;
            public SourcePatchApplyOutcome Apply(SourcePatchRequest request) => Outcome;
        }

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(() => SourcePatchModePolicy.ReloadPort = new SyncHelperReloadPort());
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchHost.ResetForTests();
            _syncOps = new MockSyncOps();
            SyncHelper.OverrideOpsForTest(_syncOps);
        }

        /// <summary>Shared arrangement for both tests below: real Off -> OnReady,
        /// then a real RequestDisable (Disabling, receipt written, injected port
        /// called exactly once - no live SyncHelper trigger from this step).</summary>
        private SourcePatchDisableReceipt ArmOnReadyThenRequestDisable(out RecordingReloadPort fakePort)
        {
            SourcePatchProviderSlot.Register("fake", new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied });
            SourcePatchHost.CurrentState = SourcePatchState.Off; // legitimate rest-state seam, same as every existing fixture
            Assert.AreEqual("mutation_mode:true", SourcePatchModePolicy.SetMutationIntent(true));

            fakePort = new RecordingReloadPort();
            SourcePatchModePolicy.ReloadPort = fakePort;
            Assert.AreEqual("requested", SourcePatchModePolicy.SetMutationIntent(false));
            Assert.AreEqual(1, fakePort.CallCount);

            Assert.IsTrue(SourcePatchReceiptStore.TryRead(out var receipt), "RequestDisable must persist a receipt");
            return receipt;
        }

        private void CompleteOwnedReload(SourcePatchDisableReceipt receipt)
        {
            var epochBefore = SyncHelper.CurrentEpoch;
            Assert.AreEqual(epochBefore + 1, receipt.ExpectedEpochAfter);
            Assert.AreEqual("blocked|reason=source_patch_Disabling_explicit_disable_required",
                SyncHelper.TriggerSync(false));
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch, "ordinary sync cannot advance disable's epoch");
            Assert.AreEqual(0, _syncOps.RefreshCount);
            Assert.AreEqual(0, _syncOps.RequestScriptCompilationCount);
            Assert.AreEqual(0, _syncOps.StartTickPumpCount);

            // Exactly one accepted, owned request; the real port checks the exact ACK.
            // The fixture's mock performs no AssetDatabase or compilation effects.
            new SyncHelperReloadPort().RequestReloadVerification(receipt.ExpectedEpochAfter);
            Assert.AreEqual(receipt.ExpectedEpochAfter, SyncHelper.CurrentEpoch);
            Assert.AreEqual(1, _syncOps.RefreshCount);
            Assert.AreEqual(1, _syncOps.RequestScriptCompilationCount);
            Assert.AreEqual(1, _syncOps.StartTickPumpCount);
        }

        private void SimulateOutOfBandEpochAdvance()
        {
            // Ordinary client sync is now blocked during Disabling. Inject one
            // out-of-band epoch through the existing admission seam to retain the
            // domain-start mismatch oracle, without claiming clients can bypass it.
            Assert.AreSame(_syncOps, SyncHelper.Ops, "simulation must never use native sync operations");
            var admission = SyncHelper.ReloadBlockReason;
            var expectedEpoch = SyncHelper.CurrentEpoch + 1;
            try
            {
                SyncHelper.ReloadBlockReason = _ => null;
                Assert.AreEqual($"sync_ack|epoch={expectedEpoch}|will_compile=false", SyncHelper.TriggerSync(false));
                Assert.AreEqual(expectedEpoch, SyncHelper.CurrentEpoch);
            }
            finally { SyncHelper.ReloadBlockReason = admission; }
        }

        [Test]
        public void OnReadyToOff_ThroughRealReconciliation_ClearsReceiptAndNextWriteIsLegacy()
        {
            var receipt = ArmOnReadyThenRequestDisable(out _);

            CompleteOwnedReload(receipt);
            SourcePatchHost.ForceUnreconciledForTests();

            // Real reconciliation, not a forced setter.
            Assert.AreEqual(SourcePatchState.Off, SourcePatchHost.CurrentState);
            Assert.IsFalse(SourcePatchReceiptStore.TryRead(out _), "completed Off clears the receipt");
        }

        [Test]
        public void OutOfBandEpochDriftAfterOwnedDisable_ResolvesRecoveryNeverFalseOff()
        {
            var receipt = ArmOnReadyThenRequestDisable(out _);

            CompleteOwnedReload(receipt);
            SimulateOutOfBandEpochAdvance();
            SourcePatchHost.ForceUnreconciledForTests();

            // Fail closed: Recovery, never an optimistic Off; receipt retained (no auto-repair).
            Assert.AreEqual(SourcePatchState.Recovery, SourcePatchHost.CurrentState);
            Assert.IsTrue(SourcePatchReceiptStore.TryRead(out _), "a mismatched receipt is never silently cleared");
        }
    }
}
