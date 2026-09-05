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
    internal sealed class SyncHelperReloadPort : ISourcePatchReloadPort
    {
        public void RequestReloadVerification() => SyncHelper.TriggerSync(resolve: false);
    }
}
