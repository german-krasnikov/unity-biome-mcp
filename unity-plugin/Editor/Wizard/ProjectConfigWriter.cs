// Editor-startup orchestrator: writes/refreshes per-project MCP config files for the
// currently resolved port + installed package version. See
// Plans/Install/11-phase1a-design.md for the full design.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Wizard
{
    [InitializeOnLoad]
    internal static class ProjectConfigWriter
    {
        private const string SessionKey = "UnityMCP.ProjectConfigWriter.Ran";

        static ProjectConfigWriter()
        {
            // SessionState (NOT EditorPrefs) — project-scoped, per-Editor-session, survives
            // domain reload, resets on Editor restart. Avoids EditorPrefs' cross-project
            // leakage (EditorPrefs is a per-machine registry shared by every Unity project
            // opened with this Editor install).
            if (SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += RunFromEditorState;
        }

        // Thin wrapper — supplies real Editor state to the testable core.
        internal static void RunFromEditorState()
        {
            SessionState.SetBool(SessionKey, true);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var port = MCPServer.IsRunning ? MCPServer.ServerPort : 9500; // ConfigureScreen.cs pattern
            var version = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(ProjectConfigWriter).Assembly)?.version ?? "";
            Run(projectRoot, port, version);
        }

        // Testable core — no Unity API except Debug.LogWarning (always called synchronously
        // on the main thread via delayCall, never after ConfigureAwait(false)).
        internal static void Run(string projectRoot, int port, string version)
        {
            var gitUrl = WizardConfigWriter.GitInstallUrlFor(version);
            foreach (var target in ProjectConfigTargets.All)
                WriteOne(projectRoot, target, port, version, gitUrl);
            GitignorePatcher.Apply(projectRoot, ProjectConfigTargets.All.Select(t => t.RelativePath));
        }

        internal static void WriteOne(string projectRoot, ProjectConfigTarget target, int port, string version, string gitUrl)
        {
            var path = Path.Combine(projectRoot, target.RelativePath);
            try
            {
                var exists = File.Exists(path);
                var existingText = exists ? File.ReadAllText(path, Encoding.UTF8) : "";
                var state = target.IsToml
                    ? ProjectConfigToml.Classify(existingText, port, version)
                    : ProjectConfigFormats.Classify(existingText, port, version);

                if (state == EntryState.OwnedCurrent) return; // no-op, cheapest path
                if (state == EntryState.Foreign)
                {
                    Debug.LogWarning($"unity-mcp: {target.RelativePath} has a hand-edited unity-mcp entry — skipping.");
                    return;
                }

                string content = target.IsToml
                    ? (exists ? ProjectConfigToml.Merge(existingText, port, gitUrl, version)
                              : ProjectConfigToml.BuildFresh(port, gitUrl, version))
                    : (exists ? ProjectConfigFormats.Merge(existingText, port, gitUrl, version, target.RootKey)
                              : ProjectConfigFormats.BuildFresh(port, gitUrl, version, target.RootKey));

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // Atomic write: tmp + delete + move — File.Move(overwrite:true) does not
                // exist in Unity's Mono/.NET Std 2.1 runtime.
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, content, new UTF8Encoding(false));
                if (exists) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // Read-only FS / permission denied / etc — never throw out of delayCall.
                Debug.LogWarning($"unity-mcp: could not write {target.RelativePath}: {ex.Message}");
            }
        }
    }
}
