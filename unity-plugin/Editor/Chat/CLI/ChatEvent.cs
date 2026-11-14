namespace UnityMCP.Editor.Chat
{
    /// <summary>Immutable event emitted by RelayEventParser.</summary>
    public readonly struct ChatEvent
    {
        public ChatEventKind Kind         { get; }
        public string        Text         { get; }  // TextDelta: token | ToolStart: name | ToolResult: result text | Error: message
        public string        ArgsJson     { get; }  // ToolStart: partial args fragment
        public bool          IsOk         { get; }  // ToolResult/TurnDone: success flag
        public string        SessionId    { get; }  // TurnDone/Init
        public string        ToolId       { get; }  // tool_use_id (ToolStart, ToolResult)
        public float         CostUsd      { get; }  // TurnDone
        public int           InputTokens  { get; }
        public int           OutputTokens { get; }
        public string        RequestId    { get; }  // PermissionPrompt/AskUser: request_id for control_response
        public string        ToolInput    { get; }  // PermissionPrompt: tool arguments JSON
        public float         Percentage   { get; }  // ToolProgress: 0-100
        public string        State        { get; }  // CapabilitiesChanged: new state string
        public string        RawJson      { get; }  // AskUser: raw questions JSON for UI rendering

        private ChatEvent(ChatEventKind kind, string text = null, string argsJson = null,
            bool isOk = true, string sessionId = null, string toolId = null,
            float costUsd = 0f, int inputTokens = 0, int outputTokens = 0,
            string requestId = null, string toolInput = null, float percentage = 0f,
            string state = null, string rawJson = null)
        {
            Kind         = kind;
            Text         = text;
            ArgsJson     = argsJson;
            IsOk         = isOk;
            SessionId    = sessionId;
            ToolId       = toolId;
            CostUsd      = costUsd;
            InputTokens  = inputTokens;
            OutputTokens = outputTokens;
            RequestId    = requestId;
            ToolInput    = toolInput;
            Percentage   = percentage;
            State        = state;
            RawJson      = rawJson;
        }

        public static ChatEvent TextDelta(string token) =>
            new ChatEvent(ChatEventKind.TextDelta, text: token);

        public static ChatEvent ToolStart(string name, string argsJson = "", string toolId = null) =>
            new ChatEvent(ChatEventKind.ToolStart, text: name, argsJson: argsJson ?? "", toolId: toolId);

        /// <param name="toolId">tool_use_id from the stream</param>
        /// <param name="resultText">tool output content</param>
        /// <param name="ok">false if is_error was true</param>
        public static ChatEvent ToolResult(string toolId, string resultText, bool ok) =>
            new ChatEvent(ChatEventKind.ToolResult, text: resultText, toolId: toolId, isOk: ok);

        public static ChatEvent ToolArgsComplete() =>
            new ChatEvent(ChatEventKind.ToolArgsComplete);

        public static ChatEvent TurnDone(string sessionId, float costUsd, int inputTokens, int outputTokens) =>
            new ChatEvent(ChatEventKind.TurnDone, sessionId: sessionId, isOk: true,
                costUsd: costUsd, inputTokens: inputTokens, outputTokens: outputTokens);

        public static ChatEvent SessionInit(string sessionId) =>
            new ChatEvent(ChatEventKind.SessionInit, sessionId: sessionId, isOk: true);

        public static ChatEvent Error(string message) =>
            new ChatEvent(ChatEventKind.Error, text: message, isOk: false);

        public static ChatEvent PermissionPrompt(string requestId, string toolName, string toolInput) =>
            new ChatEvent(ChatEventKind.PermissionPrompt, text: toolName, requestId: requestId, toolInput: toolInput);

        public static ChatEvent AskUser(string requestId, string rawJson) =>
            new ChatEvent(ChatEventKind.AskUser, requestId: requestId, rawJson: rawJson);

        public static ChatEvent ToolProgress(float percentage, string text = null) =>
            new ChatEvent(ChatEventKind.ToolProgress, text: text, percentage: percentage);

        public static ChatEvent RateLimit(string message) =>
            new ChatEvent(ChatEventKind.RateLimit, text: message);

        public static ChatEvent Heartbeat() =>
            new ChatEvent(ChatEventKind.Heartbeat);

        /// <summary>
        /// Auto-reply to write to CLI stdin (e.g. elicitation accept).
        /// json is the complete JSON-RPC 2.0 response string.
        /// </summary>
        public static ChatEvent AutoReply(string json) =>
            new ChatEvent(ChatEventKind.AutoReply, text: json);

        public static ChatEvent Thinking(string text) =>
            new ChatEvent(ChatEventKind.Thinking, text: text);

        /// <summary>Plan step event from v2 protocol. stepKind = "plan_step_started" / "plan_step_completed".</summary>
        public static ChatEvent PlanUpdate(string stepKind, string description) =>
            new ChatEvent(ChatEventKind.PlanUpdate, text: stepKind, argsJson: description);

        /// <summary>File change detected by agent (v2 protocol). path = file path.</summary>
        public static ChatEvent FileChange(string path) =>
            new ChatEvent(ChatEventKind.FileChange, text: path);

        /// <summary>Provider capabilities state change (v2 protocol).</summary>
        public static ChatEvent CapabilitiesChanged(string state) =>
            new ChatEvent(ChatEventKind.CapabilitiesChanged, state: state);
    }
}
