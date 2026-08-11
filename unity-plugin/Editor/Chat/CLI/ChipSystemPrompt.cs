// Static schema for --append-system-prompt. Claude-only; other backends return null.
namespace UnityMCP.Editor.Chat
{
    internal static class ChipSystemPrompt
    {
        // ~70 tokens. Cached by Anthropic API after first turn.
        internal const string Schema =
            "Unity Editor context chips attached by user:\n" +
            "[hierarchy:/Path] = scene object\n" +
            "[script/material/prefab/texture/model/audio/asset/folder/so:path] = asset\n" +
            "[region:UUID] = spatial scene annotation:\n" +
            "  area/center/bounds/objects = polygon selection\n" +
            "  pos = point marker\n" +
            "  type=polyline = path/waypoints\n" +
            "  dist = ruler measurement\n" +
            "[agent:name] = delegate task to named subagent (invoke Agent tool, subagent_type=name)\n" +
            "In responses: write scene objects, assets, and scripts as bare name or [kind:path] — not [name](link).";

        /// <summary>Returns schema string for the given backend, or null if not applicable.</summary>
        internal static string ForBackend(BackendKind kind)
            => kind == BackendKind.Claude ? Schema : null;
    }
}
