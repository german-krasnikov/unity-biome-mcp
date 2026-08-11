namespace UnityMCP.Editor
{
    /// <summary>
    /// Central home for SessionState/EditorPrefs key literals shared across 2+ files
    /// (DRY audit issues-23-29 Cat.3). SessionState/EditorPrefs are untyped string-keyed
    /// stores — a typo or drift between two files' copies of the same key string does not
    /// throw or fail a compile, it silently makes one file's write invisible to the other's
    /// read. Keys used by exactly one file (e.g. ConsoleProblemPersistence's own consts)
    /// stay local — nothing to desync there.
    /// </summary>
    internal static class PrefKeys
    {
        // Chat — SessionState (survives domain reload within one Editor session)
        public const string ChatBackendSessionId = "MCPChat_BackendSessionId";
        public const string ChatTranscript       = "MCPChat_Transcript";

        // Region/Annotation tools — SessionState
        public const string ActiveRegionId = "MCP_ActiveRegionId";

        // Region/Annotation tools — EditorPrefs (persists across Editor sessions)
        public const string RegionAutoAdd        = "MCP_RegionAutoAdd";
        public const string DefaultAnnotationMode = "MCP_DefaultAnnotationMode";
        public const string RegionMaxObjects     = "MCP_RegionMaxObjects";
        public const string AnnotationGridSnap   = "MCP_AnnotSnap";

        // Chat auth — EditorPrefs
        public const string ChatAuthStatus = "UnityMCP_Chat_AuthStatus";

        // Chat settings — EditorPrefs (shared: ChatSettingsSection writes, MCPChatWindow.Drain.cs reads)
        public const string ChatAutoScroll          = "MCPChat.AutoScroll";
        public const string DisableSceneNameNorm    = "MCPChat.DisableSceneNameNorm";
        public const string ShowThinkingBlocks      = "MCPChat.ShowThinkingBlocks";
    }
}
