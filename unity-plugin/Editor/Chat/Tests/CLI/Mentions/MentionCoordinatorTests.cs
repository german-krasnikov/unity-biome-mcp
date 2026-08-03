// TDD Phase 3: MentionCoordinator tests.
// Pure logic: merge, dedup, sort, cap, stale-ID, refresh delegation.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionCoordinatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private class MockSource : IMentionSource
        {
            private readonly List<MentionCandidate> _items = new List<MentionCandidate>();
            public bool RefreshCalled;

            public void Add(string name, string path, long score)
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, path, name, 0);
                _items.Add(new MentionCandidate(chip, score, "icon"));
            }

            public void RefreshIfDirty() => RefreshCalled = true;

            public void Search(string query, int maxResults, List<MentionCandidate> results)
            {
                for (int i = 0; i < _items.Count && results.Count < maxResults; i++)
                    results.Add(_items[i]);
            }
        }

        // 1. Merges results from multiple sources
        [Test]
        public void MergesMultipleSources()
        {
            var s1 = new MockSource();
            s1.Add("Alpha", "/alpha", 100);
            s1.Add("Beta", "/beta", 200);

            var s2 = new MockSource();
            s2.Add("Gamma", "/gamma", 150);
            s2.Add("Delta", "/delta", 50);

            var coordinator = new MentionCoordinator(s1, s2);
            var results = new List<MentionCandidate>();
            coordinator.Search("a", 10, results);

            Assert.That(results.Count, Is.EqualTo(4));
        }

        // 2. Deduplicates by path, keeps higher score
        [Test]
        public void DedupesByPath_KeepsHigherScore()
        {
            var s1 = new MockSource();
            s1.Add("A", "/shared", 100);

            var s2 = new MockSource();
            s2.Add("A2", "/shared", 300); // same path, higher score

            var coordinator = new MentionCoordinator(s1, s2);
            var results = new List<MentionCandidate>();
            coordinator.Search("a", 10, results);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Score, Is.EqualTo(300));
        }

        // 3. Sorts by Score descending
        [Test]
        public void SortsByScoreDesc()
        {
            var s1 = new MockSource();
            s1.Add("A", "/a", 100);
            s1.Add("B", "/b", 300);
            s1.Add("C", "/c", 200);

            var coordinator = new MentionCoordinator(s1);
            var results = new List<MentionCandidate>();
            coordinator.Search("a", 10, results);

            Assert.That(results[0].Score, Is.EqualTo(300));
            Assert.That(results[1].Score, Is.EqualTo(200));
            Assert.That(results[2].Score, Is.EqualTo(100));
        }

        // 4. Caps output at maxResults
        [Test]
        public void CapsAtMaxResults()
        {
            var s1 = new MockSource();
            for (int i = 0; i < 10; i++)
                s1.Add($"Item{i}", $"/item{i}", i * 10);

            var coordinator = new MentionCoordinator(s1);
            var results = new List<MentionCandidate>();
            coordinator.Search("item", 3, results);

            Assert.That(results.Count, Is.EqualTo(3));
        }

        // 5. Stale request ID: second search makes first ID non-current
        [Test]
        public void StaleRequestId_NotCurrent()
        {
            var s1 = new MockSource();
            s1.Add("X", "/x", 10);

            var coordinator = new MentionCoordinator(s1);
            var results = new List<MentionCandidate>();
            int firstId = coordinator.Search("x", 10, results);

            coordinator.Search("x", 10, results);

            Assert.That(coordinator.IsCurrent(firstId), Is.False);
        }

        // 6. RefreshIfDirty is called on every source
        [Test]
        public void RefreshIfDirty_CalledOnEachSource()
        {
            var s1 = new MockSource();
            var s2 = new MockSource();

            var coordinator = new MentionCoordinator(s1, s2);
            var results = new List<MentionCandidate>();
            coordinator.Search("a", 10, results);

            Assert.That(s1.RefreshCalled, Is.True);
            Assert.That(s2.RefreshCalled, Is.True);
        }

        // 7. Empty query returns no results without calling sources
        [Test]
        public void EmptyQuery_ReturnsEmpty()
        {
            var s1 = new MockSource();
            s1.Add("X", "/x", 10);

            var coordinator = new MentionCoordinator(s1);
            var results = new List<MentionCandidate>();
            coordinator.Search("", 10, results);

            Assert.That(results, Is.Empty);
            Assert.That(s1.RefreshCalled, Is.False);
        }
    }
}
