// Windows Registry PATH fallback for ChatBinaryResolver.WhichViaSh(). where.exe only sees
// Unity's INHERITED PATH, which usually lacks %APPDATA%\npm (codex/opencode/claude installed
// via `npm install -g`) since Unity isn't launched from a login shell on Windows. This reads
// PATH from HKCU/HKLM Environment registry keys — the same PATH a freshly opened terminal
// would see after re-login — plus a handful of well-known install dirs that don't always
// land in the registry PATH (npm global, cargo, uv, scoop, winget). Gives Windows PATH-parity
// with the Unix login-shell resolver (`bash/zsh -lic`) in ChatBinaryResolver.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal static class WindowsPathProbe
    {
        private static readonly string[] ExeExtensions = { ".exe", ".cmd" };

        internal static string Resolve(string binary)
        {
#if UNITY_EDITOR_WIN
            try
            {
                var dirs = new List<string>();
                AddRegistryPathDirs(dirs, Microsoft.Win32.Registry.CurrentUser, "Environment");
                AddRegistryPathDirs(dirs, Microsoft.Win32.Registry.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");

                dirs.Add(Environment.ExpandEnvironmentVariables(@"%APPDATA%\npm"));
                dirs.Add(Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\.cargo\bin"));
                dirs.Add(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\uv\bin"));
                dirs.Add(Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\scoop\shims"));
                dirs.Add(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\WinGet\Links"));

                return ProbeDirs(dirs, binary);
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

#if UNITY_EDITOR_WIN
        private static void AddRegistryPathDirs(List<string> dirs, Microsoft.Win32.RegistryKey root, string subKey)
        {
            try
            {
                using var key = root.OpenSubKey(subKey);
                // DoNotExpandEnvironmentNames: read the raw string (may itself contain %X% refs,
                // e.g. "%SystemRoot%\system32"), then expand once ourselves below.
                var raw = key?.GetValue("PATH", null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (string.IsNullOrEmpty(raw)) return;
                foreach (var dir in raw.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    dirs.Add(Environment.ExpandEnvironmentVariables(dir.Trim()));
                }
            }
            catch { /* key missing or inaccessible — skip this hive */ }
        }
#endif

        /// <summary>
        /// Pure directory-probe: for each dir, checks "&lt;dir&gt;/&lt;binary&gt;.exe" then
        /// "&lt;dir&gt;/&lt;binary&gt;.cmd", returning the first hit. OS-agnostic (File.Exists
        /// only) so it's unit-testable on any editor platform without a real Windows registry.
        /// Internal for unit tests.
        /// </summary>
        internal static string ProbeDirs(IEnumerable<string> dirs, string binary)
        {
            if (dirs == null) return null;
            foreach (var dir in dirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var ext in ExeExtensions)
                {
                    var candidate = Path.Combine(dir, binary + ext);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }
    }
}
