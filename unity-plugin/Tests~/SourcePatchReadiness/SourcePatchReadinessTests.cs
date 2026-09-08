using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.SourcePatchReadiness.Tests
{
    // Real coordinator; ports substitute external effects only.
    [TestFixture]
    internal sealed class SourcePatchReadinessTests
    {
        private sealed class Ports : ISourcePatchBytesPort, ISourcePatchProvider,
            IAutoRefreshLeasePort, ICompileEvidencePort, IDisposable
        {
            public byte[] Content = new byte[] { 1 };
            public SourcePatchApplyOutcome Outcome = SourcePatchApplyOutcome.Applied;
            public bool ThrowOnApply, ThrowOnRelease;
            public int ActiveLeases;
            public byte[] Read(string path) => Content;
            public void Write(string path, byte[] bytes) => Content = bytes;
            public SourcePatchApplyOutcome Apply(SourcePatchRequest request)
            {
                if (ThrowOnApply) throw new InvalidOperationException("provider interrupted");
                return Outcome;
            }
            public IDisposable AcquireLease() { ActiveLeases++; return this; }
            public void Dispose()
            {
                if (ThrowOnRelease) throw new InvalidOperationException("release failed");
                ActiveLeases--;
            }
            public bool ConfirmApplied(SourcePatchRequest request) => true;
        }

        private static SourcePatchCoordinator Coordinator(Ports ports) =>
            new SourcePatchCoordinator(ports, ports, ports, ports, SourcePatchState.OnReady);

        private static SourcePatchRequest Request(byte before = 1, byte after = 2)
        {
            SourcePatchRequest.TryCreate("Assets/Owned.cs", new[] { before }, new[] { after }, out var request);
            return request;
        }

        [Test]
        public void SuccessfulPatch_RemainsActiveAfterActualLeaseRelease()
        {
            var ports = new Ports(); var coordinator = Coordinator(ports);
            Assert.That(coordinator.TryApply(Request()), Is.EqualTo(SourcePatchOperationResult.Applied));
            Assert.That(ports.ActiveLeases, Is.Zero);
            Assert.That(coordinator.HasHeldLease, Is.False);
            Assert.That(coordinator.PatchesMayBeActive, Is.True,
                "released AutoRefresh lease does not undo the applied method detour");
            Assert.That(SourcePatchStateMachine.ReloadBlockReason(coordinator.CurrentState,
                coordinator.PatchesMayBeActive, coordinator.HasHeldLease, false, false),
                Does.Contain("explicit_disable_required"));
        }

        [Test]
        public void RejectedFirstPatch_HasNoPatchAndRestoresOriginalBytes()
        {
            var ports = new Ports { Outcome = SourcePatchApplyOutcome.Rejected };
            var coordinator = Coordinator(ports);
            Assert.That(coordinator.TryApply(Request()), Is.EqualTo(SourcePatchOperationResult.RolledBack));
            Assert.That(ports.Content, Is.EqualTo(new byte[] { 1 }));
            Assert.That(coordinator.PatchesMayBeActive, Is.False);
            Assert.That(coordinator.HasHeldLease, Is.False);
        }

        [Test]
        public void LaterRejection_DoesNotEraseEarlierAppliedPatch()
        {
            var ports = new Ports(); var coordinator = Coordinator(ports);
            coordinator.TryApply(Request()); ports.Outcome = SourcePatchApplyOutcome.Rejected;
            coordinator.TryApply(Request(2, 3));
            Assert.That(ports.Content, Is.EqualTo(new byte[] { 2 }));
            Assert.That(coordinator.PatchesMayBeActive, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UncertainProviderOutcome_RetainsBothPossiblePatchAndActualLease(bool throws)
        {
            var ports = new Ports { Outcome = SourcePatchApplyOutcome.Uncertain, ThrowOnApply = throws };
            var coordinator = Coordinator(ports);
            if (throws) Assert.Throws<InvalidOperationException>(() => coordinator.TryApply(Request()));
            else coordinator.TryApply(Request());
            Assert.That(coordinator.CurrentState, Is.EqualTo(SourcePatchState.Recovery));
            Assert.That(coordinator.PatchesMayBeActive, Is.True);
            Assert.That(coordinator.HasHeldLease, Is.True);
            Assert.That(ports.ActiveLeases, Is.EqualTo(1));
            coordinator.ReleaseHeldLease();
            Assert.That(coordinator.HasHeldLease, Is.False);
            Assert.That(coordinator.PatchesMayBeActive, Is.True);
        }

        [Test]
        public void FailedRelease_RemainsObservableAndCanReleaseOwnedLeaseLater()
        {
            var ports = new Ports { Outcome = SourcePatchApplyOutcome.Uncertain };
            var coordinator = Coordinator(ports); coordinator.TryApply(Request());
            ports.ThrowOnRelease = true;
            Assert.Throws<InvalidOperationException>(() => coordinator.ReleaseHeldLease());
            Assert.That(coordinator.HasHeldLease, Is.True);
            ports.ThrowOnRelease = false; coordinator.ReleaseHeldLease();
            Assert.That(ports.ActiveLeases, Is.Zero);
            Assert.That(coordinator.HasHeldLease, Is.False);
        }

        [TestCase(SourcePatchState.Off, false, false, null)]
        [TestCase(SourcePatchState.OnReady, false, false, "source_patch_OnReady_explicit_disable_required")]
        [TestCase(SourcePatchState.Disabling, true, true, null)]
        [TestCase(SourcePatchState.Disabling, true, false, "source_patch_disable_not_owned")]
        public void ReloadAdmission_OnlyOffOrOwnedExplicitDisableIsAllowed(
            SourcePatchState state, bool explicitDisable, bool matchingReceipt, string expected)
        {
            Assert.That(SourcePatchStateMachine.ReloadBlockReason(
                state, false, false, explicitDisable, matchingReceipt), Is.EqualTo(expected));
        }
    }
}
