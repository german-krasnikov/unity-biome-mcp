// Relay-backed ChatProcess replacement. Talks to chat_relay.py via TCP.
// Background thread polls "events" every 100ms, queues stdout lines.
// NO Debug.Log — called from background threads (Unity 6 not thread-safe for Debug.*).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal sealed class RelayChatProcess : IDisposable
    {
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private RelayTcpClient                   _client;
        private Func<string, string>             _sendFunc;
        private Thread                           _pollThread;
        private volatile bool                    _running;
        private int                              _lastSeq         = -1;
        private int                              _negotiatedVersion = 1;   // T14: set from start response
        internal int NegotiatedVersion => _negotiatedVersion;

        // Production constructor — SpawnViaRelay creates and connects RelayTcpClient.
        internal RelayChatProcess() { }

        // Test constructor — injects a send function, bypasses TCP entirely.
        internal RelayChatProcess(Func<string, string> sendCommand)
        {
            _sendFunc = sendCommand;
        }

        internal bool IsRunning => _running;

        /// <summary>Connect to relay, tell it to spawn the CLI, start background poll.</summary>
        internal void SpawnViaRelay(int relayPort, string binaryPath, string[] argv,
            Dictionary<string, string> envSet, string[] envStrip)
        {
            if (_sendFunc == null)
            {
                _client = new RelayTcpClient();
                _client.Connect(relayPort);
                _sendFunc = _client.SendCommand;
            }

            var resp = _sendFunc(BuildSpawnJson(binaryPath, argv, envSet, envStrip));
            if (!IsOk(resp))
                throw new InvalidOperationException($"Relay spawn failed: {ExtractErr(resp)}");

            _running = true;
            _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "RelayChatProcess.EventPoll" };
            _pollThread.Start();
        }

        /// <summary>
        /// Connect to relay, send semantic "start" command (relay resolves binary + args).
        /// Alternative to SpawnViaRelay — no binary path or argv needed.
        /// </summary>
        internal void StartViaRelay(int relayPort, string backendId, string mode,
            string model, int mcpPort, string resumeSessionId,
            string sessionToken = null, string appendSystemPrompt = null)
        {
            if (_sendFunc == null)
            {
                _client = new RelayTcpClient();
                _client.Connect(relayPort);
                _sendFunc = _client.SendCommand;
            }
            var sb = new StringBuilder();
            sb.Append("{\"cmd\":\"start\",\"args\":{")
              .Append("\"backend\":\"").Append(JsonHelper.EscapeJson(backendId ?? "")).Append("\",")
              .Append("\"mode\":\"").Append(JsonHelper.EscapeJson(mode ?? "")).Append("\",")
              .Append("\"model\":\"").Append(JsonHelper.EscapeJson(model ?? "")).Append("\",")
              .Append("\"mcp_port\":").Append(mcpPort);
            if (!string.IsNullOrEmpty(resumeSessionId))
                sb.Append(",\"resume_session_id\":\"")
                  .Append(JsonHelper.EscapeJson(resumeSessionId)).Append("\"");
            if (!string.IsNullOrEmpty(sessionToken))
                sb.Append(",\"session_token\":\"")
                  .Append(JsonHelper.EscapeJson(sessionToken)).Append("\"");
            if (!string.IsNullOrEmpty(appendSystemPrompt))
                sb.Append(",\"append_system_prompt\":\"")
                  .Append(JsonHelper.EscapeJson(appendSystemPrompt)).Append("\"");
            // T14: request v2 protocol when EditorPrefs flag is enabled (default true)
            if (EditorPrefs.GetBool("UnityMCP.Chat.ProtocolV2", true))
                sb.Append(",\"protocol_version\":2");
            sb.Append("}}");
            var resp = _sendFunc(sb.ToString());
            if (!IsOk(resp))
                throw new InvalidOperationException($"Relay start failed: {ExtractErr(resp)}");
            // T14: capture negotiated version (absent = 1, "2" = 2)
            _negotiatedVersion = 1;
            if (JsonHelper.ExtractString(resp, "negotiated_version") == "2")
                _negotiatedVersion = 2;
            _running = true;
            _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "RelayChatProcess.EventPoll" };
            _pollThread.Start();
        }

        /// <summary>Tell relay to switch mode. Returns false if not running or send fails.</summary>
        internal bool SendSetMode(string mode, string sessionId = null)
        {
            if (!_running) return false;
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"cmd\":\"set_mode\",\"args\":{\"mode\":\"")
                  .Append(JsonHelper.EscapeJson(mode)).Append("\"");
                if (!string.IsNullOrEmpty(sessionId))
                    sb.Append(",\"session_id\":\"")
                      .Append(JsonHelper.EscapeJson(sessionId)).Append("\"");
                sb.Append("}}");
                var resp = _sendFunc(sb.ToString());
                return IsOk(resp);
            }
            catch { return false; }
        }

        /// <summary>Drain buffered stdout lines into output — same signature as ChatProcess.DrainLines.</summary>
        internal void DrainLines(List<string> output)
        {
            if (output == null) return;
            while (_lines.TryDequeue(out var line))
                output.Add(line);
        }

        /// <summary>Forward text to CLI stdin via relay.</summary>
        internal void WriteLine(string text)
        {
            if (!_running) return;
            try
            {
                var resp = _sendFunc($"{{\"cmd\":\"send\",\"args\":{{\"line\":\"{JsonHelper.EscapeJson(text)}\"}}}}");
                if (!IsOk(resp))
                {
                    _lines.Enqueue($"e|relay: {ExtractErr(resp)}");
                    _running = false;
                }
            }
            catch { }
        }

        /// <summary>Close CLI stdin pipe via relay (Codex pattern).</summary>
        internal void CloseStdin()
        {
            try { _sendFunc?.Invoke("{\"cmd\":\"close_stdin\",\"args\":{}}"); } catch { }
        }

        /// <summary>Kill CLI process via relay.</summary>
        internal void Kill()
        {
            _running = false;
            try { _sendFunc?.Invoke("{\"cmd\":\"kill\",\"args\":{}}"); } catch { }
        }

        public void Dispose()
        {
            _running = false;
            try { _client?.Close(); } catch { }
            _client?.Dispose();
            _client = null;
        }

        // ── Background poll ───────────────────────────────────────────────────

        private void PollLoop()
        {
            int retries = 0;
            while (_running)
            {
                Thread.Sleep(100);
                if (!_running) break;
                try
                {
                    var resp = _sendFunc($"{{\"cmd\":\"events\",\"args\":{{\"after_seq\":{_lastSeq}}}}}");
                    if (resp != null) ParseEvents(resp);
                    retries = 0; // reset on success
                }
                catch (Exception ex)
                {
                    if (retries++ >= 3 || _client == null)
                    {
                        _lines.Enqueue($"e|{ex.Message.Replace('\n', ' ').Replace('\r', ' ')}");
                        _running = false;
                        return;
                    }
                    Thread.Sleep(Math.Min(1000 * retries, 3000));
                    try { _client.Reconnect(); }
                    catch { _running = false; return; }
                }
            }
        }

        private void ParseEvents(string resp)
        {
            var data = JsonHelper.ExtractString(resp, "data");
            if (string.IsNullOrEmpty(data)) return;
            if (_negotiatedVersion == 2) ParseV2Events(data);
            else ParseV1Events(data);
        }

        // T14: internal for direct testing from RelayChatProcessSessionTests.
        internal void ParseV1Events(string data)
        {
            var parts = data.Split('\n');
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                if (!int.TryParse(parts[i], out var seq)) break;
                if (seq > _lastSeq)
                {
                    _lines.Enqueue(parts[i + 1].Replace("\\n", "\n")); // pipe-format unescape
                    _lastSeq = seq;
                }
            }
        }

        // T14: v2 — JSON must remain intact; do NOT unescape \\n → \n.
        internal void ParseV2Events(string data)
        {
            var parts = data.Split('\n');
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                if (!int.TryParse(parts[i], out var seq)) break;
                if (seq > _lastSeq)
                {
                    _lines.Enqueue(parts[i + 1]); // no unescape: JSON escape preserved
                    _lastSeq = seq;
                }
            }
        }

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string BuildSpawnJson(string binaryPath, string[] argv,
            Dictionary<string, string> envSet, string[] envStrip)
        {
            var sb = new StringBuilder();
            sb.Append("{\"cmd\":\"spawn\",\"args\":{\"binary\":\"")
              .Append(JsonHelper.EscapeJson(binaryPath ?? ""))
              .Append("\",\"argv\":")
              .Append(JsonHelper.BuildJsonStringArray(argv))
              .Append(",\"env_set\":{");
            if (envSet != null)
            {
                bool first = true;
                foreach (var kv in envSet)
                {
                    if (!first) sb.Append(',');
                    sb.Append('"').Append(JsonHelper.EscapeJson(kv.Key))
                      .Append("\":\"").Append(JsonHelper.EscapeJson(kv.Value)).Append('"');
                    first = false;
                }
            }
            sb.Append("},\"env_strip\":")
              .Append(JsonHelper.BuildJsonStringArray(envStrip))
              .Append("}}");
            return sb.ToString();
        }

        private static bool IsOk(string resp) =>
            JsonHelper.ExtractString(resp, "ok") == "true";

        private static string ExtractErr(string resp) =>
            JsonHelper.ExtractString(resp, "err") ?? "unknown error";
    }
}
