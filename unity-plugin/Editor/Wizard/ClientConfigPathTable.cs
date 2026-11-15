// Resolves ClientConfigPath rows (dsl/data/clientconfigpath.data-contract, P1 #5) to the global
// MCP config-file path for a client. "~" and "{appdata}" are the same placeholders clients.py's
// _client_config_path() substitutes for the identical generated rows -- this is the single #if
// site that used to be duplicated per-method in AiToolCardFactory.cs.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Wizard
{
    internal static class ClientConfigPathTable
    {
        private static readonly Dictionary<string, ClientConfigPath> _byKey = Build();

        private static Dictionary<string, ClientConfigPath> Build()
        {
            var map = new Dictionary<string, ClientConfigPath>(StringComparer.Ordinal);
            foreach (var row in ClientConfigPathRows.All)
                map[row.Key] = row;
            return map;
        }

        /// <summary>Pure resolver — no Unity/env deps, NUnit-testable across all three OS branches
        /// regardless of the editor's actual platform. null when `key` has no ClientConfigPath row.</summary>
        internal static string ResolveFor(string key, bool isWindows, bool isMac, string home, string appData)
        {
            if (!_byKey.TryGetValue(key, out var row)) return null;
            if (isWindows) return (row.PathWin ?? row.Path).Replace("{appdata}", appData);
            var template = isMac && row.PathMac != null ? row.PathMac : row.Path;
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
