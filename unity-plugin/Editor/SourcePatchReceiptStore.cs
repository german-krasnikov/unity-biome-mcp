using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// SessionState-backed persistence for the one Disabling receipt (§3.3).
    /// SessionState survives a Domain Reload within the same Editor process
    /// and is cleared when the process exits — exactly the "same PID/session"
    /// scoping the receipt's own Pid field double-checks explicitly.
    /// </summary>
    internal static class SourcePatchReceiptStore
    {
        private const string Key = "MCP_SourcePatchDisableReceipt";

        public static void Write(SourcePatchDisableReceipt receipt) =>
            SessionState.SetString(Key, receipt.Serialize());

        public static bool TryRead(out SourcePatchDisableReceipt receipt) =>
            SourcePatchDisableReceipt.TryParse(SessionState.GetString(Key, ""), out receipt);

        public static void Clear() => SessionState.EraseString(Key);

        internal static void ResetForTests() => Clear();
    }
}
