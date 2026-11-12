// Resolves InstallDir rows (dsl/data/installdir.data-contract, ARC-0b) to the directory whose
// mere existence signals "this client is installed" on the current OS. "~" and "{appdata}" are
// the same placeholders clients.py's _install_dir() substitutes for the identical generated rows
// -- this is the single #if site that used to be duplicated per-row in BackendDescriptor.cs.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Wizard
{
    internal static class InstallDirTable
    {
        private static readonly Dictionary<string, InstallDir> _byKey = Build();

        private static Dictionary<string, InstallDir> Build()
        {
            var map = new Dictionary<string, InstallDir>(StringComparer.Ordinal);
            foreach (var row in InstallDirRows.All)
                map[row.Key] = row;
            return map;
        }

        /// <summary>Pure resolver — no Unity/env deps, NUnit-testable across all three OS branches
        /// regardless of the editor's actual platform. null when `key` has no InstallDir row.</summary>
        internal static string ResolveFor(string key, bool isWindows, bool isMac, string home, string appData)
        {
            if (!_byKey.TryGetValue(key, out var row)) return null;
            if (isWindows) return (row.DirWin ?? row.Dir).Replace("{appdata}", appData);
            var template = isMac && row.DirMac != null ? row.DirMac : row.Dir;
            return template.Replace("~", home);
        }

        /// <summary>Production entry point: resolves `key` for the current editor OS.</summary>
        internal static string Resolve(string key)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#if UNITY_EDITOR_WIN
            var appData = Environment.GetEnvironmentVariable("APPDATA")
                ?? System.IO.Path.Combine(home, "AppData", "Roaming");
            return ResolveFor(key, isWindows: true, isMac: false, home: home, appData: appData);
#elif UNITY_EDITOR_OSX
            return ResolveFor(key, isWindows: false, isMac: true, home: home, appData: null);
#else
            return ResolveFor(key, isWindows: false, isMac: false, home: home, appData: null);
#endif
        }
    }
}
