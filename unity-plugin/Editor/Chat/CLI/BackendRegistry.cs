// Pure discovery logic — System.IO only, zero UnityEngine deps. NUnit-testable.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal static class BackendRegistry
    {
        internal static List<BackendSpec> Discover(IEnumerable<string> agentDirs)
        {
            var result = new List<BackendSpec> { new BackendSpec("Claude", null, true, BackendKind.Claude) };
            // Dedup: block agent .md files from colliding with reserved names.
            var seen   = new HashSet<string>(BackendKindValues.Names, StringComparer.Ordinal);

            foreach (var dir in agentDirs)
                AddFromDir(dir, result, seen);

            result.Add(new BackendSpec("Codex",    null, true, BackendKind.Codex));
            result.Add(new BackendSpec("Antigravity", null, true, BackendKind.Antigravity));
            result.Add(new BackendSpec("Kimi",     null, true, BackendKind.Kimi));
            result.Add(new BackendSpec("OpenCode", null, true, BackendKind.OpenCode));
            return result;
        }

        private static void AddFromDir(string dir, List<BackendSpec> result, HashSet<string> seen)
        {
            string[] files;
            try { files = string.IsNullOrEmpty(dir) ? Array.Empty<string>() : Directory.GetFiles(dir, "*.md"); }
            catch { return; }

            foreach (var path in files)
            {
                string text;
                try { text = File.ReadAllText(path); } catch { continue; }

                var stem = Path.GetFileNameWithoutExtension(path);
                var name = AgentFrontmatterParser.ParseName(text, stem);

                if (!seen.Add(name)) continue; // dedup (project wins if added first)
                result.Add(new BackendSpec(name, name, true, BackendKind.Claude));
            }
        }
    }
}
