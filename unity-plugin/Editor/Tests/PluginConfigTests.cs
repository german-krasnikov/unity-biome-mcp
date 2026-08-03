using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PluginConfigTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string Id1 = "TestPlugin_Alpha";
        private const string Id2 = "TestPlugin_Beta";
        private const string StringKey = "test_string";
        private const string BoolKey = "test_bool";
        private const string IntKey = "test_int";
        private const string FloatKey = "test_float";

        [Test]
        public void SetString_ThenGet_RoundTrips()
        {
            ProtectEditorPrefString(PluginConfig.BuildKey(Id1, StringKey));
            PluginConfig.SetString(Id1, StringKey, "hello");
            Assert.AreEqual("hello", PluginConfig.GetString(Id1, StringKey));
        }

        [Test]
        public void GetString_MissingKey_ReturnsDefault()
        {
            DeleteEditorPrefString(PluginConfig.BuildKey(Id1, StringKey));
            Assert.AreEqual("def", PluginConfig.GetString(Id1, StringKey, "def"));
        }

        [Test]
        public void SetBool_ThenGet_RoundTrips()
        {
            ProtectEditorPrefBool(PluginConfig.BuildKey(Id1, BoolKey));
            PluginConfig.SetBool(Id1, BoolKey, false);
            Assert.IsFalse(PluginConfig.GetBool(Id1, BoolKey, defaultValue: true));
        }

        [Test]
        public void SetInt_ThenGet_RoundTrips()
        {
            ProtectEditorPrefInt(PluginConfig.BuildKey(Id1, IntKey));
            PluginConfig.SetInt(Id1, IntKey, 42);
            Assert.AreEqual(42, PluginConfig.GetInt(Id1, IntKey));
        }

        [Test]
        public void SetFloat_ThenGet_RoundTrips()
        {
            ProtectEditorPrefFloat(PluginConfig.BuildKey(Id1, FloatKey));
            PluginConfig.SetFloat(Id1, FloatKey, 3.14f);
            Assert.AreEqual(3.14f, PluginConfig.GetFloat(Id1, FloatKey), delta: 0.001f);
        }

        [Test]
        public void Delete_AfterSet_ReturnsDefault()
        {
            ProtectEditorPrefString(PluginConfig.BuildKey(Id1, StringKey));
            PluginConfig.SetString(Id1, StringKey, "val");
            PluginConfig.Delete(Id1, StringKey);
            Assert.AreEqual("def", PluginConfig.GetString(Id1, StringKey, "def"));
        }

        [Test]
        public void TwoPlugins_SameKey_StoredSeparately()
        {
            ProtectEditorPrefString(PluginConfig.BuildKey(Id1, StringKey));
            ProtectEditorPrefString(PluginConfig.BuildKey(Id2, StringKey));
            PluginConfig.SetString(Id1, StringKey, "alpha_value");
            PluginConfig.SetString(Id2, StringKey, "beta_value");
            Assert.AreEqual("alpha_value", PluginConfig.GetString(Id1, StringKey));
            Assert.AreEqual("beta_value", PluginConfig.GetString(Id2, StringKey));
        }

        [Test]
        public void BuildKey_ContainsPluginIdAndKey()
        {
            var k = PluginConfig.BuildKey("MyPlugin", "my_key");
            StringAssert.Contains("MyPlugin", k);
            StringAssert.Contains("my_key", k);
        }

        [Test]
        public void BuildKey_PrefixNotCollidingWithMCPSettings()
        {
            var k = PluginConfig.BuildKey("any", "any");
            StringAssert.DoesNotStartWith("UnityMCP_", k);
        }
    }
}
