// Thin IChatBackend backed by Python chat_relay.py.
// ZERO CLI-specific knowledge — semantic commands only.
using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal sealed class RelayBackend : IChatBackend, IDisposable
    {
        private readonly string _backendId;
        private readonly string _model;
        private readonly int    _mcpPort;
        private readonly string _resumeSessionId;
        private readonly string _appendSystemPrompt;
        private string           _mode;
        private string           _sessionToken;
        private RelayChatProcess _proc;
        private readonly ToolCallAccumulator _acc         = new ToolCallAccumulator();
        private readonly AgentEventParser    _agentParser = new AgentEventParser(); // T14: v2 JSON parser
#if !UNITY_INCLUDE_TESTS
        // Production-only: turn text queued while RelaySpawnState.RequestSpawn() is cold-starting
        // uvx (up to 45s). Flushed once the relay reports ready. Not used by the test seam path,
        // where _proc is created synchronously and SendTurn can write immediately.
        private string _queuedTurnJson;
#endif

        // F-6: set false by Stop() so OnRelayReady ignores a stale cold-start callback after cancel.
        private volatile bool _active = true;

        public bool IsRunning => _proc?.IsRunning ?? false;

        private string _sessionId;
        public string SessionId
        {
            get => _sessionId;
            private set
            {
                _sessionId = value;
                if (!string.IsNullOrEmpty(value))
                    SessionState.SetString(PrefKeys.ChatBackendSessionId, value);
            }
        }

#if UNITY_INCLUDE_TESTS
        // Seam: inject fake RelayChatProcess for unit tests (avoids real relay/TCP).
        internal static Func<RelayChatProcess> ProcessFactory;
#endif

        internal RelayBackend(string backendId, string mode, string model,
                              int mcpPort, string resumeSessionId = null,
                              string appendSystemPrompt = null)
        {
            _backendId          = backendId;
            _mode               = mode;
            _model              = model;
            _mcpPort            = mcpPort;
            _resumeSessionId    = resumeSessionId;
            _appendSystemPrompt = appendSystemPrompt;
        }

        public void Start()
        {
            // Dispose any lingering proc before creating a new one (prevents socket + thread leak).
            _proc?.Kill();
            _proc?.Dispose();
            _proc = null;
            _acc.Reset();          // also covers m2: clear dirty accumulator state
            _agentParser.Reset(); // T14: clear v2 parser state on each Start

            // T10: load or generate session token; persist before relay starts so domain reload
            // survives (Library/UnityMCP/chat_session.json is gitignored, not cleared on reload).
            if (!SessionContext.TryLoadSession(out _sessionToken) || string.IsNullOrEmpty(_sessionToken))
            {
                _sessionToken = SessionContext.GenerateToken();
                SessionContext.SaveSession(_sessionToken);
            }

#if UNITY_INCLUDE_TESTS
            var port = RelaySpawner.EnsureRunningOverride?.Invoke() ?? RelaySpawner.EnsureRunning();
            _proc = ProcessFactory?.Invoke() ?? new RelayChatProcess();
            _proc.StartViaRelay(port, _backendId, _mode, _model, _mcpPort,
                SessionId ?? _resumeSessionId, _sessionToken, _appendSystemPrompt);
#else
            // Tier 2 (chat-relay-upm-fix.md): don't block the main thread on a uvx cold start
            // (up to 45s). RelaySpawnState hops to the ThreadPool only when actually needed;
            // the "already running" fast path completes inline.
            RelaySpawnState.RequestSpawn(OnRelayReady, OnRelayError);
#endif
        }

#if !UNITY_INCLUDE_TESTS
        // Called back (on the main thread) once RelaySpawnState confirms the relay is up.
        private void OnRelayReady(int port)
        {
            // F-6: backend was stopped (CancelTurn/OnDisable) while cold start was in flight.
            // Guard prevents a new _proc + poll thread from leaking into an abandoned backend.
            if (!_active) return;
            _proc = new RelayChatProcess();
            _proc.StartViaRelay(port, _backendId, _mode, _model, _mcpPort,
                SessionId ?? _resumeSessionId, _sessionToken, _appendSystemPrompt);
            if (_queuedTurnJson != null)
            {
                var turn = _queuedTurnJson;
                _queuedTurnJson = null;
                _proc.WriteLine(turn);
            }
        }

        private void OnRelayError(string message) =>
            UnityEngine.Debug.LogError($"{BiomeLabel.Tag} {message}");
#endif

        public void SendTurn(string turnJson)
        {
#if UNITY_INCLUDE_TESTS
            if (!IsRunning) Start();
            _proc?.WriteLine(turnJson);
#else
            if (!IsRunning)
            {
                _queuedTurnJson = turnJson;
                Start();
                return;
            }
            _proc?.WriteLine(turnJson);
#endif
        }
        public void SendControlResponse(string json) => _proc?.WriteLine(json);

        public void SetMode(string mode)
        {
            _mode = mode;
            if (_proc != null && !_proc.SendSetMode(mode, SessionId))
                UnityEngine.Debug.LogWarning($"{BiomeLabel.Tag} SendSetMode failed — mode may be desynced");
        }

        /// <summary>
        /// Non-blocking mode switch: sends set_mode to the relay on a ThreadPool thread
        /// (relay kills and respawns the CLI subprocess internally),
        /// then calls onDone(bool ok) on the main thread via EditorApplication.delayCall.
        /// </summary>
        internal void SetModeAsync(string mode, Action<bool> onDone)
        {
            _mode = mode;
            if (_proc == null) { EditorApplication.delayCall += () => onDone(false); return; }
            var proc = _proc;
            var sid  = SessionId;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = proc.SendSetMode(mode, sid);
                EditorApplication.delayCall += () => onDone(ok);
            });
        }

        public void DrainEvents(List<ChatEvent> output, List<ToolCallRecord> toolOutput = null)
        {
            if (_proc == null) return;
            var lines = new List<string>(8);
            _proc.DrainLines(lines);
            foreach (var line in lines)
            {
                var ev = _agentParser.Parse(line);
                if (ev == null) continue;

                // Capture session ID from terminal events
                if ((ev.Value.Kind == ChatEventKind.TurnDone ||
                     ev.Value.Kind == ChatEventKind.SessionInit) &&
                    !string.IsNullOrEmpty(ev.Value.SessionId))
                    SessionId = ev.Value.SessionId;

                // tc| = complete tool call from relay — feed chip + args + complete
                if (ev.Value.Kind == ChatEventKind.ToolStart && ev.Value.Text != null)
                {
                    FeedCompleteToolCall(ev.Value, output, toolOutput);
                    continue;
                }

                // AutoReply must NOT go to UI output — write back to CLI stdin
                if (ev.Value.Kind == ChatEventKind.AutoReply)
                {
                    _proc.WriteLine(ev.Value.Text);
                    continue;
                }

                var rec = _acc.Feed(ev.Value);
                if (rec.HasValue && toolOutput != null) toolOutput.Add(rec.Value);
                output.Add(ev.Value);
            }
        }

        public void Stop()
        {
            _active = false;   // F-6: prevent any pending OnRelayReady from starting a new proc
            _proc?.Kill();
            _proc?.Dispose();
            _proc = null;
            _acc.Reset();
            _agentParser.Reset(); // T14: clear pending cost_update state
        }

        public void Dispose() => Stop();

        // ── Private ──────────────────────────────────────────────────────────

        // Relay sends one tc| line per tool call (name+id+complete args).
        // ToolCallAccumulator needs 3 feeds: chip-create, args-delta, args-complete.
        private void FeedCompleteToolCall(ChatEvent ev, List<ChatEvent> output,
                                          List<ToolCallRecord> toolOutput)
        {
            // 1. Chip creation (ArgsJson=null is the discriminator)
            var chipEv  = ChatEvent.ToolStart(ev.Text, null, ev.ToolId);
            var chipRec = _acc.Feed(chipEv);
            if (chipRec.HasValue && toolOutput != null) toolOutput.Add(chipRec.Value);
            output.Add(chipEv);

            // 2. Args delta
            if (!string.IsNullOrEmpty(ev.ArgsJson))
                _acc.Feed(ChatEvent.ToolStart(null, ev.ArgsJson, null));

            // 3. Args complete → produces args-assembled record
            var completeRec = _acc.Feed(ChatEvent.ToolArgsComplete());
            if (completeRec.HasValue && toolOutput != null) toolOutput.Add(completeRec.Value);
        }
    }
}
