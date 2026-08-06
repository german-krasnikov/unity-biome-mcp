using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetupDiagnosticsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
#if UNITY_INCLUDE_TESTS
            RegisterCleanup(() => InstallSourceDetector.ClearTestOverride());
            RegisterCleanup(() => SetupDiagnostics.PythonVersionOverride = null);
            RegisterCleanup(() => SetupDiagnostics.ResetPythonVersionCache());
#endif
        }

        [Test]
        public void CheckPythonForSource_GitSource_ReturnsOkWithUvxNote()
        {
            var (ok, detail) = SetupDiagnostics.CheckPythonForSource(
                InstallSourceDetector.Source.Git, "/nonexistent/server");
            Assert.IsTrue(ok);
            StringAssert.Contains("uvx install", detail);
        }

        [Test]
        public void CheckPythonForSource_RegistrySource_ReturnsOkWithUvxNote()
        {
            var (ok, detail) = SetupDiagnostics.CheckPythonForSource(
                InstallSourceDetector.Source.Registry, "/nonexistent/server");
            Assert.IsTrue(ok);
            StringAssert.Contains("uvx install", detail);
        }

        [Test]
        public void CheckPythonForSource_EmbeddedSource_ReturnsOkWithUvxNote()
        {
            var (ok, detail) = SetupDiagnostics.CheckPythonForSource(
                InstallSourceDetector.Source.Embedded, "/nonexistent/server");
            Assert.IsTrue(ok);
            StringAssert.Contains("uvx install", detail);
        }

        [Test]
        public void CheckPythonForSource_LocalSource_MissingDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPythonForSource(
                InstallSourceDetector.Source.Local, "/nonexistent/server");
            Assert.IsFalse(ok);
            StringAssert.Contains("not found", detail);
        }

        [Test]
        public void CheckPythonForSource_UnknownSource_MissingDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPythonForSource(
                InstallSourceDetector.Source.Unknown, "/nonexistent/server");
            Assert.IsFalse(ok);
            StringAssert.Contains("not found", detail);
        }

        [Test]
        public void CheckPythonForSource_LocalSource_ValidVenv_ReturnsTrue()
        {
#if UNITY_INCLUDE_TESTS
            var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SetupDiagVenv_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var venvBin = System.IO.Path.Combine(tmpDir, ".venv", "bin");
                System.IO.Directory.CreateDirectory(venvBin);
                System.IO.File.WriteAllText(System.IO.Path.Combine(venvBin, "python"), "#!/bin/sh");

                // Windows: ResolvePythonCmd checks Scripts/python.exe first
                var venvScripts = System.IO.Path.Combine(tmpDir, ".venv", "Scripts");
                System.IO.Directory.CreateDirectory(venvScripts);
                System.IO.File.WriteAllText(System.IO.Path.Combine(venvScripts, "python.exe"), "");

                var (ok, detail) = SetupDiagnostics.CheckPythonForSource(
                    InstallSourceDetector.Source.Local, tmpDir);
                Assert.IsTrue(ok);
                StringAssert.Contains("Python at", detail);
            }
            finally
            {
                if (System.IO.Directory.Exists(tmpDir))
                    System.IO.Directory.Delete(tmpDir, true);
            }
#endif
        }
    }
}
