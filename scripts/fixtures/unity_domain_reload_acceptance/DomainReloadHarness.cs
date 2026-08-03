using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Worker.DomainReloadAcceptance
{
    [InitializeOnLoad]
    internal static class BatchWorkerMcpBootstrap
    {
        static BatchWorkerMcpBootstrap()
        {
            if (!Application.isBatchMode) return;
            ConfigureReloadPort();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.delayCall += MCPServer.StartAsync;
        }

        private static void Stop()
        {
            MCPServer.Stop();
        }

        private static void ConfigureReloadPort()
        {
            var control = AcceptanceFiles.TryReadControl();
            if (control == null || !File.Exists(AcceptanceFiles.TracePath)) return;

            var ordinal = 0;
            foreach (var line in File.ReadAllLines(AcceptanceFiles.TracePath))
            {
                var fields = (line ?? "").Split('|');
                if (fields.Length == 4 && fields[0] == "queued" &&
                    int.TryParse(fields[1], out var queued))
                    ordinal = Math.Max(ordinal, queued);
            }
            var port = ordinal == 1
                ? control.reload_port_1
                : ordinal == 2 ? control.reload_port_2 : 0;
            var chatPort = ordinal == 1
                ? control.reload_chat_port_1
                : ordinal == 2 ? control.reload_chat_port_2 : 0;
            if (port <= 0 || chatPort <= 0) return;

            var manager = typeof(MCPServer).Assembly.GetType(
                "UnityMCP.Editor.PortFileManager", true);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var saveRuntimePorts = manager.GetMethod("SaveRuntimePorts", flags);
            if (saveRuntimePorts == null)
                throw new MissingMethodException(manager.FullName, "SaveRuntimePorts");
            saveRuntimePorts.Invoke(null, new object[] { port, chatPort });
        }
    }

    [TestFixture]
    [Category("UnityMCP.DomainReloadAcceptance")]
    [BiomeWorkerOnly(
        "Forces one or two domain reloads and requires an external acceptance controller.")]
    public sealed class DomainReloadHarness : UnityMcpTestBase
    {
        private const int ControlWaitMilliseconds = 120000;

        [Test]
        [Order(0)]
        public async Task LeafBeforeReloadBoundary()
        {
            var control = AcceptanceFiles.RequireControl();
            Assert.That(File.Exists(AcceptanceFiles.TracePath), Is.True,
                "The acceptance runner must create an empty trace before dispatch.");
            Assert.That(new FileInfo(AcceptanceFiles.TracePath).Length, Is.Zero,
                "The worker trace contains evidence from another run.");
            AcceptanceFiles.AppendTrace("leaf_before", 0, control);
            await Task.Yield();
        }

        [Test]
        [Order(1)]
        public async Task LeafBetweenReloadBoundaries()
        {
            var control = AcceptanceFiles.RequireControl();
            var entries = AcceptanceFiles.RequireTrace(
                control,
                new TraceExpectation("leaf_before", 0),
                new TraceExpectation("queued", 1));
            Assert.That(entries[0].Generation, Is.EqualTo(entries[1].Generation));
            Assert.That(entries[1].Generation, Is.Not.EqualTo(
                AcceptanceFiles.DomainGeneration));
            AcceptanceFiles.AppendTrace("resumed", 1, control);

            if (control.target_reloads < 2)
            {
                await Task.Yield();
                return;
            }

            var timeout = Stopwatch.StartNew();
            while (!AcceptanceFiles.RequireControl().allow_second_reload)
            {
                Assert.That(timeout.ElapsedMilliseconds,
                    Is.LessThan(ControlWaitMilliseconds),
                    "The acceptance runner did not reconnect before reload two.");
                await Task.Delay(25);
            }
        }

        [Test]
        [Order(2)]
        public async Task LeafAfterReloadBoundaries()
        {
            var control = AcceptanceFiles.RequireControl();
            if (control.target_reloads == 1)
            {
                var entries = AcceptanceFiles.RequireTrace(
                    control,
                    new TraceExpectation("leaf_before", 0),
                    new TraceExpectation("queued", 1),
                    new TraceExpectation("resumed", 1));
                Assert.That(entries[0].Generation, Is.EqualTo(entries[1].Generation));
                Assert.That(entries[1].Generation, Is.Not.EqualTo(entries[2].Generation));
                Assert.That(entries[2].Generation,
                    Is.EqualTo(AcceptanceFiles.DomainGeneration));
            }
            else
            {
                var entries = AcceptanceFiles.RequireTrace(
                    control,
                    new TraceExpectation("leaf_before", 0),
                    new TraceExpectation("queued", 1),
                    new TraceExpectation("resumed", 1),
                    new TraceExpectation("queued", 2));
                Assert.That(entries[0].Generation, Is.EqualTo(entries[1].Generation));
                Assert.That(entries[1].Generation, Is.Not.EqualTo(entries[2].Generation));
                Assert.That(entries[2].Generation, Is.EqualTo(entries[3].Generation));
                Assert.That(entries[3].Generation, Is.Not.EqualTo(
                    AcceptanceFiles.DomainGeneration));
                AcceptanceFiles.AppendTrace("resumed", 2, control);
            }

            AcceptanceFiles.AppendTrace("leaf_after", 0, control);
            AcceptanceFiles.ArchiveControl(control);
            await Task.Yield();
        }
    }

    [InitializeOnLoad]
    internal static class DomainReloadBoundaryCoordinator
    {
        private static readonly BoundaryCallbacks Callbacks;

        static DomainReloadBoundaryCoordinator()
        {
            Callbacks = new BoundaryCallbacks();
            // UnityMCP's durable observer uses -1000. Run after it has persisted
            // the boundary leaf's terminal callback.
            TestRunnerApi.RegisterTestCallback(Callbacks, -10000);
        }

        private sealed class BoundaryCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result == null || result.TestStatus != TestStatus.Passed) return;
                var fullName = result?.Test?.FullName ?? "";
                var ordinal = fullName == AcceptanceFiles.BeforeLeafName
                    ? 1
                    : fullName == AcceptanceFiles.BetweenLeafName ? 2 : 0;
                if (ordinal == 0) return;

                var control = AcceptanceFiles.TryReadControl();
                if (control == null || control.target_reloads < ordinal ||
                    !AcceptanceFiles.IsBoundaryReady(control, ordinal)) return;
                try
                {
                    PrepareActiveUtfJobForReload();
                    AcceptanceFiles.AppendTrace("queued", ordinal, control);
                    EditorUtility.RequestScriptReload();
                    EditorApplication.UnlockReloadAssemblies();
                }
                catch (Exception error)
                {
                    try
                    {
                        AcceptanceFiles.AppendTrace("reload_error", ordinal, control);
                    }
                    finally
                    {
                        AcceptanceFiles.QuarantineControl(control);
                    }
                    UnityEngine.Debug.LogException(error);
                    throw;
                }
            }
        }

        private static void PrepareActiveUtfJobForReload()
        {
            var utfAssembly = typeof(TestRunnerApi).Assembly;
            var holderType = utfAssembly.GetType(
                "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder", true);
            var instanceProperty = holderType.GetProperty(
                "instance",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var holder = instanceProperty?.GetValue(null);
            if (holder == null)
                throw new InvalidOperationException("UTF TestJobDataHolder is unavailable.");

            var runsField = holderType.GetField(
                "TestRuns", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance);
            if (!(runsField?.GetValue(holder) is System.Collections.IList jobs))
                throw new InvalidOperationException("UTF running job list is unavailable.");

            object activeJob = null;
            foreach (var job in jobs)
            {
                if (job == null) continue;
                var running = job.GetType().GetField(
                    "isRunning", BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance);
                if (running?.GetValue(job) is bool value && value)
                {
                    if (activeJob != null)
                        throw new InvalidOperationException(
                            "More than one UTF job is active in the disposable worker.");
                    activeJob = job;
                }
            }
            if (activeJob == null)
                throw new InvalidOperationException("No active UTF job was found.");

            var jobType = activeJob.GetType();
            var runner = jobType.GetField(
                "editModeRunner", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance)?.GetValue(activeJob);
            var serializer = jobType.GetField(
                "testRunnerStateSerializer", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance)?.GetValue(activeJob);
            if (runner == null || serializer == null)
                throw new InvalidOperationException(
                    "The active UTF runner cannot be serialized for reload.");

            var prepare = runner.GetType().GetMethod(
                "PrepareForDomainReload", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prepare == null)
                throw new MissingMethodException(
                    runner.GetType().FullName, "PrepareForDomainReload");
            try
            {
                prepare.Invoke(runner, new[] { serializer });
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                throw error.InnerException;
            }
        }
    }

    internal static class AcceptanceFiles
    {
        internal const int SchemaVersion = 1;
        internal const string BeforeLeafName =
            "UnityMCP.Worker.DomainReloadAcceptance.DomainReloadHarness." +
            "LeafBeforeReloadBoundary";
        internal const string BetweenLeafName =
            "UnityMCP.Worker.DomainReloadAcceptance.DomainReloadHarness." +
            "LeafBetweenReloadBoundaries";
        internal static readonly string DomainGeneration = Guid.NewGuid().ToString("N");

        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string EvidenceRoot => Path.Combine(
            ProjectRoot, "Library", "UnityMCP", "DomainReloadAcceptance");

        internal static string TracePath =>
            Path.Combine(EvidenceRoot, "harness-events.log");

        private static string ControlPath => Path.Combine(EvidenceRoot, "control.json");

        private static string CompletedControlPath =>
            Path.Combine(EvidenceRoot, "control.completed.json");

        internal static Control TryReadControl()
        {
            if (!File.Exists(ControlPath)) return null;
            try
            {
                return JsonUtility.FromJson<Control>(File.ReadAllText(ControlPath));
            }
            catch
            {
                return null;
            }
        }

        internal static Control RequireControl()
        {
            var markerPath = Path.Combine(
                ProjectRoot, "Library", "UnityMCP", "disposable-worker.json");
            Assert.That(File.Exists(markerPath), Is.True,
                "Domain reload acceptance is allowed only in a disposable worker.");
            var marker = JsonUtility.FromJson<WorkerMarker>(File.ReadAllText(markerPath));
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.disposable, Is.True);
            Assert.That(marker.unity_version, Is.EqualTo("6000.0.65f1"));
            Assert.That(marker.utf_version, Is.EqualTo("1.6.0"));

            Assert.That(File.Exists(ControlPath), Is.True,
                "The acceptance control file is missing.");
            var control = JsonUtility.FromJson<Control>(File.ReadAllText(ControlPath));
            Assert.That(control, Is.Not.Null);
            Assert.That(control.schema_version, Is.EqualTo(SchemaVersion));
            Assert.That(control.target_reloads, Is.InRange(1, 2));
            Assert.That(control.scenario_id, Is.Not.Empty);
            return control;
        }

        internal static TraceEntry[] RequireTrace(
            Control control, params TraceExpectation[] expected)
        {
            var lines = File.ReadAllLines(TracePath);
            Assert.That(lines.Length, Is.EqualTo(expected.Length));
            var entries = new TraceEntry[lines.Length];
            for (var index = 0; index < lines.Length; index++)
            {
                entries[index] = TraceEntry.Parse(lines[index]);
                Assert.That(entries[index].Kind, Is.EqualTo(expected[index].Kind));
                Assert.That(entries[index].Ordinal, Is.EqualTo(expected[index].Ordinal));
                Assert.That(entries[index].ScenarioId, Is.EqualTo(control.scenario_id));
            }
            return entries;
        }

        internal static bool IsBoundaryReady(Control control, int ordinal)
        {
            if (control == null || !File.Exists(TracePath)) return false;
            var expected = ordinal == 1
                ? new[] { new TraceExpectation("leaf_before", 0) }
                : ordinal == 2 && control.allow_second_reload
                    ? new[]
                    {
                        new TraceExpectation("leaf_before", 0),
                        new TraceExpectation("queued", 1),
                        new TraceExpectation("resumed", 1)
                    }
                    : null;
            if (expected == null) return false;

            try
            {
                var lines = File.ReadAllLines(TracePath);
                if (lines.Length != expected.Length) return false;
                for (var index = 0; index < lines.Length; index++)
                {
                    var entry = TraceEntry.Parse(lines[index]);
                    if (entry.Kind != expected[index].Kind ||
                        entry.Ordinal != expected[index].Ordinal ||
                        entry.ScenarioId != control.scenario_id)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void AppendTrace(string kind, int ordinal, Control control)
        {
            Directory.CreateDirectory(EvidenceRoot);
            File.AppendAllText(
                TracePath,
                string.Join("|", kind, ordinal, DomainGeneration, control.scenario_id) + "\n");
        }

        internal static void ArchiveControl(Control control)
        {
            var active = TryReadControl();
            Assert.That(active, Is.Not.Null, "Acceptance control disappeared before completion.");
            Assert.That(active.scenario_id, Is.EqualTo(control.scenario_id),
                "Acceptance control identity changed before completion.");
            if (File.Exists(CompletedControlPath)) File.Delete(CompletedControlPath);
            File.Move(ControlPath, CompletedControlPath);
        }

        internal static void QuarantineControl(Control control)
        {
            var active = TryReadControl();
            if (active == null || active.scenario_id != control.scenario_id) return;

            var failedPath = Path.Combine(
                EvidenceRoot, "control.failed." + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.Move(ControlPath, failedPath);
            }
            catch (FileNotFoundException)
            {
                // The controller may have quarantined the same control first.
            }
        }

        [Serializable]
        private sealed class WorkerMarker
        {
            public bool disposable;
            public string unity_version = "";
            public string utf_version = "";
        }

        [Serializable]
        internal sealed class Control
        {
            public int schema_version;
            public string scenario_id = "";
            public int target_reloads;
            public bool allow_second_reload;
            public int reload_port_1;
            public int reload_chat_port_1;
            public int reload_port_2;
            public int reload_chat_port_2;
        }
    }

    internal sealed class TraceExpectation
    {
        internal readonly string Kind;
        internal readonly int Ordinal;

        internal TraceExpectation(string kind, int ordinal)
        {
            Kind = kind;
            Ordinal = ordinal;
        }
    }

    internal sealed class TraceEntry
    {
        internal string Kind = "";
        internal int Ordinal;
        internal string Generation = "";
        internal string ScenarioId = "";

        internal static TraceEntry Parse(string line)
        {
            var fields = (line ?? "").Split('|');
            Assert.That(fields.Length, Is.EqualTo(4), "Malformed harness trace line.");
            Assert.That(int.TryParse(fields[1], out var ordinal), Is.True,
                "Malformed reload ordinal in harness trace.");
            Assert.That(fields[2], Is.Not.Empty, "Missing domain generation.");
            Assert.That(fields[3], Is.Not.Empty, "Missing scenario identity.");
            return new TraceEntry
            {
                Kind = fields[0],
                Ordinal = ordinal,
                Generation = fields[2],
                ScenarioId = fields[3]
            };
        }
    }
}
