// TDD — ToolbarButtonRegistry: registration, ordering, key validation, test seam.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal sealed class FakeToolbarButtonProvider : IToolbarButtonProvider
    {
        public string Key         { get; }
        public int    Order       { get; }
        public string ButtonLabel { get; }
        public string Tooltip     { get; }
        public bool   MenuOnly    { get; }

        public int ClickCount { get; private set; }

        public FakeToolbarButtonProvider(string key, string label = "Btn", int order = 100, bool menuOnly = false)
        {
            Key = key; ButtonLabel = label; Order = order; Tooltip = $"{key} tooltip"; MenuOnly = menuOnly;
        }

        public void OnClick(UnityEditor.EditorWindow window) => ClickCount++;
    }

    [TestFixture]
    public class ToolbarButtonRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(ToolbarButtonRegistry.PreserveStateForTests().Dispose);
            ToolbarButtonRegistry.ResetForTests();
        }

        [Test]
        public void Register_NewKey_ReturnsTrue()
        {
            Assert.IsTrue(ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn")));
        }

        [Test]
        public void Register_DuplicateKey_ReturnsFalse_KeepsFirst()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn", "First"));
            Assert.IsFalse(ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn", "Second")));
            Assert.AreEqual("First", ToolbarButtonRegistry.All[0].ButtonLabel);
        }

        [Test]
        public void Register_NullProvider_ReturnsFalse()
        {
            Assert.IsFalse(ToolbarButtonRegistry.Register(null));
        }

        [Test]
        public void Register_InvalidKey_ReturnsFalse()
        {
            Assert.IsFalse(ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("Bad Key")));
            Assert.AreEqual(0, ToolbarButtonRegistry.All.Count);
        }

        [Test]
        public void Unregister_ExistingKey_RemovesProvider()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn"));
            Assert.IsTrue(ToolbarButtonRegistry.Unregister("my_btn"));
            Assert.AreEqual(0, ToolbarButtonRegistry.All.Count);
        }

        [Test]
        public void Unregister_UnknownKey_ReturnsFalse()
        {
            Assert.IsFalse(ToolbarButtonRegistry.Unregister("nonexistent"));
        }

        [Test]
        public void All_ReturnsSortedByOrder()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("b_btn", order: 200));
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("a_btn", order: 10));
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("c_btn", order: 100));

            var all = ToolbarButtonRegistry.All;
            Assert.AreEqual("a_btn", all[0].Key);
            Assert.AreEqual("c_btn", all[1].Key);
            Assert.AreEqual("b_btn", all[2].Key);
        }

        [Test]
        public void ResetForTests_ClearsAllProviders()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn"));
            ToolbarButtonRegistry.ResetForTests();
            Assert.AreEqual(0, ToolbarButtonRegistry.All.Count);
        }

        [Test]
        public void Version_IncrementsOnChanges()
        {
            int v0 = ToolbarButtonRegistry.Version;
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn"));
            Assert.Greater(ToolbarButtonRegistry.Version, v0);
        }

        private sealed class BareProvider : IToolbarButtonProvider
        {
            public string Key         => "bare";
            public int    Order       => 0;
            public string ButtonLabel => "B";
            public string Tooltip     => "";
            public void OnClick(UnityEditor.EditorWindow w) { }
            // MenuOnly deliberately NOT overridden — tests the DIM default
        }

        [Test]
        public void MenuOnly_DefaultIsFalse()
        {
            IToolbarButtonProvider p = new BareProvider();
            Assert.IsFalse(p.MenuOnly, "MenuOnly must default to false for backward compatibility");
        }
    }
}
