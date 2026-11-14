// Generated from dsl/data/chipkinds.data-contract, dsl/data/chipkinds.core-data
// source-sha256=2257360977b5717a93a3e6b50713ba35812f2e067be634a780148b3f76626328
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Chat
{
    public sealed class ChipKind
    {
        public readonly string Key;
        public readonly string HexColor;
        public readonly string DefaultDepth;
        public readonly int Priority;
        public ChipKind(string key, string hexColor, string defaultDepth, int priority)
        {
            this.Key = key;
            this.HexColor = hexColor;
            this.DefaultDepth = defaultDepth;
            this.Priority = priority;
        }
    }

    public static class ChipKindRows
    {
        public static readonly ChipKind[] All =
        {
            new ChipKind(key: "agent", hexColor: "#9b59b6", defaultDepth: "path", priority: 5),
            new ChipKind(key: "annotated_screenshot", hexColor: "#e74c3c", defaultDepth: "summary", priority: 40),
            new ChipKind(key: "image", hexColor: "#f472b6", defaultDepth: "path", priority: 50),
            new ChipKind(key: "profile", hexColor: "#f0a030", defaultDepth: "full", priority: 75),
            new ChipKind(key: "hierarchy", hexColor: "#4a9eff", defaultDepth: "path", priority: 100),
            new ChipKind(key: "region", hexColor: "#22d3ee", defaultDepth: "summary", priority: 120),
            new ChipKind(key: "component", hexColor: "#38bdf8", defaultDepth: "summary", priority: 125),
            new ChipKind(key: "field", hexColor: "#f59e0b", defaultDepth: "summary", priority: 130),
            new ChipKind(key: "folder", hexColor: "#a78bfa", defaultDepth: "path", priority: 150),
            new ChipKind(key: "scene", hexColor: "#c084fc", defaultDepth: "path", priority: 200),
            new ChipKind(key: "script", hexColor: "#4ade80", defaultDepth: "path", priority: 300),
            new ChipKind(key: "prefab", hexColor: "#60a5fa", defaultDepth: "path", priority: 400),
            new ChipKind(key: "model", hexColor: "#34d399", defaultDepth: "path", priority: 450),
            new ChipKind(key: "material", hexColor: "#f97316", defaultDepth: "path", priority: 500),
            new ChipKind(key: "audio", hexColor: "#a78bfa", defaultDepth: "path", priority: 550),
            new ChipKind(key: "texture", hexColor: "#facc15", defaultDepth: "path", priority: 600),
            new ChipKind(key: "so", hexColor: "#fb7185", defaultDepth: "path", priority: 700),
            new ChipKind(key: "asset", hexColor: "#94a3b8", defaultDepth: "path", priority: 2147483647),
        };
    }
}
