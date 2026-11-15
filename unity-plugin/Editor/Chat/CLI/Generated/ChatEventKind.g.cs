// Generated from dsl/data/chateventkind.data-contract, dsl/data/chateventkind.core-data
// source-sha256=5c876822e1e729032636a5e7a85ec5d439d2089119be009b0e25508364838ef8
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Chat
{
    public enum ChatEventKind
    {
        TextDelta,
        ToolStart,
        ToolResult,
        ToolArgsComplete,
        TurnDone,
        SessionInit,
        Error,
        PermissionPrompt,
        AskUser,
        ToolProgress,
        RateLimit,
        Heartbeat,
        AutoReply,
        Thinking,
        PlanUpdate,
        FileChange,
        CapabilitiesChanged,
    }

    public static class ChatEventKindValues
    {
        public const string TextDelta = "text_delta";
        public const string ToolStart = "tool_start";
        public const string ToolResult = "tool_result";
        public const string ToolArgsComplete = "tool_args_complete";
        public const string TurnDone = "turn_done";
        public const string SessionInit = "session_init";
        public const string Error = "error";
        public const string PermissionPrompt = "permission_prompt";
        public const string AskUser = "ask_user";
        public const string ToolProgress = "tool_progress";
        public const string RateLimit = "rate_limit";
        public const string Heartbeat = "heartbeat";
        public const string AutoReply = "auto_reply";
        public const string Thinking = "thinking";
        public const string PlanUpdate = "plan_update";
        public const string FileChange = "file_change";
        public const string CapabilitiesChanged = "capabilities_changed";
        public static readonly string[] Names =
        {
            TextDelta,
            ToolStart,
            ToolResult,
            ToolArgsComplete,
            TurnDone,
            SessionInit,
            Error,
            PermissionPrompt,
            AskUser,
            ToolProgress,
            RateLimit,
            Heartbeat,
            AutoReply,
            Thinking,
            PlanUpdate,
            FileChange,
            CapabilitiesChanged,
        };
    }

    public sealed class KindNote
    {
        public readonly string Note;
        public KindNote(string note) { this.Note = note; }
    }

    public static class KindNoteRows
    {
        public static readonly KindNote[] All =
        {
            new KindNote(note: "tool_args_complete and auto_reply are internal protocol signals; all other values correspond to UI factory names in events.biome"),
        };
    }
}
