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
        private static bool _lifecycleCallbacksRegistered;
        internal static volatile bool _isCompiling;
        // Cached domain stamp — read from main thread in StartAsync, used in get_version fast-path
        // (which runs on ThreadPool after ConfigureAwait(false); SessionState not thread-safe).
        internal static volatile string _domainStamp = "";
        // Cached Application.dataPath — read on main thread, used by client_hello fast-path
        // on ThreadPool (Application.dataPath is main-thread-only).
        internal static volatile string _cachedDataPath = "";
        // Cached Application.temporaryCachePath — bind-retry loop may hop to ThreadPool
        // via ConfigureAwait(false), where this API throws.
        internal static volatile string _cachedTempCachePath = "";
        // T19: stable project identity — cloudProjectId or sha256(project root)[:12].
        // Cached on main thread; ThreadPool reads via client_hello fast-path.
        internal static volatile string _cachedProjectId = "";
        // Set on first compilationStarted; never cleared. Distinguishes real compile-in-progress
        // from post-domain-reload stale EditorApplication.isCompiling on Windows.
        private static volatile bool _compileStartedThisDomain;
        internal static DateTime _compileStartTime;
        // Last state written to the state file — used by StatusModel SubState computation.
        internal static volatile string _lastWrittenState = "";
        // Set when TCP bind falls back to a non-configured port; cleared on each StartAsync entry.
        internal static volatile bool _portFallback;

        public static MCPStatusModel.SubState CurrentSubState => MCPStatusModel.GetSubState(
            isCompiling: _isCompiling,
            portMismatch: _portFallback,
            bindFailed: _lastWrittenState == "bind_failed",
            compileFailed: _lastWrittenState == "compile_failed");

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
        public static int ConnectedClientCount => _mainSlot.CountActive() + _chatSlot.CountActive();
        public static int PhantomCount => _mainSlot.CountPhantoms() + _chatSlot.CountPhantoms();
        public static int KillPhantoms()
        {
            var killed = _mainSlot.KillPhantoms() + _chatSlot.KillPhantoms();
            if (killed > 0) UnityEngine.Debug.Log($"{BiomeLabel.Tag} Killed {killed} phantom connection(s)");
            return killed;
        }
        public static int ServerPort
        {
            get
            {
                return TryGetBoundPort(_listener, out var port)
                    ? port
                    : PortFileManager.Port;
            }
        }

        public static int ServerChatPort
        {
            get
            {
                return TryGetBoundPort(_chatListener, out var port)
                    ? port
                    : PortFileManager.ChatPort;
            }
        }

        internal static bool TryGetBoundPort(TcpListener listener, out int port)
        {
            port = 0;
            try
            {
                var socket = listener?.Server;
                if (socket == null || !socket.IsBound)
                    return false;
                if (!(socket.LocalEndPoint is IPEndPoint endpoint) || endpoint.Port <= 0)
                    return false;

                port = endpoint.Port;
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
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
        internal static bool ShouldStartServer(bool isBatchMode)
            => !isBatchMode || Environment.GetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE") == "1";

        internal static string ResolveBootstrapScenePath(string projectRoot, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath)) return null;
            return Path.GetFullPath(Path.IsPathRooted(scenePath)
                ? scenePath
                : Path.Combine(projectRoot, scenePath));
        }

        static MCPServer()
        {
            if (!ShouldStartServer(Application.isBatchMode)) return;

            RegisterLifecycleCallbacks();
            // Remove port files from dead PIDs before writing our own — prevents stale entries.
            PortFileManager.CleanStalePeerPortFiles();
            // Persist auto-assigned ports so they survive domain reload
            PortFileManager.SavePorts(PortFileManager.Port, PortFileManager.ChatPort);
            // Delay async start until first update — SynchronizationContext dead in static ctor
            EditorApplication.delayCall += () => StartAsync();
        }

        internal static void RegisterLifecycleCallbacks()
        {
            if (_lifecycleCallbacksRegistered) return;
            _lifecycleCallbacksRegistered = true;
            // Register WatchdogTick FIRST — so it survives even if PortFileManager throws below.
            EditorApplication.update += WatchdogTick;
            EditorApplication.update += MainThreadDispatcher.Drain;
            EditorApplication.quitting += OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            CompilationPipeline.compilationStarted += _ => OnCompilationStarted();
            CompilationPipeline.compilationFinished += _ => OnCompilationFinished();
            EditorSceneManager.sceneOpened += (_, _) => InvalidateSceneCaches();
            EditorSceneManager.newSceneCreated += (_, _, _) => InvalidateSceneCaches();
            EditorSceneManager.sceneClosed += _ => InvalidateSceneCaches();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnCompilationStarted()
        {
            _isCompiling = true;
            _compileStartedThisDomain = true;
            _compileStartTime = DateTime.UtcNow;
            WriteStateFile("compiling");
        }

        private static void OnCompilationFinished()
        {
            // R-4 fix: never write "ready" from compilationFinished.
            // If compile failed → no reload will happen, write compile_failed.
            // If compile succeeded → reload is coming; "ready" written after reload in StartAsync.
            _isCompiling = false;
            if (EditorUtility.scriptCompilationFailed)
                WriteStateFile("compile_failed");
            // else: state stays "compiling" until reload completes (StartAsync writes "ready")
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
                InvalidateSceneCaches();
        }

        private static void InvalidateSceneCaches()
        {
            RefManager.Invalidate();
            HierarchySerializer.ResetIncrementalCache();
        }

        private static void WatchdogTick()
        {
            if (_shuttingDown) return;
            if (EditorApplication.timeSinceStartup - _lastWatchdogCheck < 5.0) return;
            _lastWatchdogCheck = EditorApplication.timeSinceStartup;
            if (!IsRunning && !IsReallyCompiling)
                StartAsync();
        }

        public static async void StartAsync()
        {
            // _starting closes the re-entrancy gap: during the bind-retry await window
            // IsRunning is false, so WatchdogTick could otherwise launch a 2nd StartAsync
            // → duplicate _cts/accept-loop churn.
            if (IsRunning || _starting) return;
            _starting = true;
            _portFallback = false;
            _shuttingDown = false;
            _domainStamp = SyncHelper.CurrentDomainStamp;  // cache on main thread — safe here, ThreadPool reads below
            _cachedDataPath = Application.dataPath;         // cache on main thread — ThreadPool reads via client_hello
            _cachedTempCachePath = Application.temporaryCachePath; // cache on main thread — bind-retry loop may hop to ThreadPool
            _cachedProjectId = GetStableProjectId();        // T19: cache on main thread (PlayerSettings + Application)
            OpenBootstrapSceneFromEnvironment();
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

                // Main listener: fast retries on same port, then fall back to a free port.
                // On Windows, ExclusiveAddressUse prevents TIME_WAIT reuse by the same process;
                // more retries reduce fallback risk during the longer Windows TIME_WAIT window.
#if UNITY_EDITOR_WIN
                const int maxAttempts = 6;  // Windows TIME_WAIT can last 2+ minutes; more retries reduce fallback risk
                const int retryDelayMs = 600;
#else
                const int maxAttempts = 6;  // macOS/Linux: match Windows budget (5 same-port + 1 fallback)
                const int retryDelayMs = 400;
#endif
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    int bindPort = PortFileManager.Port;
                    try
                    {
                        if (attempt == maxAttempts - 1)
                        {
                            // BindFreePort: atomic scan+bind, eliminates TOCTOU. Handles socket opts internally.
                            _listener = PortResolver.BindFreePort(PortFileManager.Port + 1, skipPort: PortFileManager.ChatPort, skipPort2: PortFileManager.ReloadPort);
                            bindPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                        }
                        else
                        {
                            _listener = new TcpListener(IPAddress.Loopback, bindPort);
#if UNITY_EDITOR_WIN
                            // Windows: SO_REUSEADDR = port-hijack; use ExclusiveAddressUse instead (CoplayDev #1173)
                            _listener.Server.ExclusiveAddressUse = true;
#else
                            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if UNITY_EDITOR_OSX
                            try { _listener.Server.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x0200, true); } catch { }
#endif
#endif
                            _listener.Start();
                        }
                        if (bindPort != PortFileManager.Port)
                        {
                            var bp = bindPort; var origPort = PortFileManager.Port; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Port {origPort} unavailable (address in use), switched to {bp}"));
                            // SaveRuntimePorts: updates MCP_Port.json + {pid}.port but NOT MCPSettings.json.
                            // Preserves user intent so next reload retries the configured port, no cascade drift.
                            // Thread-safe overload: bind-retry may hop to ThreadPool via ConfigureAwait(false).
                            PortFileManager.SaveRuntimePorts(bindPort, PortFileManager.ChatPort, _cachedDataPath, _cachedTempCachePath);
                            _portFallback = true;
                        }
                        var boundPort = PortFileManager.Port;
                        if (attempt == 0)
                            PortFileManager.WritePortFile(boundPort);  // attempt 0 = main thread, safe
                        // Retries fall through to MainThreadDispatcher.Enqueue at L268
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _listener?.Stop(); } catch { }
                        _listener = null;
                        if (attempt == maxAttempts - 1) throw;
                        var bp2 = bindPort; var at = attempt; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Port {bp2} busy, retry {at + 1}/{maxAttempts - 1}..."));
                        await Task.Delay(retryDelayMs * (attempt + 1), token).ConfigureAwait(false);
                    }
                }

                // Chat listener — best-effort (non-fatal if chat port is unavailable)
                var chatMainPort = PortFileManager.Port; // capture main port before chat loop
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    int bindPort = PortFileManager.ChatPort;
                    try
                    {
                        if (attempt == 2)
                        {
                            // BindFreePort: atomic scan+bind, eliminates TOCTOU. Handles socket opts internally.
                            _chatListener = PortResolver.BindFreePort(PortFileManager.ChatPort + 1, skipPort: chatMainPort, skipPort2: PortFileManager.ReloadPort);
                            bindPort = ((IPEndPoint)_chatListener.LocalEndpoint).Port;
                        }
                        else
                        {
                            _chatListener = new TcpListener(IPAddress.Loopback, bindPort);
#if UNITY_EDITOR_WIN
                            _chatListener.Server.ExclusiveAddressUse = true;
#else
                            _chatListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if UNITY_EDITOR_OSX
                            try { _chatListener.Server.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x0200, true); } catch { }
#endif
#endif
                            _chatListener.Start();
                        }
                        if (bindPort != PortFileManager.ChatPort)
                        {
                            var bp = bindPort; var origChatPort = PortFileManager.ChatPort; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Chat port {origChatPort} unavailable (address in use), switched to {bp}"));
                            PortFileManager.SaveRuntimePorts(chatMainPort, bindPort, _cachedDataPath, _cachedTempCachePath);
                        }
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _chatListener?.Stop(); } catch { }
                        _chatListener = null;
                        if (attempt == 2)
                        {
                            var msg = se.Message; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Chat port {PortFileManager.ChatPort} unavailable after fallback: {msg}"));
                            break;
                        }
                        var bp2 = bindPort; var at = attempt; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Chat port {bp2} busy, retry {at + 1}/2..."));
                        await Task.Delay(300 * (attempt + 1), token).ConfigureAwait(false);
                    }
                    catch (SocketException se)
                    {
                        var msg = se.Message; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Chat port {PortFileManager.ChatPort} unavailable: {msg}"));
                        _chatListener = null;
                        break;
                    }
                }

                MainThreadDispatcher.Enqueue(() => Debug.Log($"{BiomeLabel.Tag} Server started on port {PortFileManager.Port} (chat: {PortFileManager.ChatPort})"));
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
                // Suppress OperationCanceledException on clean shutdown — CTS cancellation is expected
                if (!(e is OperationCanceledException && _shuttingDown))
                {
                    var msg = e.Message; MainThreadDispatcher.Enqueue(() => Debug.LogError($"{BiomeLabel.Tag} Server error: {msg}"));
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

        private static void OpenBootstrapSceneFromEnvironment()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var scenePath = ResolveBootstrapScenePath(
                projectRoot,
                Environment.GetEnvironmentVariable("UNITY_MCP_BOOTSTRAP_SCENE"));
            if (scenePath == null || !File.Exists(scenePath)) return;
            var active = ResolveBootstrapScenePath(projectRoot, EditorSceneManager.GetActiveScene().path);
            if (string.Equals(active, scenePath, StringComparison.OrdinalIgnoreCase)) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"{BiomeLabel.Tag} Opened bootstrap scene: {scenePath}");
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

        // Sends RST instead of FIN on teardown — avoids TIME_WAIT on Windows.
        private static void SetLingerZero()
        {
            try { var s = _listener?.Server; if (s != null) s.LingerState = new System.Net.Sockets.LingerOption(true, 0); } catch { }
            try { var s = _chatListener?.Server; if (s != null) s.LingerState = new System.Net.Sockets.LingerOption(true, 0); } catch { }
        }

        public static void Stop()
        {
            Debug.Log($"{BiomeLabel.Tag} Server stopping");
            _shuttingDown = true;
            SetLingerZero();
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
            SetLingerZero();
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

        internal static void WriteStateFile(string state)
        {
            _lastWrittenState = state;
            PortFileManager.WriteStateFile(state);
        }

        internal static void DeleteStateFile() => PortFileManager.DeleteStateFile();

        // ── Tier 4a: going_away frame ─────────────────────────────────────────

        internal static void SendGoingAwaySync(Stream stream) => ClientConnectionHandler.SendGoingAwaySync(stream);

        // ── Tier 4b: status response format ──────────────────────────────────

        // synced by sync_versions.py — do not edit manually
        internal static string PluginVersion => "1.48.1";

        internal static string BuildVersionString(string stamp, string pluginVersion)
        {
            var result = $"proto:3|plugin:{pluginVersion}";
            if (!string.IsNullOrEmpty(stamp))
                result += $"|stamp:{stamp}";
            return result;
        }

        internal static string FormatStatusResponse(string msgId, bool isCompiling, double elapsed)
        {
            var state    = isCompiling ? $"compiling|{elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}" : "idle|0";
            var compile  = isCompiling ? "true" : "false";
            var portVal  = ServerPort;
            var bindFail = _lastWrittenState == "bind_failed" ? "true" : "false";
            var fallback = _portFallback ? "true" : "false";
            return $"{{\"id\":\"{msgId}\",\"ok\":true,\"data\":\"{state}\"," +
                   $"\"compile\":{compile},\"port\":{portVal}," +
                   $"\"bind_failed\":{bindFail},\"port_fallback\":{fallback}}}";
        }

        // T19: stable project identity — main-thread-only (PlayerSettings + Application APIs).
        internal static string GetStableProjectId()
        {
            var cloudId = UnityEditor.CloudProjectSettings.projectId;
            if (!string.IsNullOrEmpty(cloudId)) return cloudId;
            var path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));
            return ComputeSha256Hex(path).Substring(0, 12);
        }

        private static string ComputeSha256Hex(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

    }
}
