namespace UnityMCP.Editor.SourcePatch
{
    /// <summary>
    /// The one public contract an optional engine adapter package (e.g. the
    /// FSR-backed provider, P0-60) implements and registers into
    /// <see cref="SourcePatchProviderSlot"/>. The provider owns only body
    /// admission, replacement compilation and exact detour application; the
    /// coordinator owns everything else (source bytes, CAS/readback,
    /// AutoRefresh lease, state, recovery). See §3.1.
    /// </summary>
    public interface ISourcePatchProvider
    {
        SourcePatchApplyOutcome Apply(SourcePatchRequest request);
    }

    /// <summary>
    /// Tri-state result of one provider Apply call. Deliberately a separate
    /// vocabulary from the coordinator-level SourcePatchOperationResult: the
    /// provider has no business knowing about CAS drift or rollback — those
    /// are coordinator-only concepts layered on top of this outcome.
    /// </summary>
    public enum SourcePatchApplyOutcome
    {
        Applied,
        Rejected,
        Uncertain,
    }
}
