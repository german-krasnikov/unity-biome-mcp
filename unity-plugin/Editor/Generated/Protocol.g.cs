// Generated from dsl/data/protocol.data-contract, dsl/data/protocol.core-data
// source-sha256=dfc9f24da191cc273adc18a3f87835896189c4d470b6bdf0010c3360014d9d96
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Wizard
{
    public sealed class Protocol
    {
        public readonly string VersionRefPattern;
        public readonly string ServerRefPattern;
        public readonly string GitRepoBase;
        public readonly string InstallSubdir;
        public Protocol(string versionRefPattern, string serverRefPattern, string gitRepoBase, string installSubdir)
        {
            this.VersionRefPattern = versionRefPattern;
            this.ServerRefPattern = serverRefPattern;
            this.GitRepoBase = gitRepoBase;
            this.InstallSubdir = installSubdir;
        }
    }

    public static class ProtocolRows
    {
        public static readonly Protocol[] All =
        {
            new Protocol(
                versionRefPattern: "[\\d.]+(?:-[0-9A-Za-z.]+)?",
                serverRefPattern: "[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.]+)?",
                gitRepoBase: "git+https://github.com/german-krasnikov/unity-biome-mcp.git",
                installSubdir: "server"
            ),
        };
    }
}
