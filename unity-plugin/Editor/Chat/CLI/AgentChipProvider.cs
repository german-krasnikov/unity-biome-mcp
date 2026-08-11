// Provider for @-mentioned Claude Code sub-agents.
// Registered in ChipKindRegistry.EnsureBuiltIns (priority 5 — before all asset providers).
// CanHandle is always false: agent chips are created programmatically by the mention UI.
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AgentChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.Agent;
        public int      Priority         => 5;
        public string   IconName         => "d_cs Script Icon";
        public string   HexColor         => "#9b59b6";
        public string   DefaultDepth     => "path";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        public bool CanHandle(Object obj, string assetPath) => false;
        public ChipData Create(Object obj, string assetPath) => default;

        /// <summary>Formats the AI-facing bracket for an agent chip: [agent:name] or "" when depth=none.</summary>
        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[agent:{chip.Path}]";

        public void Navigate(string reference) { }
        public void Ping(string reference) { }
        public void AppendContextMenuItems(DropdownMenu menu, string reference) { }
    }
}
