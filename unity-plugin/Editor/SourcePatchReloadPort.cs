namespace UnityMCP.Editor
{
    /// <summary>Outcome of a reload verification request. The port never
    /// leaves an ambiguous "did it work?" result: the caller (SourcePatchModePolicy)
    /// switches on this instead of inferring success from "no exception".</summary>
    internal enum ReloadPortOutcome { Accepted, AcceptedNoOp, Rejected }

    /// <summary>PR-04R: narrow SourcePatch-owned port over Reload's control surface.
    /// Exactly one method — any additional need routes through a new named port,
    /// never by widening this one (mirrors the discipline already applied to
    /// <see cref="UnityMCP.Editor.SourcePatch.ICompileEvidencePort"/>, a separate,
    /// unrelated responsibility this port does not replace).</summary>
    internal interface ISourcePatchReloadPort
    {
        /// <param name="expectedEpochAfter">The one Reload-owned epoch value
        /// (from the caller's receipt) this request must be acknowledged
        /// against. The port never recomputes its own CurrentEpoch+1 — the
        /// receipt is the single authority (N2a.1).</param>
        ReloadPortOutcome RequestReloadVerification(int expectedEpochAfter);
    }

    /// <summary>Legacy adapter — identical effect to today's direct
    /// <c>SyncHelper.TriggerSync(resolve: false)</c> call. Swappable in tests
    /// without touching SyncHelper itself.</summary>
    [UnityEditor.InitializeOnLoad]
    internal sealed class SyncHelperReloadPort : ISourcePatchReloadPort
    {
        // Main-assembly composition; Reload core has no optional-provider dependency.
        static SyncHelperReloadPort() => SyncHelper.ReloadBlockReason = SourcePatchHost.ReloadBlockReason;

        public ReloadPortOutcome RequestReloadVerification(int expectedEpochAfter)
        {
            var result = SyncHelper.TriggerSync(resolve: false, explicitOwnedDisable: true);
            var prefix = $"sync_ack|epoch={expectedEpochAfter}|will_compile=";
            if (result == prefix + "true") return ReloadPortOutcome.Accepted;
            if (result == prefix + "false") return ReloadPortOutcome.AcceptedNoOp;
            throw new System.InvalidOperationException(
                "source patch disable reload was not acknowledged: " + result);
        }
    }
}
