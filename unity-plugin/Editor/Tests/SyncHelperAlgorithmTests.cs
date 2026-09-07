// N2 Task 2: SyncHelper facade substitution. IReloadAlgorithm is the ONE binding
// point beneath the unchanged public consumers (TriggerSync/GetSyncStatus, and the
// "sync"/"sync_status" command route). Algorithm B changes what Begin/Observe
// report; the default algorithm (A) remains the legacy Core implementation with
// zero behavior change. See Plans/N2-reload-sourcepatch-contract.md V4.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SyncHelperAlgorithmTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeAlgorithm : SyncHelper.IReloadAlgorithm
        {
            public string BeginResult = "algo-b-begin";
            public string ObserveResult = "algo-b-observe";
            public int BeginCalls;
            public int ObserveCalls;

            public string Begin(bool resolve, bool explicitOwnedDisable)
            {
                BeginCalls++;
                return BeginResult;
            }

            public string Observe()
            {
                ObserveCalls++;
                return ObserveResult;
            }
        }

        [SetUp]
        public void SetUp() => SyncHelper.OverrideOpsForTest(new MockSyncOps());

        [Test]
        public void FakeAlgorithm_ChangesBothBeginAndObserve()
        {
            var fake = new FakeAlgorithm();
            SyncHelper.OverrideAlgorithmForTest(fake);

            Assert.AreEqual("algo-b-begin", SyncHelper.TriggerSync(false));
            Assert.AreEqual("algo-b-observe", SyncHelper.GetSyncStatus());
            Assert.AreEqual(1, fake.BeginCalls);
            Assert.AreEqual(1, fake.ObserveCalls);
        }

        [Test]
        public void Algorithm_DefaultsToLegacyCore_UnchangedEpochAck()
        {
            // No override installed anywhere in this test: the default algorithm
            // must still be today's Core implementation — zero behavior change
            // on the default path (algorithm A untouched).
            var begin = SyncHelper.TriggerSync(false);
            StringAssert.StartsWith("sync_ack|epoch=", begin);
            StringAssert.Contains("state=", SyncHelper.GetSyncStatus());
        }

        // Parametrised over kinds of Observe() evidence a real algorithm B could
        // report. The facade must pass each through unmodified — never reinterpret
        // noop/timeout/failed/reordered evidence as ready, and never suppress a
        // genuinely ready verdict either. "Never false-Ready" is proven by exact
        // pass-through: the facade contains no readiness logic of its own.
        [TestCase("noop|will_compile=false", false)]
        [TestCase("timeout|epoch=1|elapsed=99.0", false)]
        [TestCase("failed|err=compile error CS0000", false)]
        [TestCase("reordered|epoch=1|expected=2", false)]
        [TestCase("epoch=1|state=ready|stamp=abc123", true)]
        public void FakeAlgorithmObserveEvidence_PassesThroughUnmodified_NeverForcedToReady(
            string observeEvidence, bool evidenceItselfIsReady)
        {
            var fake = new FakeAlgorithm { ObserveResult = observeEvidence };
            SyncHelper.OverrideAlgorithmForTest(fake);

            var verdict = SyncHelper.GetSyncStatus();

            Assert.AreEqual(observeEvidence, verdict,
                "the facade must pass Observe() through unmodified — no reinterpretation, no force-green");
            var looksReady = verdict.Contains("state=ready");
            Assert.AreEqual(evidenceItselfIsReady, looksReady,
                "readiness is decided by the algorithm's own evidence, never invented by the facade");
        }

        [Test]
        public void CallingCoreDirectly_BypassesInjectedAlgorithm_ProvesFacadeIsTheOnlySubstitutionPoint()
        {
            // Negative control: installing a fake algorithm B and then calling
            // Core directly (the escape hatch DefaultAlgorithm wraps) must NOT see
            // the fake's evidence. This proves ordinary consumers only observe
            // algorithm substitution because they go through the facade — Core
            // itself has no knowledge of Algorithm.
            var fake = new FakeAlgorithm();
            SyncHelper.OverrideAlgorithmForTest(fake);

            var coreResult = SyncHelper.TriggerSyncCore(false, false);

            StringAssert.StartsWith("sync_ack|epoch=", coreResult);
            Assert.AreEqual(0, fake.BeginCalls, "Core must not route through the injected algorithm");
        }

        [Test]
        public void CommandRoute_Sync_And_SyncStatus_ObserveBoundAlgorithm()
        {
            // "sync"/"sync_status" are unchanged consumers (CommandRouter.Registration.cs)
            // — they call SyncHelper.TriggerSync/GetSyncStatus with no knowledge of
            // Algorithm, yet must observe the bound fake through the same facade.
            var fake = new FakeAlgorithm();
            SyncHelper.OverrideAlgorithmForTest(fake);

            Assert.AreEqual("algo-b-begin", CommandRegistry.Execute("sync", "{}"));
            Assert.AreEqual("algo-b-observe", CommandRegistry.Execute("sync_status", "{}"));
        }

        [Test]
        public void OverrideAlgorithmForTest_RejectsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => SyncHelper.OverrideAlgorithmForTest(null));
        }
    }
}
