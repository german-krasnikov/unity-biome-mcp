// Generated from dsl/data/installdir.data-contract, dsl/data/installdir.core-data
// source-sha256=9a45422d93fef0eba4c7e29a699ff8ad8ff6cff7bc2bd5572efeec7eb1a8a921
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Wizard
{
    public sealed class InstallDir
    {
        public readonly string Key;
        public readonly string Dir;
        public readonly string? DirMac;
        public readonly string? DirWin;
        public InstallDir(string key, string dir, string? dirMac = null, string? dirWin = null)
        {
            this.Key = key;
            this.Dir = dir;
            this.DirMac = dirMac;
            this.DirWin = dirWin;
        }
    }

    public static class InstallDirRows
    {
        public static readonly InstallDir[] All =
        {
            new InstallDir(key: "claude-code", dir: "~/.claude"),
            new InstallDir(
                key: "claude-desktop",
                dir: "~/.config/Claude",
                dirMac: "~/Library/Application Support/Claude",
                dirWin: "{appdata}/Claude"
            ),
            new InstallDir(key: "cursor", dir: "~/.cursor"),
            new InstallDir(key: "windsurf", dir: "~/.codeium", dirWin: "{appdata}/Codeium/windsurf"),
            new InstallDir(
                key: "vscode",
                dir: "~/.config/Code",
                dirMac: "~/Library/Application Support/Code",
                dirWin: "{appdata}/Code"
            ),
            new InstallDir(key: "codex", dir: "~/.codex"),
        };
    }
}
