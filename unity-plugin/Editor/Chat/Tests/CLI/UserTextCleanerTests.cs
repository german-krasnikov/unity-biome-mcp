using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserTextCleanerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Strip_SingleMention()
            => Assert.AreEqual("what", UserTextCleaner.Strip("@Player what"));

        [Test] public void Strip_MultipleMentions()
            => Assert.AreEqual("fix this", UserTextCleaner.Strip("@A @B fix this"));

        [Test] public void Strip_MentionOnly()
            => Assert.AreEqual("", UserTextCleaner.Strip("@Player"));

        [Test] public void Strip_NoMentions()
            => Assert.AreEqual("fix the bug", UserTextCleaner.Strip("fix the bug"));

        [Test] public void Strip_MentionMidSentence()
            => Assert.AreEqual("fix health", UserTextCleaner.Strip("fix @Player health"));

        [Test] public void Strip_MentionWithUnderscore()
            => Assert.AreEqual("check", UserTextCleaner.Strip("@Main_Camera check"));

        [Test] public void Strip_MentionWithDigits()
            => Assert.AreEqual("what", UserTextCleaner.Strip("@Collectible_2 what"));

        [Test] public void Strip_NullInput()
            => Assert.IsNull(UserTextCleaner.Strip(null));

        [Test] public void Strip_EmptyInput()
            => Assert.AreEqual("", UserTextCleaner.Strip(""));

        [Test] public void Strip_MentionWithHyphen()
            => Assert.AreEqual("check", UserTextCleaner.Strip("@Main-Camera check"));

        [Test] public void Strip_MentionWithParens()
            => Assert.AreEqual("what", UserTextCleaner.Strip("@Capsule_(1) what"));

        [Test] public void Strip_MentionWithParensAndHyphen()
            => Assert.AreEqual("fix", UserTextCleaner.Strip("@My-Object_(2) fix"));

        [Test] public void Strip_MentionWithTrailingComma()
            => Assert.AreEqual("fix", UserTextCleaner.Strip("@Player, fix"));
    }
}
