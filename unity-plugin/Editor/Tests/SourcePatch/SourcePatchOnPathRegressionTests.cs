// P0-80 Cycle A regression, extracted from SourcePatchMutationModeTests.cs
// (that file crossed 300 lines): a successful FSR provider apply must not be
// second-guessed by a self-inflicted evidence violation. Same SetUp/helper
// shape as its sibling — SyncHelper.Ops is doubled with MockSyncOps
// (SyncHelperTests.cs) so TriggerSync never touches the real AssetDatabase.
// See Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchOnPathRegressionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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

        private sealed class NoopLeasePort : IAutoRefreshLeasePort
        {
            public IDisposable AcquireLease() => new NoopLease();
            private sealed class NoopLease : IDisposable { public void Dispose() { } }
        }

        private const string TempFolder = "Assets/TestsTemp/SourcePatchOnPathRegression";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchHost.ResetForTests();
            SourcePatchProviderSlot.ResetForTests();
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
        }

        // ── P0-80 Cycle A regression: a successful provider must not be
        // second-guessed by a self-inflicted evidence violation ──────────────

        [Test]
        public void WriteText_RealPortsAndSuccessfulProvider_AppliesWithoutRecovery()
        {
            // Reproduces the exact live failure: source_patch_write stably
            // returned "outcome is uncertain; entering Recovery" even though
            // the FSR provider itself succeeded. This is the one test in this
            // file that uses the REAL bytes port and REAL evidence port
            // (FakeProvider stands in for FSR, whose own external compile is
            // confirmed to succeed independently by the P0-80 diagnostic
            // probe) — it is only safe because the fixed bytes port never
            // calls AssetDatabase.ImportAsset, so writing this real .cs file
            // never reaches Unity's own compile pipeline at all.
            //
            // A sibling P0-30 legacy test in the same filtered batch (e.g.
            // SourcePatchCsWriteGateTests) can legitimately be mid-compile
            // from its own real, unrelated OFF-path .cs write at the same
            // time; the real SyncHelperCompileEvidencePort has no way to
            // attribute that whole-project EditorApplication.isCompiling flag
            // to someone else, so it correctly (if, here, coincidentally)
            // enters Recovery too. Waiting that out inside this test was tried
            // and is actively dangerous — an actual Domain Reload firing while
            // subscribed to EditorApplication.update corrupted the whole UTF
            // run's terminal evidence. Skipping when that ambient precondition
            // isn't met keeps this test meaningful (it still fully verifies
            // the fix whenever the environment is quiet, as proven in
            // isolation) without ever guessing at, or waiting through,
            // Unity's real compile timing.
            if (EditorApplication.isCompiling)
                Assert.Ignore("ambient Unity script compilation in progress — likely a co-batched sibling's " +
                    "real .cs write (e.g. SourcePatchCsWriteGateTests), not this port's own action");

            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/real-ports-applied.cs";
            AssetHelper.EnsureDirectory(path);
            var beforeContent = System.Text.Encoding.UTF8.GetBytes("before");
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), beforeContent);
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            SourcePatchHost.Coordinator = new SourcePatchCoordinator(
                new UnitySourcePatchBytesPort(), provider, new NoopLeasePort(),
                new SyncHelperCompileEvidencePort(), SourcePatchState.OnReady);

            var result = SourcePatchHost.WriteText($"{{\"path\":\"{path}\",\"content\":\"after\"}}");

            StringAssert.StartsWith("ok:write", result);
            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState,
                "a genuinely successful apply must stay OnReady, never fall into Recovery");
        }
    }
}
