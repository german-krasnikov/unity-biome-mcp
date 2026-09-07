namespace UnityMCP.Worker.ABReloadHarness
{
    // Minimal new-code-execution + non-idempotent-effect proof for the N3
    // T3/T4 A/B live reload identity harness (Plans/N3-T3-T4-live-reload-identity.md).
    // No MCP plugin references, no NUnit, no [InitializeOnLoad]. Accessed
    // exclusively via execute_code Roslyn compilation at runtime: after a
    // domain reload with new source, typeof(AbReloadNonce) resolves to the
    // new assembly, so Nonce returns the new value — genuine execution
    // proof, not a stamp or source-text comparison.
    public static class AbReloadNonce
    {
        public static string Nonce => "NONCE_PLACEHOLDER";

        // Non-idempotent effect for the lost-ACK slice: increment must
        // happen exactly once per accepted call, never resent by the harness.
        public static int Counter = 0;
    }
}
