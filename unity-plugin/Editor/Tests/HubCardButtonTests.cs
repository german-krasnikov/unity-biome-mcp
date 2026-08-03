using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class HubCardButtonTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_ReturnsElementWithHubCardClass()
        {
            var el = HubCardButton.Build("⚙", "Tools", "subtitle", () => { });
            Assert.IsTrue(el.ClassListContains("hub-card"));
        }

        [Test]
        public void Build_ContainsTitleLabel()
        {
            var el = HubCardButton.Build("⚙", "My Title", "sub", () => { });
            var title = el.Q<Label>(className: "hub-card-title");
            Assert.IsNotNull(title);
            Assert.AreEqual("My Title", title.text);
        }

        [Test]
        public void Build_ContainsSubtitleLabel()
        {
            var el = HubCardButton.Build("⚙", "T", "My Subtitle", () => { });
            var sub = el.Q<Label>(className: "hub-card-subtitle");
            Assert.IsNotNull(sub);
            Assert.AreEqual("My Subtitle", sub.text);
        }

        [Test]
        public void Build_ContainsIconLabel()
        {
            var el = HubCardButton.Build("⚙", "T", "S", () => { });
            var icon = el.Q<Label>(className: "hub-card-icon");
            Assert.IsNotNull(icon);
            Assert.AreEqual("⚙", icon.text);
        }

        [Test]
        public void Build_NullAction_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => HubCardButton.Build("⚙", "T", "S", null));
        }

        [Test]
        public void Build_ReturnsKeyboardFocusableButton()
        {
            var el = HubCardButton.Build("⚙", "T", "S", () => { });
            Assert.IsInstanceOf<Button>(el);
            Assert.IsTrue(el.focusable);
        }
    }
}
