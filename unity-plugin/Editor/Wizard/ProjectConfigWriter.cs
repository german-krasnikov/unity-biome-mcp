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
            //
            // EditorApplication.update, not a one-shot deferred callback: a backgrounded
            // Editor (no focus/render frames — this plugin's normal MCP-driven posture)
            // keeps pumping update but does not reliably drain that older mechanism
            // (RELAY-FIX, commit 1bcc90b7), so relying on it alone could leave the
            // per-project config/pin sync unrun for the whole session.
            EditorApplication.update += RunOnce;
        }

        // Self-unsubscribing one-shot — fires on the next Editor tick regardless of window
        // focus, then removes itself. RunFromEditorState's own SessionState version-guard
        // below keeps this idempotent per version-per-session, so this is a pure
        // trigger-mechanism swap, not a behavior change. Internal so tests can invoke it
        // directly without waiting for a real Editor tick.
        internal static void RunOnce()
        {
            EditorApplication.update -= RunOnce;
            RunFromEditorState();
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
        // Always called synchronously on the main thread via the EditorApplication.update tick.
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
                    SetLastSyncedVersion(projectRoot, target.Key, version);
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
                    var baseline = GetLastSyncedVersion(projectRoot, target.Key);

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
                SetLastSyncedVersion(projectRoot, target.Key, version);
            }
            catch (Exception ex)
            {
                // Read-only FS / permission denied / etc — never throw out of the Editor-tick trigger.
                Debug.LogWarning($"unity-biome-mcp: could not write {target.RelativePath}: {ex.Message}");
            }
        }

        // C1 round2 #1: the separator between projectRoot and targetKey lives in one
        // named place — never a bare literal repeated at each call site.
        private const string TargetKeySeparator = ":";

        // ARC-11 T2: EditorPrefs, not SessionState — must survive Editor restart
        // (the "reboot" reported in P7 is exactly when SessionState resets).
        // Keyed by the raw projectRoot string — string.GetHashCode() is
        // process-randomized since .NET Core and would rotate the key every launch.
        // C1 round2 #1: also keyed by targetKey — the baseline used to be shared
        // across all enabled targets (claude-code, cursor, ...), so writing the
        // first target in Run()'s foreach clobbered the baseline before the next
        // target's own on-disk marker was checked against it, false-pinning every
        // target after the first on any version bump. internal (not private) so
        // the test fixture builds the exact same key instead of re-deriving it.
        internal static string LastSyncedVersionKey(string projectRoot, string targetKey) =>
            PrefKeys.LastSyncedVersionPrefix + projectRoot + TargetKeySeparator + targetKey;

        // R2-01: the pre-C1-r2-#1 shared key (projectRoot only, no target suffix) -- kept
        // as a fallback source so an existing user's real drift baseline isn't silently
        // discarded on the first run after the per-target scheme shipped. internal (not
        // private) so the test fixture seeds/protects the exact same key.
        internal static string LegacyLastSyncedVersionKey(string projectRoot) =>
            PrefKeys.LastSyncedVersionPrefix + projectRoot;

        // A per-target baseline always wins once recorded. Only when it has never been
        // written (empty) do we fall back to the legacy shared key, so a user upgrading
        // onto the per-target scheme keeps their real drift baseline for exactly one run
        // instead of it being misread as "no baseline yet" and silently overwritten.
        private static string GetLastSyncedVersion(string projectRoot, string targetKey)
        {
            var perTarget = EditorPrefs.GetString(LastSyncedVersionKey(projectRoot, targetKey), "");
            if (!string.IsNullOrEmpty(perTarget)) return perTarget;
            return EditorPrefs.GetString(LegacyLastSyncedVersionKey(projectRoot), "");
        }

        private static void SetLastSyncedVersion(string projectRoot, string targetKey, string version) =>
            EditorPrefs.SetString(LastSyncedVersionKey(projectRoot, targetKey), version);
    }
}
