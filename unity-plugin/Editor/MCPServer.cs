using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class MCPServer
    {
        private static TcpListener _listener;
        private static TcpListener _chatListener;
        // internal — read by ClientConnectionHandler.RunAcceptLoop to detect a StartAsync restart.
        internal static CancellationTokenSource _cts;
        // internal — set_client_label command updates Label for disconnect logging.
        internal static readonly ClientSlot _mainSlot = new ClientSlot();
        private static readonly ClientSlot _chatSlot = new ClientSlot();
        // internal — read by ClientConnectionHandler (fast-path handlers run off-main-thread).
        internal static volatile bool _shuttingDown;
        private static volatile bool _starting;
        internal static volatile bool _isCompiling;
        // Cached domain stamp — read from main thread in StartAsync, used in get_version fast-path
        // (which runs on ThreadPool after ConfigureAwait(false); SessionState not thread-safe).
        internal static volatile string _domainStamp = "";
        // Set on first compilationStarted; never cleared. Distinguishes real compile-in-progress
        // from post-domain-reload stale EditorApplication.isCompiling on Windows.
        private static volatile bool _compileStartedThisDomain;
        internal static DateTime _compileStartTime;

        public static void SavePorts(int port, int chatPort) => PortFileManager.SavePorts(port, chatPort);

        // Per-command timeout overrides (seconds). Default: 25s.
        private static readonly System.Collections.Generic.Dictionary<string, int> CommandTimeouts =
            new System.Collections.Generic.Dictionary<string, int>
            {
                { "run_tests", 130 },
                { "run_playtest", 130 },
                { "batch", 65 },
                { "wait_until", 30 },
                { "move_to", 30 },
                { "test_step", 30 },
                { "ask_user", 300 },
            };

        internal static int GetCommandTimeout(string cmd)
        {
            return CommandTimeouts.TryGetValue(cmd, out var t) ? t : 25;
        }

        public static bool IsRunning
        {
            get
            {
                try
                {
                    var l = _listener;
                    return l != null && l.Server != null && l.Server.IsBound;
                }
                catch { return false; }  // ObjectDisposedException during teardown
            }
        }

        public static bool IsChatListenerRunning
        {
            get
            {
                try
                {
                    var l = _chatListener;
                    return l != null && l.Server != null && l.Server.IsBound;
                }
                catch { return false; }
            }
        }
        public static bool IsClientConnected => _mainSlot.AnyConnected || _chatSlot.AnyConnected;
        public static int PhantomCount => _mainSlot.CountPhantoms() + _chatSlot.CountPhantoms();
        public static int KillPhantoms()
        {
            var killed = _mainSlot.KillPhantoms() + _chatSlot.KillPhantoms();
            if (killed > 0) UnityEngine.Debug.Log($"[MCP] Killed {killed} phantom connection(s)");
            return killed;
        }
        public static int ServerPort => PortFileManager.Port;
        public static int ServerChatPort => PortFileManager.ChatPort;
        // Reads reloadPort from MCP_Port.json. Returns 0 if reload-package is not installed.
        public static int ServerReloadPort => PortFileManager.ServerReloadPort;
        public static double CompileElapsedSeconds => _isCompiling
            ? (DateTime.UtcNow - _compileStartTime).TotalSeconds
            : 0.0;

        // True only if compilationStarted fired in THIS domain (never resets to false).
        // Used to detect post-reload stale EditorApplication.isCompiling on Windows:
        // if we never saw compilationStarted in this domain, isCompiling=true is a reload artifact.
        public static bool CompileStartedThisDomain => _compileStartedThisDomain;

        // Authoritative compile state: set by compilationStarted, cleared by compilationFinished.
        // Unlike EditorApplication.isCompiling, never stays latched after domain reload.
        internal static bool IsReallyCompiling => _isCompiling;

        private static double _lastWatchdogCheck;

        // Pure helper — no Unity API calls, fully unit-testable.
        // Mirrors ReloadPlugin.ShouldStartReloadServer pattern.
        internal static bool ShouldStartServer(bool isBatchMode) => !isBatchMode;

        static MCPServer()
        {
            if (!ShouldStartServer(Application.isBatchMode)) return;

            // Remove port files from dead PIDs before writing our own — prevents stale entries.
            PortFileManager.CleanStalePeerPortFiles();
            // Persist auto-assigned ports so they survive domain reload
            PortFileManager.SavePorts(PortFileManager.Port, PortFileManager.ChatPort);
            EditorApplication.update += MainThreadDispatcher.Drain;
            EditorApplication.update += WatchdogTick;
            EditorApplication.quitting += OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            CompilationPipeline.compilationStarted += _ => { _isCompiling = true; _compileStartedThisDomain = true; _compileStartTime = DateTime.UtcNow; WriteStateFile("compiling"); };
            CompilationPipeline.compilationFinished += _ =>
            {
                // R-4 fix: never write "ready" from compilationFinished.
                // If compile failed → no reload will happen, write compile_failed.
                // If compile succeeded → reload is coming; "ready" written after reload in StartAsync.
                _isCompiling = false;
                if (EditorUtility.scriptCompilationFailed)
                    WriteStateFile("compile_failed");
                // else: state stays "compiling" until reload completes (StartAsync writes "ready")
            };
            EditorSceneManager.sceneOpened += (_, _) => { RefManager.Invalidate(); HierarchySerializer.ResetIncrementalCache(); };
            EditorSceneManager.newSceneCreated += (_, _, _) => { RefManager.Invalidate(); HierarchySerializer.ResetIncrementalCache(); };
            EditorSceneManager.sceneClosed += _ => { RefManager.Invalidate(); HierarchySerializer.ResetIncrementalCache(); };
            // Delay async start until first update — SynchronizationContext dead in static ctor
            EditorApplication.delayCall += () => StartAsync();
        }

        private static void WatchdogTick()
        {
            if (_shuttingDown) return;
            if (EditorApplication.timeSinceStartup - _lastWatchdogCheck < 5.0) return;
            _lastWatchdogCheck = EditorApplication.timeSinceStartup;
            if (!IsRunning && !EditorApplication.isCompiling)
                StartAsync();
        }

        public static async void StartAsync()
        {
            // _starting closes the re-entrancy gap: during the bind-retry await window
            // IsRunning is false, so WatchdogTick could otherwise launch a 2nd StartAsync
            // → duplicate _cts/accept-loop churn.
            if (IsRunning || _starting) return;
            _starting = true;
            _shuttingDown = false;
            _domainStamp = SyncHelper.CurrentDomainStamp;  // cache on main thread — safe here, ThreadPool reads below
            // Ensure commands are registered BEFORE TCP bind.
            // InitDefaults() → RegisterAll() → populates _enabledToolsCache atomically.
            // Safe: runs on main thread after all [InitializeOnLoad] hooks (both delayCall
            // and EditorApplication.update fire after the full InitializeOnLoad sweep).
            CommandRegistry.InitDefaults();
            // Tier 0: re-register idempotently so restart after Stop() works
            EditorApplication.update -= MainThreadDispatcher.Drain;
            EditorApplication.update += MainThreadDispatcher.Drain;
            EditorApplication.update -= WatchdogTick;
            EditorApplication.update += WatchdogTick;
            CancellationTokenSource cts = null;
            try
            {
                cts = new CancellationTokenSource();
                _cts = cts;
                var token = cts.Token; // local copy — safe from null after Stop()/OnBeforeReload()

                // Main listener: 3 fast retries on same port, then fall back to a free port.
                // On Windows, ExclusiveAddressUse prevents TIME_WAIT reuse by the same process;
                // falling back to a new port avoids the full TIME_WAIT window.
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    var bindPort = (attempt < 3) ? PortFileManager.Port : PortResolver.FindFreePort(PortFileManager.Port + 1, skipPort: PortFileManager.ChatPort);
                    try
                    {
                        _listener = new TcpListener(IPAddress.Loopback, bindPort);
#if UNITY_EDITOR_WIN
                        // Windows: SO_REUSEADDR = port-hijack; use ExclusiveAddressUse instead (CoplayDev #1173)
                        _listener.Server.ExclusiveAddressUse = true;
#else
                        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                        try { _listener.Server.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x0200, true); } catch { }
#endif
#endif
                        _listener.Start();
                        if (bindPort != PortFileManager.Port)
                        {
                            var bp = bindPort; var origPort = PortFileManager.Port; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"[MCP] Port {origPort} unavailable (address in use), switched to {bp}"));
                            // SaveRuntimePorts: updates MCP_Port.json + {pid}.port but NOT MCPSettings.json.
                            // Preserves user intent so next reload retries the configured port, no cascade drift.
                            PortFileManager.SaveRuntimePorts(bindPort, PortFileManager.ChatPort);
                        }
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _listener?.Stop(); } catch { }
                        _listener = null;
                        if (attempt == 3) throw;
                        var bp2 = bindPort; var at = attempt; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"[MCP] Port {bp2} busy, retry {at + 1}/3..."));
                        await Task.Delay(400 * (attempt + 1), token).ConfigureAwait(false);
                    }
                }

                // Chat listener — best-effort (non-fatal if chat port is unavailable)
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var bindPort = (attempt < 2) ? PortFileManager.ChatPort : PortResolver.FindFreePort(PortFileManager.ChatPort + 1, skipPort: PortFileManager.Port);
                    try
                    {
                        _chatListener = new TcpListener(IPAddress.Loopback, bindPort);
#if UNITY_EDITOR_WIN
                        _chatListener.Server.ExclusiveAddressUse = true;
#else
                        _chatListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                        try { _chatListener.Server.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x0200, true); } catch { }
