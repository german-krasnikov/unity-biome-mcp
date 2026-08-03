// TDD: ReloadCompileNotifier — SessionState read/write + GetStatus format.
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadCompileNotifierTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown()
        {
            // Restore clean state after each test
            SessionState.EraseFloat(ReloadCompileNotifier.StartKey);
            SessionState.EraseFloat(ReloadCompileNotifier.DurationKey);
            SessionState.EraseBool(ReloadCompileNotifier.FailedKey);
            ReloadCompileNotifier.NowSecondsFloat = () => (float)EditorApplication.timeSinceStartup;
        }

        [Test]
        public void GetStatus_WhenNeverCompiled_ReturnsIdleNever()
        {
            // Arrange: ensure clean state (no StartKey, no DurationKey)
            SessionState.EraseFloat(ReloadCompileNotifier.StartKey);
            SessionState.EraseFloat(ReloadCompileNotifier.DurationKey);
            SessionState.EraseBool(ReloadCompileNotifier.FailedKey);

            // Act
            var status = ReloadCompileNotifier.GetStatus();

            // Assert
            Assert.AreEqual("idle-never|0", status);
        }

        [Test]
        public void GetStatus_Format_ContainsPipe()
        {
            // Any valid status must contain pipe separator
            var status = ReloadCompileNotifier.GetStatus();
            StringAssert.Contains("|", status);
        }

        [Test]
        public void IsCompiling_WhenStartKeySet_ReturnsTrue()
        {
            // Arrange: simulate compilationStarted by setting StartKey
            SessionState.SetFloat(ReloadCompileNotifier.StartKey, 1f);

            // Act + Assert
            Assert.IsTrue(ReloadCompileNotifier.IsCompiling);
        }

        [Test]
        public void GetStatus_WhenCompiling_ReturnsCompilingPrefix()
        {
            // Arrange: set start time via injectable clock seam
            ReloadCompileNotifier.NowSecondsFloat = () => 100f;
            SessionState.SetFloat(ReloadCompileNotifier.StartKey, 95f);

            // Act
            var status = ReloadCompileNotifier.GetStatus();

            // Assert: should be "compiling|5.0"
            StringAssert.StartsWith("compiling|", status);
        }

        [Test]
        public void UpdateCache_SetsIsCompilingFromEditorApplication()
        {
            ReloadCompileNotifier.UpdateCache();
            Assert.AreEqual(EditorApplication.isCompiling, ReloadCompileNotifier.CachedIsCompiling);
        }

        [Test]
        public void UpdateCache_SetsCompileErrorsFromSessionState()
        {
            SessionState.SetString(ReloadCompileNotifier.CompileErrorsSessionKey, "TestError");
            try
            {
                ReloadCompileNotifier.UpdateCache();
                Assert.AreEqual("TestError", ReloadCompileNotifier.CachedCompileErrors);
            }
            finally
            {
                SessionState.EraseString(ReloadCompileNotifier.CompileErrorsSessionKey);
            }
        }
    }
}
