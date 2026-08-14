// Provider for profiling session chips. Created by PerfWindow "Analyze in Chat" button.
// CanHandle is always false: profile chips are created programmatically.
using UnityEngine.UIElements;
using UnityMCP.Editor.Profiling;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ProfilerChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.Profile;
        public int      Priority         => 75;   // between Agent(5) and Hierarchy
        public string   IconName         => "d_UnityEditor.Profiling.ProfilerWindow";
        public string   HexColor         => "#f0a030";   // amber — matches perf theme
        public string   DefaultDepth     => "full";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        public bool CanHandle(Object obj, string assetPath) => false;
        public ChipData Create(Object obj, string assetPath) => default;

        /// <summary>
        /// Resolves session stats via ProfileContextSerializer. chip.Path = session ID.
        /// Returns empty string when depth is "none".
        /// </summary>
        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";
            return ProfileContextSerializer.Get(chip.Path);
        }

        public void Navigate(string reference)
            => UnityEditor.EditorApplication.ExecuteMenuItem("🧬MCP/Performance");
        public void Ping(string reference) { }
        public void AppendContextMenuItems(DropdownMenu menu, string reference) { }
    }
}
