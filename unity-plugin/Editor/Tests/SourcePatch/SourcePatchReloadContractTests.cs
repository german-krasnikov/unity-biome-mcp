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
        private sealed class RecordingReloadPort : ISourcePatchReloadPort
        {
            public int CallCount;
            public void RequestReloadVerification() => CallCount++;
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
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
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

        [Test]
        public void OnReadyToOff_ThroughRealReconciliation_ClearsReceiptAndNextWriteIsLegacy()
        {
            var receipt = ArmOnReadyThenRequestDisable(out _);

            // Simulate the disable's OWN expected reload actually landing: bump the
            // real epoch (via MockSyncOps, no real compile) to exactly what the
            // receipt expects, then force the lazy path to re-run - this is the one
            // thing a real Domain Reload does that ResetForTests() does not.
            while (SyncHelper.CurrentEpoch < receipt.ExpectedEpochAfter) SyncHelper.TriggerSync(false);
            SourcePatchHost.ForceUnreconciledForTests();

            // Real reconciliation, not a forced setter.
            Assert.AreEqual(SourcePatchState.Off, SourcePatchHost.CurrentState);
            Assert.IsFalse(SourcePatchReceiptStore.TryRead(out _), "completed Off clears the receipt");

            // Next .cs write: byte-identical to a direct legacy call (A07's "следующая запись идёт штатным путём").
            var legacyPath = TrackOwnedAsset("Assets/TestsTemp/SourcePatchReloadContract_legacy.cs");
            var hostPath = TrackOwnedAsset("Assets/TestsTemp/SourcePatchReloadContract_viahost.cs");
            var direct = AssetDatabaseHelper.Execute("write_text", "{\"path\":\"" + legacyPath + "\",\"content\":\"x\"}");
            var viaHost = SourcePatchHost.WriteText("{\"path\":\"" + hostPath + "\",\"content\":\"x\"}");
            Assert.AreEqual(direct.Replace(legacyPath, hostPath), viaHost);
        }

        [Test]
        public void ClientSyncBetweenDisableAndOwnReload_EpochDriftResolvesRecoveryNeverFalseOff()
        {
            var receipt = ArmOnReadyThenRequestDisable(out _);

            // R04: an unrelated client-triggered sync_unity lands ONE EXTRA reload
            // cycle before the disable's own expected epoch is reached - landing the
            // real epoch one past what the receipt expects.
            while (SyncHelper.CurrentEpoch < receipt.ExpectedEpochAfter) SyncHelper.TriggerSync(false);
            SyncHelper.TriggerSync(false); // the extra, independently-triggered client sync
            SourcePatchHost.ForceUnreconciledForTests();

            // Fail closed: Recovery, never an optimistic Off; receipt retained (no auto-repair).
            Assert.AreEqual(SourcePatchState.Recovery, SourcePatchHost.CurrentState);
            Assert.IsTrue(SourcePatchReceiptStore.TryRead(out _), "a mismatched receipt is never silently cleared");
        }
    }
}
