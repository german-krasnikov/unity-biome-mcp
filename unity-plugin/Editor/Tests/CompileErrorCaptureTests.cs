// TDD: CompileErrorCapture — C5 SessionState persistence + per-asmdef map.
// These tests cover the domain-reload survival contract (C5) and per-asmdef API.
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CompileErrorCaptureTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CompileErrorCapture.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CompileErrorCapture.Clear();
        }

        // C5 #1: errors injected via test seam are available via GetErrors()
        [Test]
        public void GetErrors_ReturnsInjectedError()
        {
            CompileErrorCapture.InjectForTest("Assets/Foo.cs:1:1: error CS0001: test error");
            var result = CompileErrorCapture.GetErrors();
            StringAssert.Contains("CS0001", result);
            StringAssert.Contains("1 compilation error(s)", result);
        }

        // C5 #2: errors survive simulated domain reload via SessionState
        [Test]
        public void GetErrors_PersistsToSessionState_AfterSimulatedReload()
        {
            CompileErrorCapture.InjectForTest("Assets/Bar.cs:5:3: error CS0002: reload test");

            // Simulate domain reload: in-memory list is cleared, SessionState survives
            CompileErrorCapture.SimulateDomainReload();

            // GetErrors must fall back to SessionState
            var result = CompileErrorCapture.GetErrors();
            StringAssert.Contains("CS0002", result,
                "errors must survive simulated domain reload via SessionState fallback");
        }

        // C5 #3: GetErrors returns sentinel when no errors
        [Test]
        public void GetErrors_ReturnsNoErrorsSentinel_WhenClean()
        {
            var result = CompileErrorCapture.GetErrors();
            Assert.AreEqual("No compilation errors", result);
        }

        // C5 #4: Clear wipes both in-memory and SessionState
        [Test]
        public void Clear_WipesSessionState()
        {
            CompileErrorCapture.InjectForTest("Assets/X.cs:1:1: error CS9999: clear test");
            Assert.AreNotEqual("No compilation errors", CompileErrorCapture.GetErrors());

            CompileErrorCapture.Clear();

            Assert.AreEqual("No compilation errors", CompileErrorCapture.GetErrors(),
                "Clear must also wipe SessionState so post-clear reads return sentinel");
        }

        // C5 #5: GetErrorsForAssembly returns sentinel for unknown assembly
        [Test]
        public void GetErrorsForAssembly_ReturnsNoErrors_WhenUnknown()
        {
            var result = CompileErrorCapture.GetErrorsForAssembly("Library/ScriptAssemblies/Unknown.dll");
            Assert.AreEqual("No compilation errors", result);
        }

        // C5 #6: multiple InjectForTest calls accumulate errors
        [Test]
        public void GetErrors_AccumulatesMultipleErrors()
        {
            CompileErrorCapture.InjectForTest("A.cs:1:1: error CS0001: first");
            CompileErrorCapture.InjectForTest("B.cs:2:2: error CS0002: second");
            var result = CompileErrorCapture.GetErrors();
            StringAssert.Contains("2 compilation error(s)", result);
            StringAssert.Contains("CS0001", result);
            StringAssert.Contains("CS0002", result);
        }

        // Task5 #MaxErrors: InjectForTest caps at 50 — 51st error must not appear
        [Test]
        public void InjectForTest_CapsAt50_51stErrorNotIncluded()
        {
            for (int i = 0; i < 51; i++)
                CompileErrorCapture.InjectForTest($"File{i}.cs:1:1: error CS{i:D4}: msg{i}");

            var result = CompileErrorCapture.GetErrors();

            StringAssert.Contains("50 compilation error(s)", result,
                "header must report exactly 50 errors (cap enforced)");
            StringAssert.DoesNotContain("msg50", result,
                "51st injected error must not appear in output");
        }

        // Task6 #HasErrors_1: HasErrors returns false when clean
        [Test]
        public void HasErrors_ReturnsFalse_WhenClean()
        {
            Assert.IsFalse(CompileErrorCapture.HasErrors());
        }

        // Task6 #HasErrors_2: HasErrors returns true after inject
        [Test]
        public void HasErrors_ReturnsTrue_AfterInject()
        {
            CompileErrorCapture.InjectForTest("Foo.cs:1:1: error CS0001: boom");
            Assert.IsTrue(CompileErrorCapture.HasErrors());
        }

        // Task6 #HasErrors_3: HasErrors returns false after Clear
        [Test]
        public void HasErrors_ReturnsFalse_AfterClear()
        {
            CompileErrorCapture.InjectForTest("Bar.cs:1:1: error CS0002: x");
            CompileErrorCapture.Clear();
            Assert.IsFalse(CompileErrorCapture.HasErrors());
        }

        // Task6 #HasErrors_4: SessionState sentinel "No compilation errors" → HasErrors false
        [Test]
        public void HasErrors_WithSessionSentinelValue_ReturnsFalse()
        {
            // Write the sentinel directly — simulates a post-clear state where the key exists
            // but holds the sentinel string. HasErrors must treat this as "no errors".
            UnityEditor.SessionState.SetString("MCP_CompileErrors", "No compilation errors");

            Assert.IsFalse(CompileErrorCapture.HasErrors(),
                "sentinel value 'No compilation errors' must not be treated as an error");
            // TearDown's Clear() will erase the key
        }

        // Task6 #HasErrors_5: GetErrors returns sentinel string when state is empty
        [Test]
        public void GetErrors_EmptyState_ReturnsCleanMessage()
        {
            var result = CompileErrorCapture.GetErrors();
            Assert.AreEqual("No compilation errors", result);
        }

        // Task7 #AsmFallback_1: SessionState pre-populated for assembly → GetErrorsForAssembly returns it
        [Test]
        public void GetErrorsForAssembly_FallsBackToSessionState_WhenInMemoryEmpty()
        {
            const string asmKey  = "MCP_CompileErrors_FallbackAsm";
            const string asmPath = "Library/ScriptAssemblies/FallbackAsm.dll";
            const string payload = "2 compilation error(s):\nFoo.cs:1:0: error CS0001: a\nBar.cs:2:0: error CS0002: b";
            UnityEditor.SessionState.SetString(asmKey, payload);
            try
            {
                var result = CompileErrorCapture.GetErrorsForAssembly(asmPath);
                StringAssert.Contains("2 compilation error(s)", result,
                    "must return SessionState-persisted errors when in-memory map is empty");
            }
            finally
            {
                UnityEditor.SessionState.EraseString(asmKey);
            }
        }

        // Task7 #AsmFallback_2: no SessionState entry for assembly → returns clean message
        [Test]
        public void GetErrorsForAssembly_NoSessionStateEntry_ReturnsCleanMessage()
        {
            UnityEditor.SessionState.EraseString("MCP_CompileErrors_NoSuchAsm");
            var result = CompileErrorCapture.GetErrorsForAssembly("Library/ScriptAssemblies/NoSuchAsm.dll");
            Assert.AreEqual("No compilation errors", result);
        }
    }
}
