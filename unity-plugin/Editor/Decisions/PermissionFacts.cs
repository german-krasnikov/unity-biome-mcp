namespace UnityMCP.Editor.Decisions
{
    /// <summary>
    /// The facts of the decision chatPermission (dsl/decisions/permission.contract), read the way the Python side reads
    /// them (permission_broker.py): chat.mode folds the wire chatMode string into the DSL's closed enum, call.dangerous
    /// is the catalog's BLAST radius (ToolBlast, generated from dsl/tools.biome) at or past the dangerous tier. The rule
    /// is Permission.Policy.ChatPermission (dsl/decisions/permission.logic); nothing here decides.
    /// Pure: no registry lookups besides ToolBlast, no Unity.
    /// </summary>
    internal static class PermissionFacts
    {
        // Mirrors server/src/unity_mcp/middleware_types.py BLAST_DANGEROUS -- the only
        // half of that threshold which isn't itself generated from dsl/tools.biome.
        internal const int DangerousThreshold = 4;

        /// <summary>"" or null (no chat token) -> external; "full-access" -> fullAccess; an unrecognized token -> unknown.</summary>
        internal static string ChatMode(string chatMode)
        {
            if (string.IsNullOrEmpty(chatMode)) return "external";
            if (chatMode == "ask" || chatMode == "agent") return chatMode;
            if (chatMode == "full-access") return "fullAccess";
            return "unknown";
        }

        internal static bool IsDangerous(string cmd) => ToolBlast.RadiusOf(cmd) >= DangerousThreshold;
    }
}
