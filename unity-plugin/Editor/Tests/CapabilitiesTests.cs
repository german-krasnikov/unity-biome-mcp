// TDD: get_capabilities command — registration, output format, render pipeline detection.
// EditMode only, no TCP required.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CapabilitiesTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetCapabilities_IsRegistered()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("get_capabilities"),
                "get_capabilities must be registered in CommandRegistry");
        }

        [Test]
        public void GetCapabilities_ContainsUnityVersion()
        {
            var result = CommandRegistry.Execute("get_capabilities", "{}");
            StringAssert.Contains("unity:", result);
            // Version string is always present and non-empty
            StringAssert.DoesNotContain("unity:\n", result,
                "Unity version must not be empty");
        }

        [Test]
        public void GetCapabilities_ContainsRenderPipeline()
        {
            var result = CommandRegistry.Execute("get_capabilities", "{}");
            StringAssert.Contains("renderPipeline:", result);
        }

        [Test]
        public void GetCapabilities_AllowedDuringCompile()
        {
            Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("get_capabilities"),
                "get_capabilities must be allowed during compile");
        }

        [Test]
        public void GetCapabilities_ContainsPlatform()
        {
            var result = CommandRegistry.Execute("get_capabilities", "{}");
            StringAssert.Contains("platform:", result);
        }

        [Test]
        public void GetCapabilities_ContainsScriptingBackend()
        {
            var result = CommandRegistry.Execute("get_capabilities", "{}");
            StringAssert.Contains("scriptingBackend:", result);
        }

        [Test]
        public void GetCapabilities_ContainsMutatingCmds()
        {
            var result = CommandRegistry.Execute("get_capabilities", "{}");
            StringAssert.Contains("mutating_cmds:", result, "mutating_cmds line missing");
            StringAssert.Contains("auto_wire", result, "auto_wire must be mutating");
        }

        [Test]
        public void GetCapabilities_ContainsRuntimeCmds()
        {
            var result = CommandRegistry.Execute("get_capabilities", "{}");
            StringAssert.Contains("runtime_cmds:", result, "runtime_cmds line missing");
            StringAssert.Contains("invoke_method", result, "invoke_method must be runtime");
        }
    }
}
