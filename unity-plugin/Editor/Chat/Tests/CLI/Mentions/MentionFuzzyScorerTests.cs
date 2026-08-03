using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionFuzzyScorerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // 1. Exact match → 1000
        [Test]
        public void ExactMatch_HighestScore()
        {
            var s = Score("camera", "camera");
            Assert.That(s, Is.EqualTo(1000));
        }

        // 2. Prefix "cam" vs "Camera" > "cam" vs "MainCamera"
        [Test]
        public void PrefixMatch_HigherThanSubstring()
        {
            var prefix    = Score("cam", "Camera");
            var substring = Score("cam", "MainCamera");
            Assert.That(prefix, Is.GreaterThan(substring));
        }

        // 3. "mc" vs "MainCamera" > "mc" vs "amcdef"
        [Test]
        public void CamelCaseBoundary_Bonus()
        {
            var camel  = Score("mc", "MainCamera");
            var noBound = Score("mc", "amcdef");
            Assert.That(camel, Is.GreaterThan(noBound));
        }

        // 4. "he" vs "player_health" > "he" vs "othello"
        [Test]
        public void DelimiterBoundary_Bonus()
        {
            var delim  = Score("he", "player_health");
            var noDelim = Score("he", "othello");
            Assert.That(delim, Is.GreaterThan(noDelim));
        }

        // 5. "sc" vs "Assets/Scripts" > "sc" vs "obscure"
        [Test]
        public void PathSeparator_Bonus()
        {
            var pathSep = Score("sc", "Assets/Scripts");
            var noSep   = Score("sc", "obscure");
            Assert.That(pathSep, Is.GreaterThan(noSep));
        }

        // 6. Consecutive chars bonus: "cam" vs "camera" > "cam" vs "cxaxm"
        [Test]
        public void ConsecutiveChars_Bonus()
        {
            var consec = Score("cam", "camera");
            var sparse = Score("cam", "cxaxm");
            Assert.That(consec, Is.GreaterThan(sparse));
        }

        // 7. No match → 0
        [Test]
        public void NoMatch_ReturnsZero()
        {
            Assert.That(Score("xyz", "camera"), Is.EqualTo(0));
        }

        // 8. Bitmask pre-filter rejects "qxz" against "camera"
        [Test]
        public void BitmaskPreFilter_RejectsAbsent()
        {
            var queryMask = MentionFuzzyScorer.BuildCharMask("qxz");
            var candMask  = MentionFuzzyScorer.BuildCharMask("camera");
            Assert.IsFalse(MentionFuzzyScorer.PassesPreFilter(queryMask, candMask));
        }

        // 9. Bitmask pre-filter passes "cam" against "camera"
        [Test]
        public void BitmaskPreFilter_PassesPresent()
        {
            var queryMask = MentionFuzzyScorer.BuildCharMask("cam");
            var candMask  = MentionFuzzyScorer.BuildCharMask("camera");
            Assert.IsTrue(MentionFuzzyScorer.PassesPreFilter(queryMask, candMask));
        }

        // 10. Short query (≤2): "ca" vs "camera" > "ca" vs "xyzca"
        [Test]
        public void ShortQuery_PrefixFastPath()
        {
            var prefix    = Score("ca", "camera");
            var nonPrefix = Score("ca", "xyzca");
            Assert.That(prefix, Is.GreaterThan(nonPrefix));
        }

        // Helper: lower-case both sides for scorer
        private static long Score(string pattern, string candidate)
        {
            var lower = candidate.ToLowerInvariant();
            return MentionFuzzyScorer.Score(pattern, lower, candidate);
        }
    }
}
