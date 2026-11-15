namespace UnityMCP.Editor.Roslyn
{
    // DI seam breaking the Roslyn -> Core back-edge (Task 0.1). Core wires these
    // fields at domain-load via RoslynHostBootstrap, mirroring the
    // ChatBackendProbe/ChatSettingsHook pattern already used for Chat (PR-05).
    // Defaults are fail-safe (Standard level, no-op logging) for the narrow
    // pre-bootstrap window -- never null-ref.
    internal static class RoslynHost
    {
        internal static System.Func<SecurityLevel> SecurityLevelProvider = () => SecurityLevel.Standard;
        internal static System.Action<string> LogWarning = _ => { };
        internal static System.Action<string> LogError   = _ => { };
    }
}
