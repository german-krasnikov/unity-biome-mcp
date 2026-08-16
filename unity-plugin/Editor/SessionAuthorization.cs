namespace UnityMCP.Editor
{
    // Defense-in-depth guard: blocks mutations when chatMode is "ask".
    // Python PermissionBroker is the primary enforcement point; this fires only
    // if Python broker is bypassed (e.g., direct TCP call or future code path).
    // Empty/null chatMode = external MCP client = always allowed.
    internal static class SessionAuthorization
    {
        // Returns null if allowed, error string if blocked.
        internal static string Check(string chatMode, string cmd, string argsJson = null)
        {
            if (string.IsNullOrEmpty(chatMode)) return null;
            if (chatMode == "agent" || chatMode == "full-access") return null;
            if (chatMode == "ask")
                return CommandRegistry.IsMutating(cmd, argsJson)
                    ? $"ask mode: '{cmd}' requires agent mode"
                    : null;
            return $"unknown chatMode '{chatMode}': denied by default";
        }
    }
}
