using System;

namespace UnityMCP.Editor.SourcePatch
{
    internal interface ISourcePatchBytesPort
    {
        byte[] Read(string assetPath);
        void Write(string assetPath, byte[] content);
    }

    internal interface IAutoRefreshLeasePort
    {
        IDisposable AcquireLease();
    }

    internal interface ICompileEvidencePort
    {
        bool ConfirmApplied(SourcePatchRequest request);
    }

    /// <summary>
    /// Coordinator-level result of one <see cref="SourcePatchCoordinator.TryApply"/>
    /// call. Deliberately a separate vocabulary from SourcePatchApplyOutcome
    /// (see that type's doc comment) — richer, coordinator-only distinctions
    /// the provider contract has no business exposing.
    /// </summary>
    internal enum SourcePatchOperationResult
    {
        RejectedInvalidState,
        Applied,
        RolledBack,
        Drift,
        Uncertain,
    }

    /// <summary>
    /// Owns source bytes, CAS/readback, the AutoRefresh lease, state and
    /// recovery for exactly one in-flight source transaction (§3.1). All
    /// Unity-API-touching behavior is injected via ports so this type stays
    /// engine/Unity-neutral and fully unit-testable with fakes.
    /// </summary>
    internal sealed class SourcePatchCoordinator
    {
        private readonly ISourcePatchBytesPort _bytes;
        private readonly ISourcePatchProvider _provider;
        private readonly IAutoRefreshLeasePort _lease;
        private readonly ICompileEvidencePort _evidence;
        private readonly SourcePatchStateMachine _state;

        // Non-null only while Recovery holds an unreleased AutoRefresh
        // lease (ROI Fix 2b) — set at every Recovery-entry point below that
        // deliberately never disposes inline (see each site's own comment),
        // released exactly once by SourcePatchModePolicy.RequestDisable's
        // Recovery branch via ReleaseHeldLease(), before the causal Domain
        // Reload it triggers.
        private IDisposable _heldLease;

        public SourcePatchCoordinator(
            ISourcePatchBytesPort bytes,
            ISourcePatchProvider provider,
            IAutoRefreshLeasePort lease,
            ICompileEvidencePort evidence,
            SourcePatchState initialState)
        {
            _bytes = bytes;
            _provider = provider;
            _lease = lease;
            _evidence = evidence;
            _state = new SourcePatchStateMachine(initialState);
        }

        public SourcePatchState CurrentState => _state.Current;

        public SourcePatchOperationResult TryApply(SourcePatchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_state.Current != SourcePatchState.OnReady)
            {
                return SourcePatchOperationResult.RejectedInvalidState;
            }

            // Taken once: each SourcePatchRequest getter clones on access, so
            // capturing locally avoids reallocating the same bytes on every
            // use below.
            var expectedBefore = request.ExpectedBeforeContent;
            var newContent = request.NewContent;

            _state.TryTransition(SourcePatchState.Busy);

            IDisposable lease = null;
            var writeAttempted = false;
            try
            {
                var current = _bytes.Read(request.AssetPath);
                if (!BytesEqual(current, expectedBefore))
                {
                    _state.TryTransition(SourcePatchState.Recovery);
                    return SourcePatchOperationResult.Drift;
                }

                lease = _lease.AcquireLease();
                writeAttempted = true;
                _bytes.Write(request.AssetPath, newContent);

                switch (_provider.Apply(request))
                {
                    case SourcePatchApplyOutcome.Applied:
                        if (_evidence.ConfirmApplied(request))
                        {
                            lease.Dispose();
                            _state.TryTransition(SourcePatchState.OnReady);
                            return SourcePatchOperationResult.Applied;
                        }
                        // Evidence not confirmed: leave the lease held for a
                        // future host-level Recovery reconciliation — never
                        // release it optimistically.
                        _heldLease = lease;
                        _state.TryTransition(SourcePatchState.Recovery);
                        return SourcePatchOperationResult.Uncertain;

                    case SourcePatchApplyOutcome.Rejected:
                        var afterWrite = _bytes.Read(request.AssetPath);
                        if (!BytesEqual(afterWrite, newContent))
                        {
                            // Something raced between our write and this
                            // readback: preserve external bytes, do not
                            // overwrite them with a rollback write.
                            _heldLease = lease;
                            _state.TryTransition(SourcePatchState.Recovery);
                            return SourcePatchOperationResult.Drift;
                        }
                        _bytes.Write(request.AssetPath, expectedBefore);
                        lease.Dispose();
                        _state.TryTransition(SourcePatchState.OnReady);
                        return SourcePatchOperationResult.RolledBack;

                    default: // Uncertain: never rolled back, never retried.
                        _heldLease = lease;
                        _state.TryTransition(SourcePatchState.Recovery);
                        return SourcePatchOperationResult.Uncertain;
                }
            }
            catch (Exception)
            {
                // A port threw instead of returning a result: never guess
                // whether the effect happened. Only a lease acquired before
                // any write attempt is safe to release (nothing external
                // could have changed yet); once Write has been attempted,
                // hold the lease exactly like the Uncertain outcome, for
                // host-level Recovery reconciliation. The exception itself is
                // a caller/port bug, not a business outcome (unlike
                // Rejected/Uncertain) — it is never swallowed into a
                // SourcePatchOperationResult, so it propagates after this
                // bookkeeping runs.
                if (lease != null && !writeAttempted)
                {
                    lease.Dispose();
                }
                else if (lease != null)
                {
                    _heldLease = lease;
                }
                _state.TryTransition(SourcePatchState.Recovery);
                throw;
            }
        }

        /// <summary>Releases an AutoRefresh lease this coordinator left held after
        /// entering Recovery (see TryApply's Uncertain/Drift-after-write/exception
        /// comments — those paths deliberately never dispose inline). No-op if no
        /// lease is currently held (e.g. Recovery entered via the pre-write CAS
        /// check, which never acquires a lease). Idempotent — safe to call more
        /// than once. Called exactly once, by SourcePatchModePolicy.RequestDisable's
        /// Recovery branch, before the causal Domain Reload it triggers.</summary>
        public void ReleaseHeldLease()
        {
            var lease = _heldLease;
            _heldLease = null;
            lease?.Dispose();
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
