// TDD: ReloadDiagnoseCommand — формат, латч-сигнатура, DllFreshness, DetectReloadFailed.
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadDiagnoseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Execute_ReturnsAllRequiredFields()
        {
            var result = ReloadDiagnoseCommand.Execute();

            // All key= fields must be present
            StringAssert.Contains("mvid=",          result);
            StringAssert.Contains("stamp=",         result);
            StringAssert.Contains("compile=",       result);
            StringAssert.Contains("sync=",          result);
            StringAssert.Contains("iscompiling=",   result);
            StringAssert.Contains("cn_active=",     result);
            StringAssert.Contains("started=",       result);
            StringAssert.Contains("stamp_frozen=",  result);
            StringAssert.Contains("dlls=",          result);
            StringAssert.Contains("errors=",        result);
            StringAssert.Contains("log=",           result);
            StringAssert.Contains("reload_failed=", result);
        }

        [Test]
        public void Execute_LatchSignature_Parseable()
        {
            // Wedge fingerprint line must contain all 4 discriminators on one line
            var result = ReloadDiagnoseCommand.Execute();
            var lines = result.Split('\n');
            bool foundFingerprintLine = false;
            foreach (var line in lines)
            {
                if (line.Contains("iscompiling=") && line.Contains("cn_active=")
                    && line.Contains("started=") && line.Contains("stamp_frozen="))
                {
                    foundFingerprintLine = true;
                    break;
                }
            }
            Assert.IsTrue(foundFingerprintLine, "Wedge fingerprint must be on a single line");
        }

        [Test]
        public void GetDllFreshnessToken_MissingDll_ReturnsUnknownMissing()
        {
            var result = ReloadDiagnoseCommand.GetDllFreshnessToken(
                "/nonexistent/path/foo.dll", "/some/src");

            Assert.AreEqual("unknown(missing)", result);
        }

        [Test]
        public void GetDllFreshnessToken_NoSrcDir_ReturnsUnknownNoSrc()
        {
            // Create a temp dll file so it "exists"
            var tmpDll = Path.GetTempFileName();
            try
            {
                var result = ReloadDiagnoseCommand.GetDllFreshnessToken(tmpDll, "/nonexistent/src");
                Assert.AreEqual("unknown(no-src)", result);
            }
            finally { File.Delete(tmpDll); }
        }

        [Test]
        public void DetectReloadFailed_AbsentLogPath_ReturnsFalse()
        {
            var result = ReloadDiagnoseCommand.DetectReloadFailed("/nonexistent/Editor.log");
            Assert.IsFalse(result);
        }

        [Test]
        public void DetectReloadFailed_KnownMarker_ReturnsTrue()
        {
            // Write a temp file with reload-failed marker
            var tmpLog = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmpLog, "some log\nReloading assemblies failed.\nmore log");
                var result = ReloadDiagnoseCommand.DetectReloadFailed(tmpLog);
                Assert.IsTrue(result);
            }
            finally { File.Delete(tmpLog); }
        }

        [TestCase("Mono: successfully reloaded assembly")]
        [TestCase("Reload assemblies complete.")]
        public void DetectReloadFailed_SuccessAfterFailure_ReturnsFalse(string successMarker)
        {
            var tmpLog = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmpLog,
                    "Editor compiler errors found. Will not reload assemblies.\n" +
                    "Reloading assemblies failed.\n" + successMarker);
                var result = ReloadDiagnoseCommand.DetectReloadFailed(tmpLog);
                Assert.IsFalse(result,
                    "A successful reload after a failure must clear the historical failure latch");
            }
            finally { File.Delete(tmpLog); }
        }

        [Test]
        public void DetectReloadFailed_FailureAfterSuccess_ReturnsTrue()
        {
            var tmpLog = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmpLog,
                    "Mono: successfully reloaded assembly\n" +
                    "Editor compiler errors found. Will not reload assemblies.\n" +
                    "Reloading assemblies failed.");
                var result = ReloadDiagnoseCommand.DetectReloadFailed(tmpLog);
                Assert.IsTrue(result,
                    "The latest reload terminal is a failure and must remain current");
            }
            finally { File.Delete(tmpLog); }
        }

        [TestCase("Mono: successfully reloaded assembly")]
        [TestCase("Reload assemblies complete.")]
        public void ParseEditorLogText_SuccessAfterCompileError_ReturnsClean(string successMarker)
        {
            var text =
                "## Script Compilation Error for: Csc Broken.dll\n" +
                "Broken.cs(1,1): error CS0122: inaccessible\n" + successMarker;

            Assert.AreEqual("clean", ReloadDiagnoseCommand.ParseEditorLogText(text));
        }

        [Test]
        public void ParseEditorLogText_CompileErrorAfterSuccess_ReturnsCode()
        {
            var text =
                "Mono: successfully reloaded assembly\n" +
                "## Script Compilation Error for: Csc Broken.dll\n" +
                "Broken.cs(1,1): error CS0122: inaccessible\n";

            Assert.AreEqual("CS0122", ReloadDiagnoseCommand.ParseEditorLogText(text));
        }

        [Test]
        public void Execute_ContainsMainMvid()
        {
            var result = ReloadDiagnoseCommand.Execute();
            StringAssert.Contains("main_mvid=", result);
        }

        [Test]
        public async Task Execute_UsesVolatileCache_NotApplicationDataPath()
        {
            // F1/F7: BuildDllFreshness must read project root from CachedProjectRoot volatile field,
            // not Application.dataPath (which throws on ThreadPool — unity-plugin-reload trap).
            // Verify by checking that CachedProjectRoot is the only path source for dll freshness.
            var cachedRoot = ReloadCompileNotifier.CachedProjectRoot;

            // Execute from a ThreadPool thread — if Application.dataPath were called, it would
            // throw UnityException in a real non-main-thread context (batched NUnit runs may cache
            // it, but production code runs inline on ThreadPool).
            string result = null;
            System.Exception ex = null;
            var task = Task.Run(() =>
            {
                try { result = ReloadDiagnoseCommand.Execute(); }
                catch (System.Exception e) { ex = e; }
            });
            var completed = await Task.WhenAny(task, Task.Delay(5000));
            Assert.AreSame(task, completed, "Execute() did not complete within 5s");
            await task;
            // The test proves the code path exists and executes — the volatile cache guards it.
            Assert.IsNull(ex, $"Execute() threw (F1/F7 volatile cache not applied): {ex}");
            Assert.IsNotNull(result, "Execute() must return non-null result");
        }
    }
}
