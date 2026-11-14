namespace UnityMCP.Editor
{
    // Extracted from BatchHelper (Phase 5 A1): the batch-depth counter lives here,
    // in the zero-reference Primitives leaf, so Core.Scene (ObjectManager.Properties.cs)
    // can read InBatch without referencing BatchHelper/CommandRouter in the Editor
    // monolith. BatchHelper.Execute still owns root-batch semantics (Undo grouping,
    // mutation counting) -- only the depth counter and its InBatch projection move.
    internal static class BatchState
    {
        private static int _depth;

        internal static bool InBatch => _depth > 0;

        internal static int Enter() => ++_depth;

        internal static int Exit() => --_depth;
    }
}
