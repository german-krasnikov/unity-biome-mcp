using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PluginToolGroupingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private class NoSubPlugin : IMCPPlugin
        {
            public string Name => "MyPlugin";
            public string CommandPrefix => "myplugin";
            public void RegisterCommands() { }
            public void OnDomainReload() { }
        }

        private class SubPlugin : IMCPPlugin
        {
            public string Name => "SubPlugin";
            public string CommandPrefix => "sp";
            public void RegisterCommands() { }
            public void OnDomainReload() { }
            public string GetToolSubcategory(string cmd) =>
                cmd.StartsWith("sp_anim") ? "Animation" :
                cmd.StartsWith("sp_audio") ? "Audio" : null;
        }

        private class EmptyStringSubPlugin : IMCPPlugin
        {
            public string Name => "EmpPlugin";
            public string CommandPrefix => "emp";
            public void RegisterCommands() { }
            public void OnDomainReload() { }
            public string GetToolSubcategory(string cmd) => "";
        }

        [Test]
        public void NoSubcategory_SingleGroup_LabelIsPluginName()
        {
            var plugin = new NoSubPlugin();
            var result = PluginToolGrouping.GroupBySubcategory(plugin, new[] { "myplugin_a", "myplugin_b" });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("MyPlugin", result[0].label);
            Assert.AreEqual(2, result[0].tools.Length);
        }

        [Test]
        public void DeclaredSubcategories_ProducesNGroups()
        {
            var plugin = new SubPlugin();
            var cmds = new[] { "sp_anim1", "sp_anim2", "sp_audio1", "sp_other" };
            var result = PluginToolGrouping.GroupBySubcategory(plugin, cmds);

            Assert.AreEqual(3, result.Count);
            // Animation: 2 tools
            Assert.AreEqual("Animation", result[0].label);
            Assert.AreEqual(2, result[0].tools.Length);
            // Audio: 1 tool
            Assert.AreEqual("Audio", result[1].label);
            Assert.AreEqual(1, result[1].tools.Length);
            // Fallback to plugin name: 1 tool
            Assert.AreEqual("SubPlugin", result[2].label);
            Assert.AreEqual(1, result[2].tools.Length);
        }

        [Test]
        public void SubcategoryOrder_IsFirstSeenInsertionOrder()
        {
            var plugin = new SubPlugin();
            // Audio appears first in list → Audio group must be first
            var cmds = new[] { "sp_audio1", "sp_anim1", "sp_audio2" };
            var result = PluginToolGrouping.GroupBySubcategory(plugin, cmds);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Audio", result[0].label);
            Assert.AreEqual("Animation", result[1].label);
        }

        [Test]
        public void EmptyCommandList_ReturnsEmptyList()
        {
            var plugin = new NoSubPlugin();
            var result = PluginToolGrouping.GroupBySubcategory(plugin, Array.Empty<string>());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void EmptyStringSubcategory_FallsBackToPluginName()
        {
            var plugin = new EmptyStringSubPlugin();
            var result = PluginToolGrouping.GroupBySubcategory(plugin, new[] { "emp_cmd" });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("EmpPlugin", result[0].label);
        }
    }
}
