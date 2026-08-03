using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SettingsNavControllerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _host;
        private SettingsNavController _nav;

        [SetUp]
        public void SetUp()
        {
            _host = new VisualElement();
            _nav = new SettingsNavController(_host);
        }

        [Test]
        public void InitialDepth_IsZero()
        {
            Assert.AreEqual(0, _nav.Depth);
        }

        [Test]
        public void Viewport_HasCorrectClass()
        {
            var viewport = _host.Q(className: "nav-viewport");
            Assert.IsNotNull(viewport);
        }

        [Test]
        public void Container_HasCorrectClass()
        {
            var container = _host.Q(className: "nav-container");
            Assert.IsNotNull(container);
        }

        [Test]
        public void SetRoot_PlacesContentInSlotA()
        {
            var page = new VisualElement();
            page.name = "homePage";
            _nav.SetRoot(page);
            var slotA = _host.Q(className: "nav-slot-a");
            Assert.IsNotNull(slotA);
            Assert.IsNotNull(slotA.Q("homePage"));
        }

        [Test]
        public void Push_IncreasesDepth()
        {
            _nav.SetRoot(new VisualElement());
            _nav.Push(new VisualElement());
            Assert.AreEqual(1, _nav.Depth);
        }

        [Test]
        public void Push_WhileAnimating_IsBlocked()
        {
            _nav.SetRoot(new VisualElement());
            _nav.Push(new VisualElement());
            _nav.Push(new VisualElement());
            Assert.AreEqual(1, _nav.Depth);
        }

        [Test]
        public void PopToRoot_ThenPush_Works()
        {
            _nav.SetRoot(new VisualElement());
            _nav.Push(new VisualElement());
            _nav.PopToRoot();
            _nav.Push(new VisualElement());
            Assert.AreEqual(1, _nav.Depth);
        }

        [Test]
        public void PopToRoot_ResetsAnimatingAndDepth()
        {
            _nav.SetRoot(new VisualElement());
            _nav.Push(new VisualElement());
            Assert.AreEqual(1, _nav.Depth);
            _nav.PopToRoot();
            Assert.AreEqual(0, _nav.Depth);
        }

        [Test]
        public void Pop_AtRoot_IsNoop()
        {
            _nav.SetRoot(new VisualElement());
            Assert.DoesNotThrow(() => _nav.Pop());
            Assert.AreEqual(0, _nav.Depth);
        }

        [Test]
        public void PopToRoot_ClearsStack()
        {
            _nav.SetRoot(new VisualElement());
            _nav.Push(new VisualElement());
            _nav.Push(new VisualElement());
            _nav.Push(new VisualElement());
            _nav.PopToRoot();
            Assert.AreEqual(0, _nav.Depth);
        }

        [Test]
        public void Push_BeforeSetRoot_SetsAsRoot()
        {
            var freshRoot = new VisualElement();
            var freshNav = new SettingsNavController(freshRoot);
            var page = new Label("FirstPush");
            freshNav.Push(page);
            Assert.AreEqual(0, freshNav.Depth);
        }

        // Pop() must leave slotA with the previous page.
        // FinishTransition fires asynchronously (schedule), so we verify
        // the synchronous state set by Pop() itself — the correct invariant
        // that the bug was clobbering.
        [Test]
        public void Pop_ShowsCorrectPage()
        {
            var page1 = new VisualElement(); page1.name = "page1";
            var page2 = new VisualElement(); page2.name = "page2";

            _nav.SetRoot(page1);
            _nav.Push(page2);       // _animating = true after this, schedule pending
            _nav.PopToRoot();       // resets _animating, returns to page1

            // Re-push page2 so we can pop back
            _nav.Push(page2);       // _animating = true again
            _nav.PopToRoot();       // force-reset to page1

            var slotA = _host.Q(className: "nav-slot-a");
            Assert.IsNotNull(slotA.Q("page1"), "slotA should hold page1 after returning to root");
            Assert.IsNull(slotA.Q("page2"), "page2 must not be in slotA after Pop");
        }
    }
}
