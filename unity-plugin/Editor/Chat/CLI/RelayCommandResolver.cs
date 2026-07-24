// Pure install-source-aware command resolution for the chat relay sidecar.
// Mirrors ChatMcpConfigWriter.GetOrCreateConfigPath()'s Local/non-local branching so both
// entrypoints (MCP server config + relay spawn) agree on how Python is invoked per install source.
using System;
using UnityEditor.PackageManager;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Chat
{
    internal static class RelayCommandResolver
    {
        // Test seam — override the plugin version used to pin the uvx git URL.
        internal static Func<string> VersionResolver = DefaultVersionResolver;

        /// <summary>
        /// Returns (cmd, argv) as one unit so the args half can never be silently dropped by a
        /// caller — the pre-existing bug the old PythonResolver-only seam had for Local+uv installs.
        ///   Local install → run the dev checkout's server/ directly (venv/uv/python3 fallback chain)
        ///   Non-local     → uvx --from &lt;git url pinned to plugin version&gt; unity-biome-mcp-relay
        /// </summary>
        internal static (string cmd, string[] argv) Resolve()
        {
            if (InstallSourceDetector.Detect() != InstallSourceDetector.Source.Local)
            {
                var uvx = ChatBinaryResolver.Resolve("uvx");
                if (string.IsNullOrEmpty(uvx)) return (null, null);
                var url = WizardConfigWriter.GitInstallUrlFor(VersionResolver());
                return (uvx, new[] { "--from", url, "unity-biome-mcp-relay" });
            }

            // ChatMcpConfigWriter.PackageRoot() honours SetPackageRootForTest() — the same seam
            // GetOrCreateConfigPath() uses — so the Local branch here is unit-testable too,
            // instead of depending on the real Packages/com.unity-biome-mcp.editor on disk.
            var serverDir = ChatMcpConfigWriter.ResolveServerDir(ChatMcpConfigWriter.PackageRoot());
            if (serverDir == null) return (null, null);

            var uvPath      = ChatBinaryResolver.Resolve("uv");
            var (cmd, args) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, uvPath);
            return (cmd, RelayArgsFor(args, serverDir));
        }

        // ResolvePythonCommand's args are tuned for the MCP server ("-m unity_mcp.server" or
        // "run --directory dir unity-biome-mcp"). Reuse its venv/uv/fallback decision (no duplicated
        // File.Exists checks) and swap in the relay's module / console-script name.
        private static string[] RelayArgsFor(string[] serverArgs, string serverDir) =>
            serverArgs.Length > 0 && serverArgs[0] == "-m"
                ? new[] { "-m", "unity_mcp.chat_relay" }
                : new[] { "run", "--directory", serverDir, "unity-biome-mcp-relay" };

        private static string DefaultVersionResolver() =>
            PackageInfo.FindForAssembly(typeof(RelayCommandResolver).Assembly)?.version;
    }
}
