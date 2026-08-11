// TDD — BareNameNormalizer tests (F14a, F20).
// Pure headless: no Unity runtime dependency.
// Verifies bare name → [kind:ref] bracket tag replacement.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.TestStringHelpers;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BareNameNormalizerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetForTests();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetForTests();

        // 1. NoChips_Unchanged
        [Test]
        public void NoChips_Unchanged()
        {
            Assert.AreEqual("hello world", BareNameNormalizer.Normalize("hello world", null));
            Assert.AreEqual("hello world", BareNameNormalizer.Normalize("hello world", new List<ChipData>()));
        }

        // 2. BareName_ReplacedWithBracketTag
        [Test]
        public void BareName_ReplacedWithBracketTag()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("fix Player health", chips);
            StringAssert.Contains("[hierarchy:/Player$1]", result);
        }

        // 3. BareNameTwice_BothReplaced
        [Test]
        public void BareNameTwice_BothReplaced()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("Player hit Player", chips);
            int count = CountOccurrences(result, "[hierarchy:/Player$1]");
            Assert.AreEqual(2, count, $"Expected 2 replacements, got: {result}");
        }

        // 4. ExistingBracketTag_NotDoubled
        [Test]
        public void ExistingBracketTag_NotDoubled()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var input  = "[hierarchy:/Player$1] is ready";
            var result = BareNameNormalizer.Normalize(input, chips);
            // Should not produce [[hierarchy:...]] or double-tag
            Assert.IsFalse(result.Contains("[["), $"Should not double-tag: {result}");
            Assert.AreEqual(1, CountOccurrences(result, "[hierarchy:/Player$1]"),
                $"Bracket tag should appear exactly once: {result}");
        }

        // 5. InsideCodeSpan_NotReplaced
        [Test]
        public void InsideCodeSpan_NotReplaced()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("use `Player` component", chips);
            StringAssert.Contains("`Player`", result);
            StringAssert.DoesNotContain("[hierarchy:/Player$1]", result);
        }

        // 6. CaseInsensitive_Matched
        [Test]
        public void CaseInsensitive_Matched()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("fix player now", chips);
            StringAssert.Contains("[hierarchy:/Player$1]", result);
        }

        // 7. WordBoundary_Respected
        [Test]
        public void WordBoundary_Respected()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("PlayerController is broken", chips);
            StringAssert.DoesNotContain("[hierarchy:/Player$1]", result);
            StringAssert.Contains("PlayerController", result);
        }

        // 8. LongestFirst_MultiWord
        [Test]
        public void LongestFirst_MultiWord()
        {
            var chips = new List<ChipData>
            {
                H("/Main",        "Main",        1),
                H("/Main Camera", "Main Camera", 2),
            };
            var result = BareNameNormalizer.Normalize("use Main Camera now", chips);
            StringAssert.Contains("[hierarchy:/Main Camera$2]", result);
            StringAssert.DoesNotContain("[hierarchy:/Main$1]", result);
        }

        // 9. SingleChar_Skipped
        [Test]
        public void SingleChar_Skipped()
        {
            var chips = new List<ChipData> { H("/A", "A", 1) };
            var result = BareNameNormalizer.Normalize("A is here", chips);
            // Single-char display names must be skipped
            StringAssert.DoesNotContain("[hierarchy:/A#1]", result);
        }

        // 10. NullText_ReturnsEmpty
        [Test]
        public void NullText_ReturnsEmpty()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            Assert.AreEqual("", BareNameNormalizer.Normalize(null, chips));
        }

        // 11. NonHierarchyChip_ScriptKind
        [Test]
        public void NonHierarchyChip_ScriptKind()
        {
            var chips = new List<ChipData> { S("Assets/Foo.cs", "Foo") };
            var result = BareNameNormalizer.Normalize("edit Foo script", chips);
            StringAssert.Contains("[script:Assets/Foo.cs]", result);
        }

        // 12. AdjacentToBracketTag_StillMatched
        [Test]
        public void AdjacentToBracketTag_StillMatched()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("Player near [hierarchy:/Enemy#2]", chips);
            StringAssert.Contains("[hierarchy:/Player$1]", result);
            StringAssert.Contains("[hierarchy:/Enemy#2]", result);
        }

        // 13. NameAtStartOfText
        [Test]
        public void NameAtStartOfText()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("Player is cool", chips);
            StringAssert.Contains("[hierarchy:/Player$1]", result);
        }

        // 14. NameAtEndOfText
        [Test]
        public void NameAtEndOfText()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("fix Player", chips);
            StringAssert.Contains("[hierarchy:/Player$1]", result);
        }

        // 15. NameWithUnderscore_WordBoundary
        [Test]
        public void NameWithUnderscore_WordBoundary()
        {
            var chips = new List<ChipData> { H("/Grid", "Grid", 1) };
            var result = BareNameNormalizer.Normalize("Grid_Floor is active", chips);
            // Underscore is a word char → should NOT match
            StringAssert.DoesNotContain("[hierarchy:/Grid#1]", result);
        }

        // 16. InsideCodeBlock_NotReplaced
        [Test]
        public void InsideCodeBlock_NotReplaced()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var text = "```\nPlayer.Move();\n```";
            var result = BareNameNormalizer.Normalize(text, chips);
            Assert.AreEqual(text, result);
        }

        // 17. OutsideCodeBlock_ReplacedButInsideNot
        [Test]
        public void OutsideCodeBlock_ReplacedButInsideNot()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var text = "fix Player here:\n```\nPlayer.Move();\n```";
            var result = BareNameNormalizer.Normalize(text, chips);
            StringAssert.Contains("[hierarchy:/Player$1]", result); // outside block
            StringAssert.Contains("Player.Move()", result);          // inside block preserved
        }

        // ── F20 boundary coverage (ported from deleted SceneNameLinkerTests) ──
        // BareNameNormalizer is chip-scoped (explicit user selections) so it uses
        // a simpler Length>1 guard — no skip list, no digit/underscore heuristic.

        // 18. TwoCharName_Accepted (unlike SceneNameLinker min-3 guard)
        [Test]
        public void TwoCharName_Accepted()
        {
            var chips = new List<ChipData> { H("/Go", "Go", 1) };
            // "Go" is 2 chars — BareNameNormalizer accepts it (length > 1)
            var result = BareNameNormalizer.Normalize("fix Go now", chips);
            StringAssert.Contains("[hierarchy:/Go$1]", result);
        }

        // 19. GenericName_Accepted (unlike SceneNameLinker SkipList for "Canvas" etc.)
        [Test]
        public void GenericName_Accepted()
        {
            var chips = new List<ChipData> { H("/Canvas", "Canvas", 1) };
            // "Canvas" is in SceneNameLinker.SkipList but BareNameNormalizer has no skip list
            var result = BareNameNormalizer.Normalize("fix Canvas here", chips);
            StringAssert.Contains("[hierarchy:/Canvas$1]", result);
        }

        // 20. AllLowerName_Accepted (unlike SceneNameLinker requiring digit/underscore/consecUpper)
        [Test]
        public void AllLowerName_Accepted()
        {
            var chips = new List<ChipData> { H("/player", "player", 1) };
            // "player" (all lower, no special chars) passes BareNameNormalizer length>1 guard
            var result = BareNameNormalizer.Normalize("fix player now", chips);
            StringAssert.Contains("[hierarchy:/player$1]", result);
        }

    }
}
