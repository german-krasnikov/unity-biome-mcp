using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class VersionPickerPage
    {
        const string GenericFailureFallback = "UPM failed — check Console.";
        const string InProgressButtonText = "Update in progress…";
        internal const string RollingBackButtonText = "Rolling back…";

        /// <summary>
        /// User-facing outcome text for a completed rollback/align UPM call — an actionable
        /// <see cref="UpmPluginUpdater.LastFailureReason"/> on failure instead of a bare
        /// generic message (ARC-10 T4). Pure, no dialog involved, so it is unit-testable
        /// without touching a real UPM round-trip.
        /// </summary>
        internal static string FormatResultMessage(bool success) =>
            success ? "Done." : (UpmPluginUpdater.LastFailureReason ?? GenericFailureFallback);

        /// <summary>Single source for the rollback button's idle-state label — used both
        /// when building/refreshing the button and when restoring it after a completed
        /// UPM call, so the two can never drift apart.</summary>
        internal static string RollbackButtonText(string version) => $"Roll Back to v{version}";

#if UNITY_INCLUDE_TESTS
        /// <summary>Test seam: override to suppress the real Roll Back result dialog
        /// (mirrors <see cref="Chat.SceneChipProvider.DisplayDialogOverride"/>).</summary>
        internal static Action<string, string> ResultDialogOverride;
#endif

        private static void ShowResultDialog(string title, string message)
        {
#if UNITY_INCLUDE_TESTS
            if (ResultDialogOverride != null) { ResultDialogOverride(title, message); return; }
#endif
            EditorUtility.DisplayDialog(title, message, "OK");
        }

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

            // Read UpmOperationGuard fresh on every Build() — it is SessionState-backed, so a
            // rebuild after a domain reload sees the same in-flight claim without any static
            // UI cache surviving the reload itself (ARC-10 T4).
            bool inFlight = UpmOperationGuard.IsInFlight;

            Button rollbackBtn = null;
            rollbackBtn = BiomeUI.PrimaryButton(
                RollbackButtonText(dd.value),
                () => ConfirmAndRollback(dd.value, rollbackBtn));
            rollbackBtn.AddToClassList("updates-check-btn");
            dd.RegisterValueChangedCallback(e =>
            {
                if (!UpmOperationGuard.IsInFlight)
                    rollbackBtn.text = RollbackButtonText(e.newValue);
            });
            scroll.Add(rollbackBtn);
            if (inFlight)
            {
                rollbackBtn.SetEnabled(false);
                rollbackBtn.text = InProgressButtonText;
            }

            if (!coherent)
            {
                var alignBtn = BiomeUI.SecondaryButton(
                    $"Align Both to v{current}", null);
                scroll.Add(alignBtn);
                if (inFlight) alignBtn.SetEnabled(false);
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
                        EditorUtility.DisplayDialog("Align", FormatResultMessage(success), "OK");
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

        /// <summary>Puts <paramref name="rollbackBtn"/> into its in-flight visual state
        /// (disabled + <see cref="RollingBackButtonText"/>) — extracted so the immediate-
        /// feedback contract (C1 #14) is directly testable without driving a full
        /// <see cref="UpmPluginUpdater.Update"/> round-trip.</summary>
        internal static void SetRollingBackState(Button rollbackBtn)
        {
            rollbackBtn.SetEnabled(false);
            rollbackBtn.text = RollingBackButtonText;
        }

        /// <summary>Post-confirm rollback body (mirrors <see cref="LevelUpPanel"/>'s
        /// testable-shape convention): gives immediate UI feedback before the UPM
        /// round-trip, then restores the button from the completion callback whether the
        /// call resolves via a real Client.Add or the guard's synchronous busy-block.</summary>
        internal static void DoRollback(string version, Button rollbackBtn)
        {
            SetRollingBackState(rollbackBtn);
            UpmPluginUpdater.Update(version, success =>
            {
                rollbackBtn.SetEnabled(true);
                rollbackBtn.text = RollbackButtonText(version);
                ShowResultDialog("Roll Back", FormatResultMessage(success));
            });
        }

        private static void ConfirmAndRollback(string version, Button rollbackBtn)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Roll Back Plugin",
                $"Install plugin v{version} via UPM?\n\nThe per-project server pin will be aligned to v{version} automatically.",
                "Roll Back", "Cancel");
            if (!ok) return;
            // Server pin re-syncs automatically: the UPM update triggers a domain reload and
            // ProjectConfigWriter rewrites .mcp.json for the new version (version-scoped guard).
            DoRollback(version, rollbackBtn);
        }
    }
}
