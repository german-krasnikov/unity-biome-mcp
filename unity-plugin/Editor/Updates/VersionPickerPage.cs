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
            page.AddToClassList("biome-page");
            page.Add(SettingsPageFactory.BackHeader("Version Picker", onBack));
            var header = EcosystemHeaderAnim.BuildVersions();
            page.Add(header);

            var current   = UpdateChecker.GetCurrentVersion();
            var serverRef = VersionCoherenceChecker.GetServerPinnedRef();
            var coherent  = VersionCoherenceChecker.IsCoherent(current, serverRef);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("biome-page-scroll");
            page.Add(scroll);

            var statusLabel = BiomeUI.StatusLabel();
            BiomeUI.SetStatus(
                statusLabel,
                BuildStatusText(current, serverRef, coherent),
                coherent ? "success" : "warning");
            scroll.Add(statusLabel);

            var versions = BuildVersionList();
            if (versions.Count == 0)
            {
                var empty = BiomeUI.StatusLabel("Changelog not found.");
                empty.name = "no-changelog";
                scroll.Add(empty);
                return page;
            }

            var dd = new DropdownField(versions, 0);
            dd.AddToClassList("sampling-backend-dd");
            scroll.Add(dd);
            EcosystemHeaderAnim.SetVersionIndex(header, 0, versions.Count);

            var noteLabel = new Label(GetVersionNote(versions[0]));
            noteLabel.AddToClassList("info-label");
            scroll.Add(noteLabel);
            dd.RegisterValueChangedCallback(e =>
            {
                noteLabel.text = GetVersionNote(e.newValue);
                EcosystemHeaderAnim.SetVersionIndex(header, versions.IndexOf(e.newValue), versions.Count);
            });

            var rollbackBtn = BiomeUI.PrimaryButton(
                $"Roll Back to v{dd.value}",
                () => ConfirmAndRollback(dd.value, page));
            rollbackBtn.AddToClassList("updates-check-btn");
            dd.RegisterValueChangedCallback(e => rollbackBtn.text = $"Roll Back to v{e.newValue}");
            scroll.Add(rollbackBtn);

            if (!coherent)
            {
                var alignBtn = BiomeUI.SecondaryButton(
                    $"Align Both to v{current}", null);
                scroll.Add(alignBtn);
                alignBtn.clicked += () =>
                {
                    bool ok = EditorUtility.DisplayDialog(
                        "Align Both to v" + current,
                        $"Install plugin v{current} via UPM and pin the per-project server to v{current}?",
                        "Align Both", "Cancel");
                    if (!ok) return;
                    alignBtn.SetEnabled(false);
                    alignBtn.text = "Aligning...";
                    UpmPluginUpdater.Update(current, success =>
                    {
                        alignBtn.SetEnabled(true);
                        alignBtn.text = $"Align Both to v{current}";
                        EditorUtility.DisplayDialog("Align",
                            success ? "Done." : "UPM failed — check Console.", "OK");
                    });
                };
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
    }
}
