// Stress tests for assembly structure — Wizard asmdef (SH-1) + MovedFrom sourceAssembly (CP-6).
// EditMode only. Every test < 15 lines.
using NUnit.Framework;
using UnityEngine.Scripting.APIUpdating;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WizardAssemblyStructureTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // T-H: Wizard asmdef has autoReferenced: false (regression guard — was true before SH-1)
        [Test]
        public void WizardAsmdef_AutoReferenced_IsFalse()
        {
            var json = ReadRequiredPackageSource(
                typeof(MCPStatusWindow), "Editor/Wizard/UnityMCP.Editor.Wizard.asmdef");
            StringAssert.Contains("\"autoReferenced\": false", json,
                "SH-1: Wizard asmdef must have autoReferenced=false to isolate compile errors");
        }

        // T-I: [MovedFrom] sourceAssembly is exactly "UnityMCP.Editor" (not "UnityMCP.Editor.Wizard")
        [Test]
        public void MCPStatusWindow_MovedFrom_SourceAssembly_IsUnityMCPEditor()
        {
            var attrs = typeof(MCPStatusWindow)
                .GetCustomAttributes(typeof(MovedFromAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "MCPStatusWindow must have [MovedFrom] attribute");
            var src = ReadRequiredPackageSource(
                typeof(MCPStatusWindow), "Editor/Wizard/MCPStatusWindow.cs");
            StringAssert.Contains("sourceAssembly: \"UnityMCP.Editor\"", src,
                "CP-6: sourceAssembly must be the OLD assembly (UnityMCP.Editor), not the new one");
        }
    }
}
