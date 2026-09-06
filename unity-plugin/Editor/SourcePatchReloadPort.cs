namespace UnityMCP.Editor
{
    /// <summary>PR-04R: narrow SourcePatch-owned port over Reload's control surface.
    /// Exactly one method — any additional need routes through a new named port,
    /// never by widening this one (mirrors the discipline already applied to
    /// <see cref="UnityMCP.Editor.SourcePatch.ICompileEvidencePort"/>, a separate,
    /// unrelated responsibility this port does not replace).</summary>
    internal interface ISourcePatchReloadPort
    {
        void RequestReloadVerification();
    }

    /// <summary>Legacy adapter — identical effect to today's direct
    /// <c>SyncHelper.TriggerSync(resolve: false)</c> call. Swappable in tests
    /// without touching SyncHelper itself.</summary>
    [UnityEditor.InitializeOnLoad]
    internal sealed class SyncHelperReloadPort : ISourcePatchReloadPort
    {
        // Main-assembly composition; Reload core has no optional-provider dependency.
        static SyncHelperReloadPort() => SyncHelper.ReloadBlockReason = SourcePatchHost.ReloadBlockReason;

        public void RequestReloadVerification()
        {
            var expectedEpoch = SyncHelper.CurrentEpoch + 1;
            var result = SyncHelper.TriggerSync(resolve: false, explicitOwnedDisable: true);
            var prefix = $"sync_ack|epoch={expectedEpoch}|will_compile=";
            if (result != prefix + "true" && result != prefix + "false")
                throw new System.InvalidOperationException(
                    "source patch disable reload was not acknowledged: " + result);
        }
    }
}
