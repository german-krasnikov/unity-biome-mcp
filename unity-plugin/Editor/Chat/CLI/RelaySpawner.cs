// Launches chat_relay.py sidecar, reads port from stdout, persists to SessionState.
// Relay survives domain reload — static field _process is null post-reload but
// IsProcessAlive() reattaches via PID stored in SessionState.
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class RelaySpawner
    {
        internal const string PortKey = "MCPChat_Relay_Port";
        internal const string PidKey  = "MCPChat_Relay_PID";

        internal static int  RelayPort => ReadPort();
        internal static int  RelayPid  => ReadPid();
        internal static bool IsRunning => CurrentProcess != null && !CurrentProcess.HasExited;

        internal static event Action OnAfterReloadResume;

        // Test seams — replace to inject mocks
        internal static Func<ProcessStartInfo, Process>   ProcessFactory  = psi => Process.Start(psi);
        // Returns (command, argv) as one unit so the args half can never be silently dropped
        // (pre-existing bug: the old PythonResolver-only seam discarded args for Local+uv installs).
        internal static Func<(string cmd, string[] argv)> CommandResolver = RelayCommandResolver.Resolve;
        internal static TimeSpan                          ReadTimeout    = TimeSpan.FromSeconds(5);
        internal static TimeSpan                          RetryDelay     = TimeSpan.FromSeconds(2);
#if UNITY_INCLUDE_TESTS
        // Override EnsureRunning entirely in unit tests — prevents GetProcessById(selfPid)
        // from attaching _process to the Unity editor, which would cause Stop() to kill it.
        internal static Func<int>      EnsureRunningOverride;
        // Override TCP alive check — avoids real connection in unit tests.
        internal static Func<int, bool> TcpAliveOverride;
#endif

        private static Process  _process;
        private static DateTime _tcpAliveExpiry;
        private static bool     _tcpAliveResult;

#if UNITY_INCLUDE_TESTS
        private sealed class TestRuntime : IDisposable
        {
            internal readonly TestRuntime Previous;
            internal int Port;
            internal int Pid;
            internal Process Process;
            private readonly Func<ProcessStartInfo, Process> _processFactory;
            private readonly Func<(string cmd, string[] argv)> _commandResolver;
            private readonly TimeSpan _readTimeout;
            private readonly TimeSpan _retryDelay;
            private readonly Func<int> _ensureRunningOverride;
            private readonly Func<int, bool> _tcpAliveOverride;
            private readonly Func<RelayChatProcess> _relayBackendProcessFactory;
            private readonly DateTime _tcpAliveExpiry;
            private readonly bool _tcpAliveResult;
            private bool _disposed;

            internal TestRuntime(TestRuntime previous)
            {
                Previous = previous;
                _processFactory = ProcessFactory;
                _commandResolver = CommandResolver;
                _readTimeout = ReadTimeout;
                _retryDelay = RetryDelay;
                _ensureRunningOverride = EnsureRunningOverride;
                _tcpAliveOverride = TcpAliveOverride;
                _relayBackendProcessFactory = RelayBackend.ProcessFactory;
                _tcpAliveExpiry = RelaySpawner._tcpAliveExpiry;
                _tcpAliveResult = RelaySpawner._tcpAliveResult;
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (!ReferenceEquals(_testRuntime, this))
                    throw new InvalidOperationException(
                        "Relay test-isolation scopes must be disposed in reverse order.");

                try { Process?.Kill(); } catch { }
                try { Process?.Dispose(); } catch { }
                Process = null;
                Port = 0;
                Pid = 0;
                ProcessFactory = _processFactory;
                CommandResolver = _commandResolver;
                ReadTimeout = _readTimeout;
                RetryDelay = _retryDelay;
                EnsureRunningOverride = _ensureRunningOverride;
                TcpAliveOverride = _tcpAliveOverride;
                RelayBackend.ProcessFactory = _relayBackendProcessFactory;
                RelaySpawner._tcpAliveExpiry = _tcpAliveExpiry;
                RelaySpawner._tcpAliveResult = _tcpAliveResult;
                _testRuntime = Previous;
                _disposed = true;
            }
        }

        private static TestRuntime _testRuntime;

        internal static IDisposable BeginTestIsolation()
        {
            var scope = new TestRuntime(_testRuntime);
            _testRuntime = scope;
            return scope;
        }

        internal static void StopForTests()
        {
            RequireTestRuntime();
            Stop();
        }

        internal static void SetSessionForTests(int port, int pid)
        {
            var runtime = RequireTestRuntime();
            runtime.Port = port;
            runtime.Pid = pid;
        }

        private static TestRuntime RequireTestRuntime()
        {
            return _testRuntime ?? throw new InvalidOperationException(
                "Relay test state requires an active UnityMcpTestBase isolation scope.");
        }
#endif

        private static Process CurrentProcess
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (_testRuntime != null) return _testRuntime.Process;
#endif
                return _process;
            }
            set
            {
#if UNITY_INCLUDE_TESTS
                if (_testRuntime != null)
                {
                    _testRuntime.Process = value;
                    return;
                }
#endif
                _process = value;
            }
        }

        private static int ReadPort()
        {
#if UNITY_INCLUDE_TESTS
            if (_testRuntime != null) return _testRuntime.Port;
#endif
            return SessionState.GetInt(PortKey, 0);
        }

        private static int ReadPid()
        {
#if UNITY_INCLUDE_TESTS
            if (_testRuntime != null) return _testRuntime.Pid;
#endif
            return SessionState.GetInt(PidKey, 0);
        }

        private static void WriteSession(int port, int pid)
        {
#if UNITY_INCLUDE_TESTS
            if (_testRuntime != null)
            {
                _testRuntime.Port = port;
                _testRuntime.Pid = pid;
                return;
            }
#endif
            if (port > 0) SessionState.SetInt(PortKey, port);
            else SessionState.EraseInt(PortKey);
            if (pid > 0) SessionState.SetInt(PidKey, pid);
            else SessionState.EraseInt(PidKey);
        }

        static RelaySpawner()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload  += OnAfterReload;
            EditorApplication.quitting                += Stop;
        }

        /// <summary>Ensure relay is running. Returns TCP port. Throws on failure.</summary>
        internal static int EnsureRunning()
        {
#if UNITY_INCLUDE_TESTS
            if (EnsureRunningOverride != null) return EnsureRunningOverride();
#endif
            var port = ReadPort();
            var pid  = ReadPid();
            if (port > 0 && IsProcessAlive(pid) && IsTcpAlive(port))
            {
                // Reattach handle after domain reload (_process is null post-reload)
                if (CurrentProcess == null || CurrentProcess.HasExited)
                    try { CurrentProcess = Process.GetProcessById(pid); } catch { /* process died */ }
                return port;
            }
            return Spawn();
        }

        /// <summary>Kill relay process. Called on Unity quit or Stop().</summary>
        internal static void Stop()
        {
            try { CurrentProcess?.Kill(); } catch { /* already gone */ }
            try { CurrentProcess?.Dispose(); } catch { }
            CurrentProcess = null;
            try
            {
                WriteSession(0, 0);
            }
            catch { /* editor shutting down */ }
        }

        // Relay survives domain reload — just a no-op; socket close happens in RelayChatProcess
        internal static void OnBeforeReload() { }

        internal static void OnAfterReload()
        {
            // Re-attach _process handle lost during domain reload so IsRunning stays true.
            var port = ReadPort();
            var pid  = ReadPid();
            if (port > 0 && pid > 0 && IsProcessAlive(pid))
                try { CurrentProcess = Process.GetProcessById(pid); } catch { }
            OnAfterReloadResume?.Invoke();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        // Immutable result of PrepareSpawn() — everything ExecuteSpawn() needs, with zero
        // further Editor-API calls. Lets RelaySpawnState run the Editor-API resolution
        // (CommandResolver → InstallSourceDetector/EditorPrefs/PackageInfo) on the main thread
        // and hand only pure I/O (Process.Start + stdout read) to the ThreadPool.
        internal readonly struct SpawnPlan
        {
            internal readonly string   Cmd;
            internal readonly string[] Argv;
            internal readonly bool     IsLocal;
            internal readonly TimeSpan Timeout;

            internal SpawnPlan(string cmd, string[] argv, bool isLocal, TimeSpan timeout)
            {
                Cmd = cmd; Argv = argv; IsLocal = isLocal; Timeout = timeout;
            }
        }

        private static int Spawn()
        {
            var plan = PrepareSpawn();
            var (port, pid) = ExecuteSpawn(plan);
            CommitSpawn(port, pid);
            return port;
        }

        // MAIN THREAD ONLY — every call here touches an Editor API (CommandResolver goes
        // through InstallSourceDetector.Detect()/PackageInfo and ChatBinaryResolver/EditorPrefs).
        // Must run before any ThreadPool hop (see RelaySpawnState.RequestSpawn).
        internal static SpawnPlan PrepareSpawn()
        {
            var (cmd, argv) = CommandResolver();
            var isLocal     = InstallSourceDetector.Detect() == InstallSourceDetector.Source.Local;

            if (string.IsNullOrEmpty(cmd))
            {
                var hint = isLocal
                    ? "Python not found. Run: python install.py setup in the repo root."
                    : SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows
                        ? "uv not found. Install: winget install astral-sh.uv, then restart Unity."
                        : "uv not found. Install: brew install uv, then restart Unity.";
                throw new InvalidOperationException($"[MCP Relay] {hint}");
            }

            return new SpawnPlan(cmd, argv, isLocal, TimeoutFor(isLocal));
        }

        // Safe to run on the ThreadPool — Process.Start/StandardOutput/StandardError are plain
        // .NET, not Unity Editor APIs. Must NOT touch SessionState/EditorPrefs/PackageInfo/Debug.
        // Retries up to 3 times; kills the previous process before each retry to avoid zombies.
        internal static (int port, int pid) ExecuteSpawn(SpawnPlan plan)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt > 0)
                {
                    var msg = $"[MCP Relay] Retry {attempt}/2 after: {last?.Message}";
                    MainThreadDispatcher.Enqueue(() => UnityEngine.Debug.Log(msg));
                    System.Threading.Thread.Sleep(RetryDelay);
                }
                try { return ExecuteSpawnOnce(plan); }
                catch (Exception ex)
                {
                    last = ex;
                    try { CurrentProcess?.Kill(); } catch { }
                    try { CurrentProcess?.Dispose(); } catch { }
                    CurrentProcess = null;
                }
            }
            throw last!;
        }

        private static (int port, int pid) ExecuteSpawnOnce(SpawnPlan plan)
        {
            var psi = new ProcessStartInfo(plan.Cmd)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,   // capture uvx download noise
                CreateNoWindow         = true,
            };
            // ArgumentList (not the Arguments string) so spaces in serverDir paths are never
            // mis-split — each argv element is passed through verbatim.
            if (plan.Argv != null)
                foreach (var a in plan.Argv) psi.ArgumentList.Add(a);

            var process = ProcessFactory(psi);
            CurrentProcess = process;
            if (process == null)
                throw new InvalidOperationException("[MCP Relay] Process.Start returned null");

            // Always drain stderr: captures Python import errors for local installs,
            // uvx download progress for non-local. Buffer shared with catch block below.
            var stderrSb   = new StringBuilder();
            var stderrTask = Task.Run(() => DrainRelayStderr(process, stderrSb));

            try
            {
                var port = ReadRelayPortWithTimeout(process.StandardOutput, plan.Timeout);
                return (port, process.Id);
            }
            catch (InvalidOperationException)
            {
                // Process exited before printing port — wait briefly for stderr drain
                // (process is already dead so the drain task completes immediately).
                stderrTask.Wait(500);
                var stderr = stderrSb.ToString().Trim();
                throw new InvalidOperationException(
                    "[MCP Relay] Process exited without reporting port." +
                    (string.IsNullOrEmpty(stderr) ? "" : $"\nstderr: {stderr}"));
            }
        }

        // MAIN THREAD ONLY — SessionState is an Editor API.
        internal static void CommitSpawn(int port, int pid)
        {
            WriteSession(port, pid);
        }

        // Pure — testable without waiting out the real timeout.
        internal static TimeSpan TimeoutFor(bool isLocal) => isLocal ? ReadTimeout : TimeSpan.FromSeconds(45);

        private static void DrainRelayStderr(Process p, StringBuilder capture)
        {
            try
            {
                while (!p.HasExited)
                {
                    var line = p.StandardError.ReadLine();
                    if (line == null) continue;
                    capture.AppendLine(line);
                    var msg = $"[MCP Relay] {line}";
                    // Marshal onto the main thread — Debug.Log is not thread-safe in Unity 6
                    // (touches scene-repaint state internally) and this runs on the ThreadPool.
                    MainThreadDispatcher.Enqueue(() => UnityEngine.Debug.Log(msg));
                }
            }
            catch { /* process gone */ }
        }

        internal static int ParseRelayPort(string line)
        {
            const string prefix = "relay_port:";
            if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal))
                throw new FormatException(
                    $"[MCP Relay] Expected 'relay_port:PORT', got: {line ?? "null"}");
            if (!int.TryParse(line.Substring(prefix.Length).Trim(), out var port))
                throw new FormatException($"[MCP Relay] Non-integer port in: {line}");
            return port;
        }

        internal static bool IsProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            if (pid == System.Diagnostics.Process.GetCurrentProcess().Id) return false;
            try { return !Process.GetProcessById(pid).HasExited; }
            catch { return false; }
        }

        // Reads lines from reader until one starts with "relay_port:", skipping noise.
        // Throws TimeoutException if deadline passes without finding the port line.
        private static int ReadRelayPortWithTimeout(StreamReader reader, TimeSpan timeout)
        {
            const string prefix  = "relay_port:";
            var          deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                var task = Task.Run(() => reader.ReadLine());
                if (!task.Wait(remaining)) break;
                var line = task.Result;
                if (line == null) throw new InvalidOperationException("[MCP Relay] Process exited without reporting port");
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return ParseRelayPort(line);
                // else: noise line — continue waiting
            }
            throw new TimeoutException("[MCP Relay] Timed out waiting for relay to report port");
        }

        internal static bool IsTcpAlive(int port)
        {
#if UNITY_INCLUDE_TESTS
            if (TcpAliveOverride != null) return TcpAliveOverride(port);
#endif
            // Cache result for 3s — avoids blocking main thread on every EnsureRunning call.
            if (DateTime.UtcNow < _tcpAliveExpiry) return _tcpAliveResult;
            try
            {
                using var tcp = new TcpClient();
                _tcpAliveResult = tcp.ConnectAsync("127.0.0.1", port).Wait(200);
            }
            catch { _tcpAliveResult = false; }
            _tcpAliveExpiry = DateTime.UtcNow.AddSeconds(3);
            return _tcpAliveResult;
        }
    }
}
