namespace UnityMCP.Decisions.Tests.Oracle
{
    // The guards of the C# plugin as they stood before step 7.2 moved their order into dsl/decisions/guards.logic (G2), transcribed over the
    // facts they read, each condition and the order of the conditions as in the code of the parent commit: CommandRouter.CheckGuards (Top) and
    // the per-line block of BatchHelper.Execute (Batch). They answer with the name of the guard that stopped the call ("ok" when none did).
    // This is an oracle, not a copy to maintain: it is never edited to follow the generated policy.
    internal static class HandGuards
    {
        internal static string Top(bool ready, bool pythonOnly, bool authDenied, bool compiling, bool allowedDuringCompile, bool playMode,
            bool mutating, bool allowedInPlay, bool runtime, bool readOnly, bool alwaysAllowed, bool toolEnabled)
        {
            if (!ready) return "initializing";
            if (pythonOnly) return "pythonOnly";
            if (authDenied) return "authDenied";
            if (compiling && !allowedDuringCompile) return "compiling";
            if (playMode && mutating && !allowedInPlay) return "playBlocked";
            if (!playMode && runtime) return "notInPlay";
            if (readOnly && mutating) return "readOnlyBlocked";
            if (!alwaysAllowed && !toolEnabled) return "disabled";
            return "ok";
        }

        internal static string Batch(bool ready, bool batchable, bool compiling, bool allowedDuringCompile, bool playMode, bool mutating,
            bool allowedInPlay, bool runtime, bool readOnly, bool alwaysAllowed, bool toolEnabled)
        {
            if (!batchable) return "asyncOnly";
            if (playMode && mutating && !allowedInPlay) return "playBlocked";
            if (!playMode && runtime) return "notInPlay";
            if (compiling && mutating && !allowedDuringCompile) return "compiling";
            if (readOnly && mutating) return "readOnlyBlocked";
            if (!alwaysAllowed && !toolEnabled) return "disabled";
            if (!ready) return "initializing";
            return "ok";
        }
    }
}
