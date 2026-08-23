namespace UnityMCP.Editor
{
    /// <summary>Single source of truth for version constants.
    /// Plugin must match unity-plugin/package.json "version".
    /// Protocol must match server/src/unity_mcp/bridge.py PROTOCOL_VERSION.</summary>
    internal static class BiomeVersion
    {
        public const string Plugin = "1.50.3";
        public const int Protocol = 4;
    }
}
