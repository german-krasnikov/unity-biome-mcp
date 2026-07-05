using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class VersionPickerPage
    {
        internal static VisualElement Build(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.Add(SettingsPageFactory.BackHeader("Version Picker", onBack));

            var current   = UpdateChecker.GetCurrentVersion();
            var serverRef = VersionCoherenceChecker.GetServerPinnedRef();
            var coherent  = VersionCoherenceChecker.IsCoherent(current, serverRef);

            var statusLabel = new Label(BuildStatusText(current, serverRef, coherent));
            statusLabel.AddToClassList("info-label");
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            page.Add(statusLabel);

            var versions = BuildVersionList();
            if (versions.Count == 0)
            {
                page.Add(new Label("Changelog not found.") { name = "no-changelog" });
                return page;
            }

            var dd = new DropdownField(versions, 0);
            dd.AddToClassList("sampling-backend-dd");
            page.Add(dd);

            var noteLabel = new Label(GetVersionNote(versions[0]));
            noteLabel.AddToClassList("info-label");
            noteLabel.style.whiteSpace = WhiteSpace.Normal;
            page.Add(noteLabel);
            dd.RegisterValueChangedCallback(e => noteLabel.text = GetVersionNote(e.newValue));

            var rollbackBtn = new Button(() => ConfirmAndRollback(dd.value, page))
                { text = $"Roll Back to v{dd.value}" };
            rollbackBtn.AddToClassList("updates-check-btn");
            dd.RegisterValueChangedCallback(e => rollbackBtn.text = $"Roll Back to v{e.newValue}");
            page.Add(rollbackBtn);

            if (!coherent)
            {
                var alignBtn = new Button(() => AlignBoth(current)) { text = $"Align Both to v{current}" };
                alignBtn.AddToClassList("nav-back-btn");
                page.Add(alignBtn);
            }

            return page;
        }

        internal static List<string> BuildVersionList()
        {
            var path = ChangelogReader.LocatePath();
            if (path == null) return new List<string>();
            try
            {
                var content = File.ReadAllText(path);
                var entries = ChangelogReader.Parse(content, "0.0.0");
                var result  = new List<string>();
                foreach (var e in entries)
                    if (e.Version != "Unreleased") result.Add(e.Version);
                return result;
            }
            catch { return new List<string>(); }
        }

        private static string BuildStatusText(string current, string serverRef, bool coherent)
        {
            if (coherent && serverRef == null)
                return $"Plugin: v{current} | Server: unpinned (HEAD). In sync.";
            if (coherent)
                return $"Plugin + Server: v{current}. In sync.";
            return $"⚠ Server pinned to v{serverRef}, Plugin is v{current}. Use 'Align Both'.";
        }

        private static string GetVersionNote(string version)
        {
            var path = ChangelogReader.LocatePath();
            if (path == null) return "";
            try
            {
                var content = File.ReadAllText(path);
                var entries = ChangelogReader.Parse(content, "0.0.0");
                foreach (var e in entries)
                    if (e.Version == version && !string.IsNullOrEmpty(e.Date))
                        return $"Released: {e.Date}";
            }
            catch { }
            return "";
        }

        private static void ConfirmAndRollback(string version, VisualElement page)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Roll Back Plugin",
                $"Install plugin v{version} via UPM?\n\nThe per-project server pin will be aligned to v{version} automatically.",
                "Roll Back", "Cancel");
            if (!ok) return;
            // Server pin re-syncs automatically: the UPM update triggers a domain reload and
            // ProjectConfigWriter rewrites .mcp.json for the new version (version-scoped guard).
            UpmPluginUpdater.Update(version, success =>
            {
                EditorUtility.DisplayDialog("Roll Back",
                    success ? "Done." : "UPM failed — check Console.", "OK");
            });
        }

        private static void AlignBoth(string version)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Align Both to v" + version,
                $"Install plugin v{version} via UPM and pin the per-project server to v{version}?",
                "Align Both", "Cancel");
            if (!ok) return;
            // Server pin re-syncs automatically on the post-update domain reload
            // (ProjectConfigWriter, version-scoped guard).
            UpmPluginUpdater.Update(version);
        }
    }
}
