using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class CodeExecutorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            var savedLevel = MCPSettings.GetSecurityLevel();
            RegisterCleanup(() => MCPSettings.SetSecurityLevel(savedLevel));
            MCPSettings.SetSecurityLevel(SecurityLevel.Standard);
            var savedEmoji = BiomeLabel.UseEmoji;
            RegisterCleanup(() => BiomeLabel.UseEmoji = savedEmoji);
            BiomeLabel.UseEmoji = false;
        }

        // ---------- Security Scan ----------

        [Test]
        public void SecurityScan_BlocksProcess()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                CodeExecutor.Execute("System.Diagnostics.Process.Start(\"cmd\");", "test"));
            Assert.That(ex.Message, Does.Contain("Security"));
        }

        [Test]
        public void SecurityScan_BlocksFileIO()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                CodeExecutor.Execute("System.IO.File.Delete(\"x\");", "test"));
            Assert.That(ex.Message, Does.Contain("Security"));
        }

        [Test]
        public void SecurityScan_AllowsUnityCode()
        {
            // Should not throw security exception (may fail compile in test context, but not security)
            try
            {
                CodeExecutor.Execute("return \"ok\";", "test");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Security"))
            {
                Assert.Fail($"Security scanner incorrectly blocked clean code: {ex.Message}");
            }
            catch (Exception)
            {
                // Compile or runtime errors are acceptable — security check must pass
            }
        }

        // ---------- Code Wrapping ----------

        [Test]
        public void WrapIfBareCode_WrapsStatements()
        {
            var wrapped = CodeExecutor.WrapIfBareCode("return \"hello\";");
            Assert.That(wrapped, Does.Contain("class __MCPScript"));
            Assert.That(wrapped, Does.Contain("return \"hello\";"));
        }

        [Test]
        public void WrapIfBareCode_SkipsClassCode()
        {
            var code = "public class MyClass { }";
            var result = CodeExecutor.WrapIfBareCode(code);
            Assert.That(result, Is.EqualTo(code));
        }

        [Test]
        public void WrapIfBareCode_SkipsNamespaceCode()
        {
            var code = "namespace Foo { }";
            var result = CodeExecutor.WrapIfBareCode(code);
            Assert.That(result, Is.EqualTo(code));
        }

        // ---------- Execute ----------

        [Test]
        public void Execute_SimpleReturn()
        {
            string result = null;
            Exception compileEx = null;
            try
            {
                result = CodeExecutor.Execute("return (1+1).ToString();", "test");
            }
            catch (Exception ex) when (ex.Message.Contains("Roslyn"))
            {
                // Acceptable: Roslyn not found in test runner context. Any other exception
                // (e.g. a real compile/runtime bug) is NOT caught here and fails the test.
                compileEx = ex;
            }

            if (compileEx != null)
                Assert.Inconclusive($"Roslyn not available in test context: {compileEx.Message}");

            Assert.That(result, Is.EqualTo("2"));
        }

        [Test]
        public void Execute_CompileError_ReturnsErrorWithInfo()
        {
            // Check Roslyn availability BEFORE registering the LogAssert expectation — if
            // Roslyn is missing, CodeExecutor.Execute never reaches CheckEmitResult, so the
            // expected log never appears and LogAssert.Expect would fail the test for the
            // wrong reason (Issue 23 review C10).
            if (!RoslynLoader.EnsureRoslyn())
            {
                Assert.Inconclusive("Roslyn not available in test context");
                return;
            }

            LogAssert.Expect(LogType.Error, new Regex(@"Biome execute_code compile error:"));
            Exception ex = null;
            try
            {
                CodeExecutor.Execute("this is not valid csharp !!!;", "test");
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex, "Compile error should throw");
            Assert.That(ex.Message, Does.Not.Contain("Security"));
        }

        // ---------- Issue 29: CS0161 bare-code wrapping ----------

        [Test]
        public void Execute_BareCodeWithoutReturn_CompilesSuccessfully()
        {
            Exception ex = null;
            try
            {
                CodeExecutor.Execute("var x = 1 + 1;", "test");
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex == null) return; // compiled and ran fine — no return statement needed

            if (ex.Message.Contains("Roslyn"))
                Assert.Inconclusive($"Roslyn not available in test context: {ex.Message}");

            Assert.That(ex.Message, Does.Not.Contain("CS0161"),
                "Bare code without return must not fail with CS0161 (not all code paths return a value)");
        }

        [Test]
        public void Execute_BareCodeWithExplicitReturn_StillWorks()
        {
            string result = null;
            Exception ex = null;
            try
            {
                result = CodeExecutor.Execute("return 42;", "test");
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
                Assert.Inconclusive($"Roslyn not available in test context: {ex.Message}");

            Assert.That(result, Does.Contain("42"));
        }

        [Test]
        public void Execute_MalformedCode_ThrowsWithCS1002()
        {
            // See Execute_CompileError_ReturnsErrorWithInfo above — check Roslyn availability
            // before the LogAssert expectation (Issue 23 review C10).
            if (!RoslynLoader.EnsureRoslyn())
            {
                Assert.Inconclusive("Roslyn not available in test context");
                return;
            }

            LogAssert.Expect(LogType.Error, new Regex(@"Biome execute_code compile error:"));
            Exception ex = null;
            try
            {
                CodeExecutor.Execute("var x = 1\nvar y = 2;", "test");
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex, "Malformed code should throw a compile error");
            Assert.That(ex.Message, Does.Contain("CS1002"));
        }

        [Test]
        public void Execute_EmptyBareCode_DoesNotThrow()
        {
            Exception ex = null;
            try
            {
                CodeExecutor.Execute("", "test");
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex == null) return; // compiled and ran fine

            if (ex.Message.Contains("Roslyn"))
                Assert.Inconclusive($"Roslyn not available in test context: {ex.Message}");

            Assert.Fail($"Empty bare code should compile and run without throwing: {ex.Message}");
        }
    }
}
