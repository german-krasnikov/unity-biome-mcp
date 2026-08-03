using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TokenFormatTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Abbr_Zero_ReturnsZero()     => Assert.AreEqual("0",      TokenFormat.Abbr(0));
        [Test] public void Abbr_999_ReturnsAsIs()      => Assert.AreEqual("999",    TokenFormat.Abbr(999));
        [Test] public void Abbr_1000_ReturnsOneK()     => Assert.AreEqual("1.0k",   TokenFormat.Abbr(1000));
        [Test] public void Abbr_1234_Returns1p2k()     => Assert.AreEqual("1.2k",   TokenFormat.Abbr(1234));
        [Test] public void Abbr_12345_Returns12p3k()   => Assert.AreEqual("12.3k",  TokenFormat.Abbr(12345));
        [Test] public void Abbr_Negative_ReturnsAsIs() => Assert.AreEqual("-5",     TokenFormat.Abbr(-5));

        [Test] public void FormatReadout_AllZero_ReturnsEmpty()
            => Assert.AreEqual("", TokenFormat.FormatReadout(0, 0));

        [Test] public void FormatReadout_WithTokens_NoUsd()
            => Assert.AreEqual("↑ 100  ↓ 50", TokenFormat.FormatReadout(100, 50));

        [Test] public void FormatReadout_LargeTokens_Abbreviated()
            => StringAssert.StartsWith("↑ 1.0k", TokenFormat.FormatReadout(1000, 50));

        [Test] public void FormatReadout_OnlyOutput_ShowsBoth()
            => Assert.AreEqual("↑ 0  ↓ 50", TokenFormat.FormatReadout(0, 50));
    }
}
