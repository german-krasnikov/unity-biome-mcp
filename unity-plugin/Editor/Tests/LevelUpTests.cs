using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class LevelUpReleaseDiffTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        static List<ChangelogReader.Entry> Entries(params (string ver, string content)[] items)
        {
            var list = new List<ChangelogReader.Entry>();
            foreach (var (ver, content) in items)
                list.Add(new ChangelogReader.Entry { Version = ver, Content = content, IsNewer = true });
            return list;
        }

        [Test]
        public void ReleaseDiff_EmptyEntries_ReturnsEmpty()
        {
            var result = ReleaseDiff.Compute(new List<ChangelogReader.Entry>(), "0.42.0");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ReleaseDiff_FromVersion_Exclusive()
        {
            var entries = Entries(("0.42.0", "- Same version line"));
            var result = ReleaseDiff.Compute(entries, "0.42.0");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ReleaseDiff_ReturnsOnlyNewerEntries()
        {
            var entries = Entries(("0.43.0", "- New thing"), ("0.42.0", "- Old thing"));
            var result = ReleaseDiff.Compute(entries, "0.42.0");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Bullets.Count);
            Assert.AreEqual("New thing", result[0].Bullets[0]);
        }

        [Test]
        public void ReleaseDiff_BulletLinesExtracted()
        {
            var entries = Entries(("0.43.0", "- Fix crash\n- Add feature"));
            var result = ReleaseDiff.Compute(entries, "0.42.0");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].Bullets.Count);
            CollectionAssert.Contains(result[0].Bullets, "Fix crash");
            CollectionAssert.Contains(result[0].Bullets, "Add feature");
        }

        [Test]
        public void ReleaseDiff_ParsesBoldSectionHeaders()
        {
            var content = "**Crash Prevention:**\n- Remove tundra\n**Hardening:**\n- Wizard split";
            var entries = Entries(("0.43.0", content));
            var result = ReleaseDiff.Compute(entries, "0.42.0");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Crash Prevention", result[0].Header);
            Assert.AreEqual(1, result[0].Bullets.Count);
            Assert.AreEqual("Hardening", result[1].Header);
            Assert.AreEqual(1, result[1].Bullets.Count);
        }

        [Test]
        public void ReleaseDiff_HandlesMissingHeaders()
        {
            var entries = Entries(("0.43.0", "- Line one\n- Line two"));
            var result = ReleaseDiff.Compute(entries, "0.42.0");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("", result[0].Header);
            Assert.AreEqual(2, result[0].Bullets.Count);
        }
    }

    [TestFixture]
    public class LevelUpPanelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // UpmOperationGuard's SessionState keys and UpmPluginUpdater.LastFailureReason
        // are outside UnityMcpTestBase's known isolations (ARC-10 T1/T3) — reset
        // explicitly so seeded in-flight/failure state can't bleed either way (ARC-10 T4).
        [SetUp]
        public void SetUpGuard()
        {
            UpmOperationGuard.Complete();
            UpmPluginUpdater.LastFailureReason = null;
        }

        [TearDown]
        public void TearDownGuard()
        {
            UpmOperationGuard.Complete();
            UpmPluginUpdater.LastFailureReason = null;
        }

        [Test]
        public void LevelUpPanel_Build_ReturnsNull_WhenNoUpdate()
        {
            var el = LevelUpPanel.Build(new VisualElement());
            Assert.IsNull(el);
        }

        [Test]
        public void LevelUpPanel_Build_ReturnsElement_WhenUpdateAvailable()
        {
            UpdateChecker.SetAvailableVersionForTest("9.99.0");
            var el = LevelUpPanel.Build(new VisualElement());
            Assert.IsNotNull(el);
        }

        [Test]
        public void LevelUpPanel_Build_ShowsBusyState_NoButton_WhenGuardInFlight()
        {
            UpdateChecker.SetAvailableVersionForTest("9.99.0");
            UpmOperationGuard.TryBegin("9.99.0");

            var el = LevelUpPanel.Build(new VisualElement());

            Assert.IsNotNull(el, "In-flight state must still render a panel.");
            Assert.IsNull(el.Q<Button>(), "In-flight state must not expose a clickable button.");
        }

        // Simulates a rebuild after a domain reload: the guard (SessionState) is still
        // claimed, but nothing in-memory remembers this — a fresh Build() call must read
        // UpmOperationGuard directly, never a static UI cache, to restore the same state.
        [Test]
        public void LevelUpPanel_Build_ShowsBusyState_SurvivesRebuildWhileGuardStillInFlight()
        {
            UpdateChecker.SetAvailableVersionForTest("9.99.0");
            UpmOperationGuard.TryBegin("9.99.0");
            LevelUpPanel.Build(new VisualElement()); // pre-reload build

            var rebuilt = LevelUpPanel.Build(new VisualElement()); // post-reload build

            Assert.IsNotNull(rebuilt);
            Assert.IsNull(rebuilt.Q<Button>(), "Rebuilt in-flight state must still show no button.");
        }

        [Test]
        public void LevelUpPanel_ShowFailureReason_RendersLastFailureReasonText()
        {
            UpmPluginUpdater.LastFailureReason =
                "Could not reach GitHub. Check your network connection and try again.";
            var root = new VisualElement();

            LevelUpPanel.ShowFailureReason(root);

            var label = root.Q<Label>(className: "lvlup-failure-reason");
            Assert.IsNotNull(label, "Expected a rendered failure-reason label.");
            Assert.AreEqual(UpmPluginUpdater.LastFailureReason, label.text);
        }

        [Test]
        public void LevelUpPanel_ShowFailureReason_NoReason_AddsNoLabel()
        {
            UpmPluginUpdater.LastFailureReason = null;
            var root = new VisualElement();

            LevelUpPanel.ShowFailureReason(root);

            Assert.IsNull(root.Q<Label>(className: "lvlup-failure-reason"));
        }
    }

    [TestFixture]
    public class LevelUpAnimatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void LevelUpAnimator_Build_ReturnsVisualElement()
        {
            var host = new VisualElement();
            var el = LevelUpAnimator.Build(host, "0.42.0", "0.43.0", () => { });
            Assert.IsNotNull(el);
        }

        [Test]
        public void LevelUpAnimator_Tree_HasXpFill()
        {
            var host = new VisualElement();
            var el = LevelUpAnimator.Build(host, "0.42.0", "0.43.0", () => { });
            var fill = el.Q(className: "lvlup-xp-fill");
            Assert.IsNotNull(fill, "Expected lvlup-xp-fill element");
        }

        [Test]
        public void LevelUpAnimator_Tree_HasSparkContainer_With5Children()
        {
            var host = new VisualElement();
            var el = LevelUpAnimator.Build(host, "0.42.0", "0.43.0", () => { });
            var container = el.Q(className: "lvlup-spark-container");
            Assert.IsNotNull(container, "Expected lvlup-spark-container");
            Assert.AreEqual(5, container.childCount);
        }

        [Test]
        public void LevelUpAnimator_Tree_HasLiftArrowAndLayeredAura()
        {
            var el = LevelUpAnimator.Build(
                new VisualElement(),
                "0.42.0",
                "0.43.0",
                () => { });

            Assert.IsNotNull(el.Q<Label>(className: "lvlup-symbol-arrow"));
            Assert.AreEqual(
                2,
                el.Query<VisualElement>(className: "lvlup-symbol-aura").ToList().Count);
        }

        [Test]
        public void BuildIdleSignal_HasLifecycleBoundLiftSymbol()
        {
            var signal = LevelUpAnimator.BuildIdleSignal();

            Assert.IsTrue(signal.ClassListContains("lvlup-idle-signal"));
            Assert.IsNotNull(signal.Q<Label>(className: "lvlup-symbol-arrow"));
        }

        [Test]
        public void LevelUpAnimator_OnComplete_InvokedExactlyOnce()
        {
            // Scheduler does not tick while detached, so drive the completion seam.
            int callCount = 0;
            var host = new VisualElement();
            LevelUpAnimator.Build(host, "0.42.0", "0.43.0", () => callCount++);

            // Simulate reaching TotalTicks via the exposed test helper.
            LevelUpAnimator.SimulateCompletion();

            Assert.AreEqual(1, callCount, "onComplete must fire exactly once");
        }
    }
}
