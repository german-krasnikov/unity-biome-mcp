// TDD Phase 6: Edge case tests for @mention subsystem.
// Covers boundary conditions not exercised by the main test suites.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionEdgeCaseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // 1. Emoji after @ is rejected (not a valid token char)
        [Test]
        public void Parser_UnicodeEmoji_Rejected()
        {
            // "🎮" = U+1F3AE → two C# chars (surrogate pair); string length = 3
            string text = "@\U0001F3AE";
            Assert.That(text.Length, Is.EqualTo(3)); // sanity: @ + 2 surrogate chars
            bool result = MentionTokenParser.TryExtract(text, text.Length,
                new List<PositionedChip>(), out _, out _);
            Assert.IsFalse(result, "Emoji after @ should be rejected");
        }

        // 2. Identical candidates produce stable result (no crash, correct count)
        [Test]
        public void Scorer_IdenticalCandidates_StableOrder()
        {
            var source = new StubSource();
            for (int i = 0; i < 5; i++)
                source.Add($"Twin", $"/twin{i}", 50); // same name & score, different paths

            var coordinator = new MentionCoordinator(source);
            var results = new List<MentionCandidate>();
            Assert.DoesNotThrow(() => coordinator.Search("twin", 10, results));
            Assert.That(results.Count, Is.EqualTo(5));
        }

        // 3. maxResults=0 returns empty without crash
        [Test]
        public void Coordinator_MaxResultsZero_ReturnsEmpty()
        {
            var source = new StubSource();
            source.Add("Alpha", "/alpha", 100);

            var coordinator = new MentionCoordinator(source);
            var results = new List<MentionCandidate>();
            Assert.DoesNotThrow(() => coordinator.Search("alpha", 0, results));
            Assert.That(results, Is.Empty);
        }

        // 4. cursor=0 always returns false
        [Test]
        public void Parser_CursorAtZero_ReturnsFalse()
        {
            bool result = MentionTokenParser.TryExtract("@test", 0,
                new List<PositionedChip>(), out _, out _);
            Assert.IsFalse(result);
        }

        // 5. Show then Show again resets selection to 0 and uses new content
        [Test]
        public void Popup_ShowThenShowAgain_ReplacesContent()
        {
            // MentionPopup loads icons by name — "icon" doesn't exist in test context.
            // Suppress all icon-load errors for this test.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var anchor = new VisualElement();
                var popup  = new MentionPopup(anchor, _ => { });

                // First show: 3 items, navigate down
                popup.Show(MakeList(3));
                popup.MoveDown(); // index → 1
                Assert.That(popup.SelectedIndex, Is.EqualTo(1));

                // Second show: 2 items → index resets, content replaced
                popup.Show(MakeList(2));
                Assert.That(popup.SelectedIndex, Is.EqualTo(0), "SelectedIndex must reset on second Show");

                // Confirm only 2 items: MoveDown twice wraps to 0
                popup.MoveDown();
                popup.MoveDown();
                Assert.That(popup.SelectedIndex, Is.EqualTo(0),
                    "Two MoveDowns on a 2-item popup must wrap back to 0");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static List<MentionCandidate> MakeList(int count)
        {
            var list = new List<MentionCandidate>();
            for (int i = 0; i < count; i++)
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, $"/obj{i}", $"Object{i}", i);
                list.Add(new MentionCandidate(chip, 100 - i, "icon"));
            }
            return list;
        }

        private class StubSource : IMentionSource
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
    }
}
