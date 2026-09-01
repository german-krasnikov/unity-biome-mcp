// P0-70: SourcePatchModePolicy — editor(mutation_mode) query/set body and the
// ON/OFF policy (§3.3/§6). SyncHelper.Ops is doubled with MockSyncOps
// (SyncHelperTests.cs) so TriggerSync never touches the real AssetDatabase —
// only its epoch bookkeeping is exercised. See
// Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchMutationModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchHost.ResetForTests();
            SourcePatchProviderSlot.ResetForTests();
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
        }

        private static FakeProvider RegisterFakeProvider()
        {
            var provider = new FakeProvider();
            SourcePatchProviderSlot.Register("fake-mode-tests", provider);
            return provider;
        }

        // ── query ────────────────────────────────────────────────────────────

        [TestCase(SourcePatchState.Unavailable, ExpectedResult = "mutation_mode:false")]
        [TestCase(SourcePatchState.Off, ExpectedResult = "mutation_mode:false")]
        [TestCase(SourcePatchState.OnReady, ExpectedResult = "mutation_mode:true")]
        [TestCase(SourcePatchState.Busy, ExpectedResult = "mutation_mode:true")]
        [TestCase(SourcePatchState.Disabling, ExpectedResult = "mutation_mode:false")]
        [TestCase(SourcePatchState.Recovery, ExpectedResult = "mutation_mode:false")]
        public string Query_ReflectsIntentDerivedFromState(SourcePatchState state)
        {
            SourcePatchHost.CurrentState = state;
            return SourcePatchModePolicy.SetMutationIntent(null);
        }

        [Test]
        public void Query_NeverMutatesState()
        {
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;
            SourcePatchModePolicy.SetMutationIntent(null);
            Assert.AreEqual(SourcePatchState.Recovery, SourcePatchHost.CurrentState);
        }

        // ── enable=true ──────────────────────────────────────────────────────

        [Test]
        public void EnableTrue_ProviderAbsent_ThrowsWithZeroEffect()
        {
            SourcePatchHost.CurrentState = SourcePatchState.Unavailable;
            var epochBefore = SyncHelper.CurrentEpoch;

            Assert.Throws<InvalidOperationException>(() => SourcePatchModePolicy.SetMutationIntent(true));

            Assert.AreEqual(SourcePatchState.Unavailable, SourcePatchHost.CurrentState);
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch);
        }

        [Test]
        public void EnableTrue_ProviderPresentAndOff_TransitionsToOnReady()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.Off;

            var result = SourcePatchModePolicy.SetMutationIntent(true);

            Assert.AreEqual("mutation_mode:true", result);
            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState);
        }

        [Test]
        public void EnableTrue_AlreadyOnReady_IsIdempotent()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;

            var result = SourcePatchModePolicy.SetMutationIntent(true);

            Assert.AreEqual("mutation_mode:true", result);
            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState);
        }

        [TestCase(SourcePatchState.Busy)]
        [TestCase(SourcePatchState.Disabling)]
        [TestCase(SourcePatchState.Recovery)]
        public void EnableTrue_BusyDisablingOrRecovery_RejectsWithoutStateChange(SourcePatchState state)
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = state;

            Assert.Throws<InvalidOperationException>(() => SourcePatchModePolicy.SetMutationIntent(true));

            Assert.AreEqual(state, SourcePatchHost.CurrentState);
        }

        // ── enable=false ─────────────────────────────────────────────────────

        [TestCase(SourcePatchState.Off)]
        [TestCase(SourcePatchState.Unavailable)]
        public void EnableFalse_AlreadyOffOrUnavailable_IsIdempotentNoSync(SourcePatchState state)
        {
            SourcePatchHost.CurrentState = state;
            var epochBefore = SyncHelper.CurrentEpoch;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("mutation_mode:false", result);
            Assert.AreEqual(state, SourcePatchHost.CurrentState);
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch);
        }

        [Test]
        public void EnableFalse_FromOnReady_WritesReceiptTransitionsDisablingAndSyncsExactlyOnce()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var epochBefore = SyncHelper.CurrentEpoch;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("requested", result);
            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState);
            Assert.AreEqual(epochBefore + 1, SyncHelper.CurrentEpoch, "exactly one sync");
            Assert.IsTrue(SourcePatchReceiptStore.TryRead(out var receipt));
            Assert.AreEqual(epochBefore + 1, receipt.ExpectedEpochAfter);
        }

        [TestCase(SourcePatchState.Busy)]
        public void EnableFalse_Busy_RejectsWithoutReceiptOrSync(SourcePatchState state)
        {
            SourcePatchHost.CurrentState = state;
            var epochBefore = SyncHelper.CurrentEpoch;

            Assert.Throws<InvalidOperationException>(() => SourcePatchModePolicy.SetMutationIntent(false));

            Assert.AreEqual(state, SourcePatchHost.CurrentState);
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch);
            Assert.IsFalse(SourcePatchReceiptStore.TryRead(out _));
        }

        // ── ROI Fix 2b: enable=false from Recovery is the one legal exit
        // edge (Recovery -> Disabling), reused by the same causal-reload
        // path OnReady -> Disabling already uses. ──────────────────────────

        [Test]
        public void EnableFalse_FromRecovery_WritesReceiptTransitionsDisablingAndSyncsExactlyOnce()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;
            var epochBefore = SyncHelper.CurrentEpoch;

            var result = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("requested", result);
            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState);
            Assert.AreEqual(epochBefore + 1, SyncHelper.CurrentEpoch, "exactly one sync");
            Assert.IsTrue(SourcePatchReceiptStore.TryRead(out var receipt));
            Assert.AreEqual(epochBefore + 1, receipt.ExpectedEpochAfter);
        }

        private sealed class FakeLease : IDisposable
        {
            public int DisposeCount;
            public void Dispose() => DisposeCount++;
        }

        private sealed class FakeLeasePort : IAutoRefreshLeasePort
        {
            public FakeLease LastLease;

            public IDisposable AcquireLease()
            {
                LastLease = new FakeLease();
                return LastLease;
            }
        }

        [Test]
        public void EnableFalse_FromRecovery_ReleasesHeldAutoRefreshLease()
        {
            // Behavioral core of ROI Fix 2b: drive a real coordinator into
            // Recovery via an Uncertain-outcome apply (same shape as
            // SourcePatchCoordinatorTests.TryApply_ProviderAppliedButEvidenceNotConfirmed_EntersRecoveryWithoutReleasingLease),
            // then prove enable=false actually disposes the lease that
            // TryApply deliberately left held.
            var provider = RegisterFakeProvider();
            provider.Outcome = SourcePatchApplyOutcome.Applied;
            var leasePort = new FakeLeasePort();
            var coordinator = new SourcePatchCoordinator(
                new StaticContentBytesPort(System.Text.Encoding.UTF8.GetBytes("before")),
                provider, leasePort, new NeverConfirmedEvidencePort(),
                SourcePatchState.OnReady);
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            SourcePatchHost.Coordinator = coordinator;
            var applyResult = coordinator.TryApply(
                SourcePatchRequestForTests("Assets/Recovery.cs", "before", "after"));
            Assert.AreEqual(SourcePatchOperationResult.Uncertain, applyResult);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(0, leasePort.LastLease.DisposeCount, "lease deliberately held on Uncertain outcome");
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;

            SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount,
                "enable=false from Recovery must release the lease TryApply left held");
        }

        private sealed class NeverConfirmedEvidencePort : ICompileEvidencePort
        {
            public bool ConfirmApplied(SourcePatchRequest request) => false;
        }

        private static SourcePatchRequest SourcePatchRequestForTests(string path, string before, string after)
        {
            SourcePatchRequest.TryCreate(
                path,
                System.Text.Encoding.UTF8.GetBytes(before),
                System.Text.Encoding.UTF8.GetBytes(after),
                out var request);
            return request;
        }

        [Test]
        public void EnableFalse_FromRecovery_WithNoLiveCoordinator_DoesNotThrow()
        {
            // Domain-start Recovery shape: SourcePatchHost.Coordinator is
            // null (nothing survives a domain reload into a fresh domain).
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;
            Assert.IsNull(SourcePatchHost.Coordinator);

            string result = null;
            Assert.DoesNotThrow(() => result = SourcePatchModePolicy.SetMutationIntent(false));

            Assert.AreEqual("requested", result);
            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState);
        }

        [Test]
        public void EnableFalse_RetryWhileAlreadyDisabling_NoRedispatch()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            SourcePatchModePolicy.SetMutationIntent(false);
            SourcePatchReceiptStore.TryRead(out var firstReceipt);
            var epochAfterFirst = SyncHelper.CurrentEpoch;

            var retryResult = SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual("requested", retryResult);
            Assert.AreEqual(epochAfterFirst, SyncHelper.CurrentEpoch, "no second sync on retry");
            SourcePatchReceiptStore.TryRead(out var secondReceipt);
            Assert.AreEqual(firstReceipt.OpId, secondReceipt.OpId, "no second receipt on retry");
        }

        // ── clarification 1: typed bounded provider stop = zero further Apply calls ──

        [Test]
        public void EnableFalse_FromOnReady_ProviderReceivesZeroApplyCalls_EvenWhenWriteRacesIn()
        {
            var provider = RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            SourcePatchHost.Coordinator = new SourcePatchCoordinator(
                new NoopBytesPort(), provider, new NoopLeasePort(), new AlwaysConfirmedEvidencePort(),
                SourcePatchState.OnReady);

            SourcePatchModePolicy.SetMutationIntent(false);

            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState);
            Assert.Throws<InvalidOperationException>(
                () => SourcePatchHost.WriteText("{\"path\":\"Assets/Never.cs\",\"content\":\"x\"}"),
                "a source_patch_write racing in during Disabling must reject pre-effect");
            Assert.AreEqual(0, provider.ApplyCount,
                "typed bounded provider stop: the coordinator never routes another Apply once state leaves OnReady");
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

        // ── coordinator-internal transitions must mirror onto the host facade ──

        private const string TempFolder = "Assets/TestsTemp/SourcePatchModePolicy";

        [Test]
        public void WriteText_UncertainApplyOutcome_MirrorsRecoveryOntoHostCurrentState()
        {
            // Deliberately a fake, no-op bytes port even though this path ends
            // in .cs: a real AssetDatabase.ImportAsset on a genuine .cs file
            // would schedule an actual Unity recompile mid-suite (see
            // SourcePatchLegacyCsWriteExplicitTests.cs's own header warning).
            // The mirroring behavior under test lives entirely in
            // TryApplyWrite's handling of the coordinator's return value/
            // CurrentState — it does not depend on which bytes port is used.
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/uncertain.cs";
            AssetHelper.EnsureDirectory(path);
            var beforeContent = System.Text.Encoding.UTF8.GetBytes("before");
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), beforeContent);
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Uncertain };
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            SourcePatchHost.Coordinator = new SourcePatchCoordinator(
                new StaticContentBytesPort(beforeContent), provider, new NoopLeasePort(),
                new AlwaysConfirmedEvidencePort(), SourcePatchState.OnReady);

            Assert.Throws<InvalidOperationException>(
                () => SourcePatchHost.WriteText($"{{\"path\":\"{path}\",\"content\":\"after\"}}"));

            Assert.AreEqual(SourcePatchState.Recovery, SourcePatchHost.CurrentState,
                "mcp_status must never report a stale OnReady after an Uncertain apply outcome");
        }

        private sealed class StaticContentBytesPort : ISourcePatchBytesPort
        {
            private readonly byte[] _content;
            public StaticContentBytesPort(byte[] content) { _content = content; }
            public byte[] Read(string assetPath) => _content;
            public void Write(string assetPath, byte[] content) { }
        }

        // ── clarification 3: an explicit setter assignment wins over lazy reconcile ──

        [Test]
        public void ExplicitSetterAssignment_IsNeverOverwrittenByLaterRead()
        {
            RegisterFakeProvider();
            // A receipt that would resolve to Recovery if lazy reconciliation
            // ever ran against it (wrong pid/project/epoch on purpose).
            SourcePatchReceiptStore.Write(new SourcePatchDisableReceipt("poison", -1, "/nowhere", -999));

            SourcePatchHost.CurrentState = SourcePatchState.Off;

            Assert.AreEqual(SourcePatchState.Off, SourcePatchHost.CurrentState,
                "the setter's own assignment must stick; lazy reconciliation must never re-fire and overwrite it");
            Assert.AreEqual(SourcePatchState.Off, SourcePatchHost.CurrentState,
                "a second read must also return the explicitly-set value, not recompute");
        }
    }
}
