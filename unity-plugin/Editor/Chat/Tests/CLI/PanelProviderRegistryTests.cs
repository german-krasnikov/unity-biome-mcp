// TDD — PanelProviderRegistry: registration, ShowPanel, key validation, test seam.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal sealed class FakePanelProvider : IPanelProvider
    {
        public string Key          { get; }
        public string MenuPath     { get; }
        public int    MenuPriority { get; }
        public string WindowTitle  { get; }

        public int ShowCount { get; private set; }
        private readonly bool _throws;

        public FakePanelProvider(string key, int priority = 100, bool throws = false)
        {
            Key = key; MenuPath = $"MCP/{key}"; MenuPriority = priority;
            WindowTitle = key; _throws = throws;
        }

        public void Show()
        {
            if (_throws) throw new System.InvalidOperationException("show error");
            ShowCount++;
        }
    }

    [TestFixture]
    public class PanelProviderRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(PanelProviderRegistry.PreserveStateForTests().Dispose);
            PanelProviderRegistry.ResetForTests();
        }

        [Test]
        public void Register_NewKey_ReturnsTrue()
        {
            Assert.IsTrue(PanelProviderRegistry.Register(new FakePanelProvider("my_panel")));
        }

        [Test]
        public void Register_DuplicateKey_ReturnsFalse()
        {
            PanelProviderRegistry.Register(new FakePanelProvider("my_panel"));
            Assert.IsFalse(PanelProviderRegistry.Register(new FakePanelProvider("my_panel")));
        }

        [Test]
        public void Register_NullProvider_ReturnsFalse()
        {
            Assert.IsFalse(PanelProviderRegistry.Register(null));
        }

        [Test]
        public void Register_InvalidKey_ReturnsFalse()
        {
            Assert.IsFalse(PanelProviderRegistry.Register(new FakePanelProvider("Bad Panel")));
        }

        [Test]
        public void Unregister_ExistingKey_RemovesProvider()
        {
            PanelProviderRegistry.Register(new FakePanelProvider("my_panel"));
            Assert.IsTrue(PanelProviderRegistry.Unregister("my_panel"));
            Assert.AreEqual(0, PanelProviderRegistry.All.Count);
        }

        [Test]
        public void ShowPanel_KnownKey_CallsShowOnProvider()
        {
            var provider = new FakePanelProvider("my_panel");
            PanelProviderRegistry.Register(provider);
            PanelProviderRegistry.ShowPanel("my_panel");
            Assert.AreEqual(1, provider.ShowCount);
        }

        [Test]
        public void ShowPanel_UnknownKey_IsNoOp_NoThrow()
        {
            Assert.DoesNotThrow(() => PanelProviderRegistry.ShowPanel("nonexistent"));
        }

        [Test]
        public void ShowPanel_NullKey_IsNoOp_NoThrow()
        {
            Assert.DoesNotThrow(() => PanelProviderRegistry.ShowPanel(null));
        }

        [Test]
        public void ShowPanel_ProviderThrows_IsHandled_NoThrow()
        {
            var throwingProvider = new FakePanelProvider("bad_panel", throws: true);
            PanelProviderRegistry.Register(throwingProvider);
            LogAssert.Expect(LogType.Exception, new Regex("show error"));
            Assert.DoesNotThrow(() => PanelProviderRegistry.ShowPanel("bad_panel"));
        }

        [Test]
        public void All_ReturnsSortedByMenuPriority()
        {
            PanelProviderRegistry.Register(new FakePanelProvider("b_panel", priority: 200));
            PanelProviderRegistry.Register(new FakePanelProvider("a_panel", priority: 10));

            var all = PanelProviderRegistry.All;
            Assert.AreEqual("a_panel", all[0].Key);
            Assert.AreEqual("b_panel", all[1].Key);
        }

        [Test]
        public void ResetForTests_ClearsAllProviders()
        {
            PanelProviderRegistry.Register(new FakePanelProvider("my_panel"));
            PanelProviderRegistry.ResetForTests();
            Assert.AreEqual(0, PanelProviderRegistry.All.Count);
        }
    }
}
