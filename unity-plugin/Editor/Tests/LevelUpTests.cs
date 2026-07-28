using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class LevelUpReleaseDiffTests
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
    public class LevelUpPanelTests
    {
        [SetUp]    public void SetUp()    => UpdateChecker.ResetForTest();
        [TearDown] public void TearDown() => UpdateChecker.ResetForTest();

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
    }

    [TestFixture]
    public class LevelUpAnimatorTests
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
