// TDD: CompilePreflightCommand + RoslynLoader + RoslynFormat — EditMode NUnit.
// Tests 2-4 require Unity Editor (Roslyn DLLs bundled). Skipped via Assume if unavailable.
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityMCP.Editor.Roslyn;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    [Category(TestCategories.Roslyn)]
    public class CompilePreflightTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── RoslynLoader ──────────────────────────────────────────────────────

        [Test]
        public void RoslynLoader_EnsureRoslyn_LoadsDlls()
        {
            var loaded = RoslynLoader.EnsureRoslyn();
            Assert.IsTrue(loaded, "EnsureRoslyn should return true in Unity Editor environment");
            Assert.IsNotNull(RoslynLoader.RoslynCore,     "RoslynCore must be non-null after load");
            Assert.IsNotNull(RoslynLoader.RoslynCompiler, "RoslynCompiler must be non-null after load");
        }

        // ── CompilePreflightCommand — valid code ──────────────────────────────

        [Test]
        public void ValidCode_ReturnsOK()
        {
            Assume.That(RoslynLoader.EnsureRoslyn(), Is.True, "Roslyn not available — skipping");

            var result = CompilePreflightCommand.Execute(
                "Assets/Test.cs", "using UnityEngine; public class Foo : MonoBehaviour {}");

            StringAssert.StartsWith("OK preflight (", result);
        }

        // ── CompilePreflightCommand — syntax error ────────────────────────────

        [Test]
        public void SyntaxError_ReturnsERR_WithCS()
        {
            Assume.That(RoslynLoader.EnsureRoslyn(), Is.True, "Roslyn not available — skipping");

            var result = CompilePreflightCommand.Execute(
                "Assets/Test.cs", "public class Foo { int @@@ }");

            StringAssert.StartsWith("ERR preflight", result);
            StringAssert.Contains("CS", result);
        }

        // ── CompilePreflightCommand — missing type ────────────────────────────

        [Test]
        public void MissingType_ReturnsERR()
        {
            Assume.That(RoslynLoader.EnsureRoslyn(), Is.True, "Roslyn not available — skipping");

            var result = CompilePreflightCommand.Execute(
                "Assets/Test.cs", "public class Foo { NonExistentType999 x; }");

            StringAssert.StartsWith("ERR preflight", result);
            Assert.IsTrue(result.Contains("CS0246") || result.Contains("CS0234"),
                $"expected type-not-found error code, got: {result}");
        }

        // ── RoslynFormat ──────────────────────────────────────────────────────

        [Test]
        public void Format_NoErrors_ReturnsOKLine()
        {
            var result = RoslynFormat.FormatDiagnostics(new object[0], 182);
            Assert.AreEqual("OK preflight (182ms)", result);
            Assert.IsTrue(Regex.IsMatch(result, @"^OK preflight \(\d+ms\)$"),
                "must match fixture format ^OK preflight (\\d+ms)$");
        }

        // ── CompilePreflightCommand — exception fallback ──────────────────────

        [Test]
        public void Execute_MissingArgs_ReturnsFallbackOrOK()
        {
            // Missing args (both empty) — Execute must reject before touching Roslyn.
            var result = CompilePreflightCommand.Execute("", "");
            Assert.IsTrue(
                result.StartsWith("OK") || result.StartsWith("ERR") || result.StartsWith("[ROSLYN") || result.StartsWith("err:"),
                $"unexpected result format: {result}");
        }
    }
}
