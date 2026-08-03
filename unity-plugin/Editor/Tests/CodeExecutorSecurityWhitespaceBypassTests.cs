using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CodeExecutorSecurityWhitespaceBypassTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp] public void SetUp()
        {
            ProtectEditorPrefInt("UnityMCP_SecurityLevel");
            MCPSettings.SetSecurityLevel(SecurityLevel.Standard);
        }

        // ── CS4.arch.2: whitespace/newline bypass ────────────────────────────

        [Test]
        public void SecurityScan_InvokeTokenSplitByNewline_Throws()
        {
            // Old code: ".Invoke(" check failed when newline splits token
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "var m = typeof(object).GetMethod(\"ToString\");\nm\n.Invoke(null, null);"));
        }

        [Test]
        public void SecurityScan_GetMethodTokenSplitByNewline_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("typeof(object)\n.GetMethod(\"ToString\");"));
        }

        // ── CS4.test.3: singular reflection accessors ────────────────────────

        [Test]
        public void SecurityScan_GetFieldSingular_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "typeof(CodeExecutor).GetField(\"_roslynLoaded\").GetValue(null);"));
        }

        [Test]
        public void SecurityScan_GetPropertySingular_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "typeof(object).GetProperty(\"Blocked\").GetValue(null);"));
        }

        [Test]
        public void SecurityScan_GetValueMethod_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("fieldInfo.GetValue(obj);"));
        }

        [Test]
        public void SecurityScan_SetValueMethod_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("fieldInfo.SetValue(obj, 42);"));
        }

        // ── Sanity: legit code still passes ─────────────────────────────────

        [Test]
        public void SecurityScan_LegitCodeWithNewlines_DoesNotThrow()
        {
            Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "var go = GameObject.Find(\"Player\");\nreturn go?.name;"));
        }
    }
}
