using System;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor
{
    /// <summary>
    /// The one main-assembly integration seam for `.cs` source writes (§3.1/§6
    /// P0-50/P0-70 in Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md).
    ///
    /// <see cref="GuardLegacyCsWrite"/> is called from
    /// <c>AssetDatabaseHelper.WriteText</c>, which both raw direct "asset"
    /// dispatch and batch dispatch reach through the identical
    /// <c>CommandRouter.ExecuteCommand</c> call — so this one call site
    /// authoritatively covers both surfaces; there is nothing batch-side left
    /// to duplicate.
    ///
    /// <see cref="CurrentState"/> (P0-70) is lazily reconciled from persisted
    /// evidence (<see cref="SourcePatchReceiptStore"/> + <see cref="SourcePatchProviderSlot"/>)
    /// the first time it is read after a Domain Reload, then cached — this
    /// sidesteps any [InitializeOnLoad] ordering race against the optional
    /// provider package's own registration hook, since real command dispatch
    /// always happens after all InitializeOnLoad hooks have run. Tests bypass
    /// reconciliation entirely by assigning the setter directly (it marks
    /// reconciliation "already done" so the lazy path never overwrites an
    /// explicitly-forced test state).
    /// </summary>
    internal static class SourcePatchHost
    {
        private static bool _reconciled;
        private static SourcePatchState _state = SourcePatchState.Unavailable;

        internal static SourcePatchState CurrentState
        {
            get
            {
                EnsureReconciled();
                return _state;
            }
            set
            {
                _reconciled = true;
                _state = value;
            }
        }

        /// <summary>The one live coordinator, armed exactly when transitioning
        /// Off -&gt; OnReady (<see cref="SourcePatchModePolicy"/>) and consulted by
        /// <see cref="WriteText"/> while OnReady. Null in every other state.</summary>
        internal static SourcePatchCoordinator Coordinator { get; set; }

        internal static void ResetForTests()
        {
            SourcePatchReceiptStore.ResetForTests();
            Coordinator = null;
            _reconciled = true;
            _state = SourcePatchState.Unavailable;
        }

        private static void EnsureReconciled()
        {
            if (_reconciled) return;
            _reconciled = true;
            _state = ComputeInitialState();
        }

        private static SourcePatchState ComputeInitialState()
        {
            if (!SourcePatchProviderSlot.TryGet(out _)) return SourcePatchState.Unavailable;

            if (!SourcePatchReceiptStore.TryRead(out var receipt)) return SourcePatchState.Off;

            var resolved = ReconcileDomainStart(
                receipt,
                System.Diagnostics.Process.GetCurrentProcess().Id,
                SourcePatchModePolicy.CurrentProjectPath(),
                SyncHelper.CurrentEpoch);

            if (resolved == SourcePatchState.Off) SourcePatchReceiptStore.Clear();
            return resolved; // Recovery: receipt intentionally left in place — no auto-repair.
        }

        /// <summary>
        /// Pure domain-start reconciliation (§3.3): "normal domain start without
        /// an armed matching receipt is OFF; stale, wrong-project/session or
        /// ambiguous receipt is Recovery, never optimistic OFF." No I/O — every
        /// input is an explicit value, so this is fully unit-testable without a
        /// real Domain Reload.
        /// </summary>
        internal static SourcePatchState ReconcileDomainStart(
            SourcePatchDisableReceipt receipt, int currentPid, string currentProjectPath, int currentEpoch)
        {
            if (receipt == null) return SourcePatchState.Off;
            var matches = receipt.Pid == currentPid
                && receipt.ProjectPath == currentProjectPath
                && receipt.ExpectedEpochAfter == currentEpoch;
            return matches ? SourcePatchState.Off : SourcePatchState.Recovery;
        }

        private static bool AllowsLegacyRoute =>
            CurrentState == SourcePatchState.Unavailable ||
            CurrentState == SourcePatchState.Off;

        private static bool IsCsPath(string assetPath) =>
            assetPath != null && assetPath.EndsWith(".cs", StringComparison.Ordinal);

        /// <summary>
        /// Called by AssetDatabaseHelper.WriteText before any file/import
        /// effect. Off/Unavailable: no-op, legacy owns `.cs` like any other
        /// asset. Anything else: reject — raw/batch `.cs` writes must go
        /// through source_patch_write instead.
        /// </summary>
        internal static void GuardLegacyCsWrite(string assetPath)
        {
            if (!IsCsPath(assetPath) || AllowsLegacyRoute) return;
            throw new InvalidOperationException(
                $"state={CurrentState}: source patch active — legacy .cs write rejected pre-effect");
        }

        /// <summary>
        /// source_patch_write command body. Off/Unavailable: delegate exactly
        /// once to the legacy writer — byte-identical to the pre-existing
        /// "asset" write_text route. OnReady: dispatch through the armed
        /// coordinator (§6 P0-70). Busy/Disabling/Recovery: typed rejection,
        /// never a silent legacy fallback (§3.2: never probes by writing first).
        /// </summary>
        internal static string WriteText(string argsJson)
        {
            if (AllowsLegacyRoute) return AssetDatabaseHelper.Execute("write_text", argsJson);
            if (CurrentState == SourcePatchState.OnReady) return SourcePatchModePolicy.TryApplyWrite(argsJson);
            throw new InvalidOperationException(
                $"state={CurrentState}: source patch active — legacy .cs write rejected pre-effect");
        }
    }
}
