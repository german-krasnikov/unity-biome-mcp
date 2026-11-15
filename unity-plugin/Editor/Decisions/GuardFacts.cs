namespace UnityMCP.Editor.Decisions
{
    /// <summary>
    /// The inputs of the decision guardVerdict (dsl/decisions/guards.contract) for the two sites of the C# plugin: `top` is
    /// CommandRouter.CheckGuards, `batch` is the per-line guards of BatchHelper.Execute. The caller reads the world (the registry, the compile
    /// and Play Mode state, the read-only setting, the tool settings) and states it here, by name: eleven or twelve booleans in a row are how
    /// a guard gets swapped with another. The rules, and the order in which they apply, are Guards.Policy.GuardVerdict
    /// (dsl/decisions/guards.logic); the text of each answer stays with the caller. Pure: nothing here reads the world or decides.
    /// </summary>
    internal static class GuardFacts
    {
        /// <summary>The facts of a call that reaches CheckGuards. A call at the top is never refused for being async: `batchable` is a fact of the batch site.</summary>
        internal static Guards.Policy.GuardVerdictInput Top(
            bool ready, bool pythonOnly, bool authDenied, bool compiling, bool allowedDuringCompile, bool playMode, bool mutating,
            bool allowedInPlay, bool runtime, bool readOnly, bool alwaysAllowed, bool toolEnabled) =>
            new Guards.Policy.GuardVerdictInput(
                GuardAllowedDuringCompile: allowedDuringCompile, GuardAllowedInPlay: allowedInPlay, GuardAlwaysAllowed: alwaysAllowed,
                GuardAuthDenied: authDenied, GuardBatchable: true, GuardCompiling: compiling, GuardMutating: mutating, GuardPlayKnown: true,
                GuardPlayMode: playMode, GuardPythonOnly: pythonOnly, GuardReadOnly: readOnly, GuardReady: ready, GuardRuntime: runtime,
                GuardSite: "top", GuardToolEnabled: toolEnabled);

        /// <summary>The facts of one line of a batch. Python-only and authorization are checked where the batch itself is called, not per line.</summary>
        internal static Guards.Policy.GuardVerdictInput Batch(
            bool ready, bool batchable, bool compiling, bool allowedDuringCompile, bool playMode, bool mutating, bool allowedInPlay,
            bool runtime, bool readOnly, bool alwaysAllowed, bool toolEnabled) =>
            new Guards.Policy.GuardVerdictInput(
                GuardAllowedDuringCompile: allowedDuringCompile, GuardAllowedInPlay: allowedInPlay, GuardAlwaysAllowed: alwaysAllowed,
                GuardAuthDenied: false, GuardBatchable: batchable, GuardCompiling: compiling, GuardMutating: mutating, GuardPlayKnown: true,
                GuardPlayMode: playMode, GuardPythonOnly: false, GuardReadOnly: readOnly, GuardReady: ready, GuardRuntime: runtime,
                GuardSite: "batch", GuardToolEnabled: toolEnabled);
    }
}
