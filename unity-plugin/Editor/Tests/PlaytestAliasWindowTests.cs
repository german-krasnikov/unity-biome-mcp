// TDD Phase 6: PlaytestAliasHelpers pure-static tests (no Unity API needed).
// Tests cover FormatVALLine, FormatVALBlock, TokenSavingsEstimate, SuggestName.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasWindowTests
    {
        // ── FormatVALLine ───────────────────────────────────────────────────────

        [Test]
        public void FormatVALLine_WithAllFields_ProducesCorrectLine()
        {
            var a = new QueryAlias { alias = "hp", path = "/Player", component = "Health", field = "hp" };
            var result = PlaytestAliasHelpers.FormatVALLine(a);
            Assert.AreEqual("VAL $hp /Player|Health|hp", result);
        }

        [Test]
        public void FormatVALLine_EmptyComponentAndField_NoTrailingPipes()
        {
            var a = new QueryAlias { alias = "player", path = "/GridPlayer", component = "", field = "" };
            var result = PlaytestAliasHelpers.FormatVALLine(a);
            Assert.AreEqual("VAL $player /GridPlayer", result);
        }

        // ── FormatVALBlock ──────────────────────────────────────────────────────

        [Test]
        public void FormatVALBlock_Empty_ReturnsEmptyString()
        {
            var result = PlaytestAliasHelpers.FormatVALBlock(new List<QueryAlias>());
            Assert.AreEqual("", result);
        }

        [Test]
        public void FormatVALBlock_TwoAliases_EachOnOwnLine()
        {
            var aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "player", path = "/P", component = "C", field = "f" },
                new QueryAlias { alias = "hp",     path = "/P", component = "H", field = "v" },
            };
            var result = PlaytestAliasHelpers.FormatVALBlock(aliases);
            var lines = result.Split('\n');
            Assert.AreEqual("VAL $player /P|C|f", lines[0]);
            Assert.AreEqual("VAL $hp /P|H|v",     lines[1]);
        }

        // ── TokenSavingsEstimate ────────────────────────────────────────────────

        [Test]
        public void TokenSavingsEstimate_Empty_ReturnsZero()
        {
            var result = PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias>());
            Assert.AreEqual(0, result);
        }

        [Test]
        public void TokenSavingsEstimate_ShortAlias_LongPath_ReturnsPositive()
        {
            // path "$longpath" saves chars vs "$hp"
            var a = new QueryAlias { alias = "hp", path = "/GridPlayer|Health|hp", component = "", field = "" };
            var result = PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { a });
            Assert.Greater(result, 0);
        }

        [Test]
        public void TokenSavingsEstimate_AliasLongerThanPath_ReturnsZeroNotNegative()
        {
            var a = new QueryAlias { alias = "verylongaliasname", path = "/A", component = "", field = "" };
            var result = PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { a });
            Assert.GreaterOrEqual(result, 0);
        }

        // ── SuggestName ─────────────────────────────────────────────────────────

        [Test]
        public void SuggestName_SpacesRemoved_Lowercased()
        {
            var result = PlaytestAliasHelpers.SuggestName("Grid Player");
            Assert.AreEqual("grid_player", result);
        }

        [Test]
        public void SuggestName_AlreadyClean_Unchanged()
        {
            var result = PlaytestAliasHelpers.SuggestName("player");
            Assert.AreEqual("player", result);
        }

        [Test]
        public void SuggestName_NonAlphanumRemoved()
        {
            var result = PlaytestAliasHelpers.SuggestName("Grid-Player!");
            Assert.AreEqual("gridplayer", result);
        }
    }
}
