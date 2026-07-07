// ShellHelper — shell primitives: quoting, PSI factory, async process spawn.
// No Unity Editor API in hot path → ThreadPool-safe.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ShellHelper
    {

        /// EditorPrefs key prefix for binary path overrides.
        /// Contract: "UnityMCP_Chat_Path_{binaryName}" — shared by core + Chat.CLI.
        internal const string EditorPrefsKeyPrefix = "UnityMCP_Chat_Path_";

        /// POSIX single-quote wrapping. ' becomes '\''
        internal static string ShellQuoteSingle(string s) =>
            "'" + s.Replace("'", "'\\''") + "'";

        /// Builds zsh/bash -lic args: -lic '{script}' {shellName} '{arg}'
        internal static string BuildLoginShellArgs(string script, string arg) =>
            $"-lic {ShellQuoteSingle(script)} {GetLoginShellName()} {ShellQuoteSingle(arg)}";

        /// Cross-platform ProcessStartInfo for login-shell invocation.
        /// macOS: /bin/zsh  Linux: /bin/bash or /bin/sh  Windows: null
        internal static ProcessStartInfo CreateLoginShellPsi(string script, string arg)
        {
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.MacOSX:
                    return new ProcessStartInfo("/bin/zsh", BuildLoginShellArgs(script, arg))
                    {
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = new UTF8Encoding(false),
                    };
                case OperatingSystemFamily.Linux:
                    var shell = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
                    return new ProcessStartInfo(shell, BuildLoginShellArgs(script, arg))
                    {
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = new UTF8Encoding(false),
                    };
                default:
                    return null;
            }
        }

        /// Runs cmdline as a login-shell script, returns trimmed stdout or null.
        /// Task.Run wrapper — does NOT marshal to main thread. Caller owns timeout.
        internal static Task<string> RunViaLoginShellAsync(string cmdline, int timeoutMs)
        {
#if UNITY_INCLUDE_TESTS
            if (RunOverride != null)
                return RunOverride(cmdline, timeoutMs)
                    .ContinueWith(t =>
                    {
                        var r = t.Result;
                        return string.IsNullOrWhiteSpace(r) ? null : r.Trim();
                    });
#endif
            var psi = CreateLoginShellPsi(cmdline, "");
            if (psi == null) return Task.FromResult<string>(null);
            psi.RedirectStandardError = true;
            psi.StandardErrorEncoding = new UTF8Encoding(false);

            return Task.Run(() =>
            {
                using var proc = new Process { StartInfo = psi };
                try   { proc.Start(); }
                catch { return (string)null; }

                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                var sw = Stopwatch.StartNew();
                Task.WhenAll(stdoutTask, stderrTask).Wait(timeoutMs);
                int remaining = Math.Max(0, timeoutMs - (int)sw.ElapsedMilliseconds);
                bool exited = proc.WaitForExit(remaining);
                if (!exited) { try { proc.Kill(); } catch { } return null; }

                var stdout = stdoutTask.IsCompleted ? stdoutTask.Result : "";
                return string.IsNullOrWhiteSpace(stdout) ? null : stdout.Trim();
            });
        }

        private static string GetLoginShellName()
        {
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux)
                return File.Exists("/bin/bash") ? "bash" : "sh";
            return "zsh";
        }

        // ── Test seams ────────────────────────────────────────────────────────
#if UNITY_INCLUDE_TESTS
        internal static Func<string, int, Task<string>> RunOverride;
        internal static void ResetForTests() => RunOverride = null;
#endif
    }
}
