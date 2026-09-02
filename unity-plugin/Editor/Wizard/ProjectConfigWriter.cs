// Editor-startup orchestrator: writes/refreshes per-project MCP config files for the
// currently resolved port + installed package version. See
// Plans/Install/11-phase1a-design.md for the full design.
using System;
using System.Collections.Generic;
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
            // Always schedule — the per-version guard lives in RunFromEditorState, where
            // PackageInfo.version is available (it is not in the static ctor). A plugin update
            // triggers a domain reload; the new version yields a fresh key, so the config is
            // rewritten for the new version without any cross-assembly call from UpdateDispatcher.
            EditorApplication.delayCall += RunFromEditorState;
        }

        // Thin wrapper — supplies real Editor state to the testable core.
        internal static void RunFromEditorState()
        {
            var version = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(ProjectConfigWriter).Assembly)?.version ?? "";

            // SessionState (NOT EditorPrefs) — project-scoped, per-Editor-session, resets on
            // Editor restart. Key is version-scoped so an in-session plugin update re-syncs the
            // server pin instead of being skipped as "already ran".
            var key = SessionKey + ":" + version;
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var port = MCPServer.IsRunning ? MCPServer.ServerPort : 9500; // ConfigureScreen.cs pattern
            Run(projectRoot, port, version);
        }

        // Testable core — uses EditorPrefs via AgentConfigPrefs when enabledKeys is null.
        // Always called synchronously on the main thread via delayCall.
        // enabledKeys: injected by tests; null means read from AgentConfigPrefs (production path).
        internal static void Run(string projectRoot, int port, string version,
            IEnumerable<string> enabledKeys = null)
        {
            if (enabledKeys == null)
            {
                if (AgentConfigPrefs.IsFirstRun)
                    AgentConfigPrefs.InitializeFromDetected(AgentConfigPrefs.DetectInstalled());
                enabledKeys = AgentConfigPrefs.GetEnabledKeys();
            }

            var keys = new HashSet<string>(enabledKeys);
            var gitUrl = WizardConfigWriter.GitInstallUrlFor(version);
            var active = GetActiveTargets(projectRoot, keys).ToList();
            foreach (var target in active)
                WriteOne(projectRoot, target, port, version, gitUrl);
            GitignorePatcher.Apply(projectRoot, active.Select(t => t.RelativePath));
        }

        // Pure helper: include target only if its key is in enabledKeys (user opted in).
        // ARC-0b T4 / ARC-14 T2 (ARC-19 §3 row 34): file-exists bypass removed — a
        // pre-existing file for a disabled key is left alone (WriteOne never deletes,
        // it is simply not visited). No Unity API, no EditorPrefs.
        internal static IEnumerable<ProjectConfigTarget> GetActiveTargets(
            string projectRoot, HashSet<string> enabledKeys)
        {
            foreach (var target in ProjectConfigTargets.All)
            {
                if (enabledKeys != null && enabledKeys.Contains(target.Key))
                    yield return target;
            }
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

                if (state == EntryState.OwnedCurrent)
                {
                    // ARC-11 T2: stamp the baseline here too (not only after a Merge
                    // below) so an already-synced project has one recorded before the
                    // next real version bump, instead of only after the first drift.
                    SetLastSyncedVersion(projectRoot, version);
                    return; // no-op, cheapest path
                }
                if (state == EntryState.Foreign)
                {
                    Debug.Log($"{BiomeLabel.Tag} Adopting hand-edited entry in {target.RelativePath} (adding version marker).");
                    var adopted = target.IsToml
                        ? ProjectConfigToml.Adopt(existingText, version)
                        : ProjectConfigFormats.Adopt(existingText, version);
                    if (ReferenceEquals(adopted, existingText)) return; // entry not found — leave intact
                    var adoptTmp = path + ".tmp";
                    File.WriteAllText(adoptTmp, adopted, new UTF8Encoding(false));
                    if (exists) File.Delete(path);
                    File.Move(adoptTmp, path);
                    return;
                }

                if (state == EntryState.OwnedStale)
                {
                    var marker = target.IsToml
                        ? ProjectConfigToml.ExtractMarkerVersion(existingText)
                        : ProjectConfigFormats.ExtractMarkerVersion(existingText);
                    var baseline = GetLastSyncedVersion(projectRoot);

                    // ARC-11 T2 (P7 regression): the on-disk marker no longer matches
                    // the version WE last wrote, yet the entry isn't flagged "_pin" —
                    // a hand-edit happened outside the CLI (install.py version --set /
                    // --unpin). Pin it instead of silently reverting the user's edit.
                    // An empty or matching baseline means this is genuinely our own
                    // stale write (or the first time we've ever recorded one) — fall
                    // through to the normal overwrite below.
                    if (!string.IsNullOrEmpty(baseline) && baseline != marker)
                    {
                        var pinned = target.IsToml
                            ? ProjectConfigToml.Pin(existingText)
                            : ProjectConfigFormats.Pin(existingText);
                        if (!ReferenceEquals(pinned, existingText))
                        {
                            var pinTmp = path + ".tmp";
                            File.WriteAllText(pinTmp, pinned, new UTF8Encoding(false));
                            File.Delete(path);
                            File.Move(pinTmp, path);
                        }
                        return;
                    }
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

                // ARC-11 T2: record the version WE just wrote — the baseline the next
                // run compares the on-disk marker against to detect a foreign edit.
                SetLastSyncedVersion(projectRoot, version);
            }
            catch (Exception ex)
            {
                // Read-only FS / permission denied / etc — never throw out of delayCall.
                Debug.LogWarning($"unity-biome-mcp: could not write {target.RelativePath}: {ex.Message}");
            }
        }

        // ARC-11 T2: EditorPrefs, not SessionState — must survive Editor restart
        // (the "reboot" reported in P7 is exactly when SessionState resets).
        // Keyed by the raw projectRoot string — string.GetHashCode() is
        // process-randomized since .NET Core and would rotate the key every launch.
        private static string GetLastSyncedVersion(string projectRoot) =>
            EditorPrefs.GetString(PrefKeys.LastSyncedVersionPrefix + projectRoot, "");

        private static void SetLastSyncedVersion(string projectRoot, string version) =>
            EditorPrefs.SetString(PrefKeys.LastSyncedVersionPrefix + projectRoot, version);
    }
}
