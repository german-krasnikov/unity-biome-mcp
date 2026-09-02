using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class LevelUpPanel
    {
        const string FailureReasonClass = "lvlup-failure-reason";

        internal static VisualElement Build(VisualElement scheduleHost)
        {
            // Read UpmOperationGuard fresh on every Build() — it is SessionState-backed,
            // so a rebuild after a domain reload sees the same in-flight claim without
            // any static UI cache surviving the reload itself (ARC-10 T4).
            if (UpmOperationGuard.IsInFlight) return BuildBusy();
            if (!UpdateChecker.HasUpdate) return null;

            var fromVer = UpdateChecker.GetCurrentVersion();
            var toVer   = UpdateChecker.AvailableVersion;

            var root = new VisualElement();
            root.AddToClassList("lvlup-cta");

            var ss = MCPEditorUtils.LoadStyleSheet("Updates/LevelUpAnim.uss");
            if (ss != null) root.styleSheets.Add(ss);

            ShowIdle(root, scheduleHost, fromVer, toVer);
            return root;
        }

        static VisualElement BuildBusy()
        {
            var root = new VisualElement();
            root.AddToClassList("lvlup-cta");

            var ss = MCPEditorUtils.LoadStyleSheet("Updates/LevelUpAnim.uss");
            if (ss != null) root.styleSheets.Add(ss);

            var title = new Label($"Update to v{UpmOperationGuard.InFlightVersion} in progress…");
            title.AddToClassList("lvlup-title");
            root.Add(title);

            var sub = new Label($"{(int)UpmOperationGuard.ElapsedSeconds}s elapsed");
            sub.AddToClassList("lvlup-subtitle");
            root.Add(sub);

            return root;
        }

        /// <summary>
        /// Renders <see cref="UpmPluginUpdater.LastFailureReason"/> into <paramref name="root"/>
        /// so a failed update is explained in the panel itself, not only in the Console
        /// (ARC-10 T4). Idempotent — updates an existing label instead of duplicating it,
        /// and adds nothing when there is no reason to show.
        /// </summary>
        internal static void ShowFailureReason(VisualElement root)
        {
            var reason = UpmPluginUpdater.LastFailureReason;
            if (string.IsNullOrEmpty(reason)) return;

            var existing = root.Q<Label>(className: FailureReasonClass);
            if (existing != null) { existing.text = reason; return; }

            var label = new Label(reason);
            label.AddToClassList(FailureReasonClass);
            root.Add(label);
        }

        static void ShowIdle(VisualElement root, VisualElement scheduleHost, string from, string to)
        {
            root.Clear();
            root.AddToClassList("lvlup-cta-pulse");
            root.Add(LevelUpAnimator.BuildIdleSignal());

            var title = new Label("You can level up!");
            title.AddToClassList("lvlup-title");
            root.Add(title);

            var sub = new Label($"v{from}  →  v{to} available");
            sub.AddToClassList("lvlup-subtitle");
            root.Add(sub);

            var btn = BiomeUI.PrimaryButton(
                "Level Up!",
                () => ShowAnimating(root, scheduleHost, from, to));
            root.Add(btn);
        }

        static void ShowAnimating(VisualElement root, VisualElement scheduleHost, string from, string to)
        {
            root.Clear();
            root.RemoveFromClassList("lvlup-cta-pulse");

            var title = new Label("LEVEL UP!");
            title.AddToClassList("lvlup-title");
            root.Add(title);

            var animEl = LevelUpAnimator.Build(scheduleHost, from, to, () => ShowDone(root, scheduleHost, from, to));
            root.Add(animEl);
        }

        static void ShowDone(VisualElement root, VisualElement scheduleHost, string from, string to)
        {
            var badge = new Label($"LEVEL UP!  v{from} → v{to}");
            badge.AddToClassList("lvlup-badge");

            var btnRow = new VisualElement();
            btnRow.AddToClassList("lvlup-button-row");

            var statsBtn = BiomeUI.SecondaryButton(
                "See new stats",
                () => ShowDiff(root, scheduleHost, from, to));
            var updateBtn = BiomeUI.PrimaryButton("Update now", () => DoUpdate(root, to));

            btnRow.Add(statsBtn);
            btnRow.Add(updateBtn);

            root.Clear();
            root.Add(badge);
            root.Add(btnRow);
            BiomeParticleBurst.Emit(root);
        }

        static void ShowDiff(VisualElement root, VisualElement scheduleHost, string from, string to)
        {
            root.Clear();

            var header = new Label($"NEW IN v{to}");
            header.AddToClassList("lvlup-diff-header");
            root.Add(header);

            var sections = LoadDiff(from);
            foreach (var sec in sections)
            {
                if (!string.IsNullOrEmpty(sec.Header))
                {
                    var h = new Label(sec.Header);
                    h.AddToClassList("lvlup-diff-section-header");
                    root.Add(h);
                }
                foreach (var bullet in sec.Bullets)
                {
                    var b = new Label("+ " + bullet);
                    b.AddToClassList("lvlup-diff-bullet");
                    root.Add(b);
                }
            }

            var updateBtn = BiomeUI.PrimaryButton(
                $"Update now — v{to}",
                () => DoUpdate(root, to));
            root.Add(updateBtn);
        }

        static List<ReleaseDiff.DiffSection> LoadDiff(string fromVersion)
        {
            var path = ChangelogReader.LocatePath();
            if (path == null) return new List<ReleaseDiff.DiffSection>();
            try
            {
                var content = File.ReadAllText(path);
                var current = UpdateChecker.GetCurrentVersion();
                var entries = ChangelogReader.Parse(content, current);
                return ReleaseDiff.Compute(entries, fromVersion);
            }
            catch (Exception e) { Debug.LogWarning($"[LevelUp] Failed to load diff: {e.Message}"); return new List<ReleaseDiff.DiffSection>(); }
        }

        static void DoUpdate(VisualElement root, string to)
        {
            root.Query<Button>().ForEach(b => b.SetEnabled(false));
            UpdateDispatcher.DoUpdate(ok =>
            {
                if (!ok)
                {
                    root.Query<Button>().ForEach(b => b.SetEnabled(true));
                    ShowFailureReason(root);
                    return;
                }
                root.Clear();
                var label = new Label($"Updated to v{to}!");
                label.AddToClassList("lvlup-badge");
                root.Add(label);
            });
        }
    }
}
