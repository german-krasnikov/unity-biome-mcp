// Generated from dsl/data/protocol.data-contract, dsl/data/protocol.core-data
// source-sha256=766abf9614b49ca7febc72e02947ba44da795f9265942a2ff9a2e47608bdefa2
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
        public readonly int DefaultPort;
        public readonly int MaxFrameBytes;
        public readonly int MaxToolResultLen;
        public readonly int MinPythonMajor;
        public readonly int MinPythonMinor;
        public Protocol(
            string versionRefPattern,
            string serverRefPattern,
            string gitRepoBase,
            string installSubdir,
            int defaultPort,
            int maxFrameBytes,
            int maxToolResultLen,
            int minPythonMajor,
            int minPythonMinor)
        {
            this.VersionRefPattern = versionRefPattern;
            this.ServerRefPattern = serverRefPattern;
            this.GitRepoBase = gitRepoBase;
            this.InstallSubdir = installSubdir;
            this.DefaultPort = defaultPort;
            this.MaxFrameBytes = maxFrameBytes;
            this.MaxToolResultLen = maxToolResultLen;
            this.MinPythonMajor = minPythonMajor;
            this.MinPythonMinor = minPythonMinor;
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
                installSubdir: "server",
                defaultPort: 9500,
                maxFrameBytes: 10000000,
                maxToolResultLen: 2000,
                minPythonMajor: 3,
                minPythonMinor: 14
            ),
        };
    }
}
