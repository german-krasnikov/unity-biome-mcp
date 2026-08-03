// ReloadMiniServer — lightweight TCP server for the reload package.
// Background Thread (not async). Single-client. 4-byte BE length prefix + JSON.
// Read-only commands execute inline; main-thread commands enqueue via ConcurrentQueue.
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Reload
{
    internal sealed class ReloadMiniServerGeneration
    {
        private int _value;

        internal int Current => Volatile.Read(ref _value);

        internal int Advance() => Interlocked.Increment(ref _value);

        internal bool IsCurrent(int generation) =>
            generation != 0 && Volatile.Read(ref _value) == generation;

        internal Action Bind(int generation, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return () =>
            {
                if (IsCurrent(generation)) action();
            };
        }
    }

    internal sealed class ReloadMainThreadDispatchGate
    {
        private const int Pending = 0;
        private const int Executing = 1;
        private const int Abandoned = 2;
        private int _state;

        internal bool TryStart() =>
            Interlocked.CompareExchange(ref _state, Executing, Pending) == Pending;

        internal bool TryAbandon() =>
            Interlocked.CompareExchange(ref _state, Abandoned, Pending) == Pending;

        internal bool HasStarted => Volatile.Read(ref _state) == Executing;

        internal void ExecuteStarted(Action accepted, Action dispatch)
        {
            if (accepted == null) throw new ArgumentNullException(nameof(accepted));
            if (dispatch == null) throw new ArgumentNullException(nameof(dispatch));
            if (!HasStarted)
                throw new InvalidOperationException(
                    "The main-thread dispatch gate has not started.");
            accepted();
            dispatch();
        }
    }

    public static class ReloadMiniServer
    {
        private static TcpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;
        private static volatile bool _starting;
        private static volatile bool _shuttingDown;
        private static readonly object _lifecycleGate = new object();
        private static readonly ReloadMiniServerGeneration _generation =
            new ReloadMiniServerGeneration();
        private static readonly ConcurrentDictionary<int, TcpClient> _activeClients =
            new ConcurrentDictionary<int, TcpClient>();

        // Main-thread work queue — drained by EditorApplication.update via ReloadPlugin.
        internal static readonly ConcurrentQueue<Action> UpdateQueue =
            new ConcurrentQueue<Action>();

        public static int ActualPort { get; private set; }

        // Idempotent start. Retries ports [port..port+50] — does not trust stale config port.
        public static void Start(int port)
        {
            lock (_lifecycleGate)
            {
                if (_running || _starting) return;
                _starting = true;
                _shuttingDown = false;
                try
                {
                    var (listener, actualPort) = ReloadBinder.BindListener(port, port + 50);
                    _listener = listener;
                    ActualPort = actualPort;
                }
                catch (SocketException e)
                {
                    Debug.LogWarning(
                        $"[Reload] Failed to start mini-server (range {port}-{port + 50}): {e.Message}");
                    _starting = false;
                    ActualPort = 0;
                    return;
                }

                var listenerForThread = _listener;
                var generation = _generation.Advance();
                _thread = new Thread(() => AcceptLoop(listenerForThread, generation))
                {
                    IsBackground = true,
                    Name = "ReloadMiniServer"
                };
                _running = true;
                _starting = false;
                _thread.Start();
                Debug.Log($"[Reload] Mini-server started on port {ActualPort}");
            }
        }

        public static void Stop()
        {
            lock (_lifecycleGate)
            {
                _shuttingDown = true;
                _running = false;
                _generation.Advance();
                var listener = _listener;
                var thread = _thread;
                _listener = null;
                _thread = null;
                ActualPort = 0;
                try { listener?.Stop(); } catch { }

                if (thread != null && thread != Thread.CurrentThread && thread.IsAlive &&
                    !thread.Join(1000))
                    Debug.LogWarning("[Reload] Accept thread did not stop within 1s.");

                // No accept loop can register another client after the bounded join.
                foreach (var kv in _activeClients)
                    try { kv.Value.Close(); } catch { }
                _activeClients.Clear();
            }
        }

        private static void AcceptLoop(TcpListener listener, int generation)
        {
            while (_running && _generation.IsCurrent(generation) &&
                   ReferenceEquals(listener, _listener))
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    var clientId = client.GetHashCode();
                    _activeClients[clientId] = client;
                    client.ReceiveTimeout = 30_000;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { HandleClient(client, generation); }
                        finally { TcpClient removed; _activeClients.TryRemove(clientId, out removed); }
                    });
                }
                catch (SocketException) when (
                    !_running || !_generation.IsCurrent(generation)) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (_running && _generation.IsCurrent(generation))
                        Debug.LogWarning($"[Reload] Accept error: {e.Message}");
                }
            }
        }

        private static void HandleClient(TcpClient client, int generation)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var header = new byte[4];
                    while (_running && _generation.IsCurrent(generation))
                    {
                        if (!ReadExact(stream, header)) break;
                        var length = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
                        if (length > 1_000_000) break;

                        var payload = new byte[length];
                        if (!ReadExact(stream, payload)) break;

                        var json = Encoding.UTF8.GetString(payload);
                        var cmd = ExtractString(json, "cmd");
                        var id  = ExtractString(json, "id") ?? "";
                        var args = ExtractArgs(json);

                        var response = DispatchCommandForGeneration(
                            cmd, args, id, stream, generation);
                        if (response != null)
                            SendFrame(stream, response);
                    }
                }
            }
            catch (Exception e)
            {
                if (_running && _generation.IsCurrent(generation))
                    Debug.LogWarning($"[Reload] Client error: {e.Message}");
            }
        }

        // Returns null when response is sent asynchronously (main-thread commands handle it).
        public static string DispatchCommand(string cmd, string args, string id, NetworkStream stream = null)
        {
            return DispatchCommandForGeneration(
                cmd, args, id, stream, _generation.Current);
        }

        private static string DispatchCommandForGeneration(
            string cmd,
            string args,
            string id,
            NetworkStream stream,
            int generation)
        {
            if (generation != 0 && !_generation.IsCurrent(generation))
                return ErrResponse(id, "server lifecycle changed");

            switch (cmd)
            {
                case "ping":         return OkResponse(id, "pong");
                case "get_version":  return OkResponse(id, ReloadDomainStamp.ComputeStamp());
                case "diagnose":     return OkResponse(id, ReloadDiagnoseCommand.Execute(args));
                case "sync_status":  return OkResponse(id, ReloadCommands.Dispatch("sync_status"));
                case "force_refresh":
                case "recompile":
                    return EnqueueMainThread(cmd, id, stream, generation);
                default:
                    return ErrResponse(id, $"unknown command: {cmd}");
            }
        }

        private static string EnqueueMainThread(
            string cmd,
            string id,
            NetworkStream stream,
            int generation)
        {
            if (stream == null)
                return ErrResponse(id, "main thread not available in test context");

            var mre = new ManualResetEventSlim(false);
            var dispatchGate = new ReloadMainThreadDispatchGate();
            // Exactly one accepted/timeout frame wins at the dequeue boundary.
            int sent = 0;
            UpdateQueue.Enqueue(() =>
            {
                // Exactly one side wins: the main thread starts the mutation, or
                // the socket timeout abandons it before it starts. Stop/Start use
                // the same monitor, so lifecycle invalidation cannot interleave
                // between this decision and the accepted mutation.
                lock (_lifecycleGate)
                {
                    if (_shuttingDown || !_generation.IsCurrent(generation) ||
                        !dispatchGate.TryStart())
                    {
                        mre.Set();
                        return;
                    }

                    dispatchGate.ExecuteStarted(
                        () =>
                        {
                            var accepted = OkResponse(
                                id, cmd + " accepted on main thread; completion is pending");
                            if (Interlocked.Exchange(ref sent, 1) == 0)
                                SendFrame(stream, accepted);
                            mre.Set();
                        },
                        () =>
                        {
                            try
                            {
                                ReloadCommands.Dispatch(cmd);
                            }
                            catch (Exception error)
                            {
                                if (_running && _generation.IsCurrent(generation))
                                    Debug.LogWarning(
                                        "[Reload] Accepted main-thread command failed: " +
                                        error.Message);
                            }
                        });
                }
            });

            if (!mre.Wait(5000))
            {
                var result = dispatchGate.TryAbandon()
                    ? ErrResponse(id, "main thread timeout before dispatch (5s)")
                    : dispatchGate.HasStarted
                        ? OkResponse(id,
                            cmd + " accepted on main thread; completion is pending")
                        : ErrResponse(id, "server lifecycle changed");
                if (Interlocked.Exchange(ref sent, 1) == 0)
                    SendFrame(stream, result);
            }
            return null; // caller must not send again
        }

        private static void SendFrame(NetworkStream stream, string json)
        {
            try
            {
                var payload = Encoding.UTF8.GetBytes(json);
                var frame = new byte[4 + payload.Length];
                var len = (uint)payload.Length;
                frame[0] = (byte)(len >> 24);
                frame[1] = (byte)(len >> 16);
                frame[2] = (byte)(len >> 8);
                frame[3] = (byte)(len);
                Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
            catch { }
        }

        private static bool ReadExact(NetworkStream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0) return false;
                total += read;
            }
            return true;
        }

        // Minimal JSON helpers — no dep on UnityMCP.Editor.JsonHelper.
        public static string OkResponse(string id, string data)
            => $"{{\"id\":\"{Esc(id)}\",\"ok\":true,\"data\":\"{Esc(data)}\"}}";

        public static string ErrResponse(string id, string error)
            => $"{{\"id\":\"{Esc(id)}\",\"ok\":false,\"err\":\"{Esc(error)}\"}}";

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += needle.Length;
            while (idx < json.Length && json[idx] != ':') idx++;
            idx++; // skip ':'
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length) return null;
            if (json[idx] == '"')
            {
                idx++;
                var sb = new StringBuilder();
                while (idx < json.Length && json[idx] != '"')
                {
                    if (json[idx] == '\\') { idx++; if (idx < json.Length) sb.Append(json[idx]); }
                    else sb.Append(json[idx]);
                    idx++;
                }
                return sb.ToString();
            }
            return null;
        }

        private static string ExtractArgs(string json)
        {
            var needle = "\"args\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return "{}";
            idx += needle.Length;
            while (idx < json.Length && json[idx] != ':') idx++;
            idx++;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length || json[idx] != '{') return "{}";
            int depth = 0, start = idx;
            for (; idx < json.Length; idx++)
            {
                if (json[idx] == '{') depth++;
                else if (json[idx] == '}') { if (--depth == 0) return json.Substring(start, idx - start + 1); }
            }
            return "{}";
        }
    }
}
