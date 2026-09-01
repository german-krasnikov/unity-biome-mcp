using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchCoordinatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

        private sealed class FakeBytesPort : ISourcePatchBytesPort
        {
            public byte[] Content;
            public int WriteCount;
            public int ReadCount;
            public List<string> Log;
            // Optional per-call override, keyed by 1-based read invocation index,
            // used to simulate external interference between two Read calls.
            public Func<int, byte[]> ReadOverride;
            public Exception ThrowOnRead;
            public Exception ThrowOnWrite;

            public byte[] Read(string assetPath)
            {
                Log?.Add("Read");
                ReadCount++;
                if (ThrowOnRead != null) throw ThrowOnRead;
                return ReadOverride != null ? ReadOverride(ReadCount) : Content;
            }

            public void Write(string assetPath, byte[] content)
            {
                Log?.Add("Write");
                WriteCount++;
                if (ThrowOnWrite != null) throw ThrowOnWrite;
                Content = content;
            }
        }

        private sealed class FakeProvider : ISourcePatchProvider
        {
            public SourcePatchApplyOutcome Outcome;
            public int ApplyCount;
            public List<string> Log;
            public Exception ThrowOnApply;

            public SourcePatchApplyOutcome Apply(SourcePatchRequest request)
            {
                Log?.Add("Apply");
                ApplyCount++;
                if (ThrowOnApply != null) throw ThrowOnApply;
                return Outcome;
            }
        }

        private sealed class FakeLease : IDisposable
        {
            public int DisposeCount;
            public void Dispose() => DisposeCount++;
        }

        private sealed class FakeLeasePort : IAutoRefreshLeasePort
        {
            public FakeLease LastLease;
            public int AcquireCount;
            public List<string> Log;

            public IDisposable AcquireLease()
            {
                Log?.Add("AcquireLease");
                AcquireCount++;
                LastLease = new FakeLease();
                return LastLease;
            }
        }

        private sealed class FakeEvidencePort : ICompileEvidencePort
        {
            public bool Confirmed;
            public bool ConfirmApplied(SourcePatchRequest request) => Confirmed;
        }

        private static SourcePatchRequest MakeRequest(string before = "before", string after = "after")
        {
            SourcePatchRequest.TryCreate("Assets/Foo.cs", Bytes(before), Bytes(after), out var request);
            return request;
        }

        [Test]
        public void TryApply_NotInOnReadyState_RejectsWithZeroEffect()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.Off);

            var result = coordinator.TryApply(MakeRequest());

            Assert.AreEqual(SourcePatchOperationResult.RejectedInvalidState, result);
            Assert.AreEqual(0, bytesPort.WriteCount);
            Assert.AreEqual(0, provider.ApplyCount);
            Assert.AreEqual(SourcePatchState.Off, coordinator.CurrentState);
        }

        [Test]
        public void TryApply_CasDriftBeforeWrite_EntersRecoveryAndNeverWrites()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("something-else") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var result = coordinator.TryApply(MakeRequest());

            Assert.AreEqual(SourcePatchOperationResult.Drift, result);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(0, bytesPort.WriteCount);
        }

        [Test]
        public void TryApply_ProviderApplied_TransitionsToOnReadyAndReleasesLease()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var result = coordinator.TryApply(MakeRequest());

            Assert.AreEqual(SourcePatchOperationResult.Applied, result);
            Assert.AreEqual(SourcePatchState.OnReady, coordinator.CurrentState);
            Assert.AreEqual(1, provider.ApplyCount);
            Assert.AreEqual(1, bytesPort.WriteCount);
            CollectionAssert.AreEqual(Bytes("after"), bytesPort.Content);
            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_ProviderAppliedButEvidenceNotConfirmed_EntersRecoveryWithoutReleasingLease()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = false };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var result = coordinator.TryApply(MakeRequest());

            Assert.AreEqual(SourcePatchOperationResult.Uncertain, result);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(1, provider.ApplyCount);
            Assert.AreEqual(1, bytesPort.WriteCount);
            Assert.AreEqual(0, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_ProviderRejectedCleanly_RollsBackAndReturnsToOnReady()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Rejected };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var result = coordinator.TryApply(MakeRequest());

            Assert.AreEqual(SourcePatchOperationResult.RolledBack, result);
            Assert.AreEqual(SourcePatchState.OnReady, coordinator.CurrentState);
            CollectionAssert.AreEqual(Bytes("before"), bytesPort.Content);
            Assert.AreEqual(2, bytesPort.WriteCount); // initial write + rollback write
            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_ProviderRejectedButDriftDetectedBeforeRollback_EntersRecoveryWithoutRollbackWrite()
        {
            var before = Bytes("before");
            var bytesPort = new FakeBytesPort
            {
                Content = before,
                // 1st Read = CAS-before-write check (must match "before" to proceed).
                // 2nd Read = CAS-before-rollback check; simulate external interference.
                ReadOverride = callIndex => callIndex == 1 ? before : Bytes("externally-changed"),
            };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Rejected };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var result = coordinator.TryApply(MakeRequest("before", "after"));

            Assert.AreEqual(SourcePatchOperationResult.Drift, result);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(1, bytesPort.WriteCount); // only the initial write; no rollback write
            Assert.AreEqual(0, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_ProviderUncertain_EntersRecoveryWithoutRollbackAndWithoutRetry()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Uncertain };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var result = coordinator.TryApply(MakeRequest());

            Assert.AreEqual(SourcePatchOperationResult.Uncertain, result);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(1, bytesPort.WriteCount); // only the initial write; no rollback attempt
            Assert.AreEqual(1, provider.ApplyCount); // exactly once, never retried
            Assert.AreEqual(0, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_SuccessPath_PortsCalledInExactOrder()
        {
            var log = new List<string>();
            var bytesPort = new FakeBytesPort { Content = Bytes("before"), Log = log };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied, Log = log };
            var leasePort = new FakeLeasePort { Log = log };
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            coordinator.TryApply(MakeRequest());

            CollectionAssert.AreEqual(new[] { "Read", "AcquireLease", "Write", "Apply" }, log);
        }

        [Test]
        public void TryApply_BytesPortReadThrows_EntersRecoveryWithLeaseNeverAcquiredAndRethrows()
        {
            // Choice: a port exception is a caller/port bug, not a business
            // outcome (unlike Rejected/Uncertain), so it is never swallowed
            // into a SourcePatchOperationResult — it propagates after the
            // coordinator's own state/lease bookkeeping runs.
            var boom = new InvalidOperationException("boom");
            var bytesPort = new FakeBytesPort { Content = Bytes("before"), ThrowOnRead = boom };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var thrown = Assert.Throws<InvalidOperationException>(() => coordinator.TryApply(MakeRequest()));

            Assert.AreSame(boom, thrown);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(0, leasePort.AcquireCount);
        }

        [Test]
        public void TryApply_BytesPortWriteThrows_EntersRecoveryWithLeaseHeld()
        {
            var boom = new InvalidOperationException("boom");
            var bytesPort = new FakeBytesPort { Content = Bytes("before"), ThrowOnWrite = boom };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var thrown = Assert.Throws<InvalidOperationException>(() => coordinator.TryApply(MakeRequest()));

            Assert.AreSame(boom, thrown);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(1, leasePort.AcquireCount);
            Assert.AreEqual(0, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_ProviderApplyThrows_EntersRecoveryWithLeaseHeldAndAppliedExactlyOnce()
        {
            var boom = new InvalidOperationException("boom");
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { ThrowOnApply = boom };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            var thrown = Assert.Throws<InvalidOperationException>(() => coordinator.TryApply(MakeRequest()));

            Assert.AreSame(boom, thrown);
            Assert.AreEqual(SourcePatchState.Recovery, coordinator.CurrentState);
            Assert.AreEqual(1, provider.ApplyCount);
            Assert.AreEqual(0, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void TryApply_NullRequest_Throws()
        {
            var bytesPort = new FakeBytesPort();
            var provider = new FakeProvider();
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort();
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);

            Assert.Throws<ArgumentNullException>(() => coordinator.TryApply(null));
        }

        // ── ReleaseHeldLease (ROI Fix 2b): closes the lease-leak documented
        // at TryApply's Uncertain/Drift-after-write/exception comments —
        // those paths deliberately never dispose inline, leaving the lease
        // for a future host-level Recovery reconciliation. Each test below
        // reuses an existing Recovery-entering test's exact setup. ──

        [Test]
        public void ReleaseHeldLease_AfterUncertainEvidenceOutcome_DisposesExactlyOnce()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = false };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            coordinator.TryApply(MakeRequest());

            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void ReleaseHeldLease_AfterDriftDetectedDuringRollback_DisposesExactlyOnce()
        {
            var before = Bytes("before");
            var bytesPort = new FakeBytesPort
            {
                Content = before,
                ReadOverride = callIndex => callIndex == 1 ? before : Bytes("externally-changed"),
            };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Rejected };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            coordinator.TryApply(MakeRequest("before", "after"));

            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void ReleaseHeldLease_AfterProviderUncertainOutcome_DisposesExactlyOnce()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Uncertain };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            coordinator.TryApply(MakeRequest());

            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void ReleaseHeldLease_AfterWriteThrows_DisposesExactlyOnce()
        {
            var boom = new InvalidOperationException("boom");
            var bytesPort = new FakeBytesPort { Content = Bytes("before"), ThrowOnWrite = boom };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            try { coordinator.TryApply(MakeRequest()); } catch (InvalidOperationException) { /* expected */ }

            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void ReleaseHeldLease_AfterProviderApplyThrows_DisposesExactlyOnce()
        {
            var boom = new InvalidOperationException("boom");
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { ThrowOnApply = boom };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            try { coordinator.TryApply(MakeRequest()); } catch (InvalidOperationException) { /* expected */ }

            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount);
        }

        [Test]
        public void ReleaseHeldLease_AfterCasDriftBeforeWrite_IsNoOpNoLeaseWasAcquired()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("something-else") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            coordinator.TryApply(MakeRequest());
            Assert.AreEqual(0, leasePort.AcquireCount, "CAS drift before write never acquires a lease");

            Assert.DoesNotThrow(() => coordinator.ReleaseHeldLease());

            Assert.AreEqual(0, leasePort.AcquireCount);
            Assert.IsNull(leasePort.LastLease, "no lease was ever acquired, so none can have been disposed");
        }

        [Test]
        public void ReleaseHeldLease_CalledTwice_DisposesOnlyOnce()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = false };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            coordinator.TryApply(MakeRequest());

            coordinator.ReleaseHeldLease();
            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount,
                "field-null-before-dispose must itself be idempotent, not merely rely on Lease.Dispose()'s own guard");
        }

        [Test]
        public void ReleaseHeldLease_AfterSuccessfulApply_IsNoOpAlreadyDisposedInline()
        {
            var bytesPort = new FakeBytesPort { Content = Bytes("before") };
            var provider = new FakeProvider { Outcome = SourcePatchApplyOutcome.Applied };
            var leasePort = new FakeLeasePort();
            var evidencePort = new FakeEvidencePort { Confirmed = true };
            var coordinator = new SourcePatchCoordinator(bytesPort, provider, leasePort, evidencePort, SourcePatchState.OnReady);
            coordinator.TryApply(MakeRequest());
            Assert.AreEqual(1, leasePort.LastLease.DisposeCount, "success path disposes inline");

            coordinator.ReleaseHeldLease();

            Assert.AreEqual(1, leasePort.LastLease.DisposeCount,
                "no double-dispose of a lease already released inline by the success path");
        }
    }
}
