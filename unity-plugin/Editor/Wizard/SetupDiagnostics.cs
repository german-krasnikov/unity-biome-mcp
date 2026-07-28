using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Pure static diagnostics — no Unity I/O, fully unit-testable.</summary>
    public static class SetupDiagnostics
    {
        // ── Repo root ─────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the repo root directory (parent of unity-plugin/) by delegating to
        /// InstallSourceDetector.LocalRepoRoot(). Returns null for UPM git/registry installs.
        /// </summary>
        public static string ResolveRepoRoot()
            => InstallSourceDetector.LocalRepoRoot();

        // ── Python ────────────────────────────────────────────────────────────

        const int PythonVersionCheckTimeoutMs = 3000;
        const int PythonProcessKillWaitMs = 500;

        static string _cachedPythonCmd;
        static string _cachedPythonVersion;

        /// <summary>
        /// Checks whether a usable Python executable exists for the given server dir.
        /// Resolution order: .venv/Scripts/python.exe (Windows) → .venv/bin/python → python3/python fallback.
        /// </summary>
        public static (bool ok, string detail) CheckPython(string serverDir)
        {
            if (string.IsNullOrEmpty(serverDir) || !Directory.Exists(serverDir))
                return (false, "Server directory not found");

            var cmd = ResolvePythonCmd(serverDir);
            var isFallback = cmd == "python3" || cmd == "python";
            if (isFallback)
            {
                var version = GetPythonVersion(cmd);
                if (version == null) return (false, "python3 found but version check failed");
                if (!IsVersionAtLeast(version, 3, 10))
                    return (false, $"Python {version} found — 3.10+ required (or install uv)");
                return (true, $"Python {version} via {cmd}");
            }

            if (File.Exists(cmd))
                return (true, $"Python at {cmd}");

            return (false, $"Python not found (tried {cmd})");
        }

#if UNITY_INCLUDE_TESTS
        // Seam: inject in tests to bypass Process.Start (cmd → "3.x.y" or null)
        public static Func<string, string> PythonVersionOverride;
        internal static void ResetPythonVersionCache() { _cachedPythonCmd = null; _cachedPythonVersion = null; }
        // Seam: inject in tests to bypass which/where.exe probe (binary → path or null)
        public static Func<string, string> WhichOverride;
#endif

        // Returns "3.12.1" or null on failure/timeout; reads both stdout+stderr (Python 2 uses stderr).
        // Cached per-session: Python version is stable within an Editor session.
        internal static string GetPythonVersion(string cmd)
        {
#if UNITY_INCLUDE_TESTS
            if (PythonVersionOverride != null) return PythonVersionOverride(cmd);
#endif
            if (_cachedPythonCmd == cmd && _cachedPythonVersion != null)
                return _cachedPythonVersion;

            try
            {
                var psi = new ProcessStartInfo(cmd, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                Task.WhenAll(outTask, errTask).Wait(PythonVersionCheckTimeoutMs);
                if (!p.WaitForExit(PythonProcessKillWaitMs)) { try { p.Kill(); } catch { } }
                var text = (outTask.IsCompleted ? outTask.Result : "") + " " + (errTask.IsCompleted ? errTask.Result : "");
                var m = Regex.Match(text, @"\d+\.\d+(?:\.\d+)?");
                var result = m.Success ? m.Value : null;
                _cachedPythonCmd = cmd;
                _cachedPythonVersion = result;
                return result;
            }
            catch { return null; }
        }

        private static bool IsVersionAtLeast(string version, int major, int minor)
        {
            var parts = version.Split('.');
            if (!int.TryParse(parts[0], out var maj)) return false;
            if (!int.TryParse(parts.Length > 1 ? parts[1] : "0", out var min)) return false;
            return maj > major || (maj == major && min >= minor);
        }

        private static string ResolvePythonCmd(string serverDir)
        {
            var win = Path.Combine(serverDir, ".venv", "Scripts", "python.exe");
            if (File.Exists(win)) return win;

            var venv = Path.Combine(serverDir, ".venv", "bin", "python");
            if (File.Exists(venv)) return venv;

            return UnityEngine.SystemInfo.operatingSystemFamily
                == UnityEngine.OperatingSystemFamily.Windows ? "python" : "python3";
        }

        // ── Server ────────────────────────────────────────────────────────────

        /// <summary>Returns running state of the MCP TCP server.</summary>
        public static (bool ok, string detail) CheckServer()
        {
            if (!MCPServer.IsRunning)
                return (false, "MCP server not running");

            return (true, $"Server on :{MCPServer.ServerPort}");
        }

        // ── uv ────────────────────────────────────────────────────────────────

        /// <summary>Returns whether uvx is on PATH, with install hint if not.</summary>
        public static (bool ok, string detail) CheckUv()
        {
            var path = WhichUvx();
            if (path != null) return (true, path);
            var hint = UnityEngine.SystemInfo.operatingSystemFamily == UnityEngine.OperatingSystemFamily.Windows
                ? "Install uv: winget install astral-sh.uv, then restart Unity"
                : "Install uv: curl -LsSf https://astral.sh/uv/install.sh | sh, then restart Unity";
            return (false, hint);
        }

        private const int UvxProbeTimeoutMs = 3000;

        // Inline which-probe for uvx — avoids Chat.CLI dependency (would create a cycle).
        // Uses ShellHelper.CreateLoginShellPsi so macOS login-shell PATH (e.g. ~/.cargo/bin) is sourced.
        private static string WhichUvx()
        {
#if UNITY_INCLUDE_TESTS
            if (WhichOverride != null) return WhichOverride("uvx");
#endif
            try
            {
                var isWin = UnityEngine.SystemInfo.operatingSystemFamily == UnityEngine.OperatingSystemFamily.Windows;
                ProcessStartInfo psi;
                if (isWin)
                {
                    psi = new ProcessStartInfo("where.exe", "uvx")
                    {
                        UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
                    };
                }
                else
                {
                    psi = ShellHelper.CreateLoginShellPsi("command -v \"$1\"", "uvx");
                    if (psi == null) return null;
                    psi.RedirectStandardError = true;
                }
                using var p = Process.Start(psi);
                if (p == null) return null;
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = isWin ? Task.FromResult("") : p.StandardError.ReadToEndAsync();
                Task.WhenAll(outTask, errTask).Wait(UvxProbeTimeoutMs);
                if (!p.WaitForExit(200)) { try { p.Kill(); } catch { } }
                var stdout = outTask.IsCompleted ? outTask.Result : "";
                if (isWin)
                {
                    var l = stdout.Split('\n')[0].Trim();
                    return string.IsNullOrEmpty(l) ? null : l;
                }
                // macOS/Linux: interactive shell banners precede the real path; pick last /… line.
                var lines = stdout.Split('\n');
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i].Trim();
                    if (line.Length > 0 && line[0] == '/') return line;
                }
                return null;
            }
            catch { return null; }
        }

        // ── Snippet ───────────────────────────────────────────────────────────

        /// <summary>Builds the <c>claude mcp add</c> command for the given port.</summary>
        public static string BuildClaudeCodeSnippet(int port)
            => $"claude mcp add unity -- env UNITY_MCP_PORT={port} uvx --from {WizardConfigWriter.GitInstallUrl} unity-biome-mcp";

        // ── Port file ─────────────────────────────────────────────────────────

        /// <summary>Checks whether ~/.unity-biome-mcp/ports/ contains at least one .port file.</summary>
        public static (bool ok, string detail) CheckPortFile()
        {
            var portsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp", "ports");
            if (!Directory.Exists(portsDir))
                return (false, "~/.unity-biome-mcp/ports not found");
            var files = Directory.GetFiles(portsDir, "*.port");
            return files.Length > 0
                ? (true, $"{files.Length} port file(s)")
                : (false, "no .port files (Unity not started yet?)");
        }

        // ── AI config ─────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether at least one AI tool config file contains a "unity-biome-mcp" entry.
        /// Returns the path of the first matching config, or an error detail.
        /// </summary>
        public static (bool ok, string detail) CheckAiConfig()
        {
            var paths = new[]
            {
                AiToolCardFactory.ClaudeCodePath(),
                AiToolCardFactory.ClaudeDesktopPath(),
                AiToolCardFactory.CursorPath(),
                AiToolCardFactory.WindsurfPath(),
            };
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                try
                {
                    if (File.ReadAllText(p).Contains($"\"{PermissionConfig.SERVER_NAME}\""))
                        return (true, p);
                }
                catch (IOException) { }
            }
            return (false, "no AI tool config found with unity-biome-mcp entry");
        }
    }
}
