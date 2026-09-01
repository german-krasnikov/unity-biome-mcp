using System.Collections.Generic;

namespace UnityMCP.Editor.SourcePatch
{
    /// <summary>
    /// Lifecycle states from §3.3. Package capability and user intent are
    /// distinct: Unavailable means no provider is registered; Off/OnReady/
    /// Busy/Disabling/Recovery all assume a provider is present.
    /// </summary>
    internal enum SourcePatchState
    {
        Unavailable,
        Off,
        OnReady,
        Busy,
        Disabling,
        Recovery,
    }

    /// <summary>
    /// Pure, minimal state machine enforcing exactly the 9 legal edges from
    /// §3.3 (ROI Fix 2b added the Recovery exit edge). Recovery has exactly
    /// one legal outgoing edge — Recovery -&gt; Disabling — reached only by an
    /// explicit user enable=false intent (SourcePatchModePolicy.RequestDisable),
    /// never automatically: no code path enters it on its own. Every other
    /// Recovery transition, in particular Recovery -&gt; OnReady and
    /// Recovery -&gt; Off, remains illegal — there is no direct re-enable and
    /// no silent auto-repair; the only way back to OnReady is a fresh
    /// Domain Reload / a freshly constructed coordinator (host-level,
    /// P0-50/70). A domain-start-in-Recovery from a stale receipt is
    /// represented by constructing with <paramref name="initial"/> = Recovery,
    /// not by a transition into it — OnReady -> Recovery is deliberately NOT
    /// a legal edge; uncertainty/drift detected while OnReady always pass
    /// through Busy first inside the coordinator.
    /// </summary>
    internal sealed class SourcePatchStateMachine
    {
        private static readonly HashSet<(SourcePatchState From, SourcePatchState To)> LegalTransitions =
            new HashSet<(SourcePatchState, SourcePatchState)>
        {
            (SourcePatchState.Unavailable, SourcePatchState.Off),
            (SourcePatchState.Off, SourcePatchState.OnReady),
            (SourcePatchState.OnReady, SourcePatchState.Busy),
            (SourcePatchState.Busy, SourcePatchState.OnReady),
            (SourcePatchState.Busy, SourcePatchState.Recovery),
            (SourcePatchState.OnReady, SourcePatchState.Disabling),
            (SourcePatchState.Disabling, SourcePatchState.Off),
            (SourcePatchState.Disabling, SourcePatchState.Recovery),
            (SourcePatchState.Recovery, SourcePatchState.Disabling),   // ROI Fix 2b
        };

        public SourcePatchState Current { get; private set; }

        public SourcePatchStateMachine(SourcePatchState initial = SourcePatchState.Unavailable)
        {
            Current = initial;
        }

        public static bool IsLegalTransition(SourcePatchState from, SourcePatchState to) =>
            LegalTransitions.Contains((from, to));

        public bool TryTransition(SourcePatchState to)
        {
            if (!IsLegalTransition(Current, to)) return false;
            Current = to;
            return true;
        }
    }
}
