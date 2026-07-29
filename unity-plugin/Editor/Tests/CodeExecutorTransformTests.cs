// TDD: CodeExecutor transform bugs C5 (return; in void local functions) and C6 (lowercase namespace using).
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CodeExecutorTransformTests
    {
        // ── C5: return; in void local function must not become return null; ──────

        [Test]
        public void WrapIfBareCode_VoidLocalFunctionReturn_NotRewrittenToReturnNull()
        {
            // Bug: regex replaces ALL `return;` including ones inside local void funcs
            var code = "void Helper() { return; } return \"done\";";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            // After fix: depth-aware replacement leaves `return;` inside Helper intact
            Assert.That(wrapped, Does.Contain("void Helper() { return; }"),
                "return; inside void local function must NOT be rewritten to return null;");
        }

        [Test]
        public void WrapIfBareCode_TopLevelBareReturn_IsRewrittenToReturnNull()
        {
            // Regression guard: top-level `return;` must still be replaced
            var code = "var x = 1; return;";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            Assert.That(wrapped, Does.Contain("return null;"),
                "Top-level return; must become return null;");
        }

        // ── C6: lowercase namespace using should be hoisted above class wrapper ──

        [TestCase("using system.text;\nvar x = 1;", "using system.text;", TestName = "WrapIfBareCode_LowercaseUsingStatement_IsHoisted")]
        [TestCase("using System.Text;\nvar x = 1;", "using System.Text;", TestName = "WrapIfBareCode_UppercaseUsingStatement_IsHoisted")]
        [TestCase("using _helpers;\nvar x = 1;", "using _helpers;", TestName = "WrapIfBareCode_UnderscoreUsingStatement_IsHoisted")]
        public void WrapIfBareCode_UsingStatement_IsHoisted(string code, string usingStmt)
        {
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            var classIdx = wrapped.IndexOf("public static class __MCPScript");
            var usingIdx = wrapped.IndexOf(usingStmt);
            Assert.That(usingIdx, Is.GreaterThanOrEqualTo(0), $"{usingStmt} not found in output");
            Assert.That(usingIdx, Is.LessThan(classIdx), $"{usingStmt} must be hoisted before class");
        }
    }
}
