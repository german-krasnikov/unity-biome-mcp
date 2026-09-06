// TDD: CodeExecutor transform bugs C5 (return; in void local functions) and C6 (lowercase namespace using).
using NUnit.Framework;
using System;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CodeExecutorTransformTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Execute_HelperTypeBeforeEntryDoesNotHideRun()
        {
            Assert.That(CodeExecutor.Execute(
                "public sealed class Helper {} public static class Entry { public static object Run() { return 202; } }",
                "entry selection test"), Is.EqualTo("202"));
        }

        [Test]
        public void Execute_WrapperPreferencePreservesNonPublicAndReturnType()
        {
            Assert.That(CodeExecutor.Execute(
                "public static class Helper { public static object Run() { throw new System.Exception(\"wrong entry invoked\"); } } " +
                "public static class __MCPScript { private static string Run() { return \"wrapper\"; } }",
                "entry selection test"), Is.EqualTo("wrapper"));
        }

        [TestCase("public static class Missing { public static object Run(int value) { return value; } }",
                  "No static parameterless Run() found.")]
        [TestCase("public static class First { public static object Run() { throw new System.Exception(\"wrong entry invoked\"); } } " +
                  "public static class Second { public static object Run() { throw new System.Exception(\"wrong entry invoked\"); } }",
                  "Multiple static parameterless Run() methods found.")]
        public void Execute_MissingOrAmbiguousEntryRejectsBeforeUndo(string source, string expectedReason)
        {
            var group = Undo.GetCurrentGroup();
            var error = Assert.Throws<InvalidOperationException>(() => CodeExecutor.Execute(source, "must not change undo"));
            Assert.That(error.Message, Does.StartWith(expectedReason));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
        }

        [Test]
        public void Execute_OpenGenericEntryIsNotInvokable()
        {
            var group = Undo.GetCurrentGroup();
            var error = Assert.Throws<InvalidOperationException>(() => CodeExecutor.Execute(
                "public static class Generic { public static object Run<T>() { throw new System.Exception(\"wrong entry invoked\"); } }",
                "must not change undo"));
            Assert.That(error.Message, Does.StartWith("No static parameterless Run() found."));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
        }

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
