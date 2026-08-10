using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BiomeLabelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string PrefKey = "MCPPlugin_UseEmojiLabel";

        [Test]
        public void Tag_EmojiMode_IsEmoji()
        {
            SetEditorPrefBool(PrefKey, true);
            Assert.AreEqual("🧬", BiomeLabel.Tag);
        }

        [Test]
        public void Tag_TextMode_IsBiome()
        {
            SetEditorPrefBool(PrefKey, false);
            Assert.AreEqual("Biome", BiomeLabel.Tag);
        }

        [Test]
        public void DisplayName_EmojiMode_IsEmoji()
        {
            SetEditorPrefBool(PrefKey, true);
            Assert.AreEqual("🧬", BiomeLabel.DisplayName);
        }

        [Test]
        public void DisplayName_TextMode_IsBiome()
        {
            SetEditorPrefBool(PrefKey, false);
            Assert.AreEqual("Biome", BiomeLabel.DisplayName);
        }

        [Test]
        public void UseEmoji_Toggle_FiresChangedEvent()
        {
            SetEditorPrefBool(PrefKey, false);
            var fired = false;
            Action handler = () => fired = true;
            BiomeLabel.Changed += handler;
            RegisterCleanup(() => BiomeLabel.Changed -= handler);
            BiomeLabel.UseEmoji = true;
            Assert.IsTrue(fired);
        }

        [Test]
        public void UseEmoji_SetSameValue_DoesNotFireEvent()
        {
            SetEditorPrefBool(PrefKey, true);
            var fired = false;
            Action handler = () => fired = true;
            BiomeLabel.Changed += handler;
            RegisterCleanup(() => BiomeLabel.Changed -= handler);
            BiomeLabel.UseEmoji = true;  // same value — must not fire
            Assert.IsFalse(fired);
        }
    }
}
