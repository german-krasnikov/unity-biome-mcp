// T-4.4: IToolCardRenderer for Agent tool calls.
//
// Idempotency: CSS class "agent-rendered" prevents double-render of the header.
// Two-branch OnUpdate:
//   • PRIMARY (always): render subagent_type + description from argsJson.
//   • ENRICHMENT (Codex / Phase 2.9+): append result summary when rec.HasResult.
//
// Registration: "Agent" is primary (192 confirmed calls); "Task" is insurance for
// older Claude Agent SDK versions where the tool was named differently.
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;  // JsonFieldReader (InternalsVisibleTo)

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class AgentCard : ToolCardBase
    {
        private const int MaxResultLen = 200;

        static AgentCard()
        {
            var inst = new AgentCard();
            ToolCardRendererRegistry.Register("Agent", inst);  // PRIMARY: 192 calls confirmed
            ToolCardRendererRegistry.Register("Task",  inst);  // insurance: older SDK versions
        }

        internal AgentCard() : base("agent-rendered") { }

        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(rec.ArgsJson)) return false;
            RenderHeader(chip, rec.ArgsJson);
            return true;
        }

        // Element-presence guard: chip.Q("agent-result") == null prevents duplicate result sections.
        // This is correct for an append-once pattern — RunSecondaryPass is not needed here.
        protected override void OnAdditionalRender(VisualElement chip, ToolCallRecord rec)
        {
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

        // Uses shared JsonFieldReader for correct escape handling and null-value safety.
        private static (string subagentType, string description) ParseArgs(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson)) return (null, null);
            return (JsonFieldReader.ReadString(argsJson, "subagent_type"),
                    JsonFieldReader.ReadString(argsJson, "description"));
        }
    }
}
