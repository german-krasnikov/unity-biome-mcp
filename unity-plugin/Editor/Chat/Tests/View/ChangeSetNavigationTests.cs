// T16: ChangeSetNavigation unit tests — discriminating routing assertions.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChangeSetNavigationTests : UnityMcpTestBase
    {
        [Test]
        public void ResolveKindKey_Asset_ReturnsScript()
        {
            Assert.AreEqual(ChipKindKeys.Script, ChangeSetNavigation.ResolveKindKey("asset"));
        }

        [Test]
        public void ResolveKindKey_SceneObject_ReturnsHierarchy()
        {
            Assert.AreEqual(ChipKindKeys.Hierarchy, ChangeSetNavigation.ResolveKindKey("scene_object"));
        }

        [Test]
        public void ResolveKindKey_Property_ReturnsHierarchy()
        {
            Assert.AreEqual(ChipKindKeys.Hierarchy, ChangeSetNavigation.ResolveKindKey("property"));
        }

        [Test]
        public void Attach_EmptyPath_IsNoop()
        {
            var el = new VisualElement();
            var op = new OperationViewModel("modify", "asset", "", null, null, null, true);
            Assert.DoesNotThrow(() => ChangeSetNavigation.Attach(el, op));
        }
    }
}
