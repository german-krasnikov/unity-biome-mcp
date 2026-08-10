// TDD — RED first. Tests for MentionCoordinator sort modes.
// Verifies ByRelevance (default), ByName, ByType, ByRecency, ByRecency-fallback.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionCoordinatorSortTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private class FixedSource : IMentionSource
        {
            private readonly List<MentionCandidate> _items = new List<MentionCandidate>();

            public void AddHierarchy(string name, string path, long score)
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, path, name, 0);
                _items.Add(new MentionCandidate(chip, score, "icon"));
            }

            public void AddAsset(string name, string path, long score)
            {
                var chip = new ChipData(ChipKindKeys.Scene, path, name, "");
                _items.Add(new MentionCandidate(chip, score, "icon"));
            }

            public void RefreshIfDirty() { }

            public void Search(string query, int maxResults, List<MentionCandidate> results)
            {
                foreach (var item in _items)
                    results.Add(item);
            }
        }

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Search_ByRelevance_ReturnsByScoreDesc()
        {
            var src = new FixedSource();
            src.AddHierarchy("Zebra", "/z", 100);
            src.AddHierarchy("Alpha", "/a", 300);
            src.AddHierarchy("Mid",   "/m", 200);

            var coord = new MentionCoordinator(src);
            var results = new List<MentionCandidate>();
            coord.Search("x", 10, results, MentionSortOrder.ByRelevance);

            Assert.AreEqual(300, results[0].Score);
            Assert.AreEqual(200, results[1].Score);
            Assert.AreEqual(100, results[2].Score);
        }

        [Test]
        public void Search_DefaultSortOrder_IsBackwardCompatible()
        {
            // Existing tests call Search(query, max, results) — 3-arg overload.
            // Verify default param means ByRelevance.
            var src = new FixedSource();
            src.AddHierarchy("B", "/b", 50);
            src.AddHierarchy("A", "/a", 200);

            var coord = new MentionCoordinator(src);
            var results = new List<MentionCandidate>();
            coord.Search("x", 10, results); // no sortOrder → default ByRelevance

            Assert.AreEqual(200, results[0].Score); // higher score first
        }

        [Test]
        public void Search_ByName_ReturnsCandidatesAlphabetically()
        {
            var src = new FixedSource();
            src.AddHierarchy("Zebra", "/z", 100);
            src.AddHierarchy("Alpha", "/a", 50);
            src.AddHierarchy("Mid",   "/m", 200);

            var coord = new MentionCoordinator(src);
            var results = new List<MentionCandidate>();
            coord.Search("x", 10, results, MentionSortOrder.ByName);

            Assert.AreEqual("Alpha", results[0].Chip.DisplayName);
            Assert.AreEqual("Mid",   results[1].Chip.DisplayName);
            Assert.AreEqual("Zebra", results[2].Chip.DisplayName);
        }

        [Test]
        public void Search_ByType_GroupsByKindKey_ThenAlphabetical()
        {
            // "hierarchy" < "scene" lexicographically, so hierarchy chips come first
            var src = new FixedSource();
            src.AddAsset("BScene",    "Assets/BScene.unity", 100);
            src.AddHierarchy("AHier", "/a",                  50);
            src.AddHierarchy("ZHier", "/z",                  200);
            src.AddAsset("AScene",    "Assets/AScene.unity", 150);

            var coord = new MentionCoordinator(src);
            var results = new List<MentionCandidate>();
            coord.Search("x", 10, results, MentionSortOrder.ByType);

            // hierarchy group first (kindKey "hierarchy" < "scene")
            Assert.AreEqual(ChipKindKeys.Hierarchy, results[0].Chip.KindKey);
            Assert.AreEqual(ChipKindKeys.Hierarchy, results[1].Chip.KindKey);
            // within hierarchy group, alphabetical by DisplayName
            Assert.AreEqual("AHier", results[0].Chip.DisplayName);
            Assert.AreEqual("ZHier", results[1].Chip.DisplayName);
            // scene group second
            Assert.AreEqual(ChipKindKeys.Scene, results[2].Chip.KindKey);
            Assert.AreEqual(ChipKindKeys.Scene, results[3].Chip.KindKey);
            Assert.AreEqual("AScene", results[2].Chip.DisplayName);
        }

        [Test]
        public void Search_ByRecency_WithHistory_ReturnsLatestFirst()
        {
            var src = new FixedSource();
            src.AddHierarchy("A", "/a", 100);
            src.AddHierarchy("B", "/b", 100);

            var coord = new MentionCoordinator(src);

            // Use a real MentionHistory with temp path — record /b after /a
            var tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MentionSort_{System.Guid.NewGuid():N}.json");
            try
            {
                long tick = 0;
                var history = new MentionHistory(tempPath, clock: () => ++tick);
                history.RecordCommit("/a"); // tick=1
                history.RecordCommit("/b"); // tick=2
                coord.History = history;

                var results = new List<MentionCandidate>();
                coord.Search("x", 10, results, MentionSortOrder.ByRecency);

                // /b was committed last → most recent → first
                Assert.AreEqual("/b", results[0].Chip.Path);
                Assert.AreEqual("/a", results[1].Chip.Path);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
        }

        [Test]
        public void Search_ByRecency_NoHistory_FallsBackToRelevance()
        {
            var src = new FixedSource();
            src.AddHierarchy("Low",  "/low",  50);
            src.AddHierarchy("High", "/high", 200);

            var coord = new MentionCoordinator(src);
            // coord.History is null by default

            var results = new List<MentionCandidate>();
            coord.Search("x", 10, results, MentionSortOrder.ByRecency);

            // Fallback to ByRelevance → higher score first
            Assert.AreEqual(200, results[0].Score);
            Assert.AreEqual(50,  results[1].Score);
        }
    }
}
