// Resolves absolute paths to CLI binaries (claude, codex, uv, …).
// Platform dispatch: where.exe (Windows), bash -lic (Linux), zsh -lic (macOS).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatBinaryResolver
    {
        internal const string PrefKey          = "UnityMCP_Chat_ClaudePath";
        internal const string CodexPrefKey    = "UnityMCP_Chat_Path_codex";
        internal const string GeminiPrefKey   = "UnityMCP_Chat_Path_gemini";
        internal const string AgyPrefKey      = "UnityMCP_Chat_Path_agy";
        internal const string KimiPrefKey     = "UnityMCP_Chat_Path_kimi";
        internal const string OpenCodePrefKey = "UnityMCP_Chat_Path_opencode";

        // Per-binary negative cache: key = binary name, value = resolved path (null = not found).
        // Populated on first probe per binary; cleared by ResetCacheForTests.
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

        private const int WhichTimeoutMs     = 3000;
        private const int WhichReadTimeoutMs = 2800;

#if UNITY_INCLUDE_TESTS
        // Seam: inject in tests instead of spawning a shell (mirrors FindObjectOverride pattern)
        internal static Func<string, string> WhichOverride;
        internal static void ResetCacheForTests() { _cache.Clear(); }
#endif

        /// <summary>
        /// Returns the absolute path to the claude binary, or null if not found.
        /// Checks EditorPrefs override first, then PATH-resolves via login shell.
        /// Negative results are cached — use forceRefresh:true to bust the cache.
        /// </summary>
        internal static string Resolve(bool forceRefresh = false)
        {
            var pref = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(pref)) return pref;

            if (!forceRefresh && _cache.ContainsKey("claude")) return _cache["claude"];

            var result = WhichViaSh("claude");
            _cache["claude"] = result;
            return result;
        }

        /// <summary>
        /// Resolve an arbitrary CLI binary by name via login shell.
        /// For "claude" delegates to <see cref="Resolve()"/> to honour EditorPrefs + cache.
        /// For other binaries, checks a per-binary EditorPrefs key first, then caches result.
        /// Negative results are cached per binary name to avoid repeated shell probes.
        /// </summary>
        internal static string Resolve(string binaryName)
        {
            if (binaryName == "claude") return Resolve();

            // Per-backend EditorPrefs escape-hatch (R1: codex, etc.)
            var overrideKey  = $"UnityMCP_Chat_Path_{binaryName}";
            var overridePref = EditorPrefs.GetString(overrideKey, "");
            if (!string.IsNullOrEmpty(overridePref)) return overridePref;

            // Per-binary negative cache: probe once, then reuse result.
            if (_cache.TryGetValue(binaryName, out var cached)) return cached;

            var result = WhichViaSh(binaryName);
            _cache[binaryName] = result;
            return result;
        }

        private static string WhichViaSh(string binary)
        {
#if UNITY_INCLUDE_TESTS
            if (WhichOverride != null) return WhichOverride(binary);
#endif
            try
            {
                ProcessStartInfo psi;
                switch (SystemInfo.operatingSystemFamily)
                {
                    case OperatingSystemFamily.Windows:
                        psi = new ProcessStartInfo("where.exe", binary)
                        {
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow         = true,
                            StandardOutputEncoding = new UTF8Encoding(false),  // cp1251 safety: Cyrillic in %PATH%
                        };
                        // CWD-hijack mitigation (MITRE T1574.008)
                        psi.EnvironmentVariables["NoDefaultCurrentDirectoryInExePath"] = "1";
                        break;

                    case OperatingSystemFamily.Linux:
                        // -lic: login+interactive so ~/.bashrc (nvm/pyenv/mise) is sourced.
                        // bash -lc skips .bashrc due to non-interactive guard.
                        psi = ShellHelper.CreateLoginShellPsi("command -v \"$1\"", binary);
                        if (psi != null)
                        {
                            psi.RedirectStandardError = true;  // suppress "no job control" warning
                            psi.StandardErrorEncoding = new UTF8Encoding(false);
                        }
                        break;

                    case OperatingSystemFamily.MacOSX:
                        // -lic: login+interactive so ~/.zshrc (kimi/opencode PATH) is sourced automatically.
                        psi = ShellHelper.CreateLoginShellPsi("command -v \"$1\"", binary);
                        if (psi != null)
                        {
                            psi.RedirectStandardError = true;
                            psi.StandardErrorEncoding = new UTF8Encoding(false);
                        }
                        break;

                    default:
                        return null;
                }

                using var p = Process.Start(psi);
                if (p == null) return null;

                string result;
                var sw = Stopwatch.StartNew();
                if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
                {
                    result = PickWindowsPath(p.StandardOutput.ReadToEnd());
                }
                else
                {
                    // Linux + macOS: parallel stdout+stderr read to avoid deadlock when stderr buffer fills.
                    var stdoutTask = p.StandardOutput.ReadToEndAsync();
                    var stderrTask = p.StandardError.ReadToEndAsync();
                    Task.WhenAll(stdoutTask, stderrTask).Wait(WhichReadTimeoutMs);
                    result = PickLinuxPath(stdoutTask.IsCompleted ? stdoutTask.Result : "");
                }

                int remaining = Math.Max(0, WhichTimeoutMs - (int)sw.ElapsedMilliseconds);
                if (!p.WaitForExit(remaining)) { try { p.Kill(); } catch { } }

                // where.exe only resolves binaries on Unity's INHERITED PATH, which usually
                // lacks %APPDATA%\npm (codex/opencode/claude installed via `npm install -g`)
                // since Unity isn't launched from a login shell on Windows. Fall back to a
                // Registry PATH read for parity with the Unix login-shell resolver above.
                if (string.IsNullOrEmpty(result) && SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
                    result = WindowsPathProbe.Resolve(binary);

                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        // ── Output-parsing helpers (internal for unit tests) ─────────────────

        /// <summary>
        /// From multi-line where.exe output, return first .exe line (preferred),
        /// then first .cmd line, else null. Extensionless npm bash-shim lines are rejected.
        /// </summary>
        internal static string PickWindowsPath(string whereOutput)
        {
            string exeLine = null, cmdLine = null;
            foreach (var raw in whereOutput.Split('\n'))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (exeLine == null && line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    exeLine = line;
                if (cmdLine == null && line.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                    cmdLine = line;
            }
            return exeLine ?? cmdLine;
        }

        /// <summary>
        /// From bash -lic output, return the last line starting with '/' (the real path).
        /// Interactive .bashrc banners precede the path and don't start with '/'.
        /// </summary>
        internal static string PickLinuxPath(string stdout)
        {
            var lines = stdout.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Length > 0 && line[0] == '/') return line;
            }
            return null;
        }

    }
}
