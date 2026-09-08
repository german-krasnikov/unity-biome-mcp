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

        [TestCase(false)]
        [TestCase(true)]
        public void KnownSourceMismatch_SurvivesMissingOrUnreadableLoadedAssembly(bool throws)
        {
            var token = DiagnoseCommand.ResolveLoadedFreshness("stale", () =>
                throws ? throw new IOException("unreadable") : "unknown(not-loaded)");
            Assert.That(token, Is.EqualTo("stale"));
        }

        [Test]
        public void LoadedAssemblyMismatch_OverridesFreshSource()
            => Assert.That(DiagnoseCommand.ResolveLoadedFreshness("fresh", () => "stale"), Is.EqualTo("stale"));

        [Test]
        public void KnownMismatch_SurvivesLaterTargetObservationFailure()
        {
            int call = 0;
            var entries = new UnityEditor.Compilation.Assembly[] { null, null };
            var result = DiagnoseCommand.BuildDllFreshness(entries, _ =>
                ++call == 1 ? "Known:1:stale" : throw new IOException("unavailable next output"));
            StringAssert.Contains("Known:1:stale", result);
            StringAssert.Contains("unknown(source-inventory)", result);
            Assert.That(call, Is.EqualTo(2));
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
        public void GetDllFreshnessToken_NewerSourceWithoutPdb_IsUnknown()
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
            Assert.AreEqual("unknown(missing-pdb)", token);
        }

        // C8 #F3b: GetDllFreshnessToken — fresh when dll newer than all .cs
        [Test]
        public void GetDllFreshnessToken_OlderSourceWithoutPdb_IsUnknown()
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
            Assert.AreEqual("unknown(missing-pdb)", token);
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
        public void GetDllFreshnessToken_IdleCannotCertifySourceWithoutPdb()
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
                Assert.AreEqual("unknown(missing-pdb)", token,
                    "an idle status is not source-to-output proof");
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

        [Test]
        public void BuildDllFreshness_ReadsCompilerInventoryOnce()
        {
            var original = DiagnoseCommand.CompilerAssemblies;
            var count = 0;
            try
            {
                DiagnoseCommand.CompilerAssemblies = () =>
                {
                    count++;
                    return Array.Empty<UnityEditor.Compilation.Assembly>();
                };
                Assert.That(DiagnoseCommand.BuildDllFreshness(), Is.EqualTo("none"));
                Assert.That(count, Is.EqualTo(1));
            }
            finally { DiagnoseCommand.CompilerAssemblies = original; }
        }

        [Test]
        public void BuildDllFreshness_UnavailableCompilerInventory_IsUnknown()
        {
            var original = DiagnoseCommand.CompilerAssemblies;
            try
            {
                DiagnoseCommand.CompilerAssemblies = () => throw new IOException("unavailable");
                Assert.That(DiagnoseCommand.BuildDllFreshness(), Is.EqualTo("unknown(source-inventory)"));
            }
            finally { DiagnoseCommand.CompilerAssemblies = original; }
        }
    }
}
