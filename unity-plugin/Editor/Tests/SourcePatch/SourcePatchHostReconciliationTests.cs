// P0-70: pure domain-start reconciliation (§3.3). "Normal domain start
// without an armed matching receipt is OFF. Stale, wrong-project/session or
// ambiguous receipt is Recovery, never optimistic OFF." No SessionState/Unity
// I/O — every input is an explicit value, so this exercises the exact policy
// table without a real Domain Reload. See
// Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchHostReconciliationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const int Pid = 100;
        private const string ProjectPath = "/Users/x/proj";
        private const int EpochAfter = 5;

        private static SourcePatchDisableReceipt Matching() =>
            new SourcePatchDisableReceipt("op-1", Pid, ProjectPath, EpochAfter);

        [Test]
        public void NoReceipt_IsOff()
        {
            var result = SourcePatchHost.ReconcileDomainStart(null, Pid, ProjectPath, EpochAfter);
            Assert.AreEqual(SourcePatchState.Off, result);
        }

        [Test]
        public void ExactMatch_IsOff()
        {
            var result = SourcePatchHost.ReconcileDomainStart(Matching(), Pid, ProjectPath, EpochAfter);
            Assert.AreEqual(SourcePatchState.Off, result);
        }

        [Test]
        public void WrongPid_IsRecovery()
        {
            var result = SourcePatchHost.ReconcileDomainStart(Matching(), Pid + 1, ProjectPath, EpochAfter);
            Assert.AreEqual(SourcePatchState.Recovery, result);
        }

        [Test]
        public void WrongProject_IsRecovery()
        {
            var result = SourcePatchHost.ReconcileDomainStart(Matching(), Pid, "/Users/x/other", EpochAfter);
            Assert.AreEqual(SourcePatchState.Recovery, result);
        }

        [Test]
        public void EpochAdvancedByTwo_IsRecovery()
        {
            // N+2: another sync happened besides ours — never trust it as our OFF.
            var result = SourcePatchHost.ReconcileDomainStart(Matching(), Pid, ProjectPath, EpochAfter + 1);
            Assert.AreEqual(SourcePatchState.Recovery, result);
        }

        [Test]
        public void EpochUnchanged_IsRecovery()
        {
            // N+0: the triggered sync never actually advanced the domain.
            var result = SourcePatchHost.ReconcileDomainStart(Matching(), Pid, ProjectPath, EpochAfter - 1);
            Assert.AreEqual(SourcePatchState.Recovery, result);
        }
    }
}
