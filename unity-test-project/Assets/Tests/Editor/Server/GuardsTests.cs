using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    [TestFixture]
    public class GuardsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void OwnToolPreferences()
        {
            RegisterCleanup(CommandRouter.InvalidateEnabledToolsCache);
            foreach (var tool in new[]
                     { "get_hierarchy", "screenshot", "run_tests", "scene", "animation" })
                SetEditorPrefBool("UnityMCP_Tool_" + tool, true);
            CommandRouter.InvalidateEnabledToolsCache();
        }

        [Test]
        public void MCPSettings_DefaultAllEnabled()
        {
            Assert.IsTrue(MCPSettings.IsToolEnabled("get_hierarchy"));
            Assert.IsTrue(MCPSettings.IsToolEnabled("screenshot"));
            Assert.IsTrue(MCPSettings.IsToolEnabled("run_tests"));
        }

        [Test]
        public void MCPSettings_DisableTool_BlocksExecution()
        {
            var key = "UnityMCP_Tool_screenshot";
            SetEditorPrefBool(key, false);
            Assert.IsFalse(MCPSettings.IsToolEnabled("screenshot"));
        }

        [Test]
        public void MCPSettings_SceneTools_Enabled()
        {
            Assert.IsTrue(MCPSettings.IsToolEnabled("scene"));
            Assert.IsTrue(MCPSettings.IsToolEnabled("animation"));
        }

        [Test]
        public void MCPSettings_GetToolNames_ReturnsArray()
        {
            var names = MCPSettings.GetToolNames();
            Assert.IsNotNull(names);
            Assert.Greater(names.Length, 0);
            Assert.Contains("get_hierarchy", names);
            Assert.Contains("search_scene", names);
            Assert.Contains("set_material", names);
            Assert.Contains("batch", names);
        }

        [Test]
        public void GetEnabledTools_ReturnsCommaSeparatedList()
        {
            var json = "{\"id\":\"t200\",\"cmd\":\"get_enabled_tools\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("get_hierarchy", result);
            StringAssert.Contains("screenshot", result);
        }

        [Test]
        public void GetEnabledTools_ExcludesDisabledTool()
        {
            var key = "UnityMCP_Tool_screenshot";
            SetEditorPrefBool(key, false);
            var json = "{\"id\":\"t201\",\"cmd\":\"get_enabled_tools\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            var data = JsonHelper.ExtractString(result, "data");
            var tools = new System.Collections.Generic.HashSet<string>(data.Split(','));
            Assert.IsFalse(tools.Contains("screenshot"), "Disabled tool should not appear in enabled list");
            Assert.IsTrue(tools.Contains("get_hierarchy"), "Enabled tool should appear");
        }

        [Test]
        public void PlayModeGuard_BlocksMutations()
        {
            var original = CommandRouter.IsPlayMode;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var json = "{\"id\":\"pm1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"ShouldFail\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("Play mode", result);
            }
            finally
            {
                CommandRouter.IsPlayMode = original;
            }
        }

        [Test]
        public void PlayModeGuard_AllowsReads()
        {
            var original = CommandRouter.IsPlayMode;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var json = "{\"id\":\"pm2\",\"cmd\":\"get_hierarchy\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
            }
            finally
            {
                CommandRouter.IsPlayMode = original;
            }
        }

        [Test]
        public void CompilationGuard_ReturnsBusyWithRetry()
        {
            var original = CommandRouter.IsCompiling;
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"cg1\",\"cmd\":\"get_hierarchy\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("compiling", result);
                StringAssert.Contains("\"retry\":5000", result);
            }
            finally
            {
                CommandRouter.IsCompiling = original;
            }
        }

        [TestCase("ping")]
        [TestCase("get_console")]
        [TestCase("screenshot")]
        public void CompilationGuard_AllowsReadonly_WhenCompiling(string cmd)
        {
            var original = CommandRouter.IsCompiling;
            CommandRouter.IsCompiling = () => true;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                var json = "{\"id\":\"cg_ro\",\"cmd\":\"" + cmd + "\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                LogAssert.ignoreFailingMessages = false;
                Assert.That(result, Does.Not.Contain("compiling"));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                CommandRouter.IsCompiling = original;
            }
        }

        [Test]
        public void CompilationGuard_NormalFlow_NotAffected()
        {
            var original = CommandRouter.IsCompiling;
            CommandRouter.IsCompiling = () => false;
            try
            {
                var json = "{\"id\":\"cg6\",\"cmd\":\"get_hierarchy\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.DoesNotContain("retry", result);
            }
            finally
            {
                CommandRouter.IsCompiling = original;
            }
        }
    }
}
