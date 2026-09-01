// ROI Fix 1 (P0-70 pre-effect boundary check) wiring/integration tests:
// prove SourcePatchModePolicy.TryApplyWrite rejects a hostile path before
// any Read/AcquireLease/Write/Apply effect, and mcp_status never observes
// a spurious Recovery entry from a rejected path. See
// Plans/roi-fix-1-2-blueprint.md "Fix 1" section. SetUp mirrors
// SourcePatchMutationModeTests.cs:28-36.
using System;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchOnPathValidationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeProvider : ISourcePatchProvider
        {
            public int ApplyCount;
            public SourcePatchApplyOutcome Outcome = SourcePatchApplyOutcome.Applied;

            public SourcePatchApplyOutcome Apply(SourcePatchRequest request)
            {
                ApplyCount++;
                return Outcome;
            }
        }

        private sealed class NoopBytesPort : ISourcePatchBytesPort
        {
            public byte[] Read(string assetPath) => System.Array.Empty<byte>();
            public void Write(string assetPath, byte[] content) { }
        }

        private sealed class NoopLeasePort : IAutoRefreshLeasePort
        {
            public IDisposable AcquireLease() => new NoopLease();
            private sealed class NoopLease : IDisposable { public void Dispose() { } }
        }

        private sealed class AlwaysConfirmedEvidencePort : ICompileEvidencePort
        {
            public bool ConfirmApplied(SourcePatchRequest request) => true;
        }

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchHost.ResetForTests();
            SourcePatchProviderSlot.ResetForTests();
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
        }

        // Real armed coordinator (all fake ports, no disk/AssetDatabase touch)
        // so a rejected path can be proven to never reach it.
        private static FakeProvider ArmRealCoordinator()
        {
            var provider = new FakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            SourcePatchHost.Coordinator = new SourcePatchCoordinator(
                new NoopBytesPort(), provider, new NoopLeasePort(),
                new AlwaysConfirmedEvidencePort(), SourcePatchState.OnReady);
            return provider;
        }

        [Test]
        public void WriteText_TraversalPath_RejectsBeforeAnyFileRead_StateStaysOnReady()
        {
            ArmRealCoordinator();

            Assert.Throws<ArgumentException>(
                () => SourcePatchHost.WriteText("{\"path\":\"../outside.cs\",\"content\":\"x\"}"));

            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState,
                "a rejected path must never move state off OnReady (no Busy/Recovery)");
        }

        [Test]
        public void WriteText_PackagesPath_RejectsWithReasonAndStaysOnReady()
        {
            ArmRealCoordinator();

            var ex = Assert.Throws<ArgumentException>(
                () => SourcePatchHost.WriteText("{\"path\":\"Packages/com.foo/Bar.cs\",\"content\":\"x\"}"));

            StringAssert.Contains("Assets/", ex.Message);
            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState);
        }

        [Test]
        public void WriteText_AbsolutePath_RejectsWithReasonAndStaysOnReady()
        {
            ArmRealCoordinator();

            Assert.Throws<ArgumentException>(
                () => SourcePatchHost.WriteText("{\"path\":\"/etc/evil.cs\",\"content\":\"x\"}"));

            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState);
        }

        [Test]
        public void TryApplyWrite_TraversalPath_ProviderNeverInvoked()
        {
            var provider = ArmRealCoordinator();

            Assert.Throws<ArgumentException>(
                () => SourcePatchModePolicy.TryApplyWrite("{\"path\":\"../outside.cs\",\"content\":\"x\"}"));

            Assert.AreEqual(0, provider.ApplyCount,
                "the guard must run before coordinator.TryApply — provider must never be invoked");
        }
    }
}
