// TDD: DiagnoseCommand — C8. Read-only multi-signal snapshot tests.
// Verifies wire-format fields present + read-only (no epoch bump).
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DiagnoseCommandTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MockSyncOps _mock;

        [SetUp]
        public void SetUp()
        {
            _mock = new MockSyncOps();
            SyncHelper.OverrideOpsForTest(_mock);
            SyncHelper.ResetForTest();
            SyncHelper.OverrideDomainStampForTest("");
            CompileErrorCapture.Clear();
            // Clean CompileNotifier state so Bee cache-hit logic starts from idle-never
            SessionState.EraseFloat("MCP_CompileStart");
            SessionState.EraseFloat("MCP_LastDuration");
            SessionState.EraseBool("MCP_CompileFailed");
            CompileNotifier.NowSecondsFloat = () => (float)UnityEditor.EditorApplication.timeSinceStartup;
        }

        [TearDown]
        public void TearDown()
        {
            SyncHelper.ResetForTest();
            CompileErrorCapture.Clear();
            SessionState.EraseFloat("MCP_CompileStart");
            SessionState.EraseFloat("MCP_LastDuration");
            SessionState.EraseBool("MCP_CompileFailed");
            CompileNotifier.NowSecondsFloat = () => (float)UnityEditor.EditorApplication.timeSinceStartup;
        }

        // C8 #1: Execute returns all required wire-format field prefixes
        [Test]
        public void DiagnoseCommand_Execute_ReturnsAllFields()
        {
            var result = DiagnoseCommand.Execute("{}");

            StringAssert.Contains("mvid=", result, "must contain mvid=");
            StringAssert.Contains("stamp=", result, "must contain stamp=");
            StringAssert.Contains("compile=", result, "must contain compile=");
            StringAssert.Contains("sync=", result, "must contain sync=");
            StringAssert.Contains("iscompiling=", result, "must contain iscompiling=");
            StringAssert.Contains("cn_active=", result, "must contain cn_active=");
            StringAssert.Contains("started=", result, "must contain started=");
            StringAssert.Contains("stamp_frozen=", result, "must contain stamp_frozen=");
            StringAssert.Contains("dlls=", result, "must contain dlls=");
            StringAssert.Contains("errors=", result, "must contain errors=");
            StringAssert.Contains("log=", result, "must contain log=");
            StringAssert.Contains("main_mvid=", result, "BLOCKER2: must contain main_mvid=");
        }

        // BLOCKER2: main_mvid= field contains a non-absent GUID (assembly is loaded)
        [Test]
        public void DiagnoseCommand_Execute_MainMvid_IsPresent_AndNotAbsent()
        {
            var result = DiagnoseCommand.Execute("{}");
            // main_mvid= must be present and contain a real GUID (not "absent")
            // so Python _parse_diagnose can compare it as heal proof
            StringAssert.Contains("main_mvid=", result, "main_mvid= field must be emitted");
            StringAssert.DoesNotContain("main_mvid=absent", result,
                "main_mvid must not be 'absent' — UnityMCP.Editor is this running assembly");
        }

        // C8 #2: Execute is read-only — does NOT bump epoch
        [Test]
        public void DiagnoseCommand_Execute_IsReadOnly_DoesNotMutateSyncState()
        {
            var epochBefore = SyncHelper.CurrentEpoch;

            DiagnoseCommand.Execute("{}");

            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch,
                "diagnose must NOT bump epoch (read-only command)");
        }

        // C8 #3: errors= field reflects CompileErrorCapture.GetErrors()
        [Test]
        public void DiagnoseCommand_Execute_ErrorsField_ReflectsCompileErrors()
        {
            CompileErrorCapture.InjectForTest("Foo.cs:1:1: error CS0001: test");

            var result = DiagnoseCommand.Execute("{}");

            StringAssert.Contains("CS0001", result, "errors= field must contain injected CS0001");
        }

        // C8 #4: stamp= field shows UNDETERMINED when no stamp set
        [Test]
        public void DiagnoseCommand_Execute_Stamp_UNDETERMINED_WhenNoStamp()
        {
            SyncHelper.OverrideDomainStampForTest("");

            var result = DiagnoseCommand.Execute("{}");

            StringAssert.Contains("stamp=UNDETERMINED", result,
                "stamp= must be UNDETERMINED when CurrentDomainStamp is empty");
        }

        // C8 #F3a: GetDllFreshnessToken — stale when .cs newer than dll
        [Test]
        public void GetDllFreshnessToken_Stale_WhenCsNewerThanDll()
        {
            using var scope = new TempDirScope("McpF3Test");
            var tmp = scope.Path;
            var dllPath = Path.Combine(tmp, "Test.dll");
            var csPath  = Path.Combine(tmp, "Code.cs");

            // dll older than .cs
            File.WriteAllText(dllPath, "dll");
            File.SetLastWriteTimeUtc(dllPath, DateTime.UtcNow.AddSeconds(-10));
            File.WriteAllText(csPath, "// cs");
            File.SetLastWriteTimeUtc(csPath, DateTime.UtcNow);

            var token = DiagnoseCommand.GetDllFreshnessToken(dllPath, tmp);
            Assert.AreEqual("stale", token, "stale when .cs is newer than dll");
        }

        // C8 #F3b: GetDllFreshnessToken — fresh when dll newer than all .cs
        [Test]
        public void GetDllFreshnessToken_Fresh_WhenDllNewerThanCs()
        {
            using var scope = new TempDirScope("McpF3Test");
            var tmp = scope.Path;
            var dllPath = Path.Combine(tmp, "Test.dll");
            var csPath  = Path.Combine(tmp, "Code.cs");

            File.WriteAllText(csPath, "// cs");
            File.SetLastWriteTimeUtc(csPath, DateTime.UtcNow.AddSeconds(-10));
            File.WriteAllText(dllPath, "dll");
            File.SetLastWriteTimeUtc(dllPath, DateTime.UtcNow);

            var token = DiagnoseCommand.GetDllFreshnessToken(dllPath, tmp);
            Assert.AreEqual("fresh", token, "fresh when dll is newer than all .cs");
        }

        // C8 #F3c: GetDllFreshnessToken — unknown(missing) when dll doesn't exist
        [Test]
        public void GetDllFreshnessToken_Unknown_WhenDllMissing()
        {
            using var scope = new TempDirScope("McpF3Test");
            var dllPath = Path.Combine(scope.Path, "Missing.dll");
            var token   = DiagnoseCommand.GetDllFreshnessToken(dllPath, scope.Path);
            Assert.AreEqual("unknown(missing)", token, "unknown(missing) when dll absent");
        }

        // C8 #5: diagnose is registered in CommandRegistry
        [Test]
        public void DiagnoseCommand_IsRegistered_InCommandRegistry()
            => Assert.IsTrue(CommandRegistry.IsRegistered("diagnose"),
                "diagnose must be registered in CommandRegistry");

        // G29: GetKnownDlls enumerates dynamically — more than the 2 hardcoded names
        [Test]
        public void GetKnownDlls_DynamicEnumeration_MoreThanTwoEntries()
        {
            var dlls = DiagnoseCommand.GetKnownDlls();
            Assert.IsNotNull(dlls, "GetKnownDlls must not return null");
            // Unity has many editor asmdefs — the dynamic list must exceed the old hardcoded 2
            Assert.Greater(dlls.Length, 2,
                $"G29: dynamic enumeration must exceed 2 hardcoded names, got: {string.Join(", ", dlls)}");
        }

        // G29: GetKnownDlls includes the Chat.Tests asmdef that caused the incident
        [Test]
        public void GetKnownDlls_IncludesChatTestsDll()
        {
            var dlls = DiagnoseCommand.GetKnownDlls();
            // Any entry containing "Chat.Tests" or "Chat" + "Tests" covers the incident assembly
            bool found = false;
            foreach (var dll in dlls)
            {
                if (dll.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    dll.IndexOf("Tests", StringComparison.OrdinalIgnoreCase) >= 0)
                { found = true; break; }
            }
            Assert.IsTrue(found,
                $"G29: Chat.Tests dll must appear in dynamic enumeration. Got: {string.Join(", ", dlls)}");
        }

        // C10: DetectReloadFailed(logPath) — returns true when reload-failed marker present
        [Test]
        public void DetectReloadFailed_ReturnsTrue_ForReloadingAssembliesFailedText()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "Reloading assemblies failed.");
                Assert.IsTrue(DiagnoseCommand.DetectReloadFailed(tmp),
                    "C10: must return true for 'Reloading assemblies failed.' marker");
            }
            finally { File.Delete(tmp); }
        }

        // C10: DetectReloadFailed(logPath) — returns true for the aborted-reload marker
        [Test]
        public void DetectReloadFailed_ReturnsTrue_ForReloadAbortedText()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "Editor compiler errors found. Will not reload assemblies.");
                Assert.IsTrue(DiagnoseCommand.DetectReloadFailed(tmp),
                    "C10: must return true for 'Editor compiler errors found. Will not reload assemblies.' marker");
            }
            finally { File.Delete(tmp); }
        }

        // C10: DetectReloadFailed(logPath) — returns false when no marker present
        [Test]
        public void DetectReloadFailed_ReturnsFalse_WhenNoMarker()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "Normal editor output, no failures here.");
                Assert.IsFalse(DiagnoseCommand.DetectReloadFailed(tmp),
                    "C10: must return false when no reload-failed marker present");
            }
            finally { File.Delete(tmp); }
        }

        [TestCase("Mono: successfully reloaded assembly")]
        [TestCase("Reload assemblies complete.")]
        public void DetectReloadFailed_ReturnsFalse_WhenSuccessFollowsFailure(string successMarker)
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp,
                    "Editor compiler errors found. Will not reload assemblies.\n" +
                    "Reloading assemblies failed.\n" + successMarker);
                Assert.IsFalse(DiagnoseCommand.DetectReloadFailed(tmp),
                    "A successful reload after a failure must clear the historical failure latch");
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void DetectReloadFailed_ReturnsTrue_WhenFailureFollowsSuccess()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp,
                    "Mono: successfully reloaded assembly\n" +
                    "Editor compiler errors found. Will not reload assemblies.\n" +
                    "Reloading assemblies failed.");
                Assert.IsTrue(DiagnoseCommand.DetectReloadFailed(tmp),
                    "The latest reload terminal is a failure and must remain current");
            }
            finally { File.Delete(tmp); }
        }

        [TestCase("Mono: successfully reloaded assembly")]
        [TestCase("Reload assemblies complete.")]
        public void ParseEditorLogText_ReturnsClean_WhenSuccessFollowsCompileError(string successMarker)
        {
            var text =
                "## Script Compilation Error for: Csc Broken.dll\n" +
                "Broken.cs(1,1): error CS0122: inaccessible\n" + successMarker;

            Assert.AreEqual("clean", DiagnoseCommand.ParseEditorLogText(text));
        }

        [Test]
        public void ParseEditorLogText_ReturnsCodes_WhenCompileErrorFollowsSuccess()
        {
            var text =
                "Mono: successfully reloaded assembly\n" +
                "## Script Compilation Error for: Csc Broken.dll\n" +
                "Broken.cs(1,1): error CS0122: inaccessible\n";

            Assert.AreEqual("CS0122", DiagnoseCommand.ParseEditorLogText(text));
        }

        // C10: Execute output contains reload_failed= field
        [Test]
        public void DiagnoseCommand_Execute_ContainsReloadFailedField()
        {
            var result = DiagnoseCommand.Execute("{}");
            StringAssert.Contains("reload_failed=", result,
                "C10: Execute() must emit reload_failed= field in wire output");
        }

        // Bee cache-hit: mtime stale + compile=idle → "fresh" (not "stale")
        [Test]
        public void GetDllFreshnessToken_StaleButIdleCompile_ReturnsFresh()
        {
            using var scope = new TempDirScope("McpBeeHit");
            var tmp = scope.Path;
            // Save and restore CompileNotifier state
            var savedNow = CompileNotifier.NowSecondsFloat;
            try
            {
                var dllPath = Path.Combine(tmp, "Test.dll");
                var csPath  = Path.Combine(tmp, "Code.cs");

                // dll older than .cs → mtime says stale
                File.WriteAllText(dllPath, "dll");
                File.SetLastWriteTimeUtc(dllPath, DateTime.UtcNow.AddSeconds(-10));
                File.WriteAllText(csPath, "// cs");
                File.SetLastWriteTimeUtc(csPath, DateTime.UtcNow);

                // Simulate idle compile (Bee cache-hit): StartKey=0, duration>0, no fail
                SessionState.EraseFloat("MCP_CompileStart");
                SessionState.SetFloat("MCP_LastDuration", 1.5f);
                SessionState.EraseBool("MCP_CompileFailed");

                var token = DiagnoseCommand.GetDllFreshnessToken(dllPath, tmp);
                Assert.AreEqual("fresh", token,
                    "Bee cache-hit (idle compile, no failure) must override mtime-stale to fresh");
            }
            finally
            {
                CompileNotifier.NowSecondsFloat = savedNow;
            }
        }

        // C8 #6: stamp_frozen=true when DomainStamp matches StampAtTrigger
        [Test]
        public void DiagnoseCommand_StampFrozen_True_WhenStampsMatch()
        {
            SyncHelper.OverrideDomainStampForTest("FREEZE_STAMP");
            SessionState.SetString("MCP_StampAtTrigger", "FREEZE_STAMP");

            var result = DiagnoseCommand.Execute("{}");

            StringAssert.Contains("stamp_frozen=true", result,
                "stamp_frozen must be true when domain stamp equals StampAtTrigger");
        }

        // Fix C #1: FindAsmdefDir — Assets/ scan still works (regression guard)
        [Test]
        public void FindAsmdefDir_AssetsPath_StillWorks()
        {
            using var scope = new TempDirScope("McpFixC");
            var tmp = scope.Path;
            File.WriteAllText(Path.Combine(tmp, "MyLib.asmdef"), "{}");
            var result = DiagnoseCommand.FindAsmdefDir(tmp, "MyLib");
            Assert.AreEqual(tmp, result, "Must find asmdef via Directory.GetFiles scan");
        }

        // Fix C #2: FindAsmdefDir falls back to FindInPackages when Assets/ empty
        [Test]
        public void FindAsmdefDir_FallsBackToPackages_WhenAssetsEmpty()
        {
            using var dataScope = new TempDirScope("McpFixCEmpty");
            using var pkgScope  = new TempDirScope("McpFixCPkg");
            var tmpDataPath = dataScope.Path;
            var tmpPkgDir   = pkgScope.Path;
            var originalSeam = DiagnoseCommand.FindInPackages;
            try
            {
                DiagnoseCommand.FindInPackages = (f) =>
                    f == "UnityMCP.Editor.asmdef" ? tmpPkgDir : null;
                var result = DiagnoseCommand.FindAsmdefDir(tmpDataPath, "UnityMCP.Editor");
                Assert.AreEqual(tmpPkgDir, result, "Must fall back to Packages/ via injected seam");
            }
            finally
            {
                DiagnoseCommand.FindInPackages = originalSeam;
            }
        }

        // Fix C #3: GetDllFreshnessToken detects stale dll with seam-injected UPM src dir
        [Test]
        public void BuildDllFreshness_ReturnsStale_ForUPMPackage_ViaSeam()
        {
            using var scope = new TempDirScope("McpFixCStale");
            var tmp = scope.Path;
            var originalSeam = DiagnoseCommand.FindInPackages;
            try
            {
                var dllPath = Path.Combine(tmp, "Fake.dll");
                var csPath  = Path.Combine(tmp, "Code.cs");
                File.WriteAllText(dllPath, "dll");
                File.SetLastWriteTimeUtc(dllPath, DateTime.UtcNow.AddSeconds(-5));
                File.WriteAllText(csPath, "// code");
                File.SetLastWriteTimeUtc(csPath, DateTime.UtcNow);

                // Seam returns tmp as the package source dir
                DiagnoseCommand.FindInPackages = (_) => tmp;
                // GetDllFreshnessToken directly — dll is older than .cs
                var token = DiagnoseCommand.GetDllFreshnessToken(dllPath, tmp);
                Assert.AreEqual("stale", token,
                    "Must detect stale dll when .cs is newer than dll");
            }
            finally
            {
                DiagnoseCommand.FindInPackages = originalSeam;
            }
        }

        // T1: empty or non-existent srcDir → unknown(no-src) (file: package outside Assets/).
        [Test]
        public void GetDllFreshnessToken_Returns_UnknownNoSrc_WhenSrcDirEmpty()
        {
            using var scope = new TempDirScope("McpNoSrcTest");
            var dllPath = Path.Combine(scope.Path, "Fake.dll");
            File.WriteAllBytes(dllPath, new byte[] { 0 }); // dll exists

            Assert.AreEqual("unknown(no-src)",
                DiagnoseCommand.GetDllFreshnessToken(dllPath, ""),
                "empty srcDir → unknown(no-src) (file: package outside Assets/)");

            Assert.AreEqual("unknown(no-src)",
                DiagnoseCommand.GetDllFreshnessToken(dllPath, "/nonexistent/path/xyz"),
                "missing srcDir → unknown(no-src)");
        }

        // T2: FindAsmdefDir returns "" when both Assets/ scan and FindInPackages return null.
        [Test]
        public void FindAsmdefDir_Returns_Empty_WhenFindInPackages_ReturnsNull()
        {
            var originalSeam = DiagnoseCommand.FindInPackages;
            try
            {
                DiagnoseCommand.FindInPackages = (_) => null; // simulate unregistered package
                using var tmp = new TempDirScope("McpEmpty");
                // dataPath has NO .asmdef files
                var result = DiagnoseCommand.FindAsmdefDir(tmp.Path, "UnityMCP.Missing");
                Assert.AreEqual("", result,
                    "When Assets/ scan misses and FindInPackages returns null, must return empty string");
            }
            finally { DiagnoseCommand.FindInPackages = originalSeam; }
        }

        // T8: end-to-end BuildDllFreshness shows unknown(no-src) when package is unreachable.
        [Test]
        public void BuildDllFreshness_Returns_UnknownNoSrc_WhenFindInPackagesReturnsNull()
        {
            var originalSeam = DiagnoseCommand.FindInPackages;
            try
            {
                DiagnoseCommand.FindInPackages = (_) => null; // package not found
                var output = DiagnoseCommand.Execute("{}");
                var dllsLine = System.Linq.Enumerable.FirstOrDefault(
                    output.Split('\n'), l => l.StartsWith("dlls=")) ?? "";
                Assert.IsTrue(
                    dllsLine.Contains("unknown(no-src)") || dllsLine.Contains("unknown(missing)"),
                    $"When FindInPackages returns null, dll freshness must degrade to unknown token: {dllsLine}");
            }
            finally { DiagnoseCommand.FindInPackages = originalSeam; }
        }

        // Issue #53 Fix B: all_errors= must be emitted AFTER substate=/port=/port_fallback=
        [Test]
        public void DiagnoseCommand_AllErrors_IsLastField()
        {
            var result = DiagnoseCommand.Execute("{}");
            var lines = result.Split('\n');
            int substateIdx  = System.Array.FindIndex(lines, l => l.TrimEnd().StartsWith("substate="));
            int portIdx      = System.Array.FindIndex(lines, l => l.TrimEnd().StartsWith("port=") &&
                                                                  !l.TrimEnd().StartsWith("port_fallback="));
            int allErrorsIdx = System.Array.FindIndex(lines, l => l.TrimEnd().StartsWith("all_errors="));

            Assert.GreaterOrEqual(substateIdx, 0, "substate= must be present in wire output");
            Assert.GreaterOrEqual(portIdx, 0,     "port= must be present in wire output");
            Assert.GreaterOrEqual(allErrorsIdx, 0,"all_errors= must be present in wire output");

            Assert.Greater(allErrorsIdx, substateIdx,
                "all_errors= must come AFTER substate= (protocol contract)");
            Assert.Greater(allErrorsIdx, portIdx,
                "all_errors= must come AFTER port= (protocol contract)");
        }

        // Issue #53 Fix C: BuildDllFreshness must scan Assets/ exactly once (not N times)
        [Test]
        public void BuildDllFreshness_ScanOnce()
        {
            int callCount = 0;
            var originalScan = DiagnoseCommand.ScanAssets;
            var originalPkgs = DiagnoseCommand.ScanPackages;
            try
            {
                DiagnoseCommand.ScanAssets  = (_) => { callCount++; return new System.Collections.Generic.Dictionary<string, string>(); };
                DiagnoseCommand.ScanPackages = ()  => new System.Collections.Generic.Dictionary<string, string>();

                DiagnoseCommand.Execute("{}");

                Assert.AreEqual(1, callCount,
                    "ScanAssets must be called exactly once regardless of assembly count");
            }
            finally
            {
                DiagnoseCommand.ScanAssets  = originalScan;
                DiagnoseCommand.ScanPackages = originalPkgs;
            }
        }

        // Issue #53 Fix C: BuildDllFreshness must complete in <500ms with seam (scan-once path)
        [Test]
        public void BuildDllFreshness_Performance()
        {
            // Build a 150-entry fake map — simulates large project
            var fakeMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var scope = new TempDirScope("McpPerfTest");
            for (int i = 0; i < 150; i++)
                fakeMap[$"FakeAsm{i}"] = scope.Path;

            var originalScan = DiagnoseCommand.ScanAssets;
            var originalPkgs = DiagnoseCommand.ScanPackages;
            try
            {
                DiagnoseCommand.ScanAssets  = (_) => fakeMap;
                DiagnoseCommand.ScanPackages = ()  => new System.Collections.Generic.Dictionary<string, string>();

                var sw = System.Diagnostics.Stopwatch.StartNew();
                DiagnoseCommand.Execute("{}");
                sw.Stop();

                // 500ms is generous to handle slow CI disk I/O for real DLL mtime checks.
                // On fast dev machines this should be <100ms.
                Assert.Less(sw.ElapsedMilliseconds, 500,
                    $"BuildDllFreshness must complete in <500ms with scan-once seam, took {sw.ElapsedMilliseconds}ms");
            }
            finally
            {
                DiagnoseCommand.ScanAssets  = originalScan;
                DiagnoseCommand.ScanPackages = originalPkgs;
            }
        }
    }
}
