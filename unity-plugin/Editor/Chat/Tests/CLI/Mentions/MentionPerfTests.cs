// TDD Phase 6: Performance tests for @mention subsystem.
// Validates the system handles large datasets without catastrophic slowdown.
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionPerfTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Shared mock — mirrors MentionCoordinatorTests.MockSource
        private class PerfMockSource : IMentionSource
        {
            private readonly List<MentionCandidate> _items = new List<MentionCandidate>();

            public void Add(string name, string path, long score)
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, path, name, 0);
                _items.Add(new MentionCandidate(chip, score, "icon"));
            }

            public void RefreshIfDirty() { }

            public void Search(string query, int maxResults, List<MentionCandidate> results)
            {
                for (int i = 0; i < _items.Count && results.Count < maxResults; i++)
                    results.Add(_items[i]);
            }
        }

        // 1. Scoring 1000 candidates stays under 5ms
        [Test]
        public void FuzzyScorer_1000Candidates_Under5ms()
        {
            const int N = 1000;
            var candidates = new string[N];
            var masks = new uint[N];
            for (int i = 0; i < N; i++)
            {
                candidates[i] = $"Object_{i}";
                masks[i] = MentionFuzzyScorer.BuildCharMask(candidates[i].ToLowerInvariant());
            }

            uint queryMask = MentionFuzzyScorer.BuildCharMask("obj");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                if (!MentionFuzzyScorer.PassesPreFilter(queryMask, masks[i])) continue;
                var lower = candidates[i].ToLowerInvariant();
                MentionFuzzyScorer.Score("obj", lower, candidates[i]);
            }
            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5),
                $"Scoring 1000 candidates took {sw.ElapsedMilliseconds}ms (limit 5ms)");
        }

        // 2. Coordinator with 3 sources × ~167 items each stays under 10ms
        [Test]
        public void Coordinator_3Sources_500Items_Under10ms()
        {
            const int ItemsPerSource = 167;
            var sources = new PerfMockSource[3];
            for (int s = 0; s < 3; s++)
            {
                sources[s] = new PerfMockSource();
                for (int i = 0; i < ItemsPerSource; i++)
                    sources[s].Add($"Item_{s}_{i}", $"/src{s}/item{i}", i);
            }

            var coordinator = new MentionCoordinator(sources[0], sources[1], sources[2]);
            var results = new List<MentionCandidate>();

            var sw = Stopwatch.StartNew();
            coordinator.Search("test", 8, results);
            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
                $"Coordinator search took {sw.ElapsedMilliseconds}ms (limit 10ms)");
        }

        // 3. Parsing a 2000-char text with scattered @ symbols stays under 1ms
        [Test]
        public void TokenParser_LongText_Under1ms()
        {
            // Build 2000-char text with @ scattered every 200 chars
            var sb = new System.Text.StringBuilder(2000);
            for (int i = 0; i < 2000; i++)
            {
                if (i > 0 && i % 200 == 0) sb.Append(" @mention ");
                else sb.Append('x');
            }
            string text = sb.ToString();
            int cursor = text.Length;
            var chips = new List<PositionedChip>();

            var sw = Stopwatch.StartNew();
            MentionTokenParser.TryExtract(text, cursor, chips, out _, out _);
            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1),
                $"TokenParser on 2000-char text took {sw.ElapsedMilliseconds}ms (limit 1ms)");
        }

        // 4. Bitmask pre-filter rejects >90% of 10000 candidates for rare query "xyz"
        [Test]
        public void BitmaskPreFilter_RejectsEarly()
        {
            const int N = 10000;
            uint queryMask = MentionFuzzyScorer.BuildCharMask("xyz");

            int passed = 0;
            for (int i = 0; i < N; i++)
            {
                // Names built from a-f digits only — no x, y, z
                string name = $"Object_{i % 100}_abcdef";
                uint mask = MentionFuzzyScorer.BuildCharMask(name.ToLowerInvariant());
                if (MentionFuzzyScorer.PassesPreFilter(queryMask, mask)) passed++;
            }

            double rejectionRate = 1.0 - (double)passed / N;
            Assert.That(rejectionRate, Is.GreaterThan(0.90),
                $"Bitmask rejection rate was {rejectionRate:P0} — expected >90%");
        }

        // 5. Coordinator deduplicates 2 sources with 50% overlap efficiently
        [Test]
        public void Coordinator_DeduplicatesEfficiently()
        {
            const int ItemsPerSource = 200;
            const int OverlapCount = 100; // 50% overlap

            var s1 = new PerfMockSource();
            var s2 = new PerfMockSource();

            // s1: items 0..199
            for (int i = 0; i < ItemsPerSource; i++)
                s1.Add($"Item{i}", $"/shared/item{i}", i);

            // s2: items 100..299 — items 100-199 overlap with s1
            for (int i = OverlapCount; i < ItemsPerSource + OverlapCount; i++)
                s2.Add($"Item{i}", $"/shared/item{i}", i + 10);

            var coordinator = new MentionCoordinator(s1, s2);
            var results = new List<MentionCandidate>();

            var sw = Stopwatch.StartNew();
            coordinator.Search("item", 500, results);
            sw.Stop();

            // 200 unique from s1 + 100 unique from s2 = 300 total
            Assert.That(results.Count, Is.LessThan(ItemsPerSource * 2),
                "Coordinator should deduplicate overlapping paths");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
                $"Dedup search took {sw.ElapsedMilliseconds}ms (limit 10ms)");
        }
    }
}
