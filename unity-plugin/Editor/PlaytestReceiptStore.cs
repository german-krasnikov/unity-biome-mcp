namespace UnityMCP.Editor
{
    /// <summary>
    /// Single source of truth for every on-disk playtest artifact path (canonical receipt +
    /// running sentinel). Returns project-relative paths (no UnityEngine dependency, so this
    /// stays portable to Wave D's Core) — callers resolve against the project root themselves.
    /// </summary>
    internal static class PlaytestReceiptStore
    {
        internal const string Root = "Library/UnityMCP/playtest";

        private const string ReceiptExtension = ".json";
        private const string SentinelExtension = ".running";

        internal static string ReceiptPath(string runId) => BuildPath(runId, ReceiptExtension);
        internal static string SentinelPath(string runId) => BuildPath(runId, SentinelExtension);

        private static string BuildPath(string runId, string extension) => Root + "/" + runId + extension;
    }
}
