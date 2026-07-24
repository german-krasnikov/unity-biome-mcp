using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CodeExecutorSecurityBypassTests
    {
        private SecurityLevel _savedLevel;

        [SetUp]    public void SetUp()    { _savedLevel = MCPSettings.GetSecurityLevel(); MCPSettings.SetSecurityLevel(SecurityLevel.Standard); }
        [TearDown] public void TearDown() { MCPSettings.SetSecurityLevel(_savedLevel); }

        // ── Comment-injection bypass ─────────────────────────────────────────

        [Test]
        public void BlocksCommentSplitBypass()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("Sys/**/tem.Diagnostics.Process.Start(\"calc\");"));
        }

        [Test]
        public void BlocksMultiLineCommentSplit()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("System.IO.Fi/*\n*/le.Delete(\"x\");"));
        }

        [Test]
        public void BlocksSingleLineCommentBypass()
        {
            // System.IO//comment\n.File.Delete → strip → System.IO\n.File.Delete → densify → System.IO.File.Delete
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("System.IO//comment\n.File.Delete(\"x\");"));
        }

        // ── Case-insensitive bypass ──────────────────────────────────────────

        [Test]
        public void BlocksCaseVariant()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("system.io.file.Delete(\"x\");"));
        }

        // ── Using-alias bypass ───────────────────────────────────────────────

        [Test]
        public void BlocksUsingAlias()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("using IO = System.IO; IO.File.Delete(\"x\");"));
        }

        [Test]
        public void BlocksUsingAliasReflection()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("using R = System.Reflection; R.Assembly.Load(null);"));
        }

        [Test]
        public void BlocksUsingAliasProcess()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("using P = System.Diagnostics; P.Process.Start(\"cmd\");"));
        }

        [Test]
        public void NormalUsingNotAffected()
        {
            Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("using System.Linq; var x = new System.Collections.Generic.List<int>{1,2,3}.First(); return x;"));
        }

        // ── Missing blocked entries ──────────────────────────────────────────

        [Test]
        public void BlocksEditorApplicationExit()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("EditorApplication.Exit(0);"));
        }

        [Test]
        public void BlocksApplicationQuit()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("Application.Quit();"));
        }

        [Test]
        public void BlocksEnvironmentFailFast()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("Environment.FailFast(\"boom\");"));
        }

        [Test]
        public void BlocksExportPackage()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("AssetDatabase.ExportPackage(null,null);"));
        }

        [Test]
        public void BlocksImportPackage()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("AssetDatabase.ImportPackage(\"x\",false);"));
        }

        [Test]
        public void BlocksOpenProject()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("EditorApplication.OpenProject(\"x\");"));
        }

        [Test]
        public void BlocksProjectWindowUtil()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("ProjectWindowUtil.CreateFolder();"));
        }

        // ── String-literal containing // must not swallow payload ─────────────

        [Test]
        public void BlocksPayloadAfterStringContainingSlashSlash()
        {
            // "http://url" contains // but must NOT trigger line-comment strip
            Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "var s = \"http://x\"; System.IO.File.Delete(\"y\");"));
        }

        // ── False-positive sanity check ──────────────────────────────────────

        [Test]
        public void LegitProcessVariableName_DoesNotThrow()
        {
            Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("var processManager = 42; return processManager;"));
        }
    }
}
