// TDD: MCPDebugUI builds VisualElement tree without throwing.
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPDebugUITests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MCPDebugUI _ui;

        [SetUp]
        public void SetUp()
        {
            WatchRegistry.Clear();
            _ui = new MCPDebugUI();
        }

        [TearDown]
        public void TearDown() => WatchRegistry.Clear();

        [Test]
        public void Build_CreatesChildren()
        {
            var root = new VisualElement();
            Assert.DoesNotThrow(() => _ui.Build(root));
            Assert.Greater(root.childCount, 0);
        }

        [Test]
        public void Build_RootHasDebugPanelClass()
        {
            var root = new VisualElement();
            _ui.Build(root);
            Assert.IsTrue(root.ClassListContains("mcp-debug-panel"));
        }

        [Test]
        public void RefreshAll_DoesNotThrow_EmptyRegistry()
        {
            var root = new VisualElement();
            _ui.Build(root);
            Assert.DoesNotThrow(() => _ui.RefreshAll());
        }

        [Test]
        public void RefreshAll_DoesNotThrow_WithWatch()
        {
            var root = new VisualElement();
            _ui.Build(root);
            WatchRegistry.Add("/Player", "Health", "hp");
            Assert.DoesNotThrow(() => _ui.RefreshAll());
        }

        [Test]
        public void Build_CalledTwice_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => {
                new MCPDebugUI().Build(new VisualElement());
                new MCPDebugUI().Build(new VisualElement());
            });
        }
    }
}
