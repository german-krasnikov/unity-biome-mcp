// TDD — SettingsProviderRegistry: registration, ordering, key validation, test seam.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal sealed class FakeSettingsProvider : ISettingsProvider
    {
        public string Key         { get; }
        public string DisplayName { get; }
        public int    Order       { get; }
        private readonly bool _throws;

        public FakeSettingsProvider(string key, string displayName = "Test", int order = 1000, bool throws = false)
        {
            Key = key; DisplayName = displayName; Order = order; _throws = throws;
        }

        public void BuildUI(VisualElement parent)
        {
            if (_throws) throw new System.InvalidOperationException("provider error");
            parent.Add(new Label(DisplayName));
        }
    }

    [TestFixture]
    public class SettingsProviderRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SettingsProviderRegistry.PreserveStateForTests().Dispose);
            SettingsProviderRegistry.ResetForTests();
        }

        [Test]
        public void Register_NewKey_ReturnsTrue()
        {
            Assert.IsTrue(SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin")));
        }

        [Test]
        public void Register_DuplicateKey_ReturnsFalse_KeepsFirst()
        {
            SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin", "First"));
            Assert.IsFalse(SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin", "Second")));
            Assert.AreEqual("First", SettingsProviderRegistry.All[0].DisplayName);
        }

        [Test]
        public void Register_InvalidKey_WithSpaces_ReturnsFalse()
        {
            Assert.IsFalse(SettingsProviderRegistry.Register(new FakeSettingsProvider("bad key")));
            Assert.AreEqual(0, SettingsProviderRegistry.All.Count);
        }

        [Test]
        public void Register_InvalidKey_Uppercase_ReturnsFalse()
        {
            Assert.IsFalse(SettingsProviderRegistry.Register(new FakeSettingsProvider("MyPlugin")));
        }

        [Test]
        public void Register_NullProvider_ReturnsFalse()
        {
            Assert.IsFalse(SettingsProviderRegistry.Register(null));
        }

        [Test]
        public void Unregister_ExistingKey_RemovesProvider()
        {
            SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin"));
            Assert.IsTrue(SettingsProviderRegistry.Unregister("my_plugin"));
            Assert.AreEqual(0, SettingsProviderRegistry.All.Count);
        }

        [Test]
        public void Unregister_UnknownKey_ReturnsFalse()
        {
            Assert.IsFalse(SettingsProviderRegistry.Unregister("nonexistent"));
        }

        [Test]
        public void All_ReturnsSortedByOrder()
        {
            SettingsProviderRegistry.Register(new FakeSettingsProvider("b_plugin", "B", order: 2000));
            SettingsProviderRegistry.Register(new FakeSettingsProvider("a_plugin", "A", order: 100));
            SettingsProviderRegistry.Register(new FakeSettingsProvider("c_plugin", "C", order: 1500));

            var all = SettingsProviderRegistry.All;
            Assert.AreEqual("a_plugin", all[0].Key);
            Assert.AreEqual("c_plugin", all[1].Key);
            Assert.AreEqual("b_plugin", all[2].Key);
        }

        [Test]
        public void ResetForTests_ClearsAllProviders()
        {
            SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin"));
            SettingsProviderRegistry.ResetForTests();
            Assert.AreEqual(0, SettingsProviderRegistry.All.Count);
        }

        [Test]
        public void Version_IncrementsOnRegisterAndUnregister()
        {
            int v0 = SettingsProviderRegistry.Version;
            SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin"));
            int v1 = SettingsProviderRegistry.Version;
            SettingsProviderRegistry.Unregister("my_plugin");
            int v2 = SettingsProviderRegistry.Version;

            Assert.Greater(v1, v0);
            Assert.Greater(v2, v1);
        }

        [Test]
        public void PreserveStateForTests_RestoresIdentityOrderAndVersionExactly()
        {
            var first = new FakeSettingsProvider("first", order: 200);
            var second = new FakeSettingsProvider("second", order: 100);
            SettingsProviderRegistry.Register(first);
            SettingsProviderRegistry.Register(second);
            var version = SettingsProviderRegistry.Version;

            using (SettingsProviderRegistry.PreserveStateForTests())
            {
                SettingsProviderRegistry.ResetForTests();
                SettingsProviderRegistry.Register(new FakeSettingsProvider("replacement"));
            }

            Assert.AreEqual(version, SettingsProviderRegistry.Version);
            Assert.AreEqual(2, SettingsProviderRegistry.All.Count);
            Assert.AreSame(second, SettingsProviderRegistry.All[0]);
            Assert.AreSame(first, SettingsProviderRegistry.All[1]);
        }
    }
}
