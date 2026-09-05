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
            public void RequestReloadVerification() => CallCount++;
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
    }
}