#endif
#endif
                        _chatListener.Start();
                        if (bindPort != PortFileManager.ChatPort)
                        {
                            var bp = bindPort; var origChatPort = PortFileManager.ChatPort; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {origChatPort} unavailable (address in use), switched to {bp}"));
                            PortFileManager.SaveRuntimePorts(PortFileManager.Port, bindPort);
                        }
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _chatListener?.Stop(); } catch { }
                        _chatListener = null;
                        if (attempt == 2)
                        {
                            var msg = se.Message; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {PortFileManager.ChatPort} unavailable after fallback: {msg}"));
                            break;
                        }
                        var bp2 = bindPort; var at = attempt; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {bp2} busy, retry {at + 1}/2..."));
                        await Task.Delay(300 * (attempt + 1), token).ConfigureAwait(false);
                    }
                    catch (SocketException se)
                    {
                        var msg = se.Message; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {PortFileManager.ChatPort} unavailable: {msg}"));
                        _chatListener = null;
                        break;
                    }
                }

                MainThreadDispatcher.Enqueue(() => Debug.Log($"[MCP] Server started on port {PortFileManager.Port} (chat: {PortFileManager.ChatPort})"));
                // Marshal onto main thread — a bind-retry above may have hopped this
                // continuation onto ThreadPool via ConfigureAwait(false), and both
                // WritePortFile/WriteStateFile touch Unity main-thread-only APIs
                // (Application.temporaryCachePath / SyncHelper.CurrentEpoch).
                MainThreadDispatcher.Enqueue(() =>
                {
                    PortFileManager.WritePortFile(PortFileManager.Port);
                    WriteStateFile("ready");
                });

                // Run both accept loops concurrently — chat loop is optional
                var mainLoop = ClientConnectionHandler.RunAcceptLoop(_listener, _mainSlot, "CLI", cts, token);
                var chatLoop = _chatListener != null
                    ? ClientConnectionHandler.RunAcceptLoop(_chatListener, _chatSlot, "Chat", cts, token)
                    : Task.CompletedTask;
                await Task.WhenAll(mainLoop, chatLoop).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!_shuttingDown)
                {
                    var msg = e.Message; MainThreadDispatcher.Enqueue(() => Debug.LogError($"[MCP] Server error: {msg}"));
                }
                // Clean up on bind failure — prevent CTS/listener leak
                if (!IsRunning)
                {
                    try { cts?.Dispose(); } catch { }
                    if (_cts == cts) _cts = null;
                    try { _listener?.Stop(); } catch { }
                    _listener = null;
                    if (!_shuttingDown) WriteStateFile("bind_failed");
                }
            }
            finally { _starting = false; }
        }

        private static void TeardownCore()
        {
            try { _cts?.Cancel(); } catch { }      // fires linked tokens safely FIRST
            _mainSlot.DisconnectAll();
            _chatSlot.DisconnectAll();
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            try { _listener?.Server?.Shutdown(SocketShutdown.Both); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            try { _chatListener?.Server?.Shutdown(SocketShutdown.Both); } catch { }
            try { _chatListener?.Stop(); } catch { }
            _chatListener = null;
            EditorApplication.update -= MainThreadDispatcher.Drain;
            EditorApplication.update -= WatchdogTick;
            MainThreadDispatcher.Clear();  // prevent queued actions after domain tear-down
        }

        public static void Stop()
        {
            Debug.Log("[MCP] Server stopping");
            _shuttingDown = true;
            TeardownCore();  // already drains queue
            PortFileManager.DeletePortFile();
        }

        private static void OnQuit()
        {
            _shuttingDown = true;
            EditorApplication.update -= WatchdogTick;
            DeleteStateFile();
            Stop();
        }

        private static void OnBeforeReload()
        {
            _shuttingDown = true;
            WriteStateFile("reloading");
            // Send going_away FIRST — streams still alive, handlers still running
            _mainSlot.ForEach(c => SendGoingAwaySync(c.GetStream()));
            _chatSlot.ForEach(c => SendGoingAwaySync(c.GetStream()));
            TeardownCore();
            // Do NOT delete port file — port stays the same after reload, Python needs it
        }

        // ── Tier 2: State file ────────────────────────────────────────────────

#if UNITY_INCLUDE_TESTS
        internal static void ResetDomainStateForTests()
        {
            _compileStartedThisDomain = false;
            _isCompiling = false;
        }
#endif

        internal static void WriteStateFile(string state) => PortFileManager.WriteStateFile(state);

        internal static void DeleteStateFile() => PortFileManager.DeleteStateFile();

        // ── Tier 4a: going_away frame ─────────────────────────────────────────

        internal static void SendGoingAwaySync(Stream stream) => ClientConnectionHandler.SendGoingAwaySync(stream);

        // ── Tier 4b: status response format ──────────────────────────────────

        // synced by sync_versions.py — do not edit manually
        internal static string PluginVersion => "1.6.0";

        internal static string BuildVersionString(string stamp, string pluginVersion)
        {
            var result = $"proto:3|plugin:{pluginVersion}";
            if (!string.IsNullOrEmpty(stamp))
                result += $"|stamp:{stamp}";
            return result;
        }

        internal static string FormatStatusResponse(string msgId, bool isCompiling, double elapsed)
        {
            var state = isCompiling ? $"compiling|{elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}" : "idle|0";
            var compile = isCompiling ? "true" : "false";
            return $"{{\"id\":\"{msgId}\",\"ok\":true,\"data\":\"{state}\",\"compile\":{compile}}}";
        }

    }
}
