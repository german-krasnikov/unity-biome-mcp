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
    }
}
