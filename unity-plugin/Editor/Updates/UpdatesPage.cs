using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class UpdatesPage
    {
        internal static VisualElement Build(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.AddToClassList("biome-page");
            page.Add(SettingsPageFactory.BackHeader("Updates", onBack));
            var header = UpdatesHeaderAnim.Build(page);
            page.Add(header);

            var scroll = new ScrollView();
            scroll.AddToClassList("biome-page-scroll");

            var bannerSlot = new VisualElement();
            bannerSlot.AddToClassList("updates-banner-slot");
            var levelUp = LevelUpPanel.Build(scheduleHost: scroll);
            if (levelUp != null) bannerSlot.Add(levelUp);
            scroll.Add(bannerSlot);

            var checkStatus = BiomeUI.StatusLabel();
            scroll.Add(checkStatus);

            var checkBtn = BiomeUI.PrimaryButton("Check for Updates", null);
            checkBtn.AddToClassList("updates-check-btn");
            bool ownsActiveCheck = false;

            void Refresh()
            {
                bool checking = UpdateChecker.IsChecking;
                checkBtn.SetEnabled(!checking);
                checkBtn.text = checking ? "Checking..." : "Check for Updates";
                UpdatesHeaderAnim.SetChecking(header, checking);

                bannerSlot.Clear();
                var newLevelUp = LevelUpPanel.Build(scheduleHost: scroll);
                if (newLevelUp != null)
                    bannerSlot.Add(newLevelUp);

                if (checking)
                    BiomeUI.SetStatus(checkStatus, "Checking GitHub releases...", "warning");
                else if (!string.IsNullOrEmpty(UpdateChecker.LastError))
                    BiomeUI.SetStatus(checkStatus, UpdateChecker.LastError, "error");
                else if (UpdateChecker.HasUpdate)
                    BiomeUI.SetStatus(
                        checkStatus,
                        $"Version {UpdateChecker.AvailableVersion} is available.",
                        "success");
                else
                    BiomeUI.SetStatus(
                        checkStatus,
                        $"Version {UpdateChecker.GetCurrentVersion()} is up to date.",
                        "neutral");
            }

            void OnCheckCompleted()
            {
                ownsActiveCheck = false;
                if (page.panel != null)
                    Refresh();
            }

            checkBtn.clicked += () =>
            {
                ownsActiveCheck = true;
                UpdateChecker.ForceCheckAsync();
                Refresh();
            };
            UpdateChecker.CheckCompleted += OnCheckCompleted;
            page.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                UpdateChecker.CheckCompleted -= OnCheckCompleted;
                if (ownsActiveCheck)
                    UpdateChecker.CancelActiveCheck();
            });
            Refresh();
            scroll.Add(checkBtn);

            var changelogArea = new VisualElement();
            changelogArea.AddToClassList("updates-changelog");
            BuildChangelogEntries(changelogArea);
            scroll.Add(changelogArea);

            page.Add(scroll);
            return page;
        }

        private static void BuildChangelogEntries(VisualElement parent)
        {
            var path = ChangelogReader.LocatePath();
            if (path == null) { parent.Add(new Label("Changelog not found.")); return; }

            string content;
            try { content = File.ReadAllText(path); }
            catch { parent.Add(new Label("Could not read changelog.")); return; }

            var current = UpdateChecker.GetCurrentVersion();
            var entries = ChangelogReader.Parse(content, current);

            var foldouts = new List<VisualElement>();
            foreach (var entry in entries)
            {
                var header = string.IsNullOrEmpty(entry.Date)
                    ? entry.Version
                    : $"{entry.Version} — {entry.Date}";
                var foldout = new Foldout { text = header, value = entry.IsNewer };
                if (entry.IsNewer) foldout.AddToClassList("updates-entry-newer");

                var body = new Label(MarkdownInlineFormatter.ToRichText(entry.Content));
                body.enableRichText = true;
                body.AddToClassList("updates-entry-body");
                foldout.Add(body);
                parent.Add(foldout);
                foldouts.Add(foldout);
            }
            ArcadeAnim.StaggerFadeIn(foldouts, 60);
        }
    }
}
