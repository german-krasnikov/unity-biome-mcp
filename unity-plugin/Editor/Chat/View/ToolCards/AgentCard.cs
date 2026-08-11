// T-4.4: IToolCardRenderer for Agent tool calls.
//
// Idempotency: CSS class "agent-rendered" prevents double-render of the header.
// Two-branch OnUpdate:
//   • PRIMARY (always): render subagent_type + description from argsJson.
//   • ENRICHMENT (Codex / Phase 2.9+): append result summary when rec.HasResult.
//
// Registration: "Agent" is primary (192 confirmed calls); "Task" is insurance for
// older Claude Agent SDK versions where the tool was named differently.
using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class AgentCard : IToolCardRenderer
    {
        private const int MaxResultLen = 200;

        static AgentCard()
        {
            var inst = new AgentCard();
            ToolCardRendererRegistry.Register("Agent", inst);  // PRIMARY: 192 calls confirmed
            ToolCardRendererRegistry.Register("Task",  inst);  // insurance: older SDK versions
        }

        public void OnStart(VisualElement chip, ToolCallRecord rec) { }

        public void OnUpdate(VisualElement chip, ToolCallRecord rec)
        {
            if (!chip.ClassListContains("agent-rendered"))
            {
                RenderHeader(chip, rec.ArgsJson);
                chip.AddToClassList("agent-rendered");
            }

            if (rec.HasResult && chip.Q(className: "agent-result") == null)
                AppendResultSummary(chip, rec.ResultText);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static void RenderHeader(VisualElement chip, string argsJson)
        {
            var (subagentType, description) = ParseArgs(argsJson);

            if (!string.IsNullOrEmpty(subagentType))
            {
                var typeLabel = new Label(subagentType);
                typeLabel.AddToClassList("agent-type");
                chip.Add(typeLabel);
            }

            if (description != null)
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("agent-desc");
                chip.Add(descLabel);
            }
        }

        private static void AppendResultSummary(VisualElement chip, string resultText)
        {
            var text = string.IsNullOrEmpty(resultText)
                ? ""
                : resultText.Length > MaxResultLen
                    ? resultText.Substring(0, MaxResultLen) + "…"  // … ellipsis
                    : resultText;

            var label = new Label(text);
            label.AddToClassList("agent-result");
            chip.Add(label);
        }

        // Hand-rolled JSON string extraction (same pattern as ToolDetailBuilder.cs:L75-105).
        // Only extracts string values; ignores booleans and other non-string fields.
        private static (string subagentType, string description) ParseArgs(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson)) return (null, null);
            var subagentType = ExtractStringValue(argsJson, "subagent_type");
            var description  = ExtractStringValue(argsJson, "description");
            return (subagentType, description);
        }

        private static string ExtractStringValue(string json, string key)
        {
            var search = "\"" + key + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            // Skip past the key to the value's opening quote.
            idx  = json.IndexOf('"', idx + search.Length);
            if (idx < 0) return null;

            var start = idx + 1;
            var end   = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start);
        }
    }
}
