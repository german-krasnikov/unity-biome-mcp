// Generated from dsl/data/protocol.data-contract, dsl/data/protocol.core-data
// source-sha256=178a86c73cacf90bfe937a878726cc1d450801b4c38d661fe76e55840ceaffcd
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Wizard
{
    public sealed class Protocol
    {
        public readonly string VersionRefPattern;
        public Protocol(string versionRefPattern)
        {
            this.VersionRefPattern = versionRefPattern;
        }
    }

    public static class ProtocolRows
    {
        public static readonly Protocol[] All =
        {
            new Protocol(versionRefPattern: "[\\d.]+(?:-[0-9A-Za-z.]+)?"),
        };
    }
}
