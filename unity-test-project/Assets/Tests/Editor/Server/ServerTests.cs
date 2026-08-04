// v0.25.10
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    // Tests for MCPServer stability improvements:
    // Tier 0: ProcessMainThreadQueue re-registration
    // Tier 2: State file write/delete
    // Tier 4a: SendGoingAwaySync
    // Tier 4b: Status fast-path response format
    [TestFixture]
    public class ServerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _stateDirectory;

        private string StateFilePath() =>
            PortFileManager.StateFilePath(_stateDirectory);

        [SetUp]
        public void SetUp()
        {
            _stateDirectory = Path.Combine(
                Path.GetTempPath(), "unity-biome-mcp-state-tests",
                Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(_stateDirectory))
                    Directory.Delete(_stateDirectory, true);
            });
        }

        // ── Tier 0 ────────────────────────────────────────────────────────────

        [Test]
        public void StartAsync_ReRegistersProcessMainThreadQueue()
        {
            // Verify MainThreadDispatcher.Drain IS registered (server was started by [InitializeOnLoad]).
            // Extracted from MCPServer.ProcessMainThreadQueue to MainThreadDispatcher.Drain during
            // the Phase 2 structural split (ROI reliability sprint, M1).
            if (UnityEngine.Application.isBatchMode)
                Assert.Ignore("EditorApplication.update delegates are not populated in headless batchmode");
            var updateDelegate = EditorApplication.update;
            bool found = false;
            if (updateDelegate != null)
            {
                foreach (var d in updateDelegate.GetInvocationList())
                {
                    if (d.Method.Name == "Drain")
                    {
                        found = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(found, "MainThreadDispatcher.Drain must be in EditorApplication.update");
        }

        // ── Tier 2 ────────────────────────────────────────────────────────────

        [Test]
        public void WriteStateFile_CreatesFileWithCorrectFormat()
        {
            PortFileManager.WriteStateFile("compiling", _stateDirectory);

            var path = StateFilePath();
            Assert.IsTrue(File.Exists(path), "State file must exist");

            var lines = File.ReadAllLines(path);
            // State file: line 0=state, 1=timestamp, 2=pid, 3=epoch (added v0.21)
            Assert.GreaterOrEqual(lines.Length, 3, "State file must have at least 3 lines (state, timestamp, PID)");
            Assert.AreEqual("compiling", lines[0]);
            Assert.IsTrue(double.TryParse(lines[1], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var ts),
                "Second line must be a valid number");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            Assert.Less(Math.Abs(now - ts), 5.0, "Timestamp must be within 5 seconds of now");
        }

        [Test]
        public void WriteStateFile_AtomicNoPartialReads()
        {
            // Write 100 times and verify every read is always 2 complete lines
            for (int i = 0; i < 100; i++)
            {
                PortFileManager.WriteStateFile(
                    i % 2 == 0 ? "ready" : "compiling", _stateDirectory);
                var path = StateFilePath();
                if (!File.Exists(path)) continue;
                var content = File.ReadAllText(path);
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Assert.GreaterOrEqual(lines.Length, 3, $"Iteration {i}: file must have 3 lines, got: '{content}'");
            }
        }

        [Test]
        public void DeleteStateFile_RemovesFile()
        {
            PortFileManager.WriteStateFile("ready", _stateDirectory);
            Assert.IsTrue(File.Exists(StateFilePath()), "File must exist before delete");

            PortFileManager.DeleteStateFile(_stateDirectory);
            Assert.IsFalse(File.Exists(StateFilePath()), "File must not exist after delete");
        }

        [Test]
        public void StateFilePath_UsesExactFixtureDirectory()
        {
            Assert.AreEqual(
                Path.GetFullPath(_stateDirectory),
                Path.GetFullPath(Path.GetDirectoryName(StateFilePath())));
        }

        [Test]
        public void DefaultStateDirectory_RemainsProductionLocation()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp", "state");
            Assert.AreEqual(expected, PortFileManager.DefaultStateDirectory());
        }

        [Test]
        public void WriteStateFile_CreatesMissingDirectory()
        {
            Assert.IsFalse(Directory.Exists(_stateDirectory));

            Assert.DoesNotThrow(() =>
                PortFileManager.WriteStateFile("ready", _stateDirectory));
            Assert.IsTrue(File.Exists(StateFilePath()), "File must be created even if dir was missing");
        }

        // ── Tier 4a ───────────────────────────────────────────────────────────

        [Test]
        public void SendGoingAwaySync_WritesCorrectFrameToStream()
        {
            using var ms = new MemoryStream();
            MCPServer.SendGoingAwaySync(ms);

            ms.Position = 0;
            var headerBytes = new byte[4];
            ms.Read(headerBytes, 0, 4);
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(headerBytes);

            var payload = new byte[length];
            ms.Read(payload, 0, (int)length);
            var json = System.Text.Encoding.UTF8.GetString(payload);

            Assert.IsTrue(json.Contains("\"going_away\""), $"Payload must contain going_away, got: {json}");
            Assert.IsTrue(json.Contains("\"ev\""), $"Payload must have 'ev' key, got: {json}");
        }

        [Test]
        public void SendGoingAwaySync_NullStream_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MCPServer.SendGoingAwaySync(null));
        }

        [Test]
        public void SendGoingAwaySync_DisposedStream_DoesNotThrow()
        {
            var ms = new MemoryStream();
            ms.Dispose();
            Assert.DoesNotThrow(() => MCPServer.SendGoingAwaySync(ms));
        }

        // ── Tier 4b ───────────────────────────────────────────────────────────

        [Test]
        public void FormatStatusResponse_NotCompiling_HasCompileFalse()
        {
            var json = MCPServer.FormatStatusResponse("msg-1", isCompiling: false, elapsed: 0.0);
            Assert.IsTrue(json.Contains("\"compile\":false"), $"Expected compile:false, got: {json}");
            Assert.IsTrue(json.Contains("\"ok\":true"), $"Expected ok:true, got: {json}");
            Assert.IsTrue(json.Contains("idle"), $"Expected 'idle' in data, got: {json}");
        }

        [Test]
        public void FormatStatusResponse_Compiling_HasCompileTrue()
        {
            var json = MCPServer.FormatStatusResponse("msg-2", isCompiling: true, elapsed: 3.5);
            Assert.IsTrue(json.Contains("\"compile\":true"), $"Expected compile:true, got: {json}");
            Assert.IsTrue(json.Contains("compiling"), $"Expected 'compiling' in data, got: {json}");
            Assert.IsTrue(json.Contains("3.5"), $"Expected elapsed '3.5' in data, got: {json}");
        }

        // ── Tier 1b: TCS timeout ──────────────────────────────────────────────

        [Test]
        public async System.Threading.Tasks.Task CreateTcsWithTimeout_TimesOutAndReturnsError()
        {
            // Verify the TCS timeout pattern used in HandleClientAsync:
            // a TCS with linked 25s CancellationTokenSource correctly cancels.
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            using var cmdTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            using var reg = cmdTimeout.Token.Register(() =>
                tcs.TrySetResult("{\"ok\":false,\"err\":\"timeout\",\"retry\":2000}"));

            var completed = await System.Threading.Tasks.Task.WhenAny(
                tcs.Task, System.Threading.Tasks.Task.Delay(500));
            Assert.That(completed, Is.SameAs(tcs.Task),
                "TCS must resolve within timeout period");

            var result = await tcs.Task;
            Assert.IsTrue(result.Contains("timeout"), $"Result must indicate timeout, got: {result}");
            Assert.IsTrue(result.Contains("\"ok\":false"), $"Result must be ok:false, got: {result}");
        }

        [Test]
        public async System.Threading.Tasks.Task CreateTcsWithTimeout_CompletesBeforeTimeout_NoError()
        {
            // Verify that if the TCS resolves before timeout, the timeout doesn't override it.
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            using var cmdTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            using var reg = cmdTimeout.Token.Register(() =>
                tcs.TrySetResult("{\"ok\":false,\"err\":\"timeout\",\"retry\":2000}"));

            // Complete it immediately with success
            tcs.TrySetResult("{\"ok\":true,\"data\":\"done\"}");

            var completed = await System.Threading.Tasks.Task.WhenAny(
                tcs.Task, System.Threading.Tasks.Task.Delay(500));
            Assert.That(completed, Is.SameAs(tcs.Task), "TCS must resolve");
            var result = await tcs.Task;
            Assert.IsTrue(result.Contains("\"ok\":true"),
                $"Early completion must win over timeout, got: {result}");
        }

        // ── Tier 2b: State file race ──────────────────────────────────────────

        [Test]
        public void WriteStateFile_Ready_CanBeCalledExplicitly()
        {
            // Verify WriteStateFile("ready") is callable from StartAsync context
            // (the ONLY correct place per RC5 fix).
            Assert.DoesNotThrow(() =>
                PortFileManager.WriteStateFile("ready", _stateDirectory));
            var path = StateFilePath();
            Assert.IsTrue(File.Exists(path), "State file must exist after explicit WriteStateFile(ready)");
            var lines = File.ReadAllLines(path);
            Assert.AreEqual("ready", lines[0], "State must be 'ready'");
        }

        [Test]
        public void CompilationFinished_WritesRestartingWhenServerNotRunning()
        {
            PortFileManager.WriteStateFile("restarting", _stateDirectory);
            var path = StateFilePath();
            Assert.IsTrue(File.Exists(path));
            var lines = File.ReadAllLines(path);
            Assert.AreEqual("restarting", lines[0], "state file accepts 'restarting' state");
        }

        // ── Tier 5: Shutdown guard ────────────────────────────────────────────

        [Test]
        public void ProcessMainThreadQueue_ChecksShuttingDown()
        {
            // Source-level check: MainThreadDispatcher.Drain must check _shuttingDown.
            // Extracted from MCPServer.ProcessMainThreadQueue during the Phase 2 structural
            // split (ROI reliability sprint, M1) — the queue-drain loop now lives here.
            var source = File.ReadAllText(
                Path.Combine(UnityEngine.Application.dataPath,
                    "../Packages/com.unity-biome-mcp.editor/Editor/MainThreadDispatcher.cs"));
            var idx = source.IndexOf("internal static void Drain()");
            Assert.Greater(idx, 0, "Drain must exist");
            var endIdx = source.IndexOf("internal static void Clear()", idx);
            var body = source.Substring(idx, endIdx - idx);
            Assert.IsTrue(body.Contains("_shuttingDown"),
                "Drain must check _shuttingDown");
        }

        [Test]
        public void Stop_ContainsQueueDrain()
        {
            // Source-level check: Stop must call TeardownCore (which drains the dispatcher queue)
            var source = File.ReadAllText(
                Path.Combine(UnityEngine.Application.dataPath,
                    "../Packages/com.unity-biome-mcp.editor/Editor/MCPServer.cs"));
            var stopIdx = source.IndexOf("public static void Stop()");
            var nextMethodIdx = source.IndexOf("private static void OnQuit()", stopIdx);
            var stopBody = source.Substring(stopIdx, nextMethodIdx - stopIdx);
            Assert.IsTrue(stopBody.Contains("TeardownCore"),
                "Stop() must call TeardownCore (which drains the dispatcher queue)");
            // Verify TeardownCore itself drains the queue. Phase 2 structural split (ROI
            // reliability sprint, M1) moved the raw _mainThreadQueue field into
            // MainThreadDispatcher; TeardownCore now drains it via MainThreadDispatcher.Clear().
            var teardownIdx = source.IndexOf("private static void TeardownCore()");
            var teardownEndIdx = source.IndexOf("public static void Stop()", teardownIdx);
            var teardownBody = source.Substring(teardownIdx, teardownEndIdx - teardownIdx);
            Assert.IsTrue(teardownBody.Contains("MainThreadDispatcher.Clear()"),
                "TeardownCore() must drain the dispatcher queue via MainThreadDispatcher.Clear()");
        }

        // ── Double domain reload resilience ───────────────────────────────────

        [Test]
        public void Stop_ClearsListenerField()
        {
            // Verify Stop() or its callees set _listener to null (prevents stale IsRunning after double reload)
            var stopMethod = typeof(MCPServer).GetMethod("Stop", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(stopMethod, "Stop() must exist");
            var listenerField = typeof(MCPServer).GetField("_listener",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(listenerField, "_listener field must exist");
            // Source-level check: TeardownCore (called by Stop) must contain "_listener = null"
            var source = File.ReadAllText(
                Path.Combine(UnityEngine.Application.dataPath,
                    "../Packages/com.unity-biome-mcp.editor/Editor/MCPServer.cs"));
            var teardownIdx = source.IndexOf("private static void TeardownCore()");
            Assert.Greater(teardownIdx, 0, "TeardownCore() must exist (called by Stop)");
            var teardownEndIdx = source.IndexOf("public static void Stop()", teardownIdx);
            var teardownBody = source.Substring(teardownIdx, teardownEndIdx - teardownIdx);
            Assert.IsTrue(teardownBody.Contains("_listener = null"),
                "Stop() must set _listener = null to prevent stale IsRunning");
        }

        [Test]
        public void OnBeforeReload_ClearsListenerField()
        {
            var source = File.ReadAllText(
                Path.Combine(UnityEngine.Application.dataPath,
                    "../Packages/com.unity-biome-mcp.editor/Editor/MCPServer.cs"));
            // OnBeforeReload delegates to TeardownCore — verify TeardownCore sets _listener = null
            var teardownIdx = source.IndexOf("private static void TeardownCore()");
            Assert.Greater(teardownIdx, 0, "TeardownCore() must exist (called by OnBeforeReload)");
            var teardownEndIdx = source.IndexOf("public static void Stop()", teardownIdx);
            var teardownBody = source.Substring(teardownIdx, teardownEndIdx - teardownIdx);
            Assert.IsTrue(teardownBody.Contains("_listener = null"),
                "OnBeforeReload() must set _listener = null");
        }

        // ── State file PID line ───────────────────────────────────────────────

        [Test]
        public void WriteStateFile_ContainsPidOnThirdLine()
        {
            PortFileManager.WriteStateFile("ready", _stateDirectory);
            var lines = File.ReadAllLines(StateFilePath());
            // State file: line 0=state, 1=timestamp, 2=pid, 3=epoch (added v0.21)
            Assert.GreaterOrEqual(lines.Length, 3, "State file must have at least 3 lines");
            Assert.IsTrue(int.TryParse(lines[2], out var pid), "Third line must be integer PID");
            Assert.AreEqual(System.Diagnostics.Process.GetCurrentProcess().Id, pid);
        }

        // ── going_away ordering ───────────────────────────────────────────────

        [Test]
        public void OnBeforeReload_SendsGoingAwayBeforeCancellingCts()
        {
            // Verify ordering: OnBeforeReload calls SendGoingAwaySync BEFORE TeardownCore
            // (TeardownCore contains _clientCts?.Cancel)
            var source = File.ReadAllText(
                Path.Combine(UnityEngine.Application.dataPath,
                    "../Packages/com.unity-biome-mcp.editor/Editor/MCPServer.cs"));
            var onBeforeIdx = source.IndexOf("private static void OnBeforeReload()");
            Assert.Greater(onBeforeIdx, 0, "OnBeforeReload must exist");
            // Find SendGoingAwaySync call inside OnBeforeReload body
            var goingAwayAfterReload = source.IndexOf("SendGoingAwaySync", onBeforeIdx);
            Assert.Greater(goingAwayAfterReload, 0, "SendGoingAwaySync must exist after OnBeforeReload");
            // Find TeardownCore call inside OnBeforeReload body (it contains _clientCts?.Cancel)
            var teardownCallAfterReload = source.IndexOf("TeardownCore()", onBeforeIdx);
            Assert.Greater(teardownCallAfterReload, 0, "_clientCts?.Cancel must exist after OnBeforeReload");
            Assert.Less(goingAwayAfterReload, teardownCallAfterReload,
                "SendGoingAwaySync must come BEFORE _clientCts?.Cancel in OnBeforeReload");
        }

        // ── TestRunner resilience ─────────────────────────────────────────────

        [Test]
        public void TestRunner_HasDurableReloadObserverRegistration()
        {
            var registration = typeof(UnityMCP.Editor.TestRunner).Assembly.GetType(
                "UnityMCP.Editor.TestRuns.TestRunObserverRegistration", false);
            Assert.IsNotNull(registration,
                "The durable observer registration must exist in the Editor assembly");
            var attributes = registration.GetCustomAttributes(
                typeof(InitializeOnLoadAttribute), false);
            Assert.AreEqual(1, attributes.Length,
                "The durable observer must be recreated after every domain reload");
        }
    }

    /// <summary>
    /// Structural and precision tests for QueuePlayerLoopUpdate presence,
    /// G4 float format, and MultiView reflection cache.
    /// These tests do NOT require Unity to run commands — they verify
    /// code structure and format correctness only.
    /// </summary>
    [TestFixture]
    public class MCPServerPrecisionAndQueueTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ── F13: G4 float precision ──────────────────────────────────────────────

        [Test]
        public void GetPropertyValueString_Float_G4Precision_Pi()
        {
            // 3.14159... with G4 rounds to 4 significant figures → "3.142"
            Assert.AreEqual("3.142", 3.14159f.ToString("G4", IC));
        }

        [Test]
        public void GetPropertyValueString_Float_G4Precision_Half()
        {
            // 0.5 has 1 significant figure, G4 keeps it as-is → "0.5"
            Assert.AreEqual("0.5", 0.5f.ToString("G4", IC));
        }

        [Test]
        public void GetPropertyValueString_Float_G4Precision_One()
        {
            // 1.0 → "1"
            Assert.AreEqual("1", 1.0f.ToString("G4", IC));
        }

        // ── F02: QueuePlayerLoopUpdate present in MCPServer.cs ──────────────────

        [Test]
        public void MCPServer_SourceContains_QueuePlayerLoopUpdate()
        {
            // Verify the source file contains the call — structural test.
            // Moved from MCPServer.cs to ClientConnectionHandler.cs during the Phase 2
            // structural split (ROI reliability sprint, M1) — the client message pump
            // (and its QueuePlayerLoopUpdate wakeup) now lives there.
            var repoRoot = FindRepoRoot();
            if (repoRoot == null)
            {
                Assert.Fail("Could not locate repo root to verify ClientConnectionHandler.cs");
                return;
            }
            var path = Path.Combine(repoRoot, "unity-plugin", "Editor", "ClientConnectionHandler.cs");
            Assert.IsTrue(File.Exists(path), $"ClientConnectionHandler.cs not found at {path}");
            var source = File.ReadAllText(path);
            StringAssert.Contains("QueuePlayerLoopUpdate", source,
                "ClientConnectionHandler.cs must call EditorApplication.QueuePlayerLoopUpdate() after enqueue");
        }

        // ── F18: MultiViewCapture delegates rendering to ScreenshotCapture ────────

        [Test]
        public void MultiViewCapture_SourceDelegatesToScreenshotCapture()
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot == null)
            {
                Assert.Fail("Could not locate repo root to verify MultiViewCapture.cs");
                return;
            }
            var path = Path.Combine(repoRoot, "unity-plugin", "Editor", "MultiViewCapture.cs");
            Assert.IsTrue(File.Exists(path), $"MultiViewCapture.cs not found at {path}");
            var source = File.ReadAllText(path);
            StringAssert.Contains("ScreenshotCapture.RenderOffscreen", source,
                "MultiViewCapture.cs must delegate to ScreenshotCapture.RenderOffscreen (FIX-29)");
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        /// <summary>Walk up from the test file's assembly location to find the repo root.</summary>
        private static string FindRepoRoot()
        {
            // Try known absolute path first (CI / dev machine)
            const string knownRoot = "/Users/german/Work/python/unity-biome-mcp";
            if (Directory.Exists(knownRoot))
                return knownRoot;

            // Walk up from current directory
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "unity-plugin", "package.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
