// P0-70: get_status/mcp_status projection (§6 "mcp_status projects intent,
// provider capability, lifecycle state, active op and recovery truthfully").
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchStatusProjectionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeProvider : ISourcePatchProvider
        {
            public SourcePatchApplyOutcome Apply(SourcePatchRequest request) => SourcePatchApplyOutcome.Applied;
        }

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchHost.ResetForTests();
            SourcePatchProviderSlot.ResetForTests();
        }

        [Test]
        public void ProviderAbsent_ProjectsAbsentAndOff()
        {
            SourcePatchHost.CurrentState = SourcePatchState.Unavailable;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=off", status);
            StringAssert.Contains("source_patch_provider=absent", status);
            StringAssert.Contains("source_patch_state=Unavailable", status);
            StringAssert.Contains("source_patch_recovery=false", status);
        }

        // Off + provider-absent is legal: it is the exact combination the
        // Editor is left in right after a user physically removes the
        // optional package while intent was already Off/zero-lease (§6
        // P0-80's "remove optional package offline only after Off/zero
        // lease"), before the next domain start's lazy reconciliation would
        // otherwise settle it to Unavailable. The other ×absent combinations
        // (OnReady/Busy/Disabling/Recovery + absent) are architecturally
        // unreachable in production — every one of those states requires
        // having passed through Off with a provider present — so they are
        // deliberately not covered here.
        [Test]
        public void ProviderAbsent_Off_ProjectsOffIntentAbsentCapabilityOffState()
        {
            SourcePatchHost.CurrentState = SourcePatchState.Off;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=off", status);
            StringAssert.Contains("source_patch_provider=absent", status);
            StringAssert.Contains("source_patch_state=Off", status);
        }

        [Test]
        public void ProviderInstalled_Off_ProjectsInstalledAndOff()
        {
            SourcePatchProviderSlot.Register("fake-status", new FakeProvider());
            SourcePatchHost.CurrentState = SourcePatchState.Off;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=off", status);
            StringAssert.Contains("source_patch_provider=installed", status);
            StringAssert.Contains("source_patch_state=Off", status);
        }

        [Test]
        public void OnReady_ProjectsIntentOn()
        {
            SourcePatchProviderSlot.Register("fake-status", new FakeProvider());
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=on", status);
            StringAssert.Contains("source_patch_state=OnReady", status);
            StringAssert.Contains("source_patch_op=none", status);
        }

        [Test]
        public void Busy_ProjectsIntentOnAndActiveOp()
        {
            SourcePatchProviderSlot.Register("fake-status", new FakeProvider());
            SourcePatchHost.CurrentState = SourcePatchState.Busy;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=on", status);
            StringAssert.Contains("source_patch_op=apply-in-progress", status);
        }

        [Test]
        public void Disabling_ProjectsIntentOffAndReceiptOpId()
        {
            SourcePatchProviderSlot.Register("fake-status", new FakeProvider());
            SourcePatchReceiptStore.Write(new SourcePatchDisableReceipt("disable-op-1", 1, "/p", 2));
            SourcePatchHost.CurrentState = SourcePatchState.Disabling;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=off", status);
            StringAssert.Contains("source_patch_state=Disabling", status);
            StringAssert.Contains("source_patch_op=disable-op-1", status);
            StringAssert.Contains("source_patch_recovery=false", status);
        }

        [Test]
        public void Recovery_ProjectsRecoveryTrueAndIntentOff()
        {
            SourcePatchProviderSlot.Register("fake-status", new FakeProvider());
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;

            var status = SourcePatchModePolicy.StatusProjection();

            StringAssert.Contains("source_patch_intent=off", status);
            StringAssert.Contains("source_patch_recovery=true", status);
        }
    }
}
