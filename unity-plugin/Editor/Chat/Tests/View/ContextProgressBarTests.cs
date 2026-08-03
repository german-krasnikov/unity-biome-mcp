using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ContextProgressBarTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Update_ZeroWindow_HidesElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(5000, 0);
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Update_NegativeWindow_HidesElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(5000, -1);
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Update_NonZeroWindow_ShowsElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Update_ZeroTokens_ShowsZeroPercent()
        {
            var bar = new ContextProgressBar();
            bar.Update(0, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Reset_HidesElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            bar.Reset();
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Constructor_StartsHidden()
        {
            var bar = new ContextProgressBar();
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }
    }
}
