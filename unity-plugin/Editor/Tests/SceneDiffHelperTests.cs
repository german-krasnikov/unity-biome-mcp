// TDD — SceneDiffHelper.NormalizeSnapshot: strips both $HEX and #decimal ID tokens.
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SceneDiffHelperTests : UnityMcpTestBase
    {
        [Test]
        public void NormalizeSnapshot_HashDecimal_Stripped()
        {
            // Pre-existing behavior: #decimal stripped from hierarchy output
            var input = "Player #12345\nEnemy #67890";
            var result = SceneDiffHelper.NormalizeSnapshot(input);
            Assert.IsFalse(result.Contains("#12345"), "# decimal ID must be stripped");
            Assert.IsFalse(result.Contains("#67890"), "# decimal ID must be stripped");
            StringAssert.Contains("Player", result);
            StringAssert.Contains("Enemy", result);
        }

        [Test]
        public void NormalizeSnapshot_DollarHex_Stripped()
        {
            // New behavior: $HEX also stripped so diff is stable
            var input = "Player $FFFFCFC7\nEnemy $3E8";
            var result = SceneDiffHelper.NormalizeSnapshot(input);
            Assert.IsFalse(result.Contains("$FFFFCFC7"), "$HEX ID must be stripped");
            Assert.IsFalse(result.Contains("$3E8"), "$HEX ID must be stripped");
            StringAssert.Contains("Player", result);
            StringAssert.Contains("Enemy", result);
        }

        [Test]
        public void NormalizeSnapshot_ColorHash_NotStripped()
        {
            // #RRGGBBAA color syntax must NOT be stripped (no preceding space)
            var input = "color: #FF0000FF";
            var result = SceneDiffHelper.NormalizeSnapshot(input);
            StringAssert.Contains("#FF0000FF", result);
        }
    }
}
