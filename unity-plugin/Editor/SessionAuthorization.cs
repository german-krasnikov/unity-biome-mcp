using UnityMCP.Editor.Decisions;
using UnityMCP.Editor.Decisions.Permission;

namespace UnityMCP.Editor
{
    // Defense-in-depth guard: blocks mutations when chatMode is "ask", and dangerous
    // commands (BLAST >= dangerous tier) even in "agent" mode. Python PermissionBroker
    // is the primary enforcement point; this fires only if that broker is bypassed
    // (e.g., direct TCP call or future code path). Empty/null chatMode = external MCP
    // client = always allowed. The rule itself is the one DECISION chatPermission
    // (dsl/decisions/permission.logic), shared with server/src/unity_mcp/permission_broker.py (SV2/Q15).
    internal static class SessionAuthorization
    {
        // Returns null if allowed, error string if blocked.
        internal static string Check(string chatMode, string cmd, string argsJson = null)
        {
            var mode = PermissionFacts.ChatMode(chatMode);
            var outcome = Policy.ChatPermission(new Policy.ChatPermissionInput(
                CallDangerous: PermissionFacts.IsDangerous(cmd),
                CallMutating: CommandRegistry.IsMutating(cmd, argsJson),
                ChatMode: mode));

            if (outcome == Policy.ChatPermissionValues.Allow) return null;
            if (outcome == Policy.ChatPermissionValues.DenyUnknownMode)
                return $"unknown chatMode '{chatMode}': denied by default";
            if (outcome == Policy.ChatPermissionValues.DenyDangerous)
                return $"agent mode: '{cmd}' is dangerous — use full-access mode";
            return $"ask mode: '{cmd}' requires agent mode";
        }
    }
}
