using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// CleanupForRun's fast path (A28) is exercised via 3 injectable seams
    /// (RootIsValidFolderImpl/ReadLedgerImpl/SweepReservedRootImpl -- same
    /// shape as TestRunAssemblyFingerprint.HashFileImpl) rather than by
    /// deleting the real, shared "Assets/TestsTemp" reserved root: once any
    /// fixture creates that folder it persists for the rest of the Editor
    /// session, so a test asserting on its real absence would be flaky by
    /// construction.
    /// </summary>
    [TestFixture]
    internal sealed class TestRunAssetOwnershipTests : UnityMcpTestBase
    {
        [Test]
        public void CleanupForRun_NoTestsTempRootAndNoLedger_ReturnsEmptyReportWithoutFileIo()
        {
            var runId = "unowned-run-" + Guid.NewGuid().ToString("N");
            var readLedgerCalls = 0;
            var sweepCalls = 0;
            StubRootMissing(
                onReadLedger: _ => readLedgerCalls++,
                onSweep: _ => sweepCalls++);

            var report = TestRunAssetOwnership.CleanupForRun(runId, "");

            Assert.That(report.HasWarning, Is.False);
            Assert.That(report.QuarantinedLedgerPath, Is.Empty);
            Assert.That(readLedgerCalls, Is.Zero,
                "ReadLedger must not run on the fast path.");
            Assert.That(sweepCalls, Is.Zero,
                "SweepReservedRoot must not run on the fast path.");
        }

        [Test]
        public void CleanupForRun_UnsafeRunId_ThrowsEvenWhenRootAndLedgerAreAbsent()
        {
            StubRootMissing();

            Assert.Throws<ArgumentException>(() =>
                TestRunAssetOwnership.CleanupForRun("../escape-run", ""));
        }

        [Test]
        public void CleanupForRun_ArbitraryPreservePath_ThrowsEvenWhenRootAndLedgerAreAbsent()
        {
            var runId = "unowned-run-" + Guid.NewGuid().ToString("N");
            StubRootMissing();
            var arbitraryPreserve = TestRunAssetOwnership.Root + "/not-the-run-scene.unity";

            Assert.Throws<ArgumentException>(() =>
                TestRunAssetOwnership.CleanupForRun(runId, arbitraryPreserve));
        }

        // Restored via RegisterCleanup so a failed assertion (or an exception
        // from the method under test) can never leave the real production
        // seams pointed at a test spy for the rest of this run.
        private void StubRootMissing(
            Action<string> onReadLedger = null, Action<string> onSweep = null)
        {
            var originalRootCheck = TestRunAssetOwnership.RootIsValidFolderImpl;
            var originalReadLedger = TestRunAssetOwnership.ReadLedgerImpl;
            var originalSweep = TestRunAssetOwnership.SweepReservedRootImpl;
            TestRunAssetOwnership.RootIsValidFolderImpl = () => false;
            TestRunAssetOwnership.ReadLedgerImpl = id =>
            {
                onReadLedger?.Invoke(id);
                return originalReadLedger(id);
            };
            TestRunAssetOwnership.SweepReservedRootImpl = preserve =>
            {
                onSweep?.Invoke(preserve);
                originalSweep(preserve);
            };
            RegisterCleanup(() =>
            {
                TestRunAssetOwnership.RootIsValidFolderImpl = originalRootCheck;
                TestRunAssetOwnership.ReadLedgerImpl = originalReadLedger;
                TestRunAssetOwnership.SweepReservedRootImpl = originalSweep;
            });
        }
    }
}
