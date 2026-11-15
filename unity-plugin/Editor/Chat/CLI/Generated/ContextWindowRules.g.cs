// Generated from dsl/data/contextwindow.data-contract, dsl/data/contextwindow.core-data
// source-sha256=8f3432d3c670c531f5eaf77809bd829e4810e55dd46e21ee3926b14710ce5943
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Chat
{
    public sealed class ContextWindowRule
    {
        public readonly string Pattern;
        public readonly string Mode;
        public readonly int Window;
        public ContextWindowRule(string pattern, string mode, int window)
        {
            this.Pattern = pattern;
            this.Mode = mode;
            this.Window = window;
        }
    }

    public static class ContextWindowRuleRows
    {
        public static readonly ContextWindowRule[] All =
        {
            new ContextWindowRule(pattern: "fable", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "mythos", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "opus-5", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "sonnet-5", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "opus-4-8", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "opus-4-7", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "opus-4-6", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "sonnet-4-6", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "opus", mode: "contains", window: 200000),
            new ContextWindowRule(pattern: "sonnet", mode: "contains", window: 200000),
            new ContextWindowRule(pattern: "haiku", mode: "contains", window: 200000),
            new ContextWindowRule(pattern: "gpt-5.4", mode: "contains", window: 1050000),
            new ContextWindowRule(pattern: "gpt-5.5", mode: "contains", window: 1050000),
            new ContextWindowRule(pattern: "gpt-5.6", mode: "contains", window: 1050000),
            new ContextWindowRule(pattern: "gpt-5", mode: "contains", window: 400000),
            new ContextWindowRule(pattern: "gpt-4.1", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "gpt-4", mode: "contains", window: 128000),
            new ContextWindowRule(pattern: "o3", mode: "starts", window: 200000),
            new ContextWindowRule(pattern: "o4", mode: "starts", window: 200000),
            new ContextWindowRule(pattern: "gemini", mode: "contains", window: 1000000),
            new ContextWindowRule(pattern: "kimi", mode: "contains", window: 128000),
            new ContextWindowRule(pattern: "moonshot", mode: "contains", window: 128000),
            new ContextWindowRule(pattern: "codex", mode: "contains", window: 192000),
        };
    }
}
