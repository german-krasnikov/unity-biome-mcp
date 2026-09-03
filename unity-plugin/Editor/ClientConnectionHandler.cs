using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Accept-loop + per-client message pump extracted from MCPServer (Phase 2, M1).
    // WARNING: All awaits below use ConfigureAwait(false) — code after any await
    // here runs on ThreadPool, NOT main thread. Do NOT call Unity Editor APIs
    // directly — use MainThreadDispatcher.Enqueue().
    internal static class ClientConnectionHandler
    {
        private const int MaxMessageSize = 10_000_000;
        // Rate-limit window for the unrecognized-desync warning path (ARC-15 T2).
        private const int DesyncWarnWindowSeconds = 30;

        internal static async Task RunAcceptLoop(TcpListener listener, ClientSlot slot, string label,
            CancellationTokenSource masterCts, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested) break;
                    var msg = e.Message; MainThreadDispatcher.Enqueue(() => Debug.LogError($"{BiomeLabel.Tag} {label} accept error: {msg}"));
                    if (MCPServer._cts != masterCts || !MCPServer.IsRunning) break;
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                try { client.NoDelay = true; } catch { }
                ApplyKeepAlive(client.Client);
#if UNITY_EDITOR_WIN
                // Linger(true, 0) on the accepted socket sends RST on Dispose instead of FIN.
                // Prevents TIME_WAIT accumulation when Python disconnects during domain reload —
                // ExclusiveAddressUse on the listener blocks rebind if any local socket is in TIME_WAIT.
                try { client.Client.SetSocketOption(SocketOptionLevel.Socket,
                    SocketOptionName.Linger, new LingerOption(true, 0)); } catch (SocketException) { }
#endif
                if (!slot.TryAdd(client, token, out var idx, out var gen, out var clientCts))
                {
                    // MCP-CAP-025: send typed rejection before closing so Python gets a
                    // machine-readable CapacityBusyError instead of EOF/unusable state.
                    var active = slot.CountActive();
                    var lbl0 = label;
                    _ = RejectCapacityAsync(client, active, lbl0);
                    continue;
                }
                slot.SetEntryEndpoint(idx, gen, client.Client.RemoteEndPoint?.ToString() ?? "unknown");
                _ = HandleClientAsync(client, slot, idx, gen, label, clientCts.Token);
            }
        }

        private static void ApplyKeepAlive(Socket socket)
        {
            try { socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }
            // Platform-specific keepalive tuning (idle=60s, interval=10s, count=3)
            // Detects dead peers within ~90s. Relaxed from 10s/5s/3 to survive
            // macOS App Nap timer coalescing when Unity is in background.
            // App-level heartbeat (15s) handles faster detection for normal cases.
#if UNITY_EDITOR_OSX
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x10, 60); } catch { }   // TCP_KEEPALIVE (idle)
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x101, 10); } catch { }  // TCP_KEEPINTVL
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x102, 3); } catch { }   // TCP_KEEPCNT
#elif UNITY_EDITOR_WIN
            // Windows: use SIO_KEEPALIVE_VALS via IOControl
            try
            {
                var vals = new byte[12];
                BitConverter.GetBytes(1).CopyTo(vals, 0);       // on
                BitConverter.GetBytes(60000).CopyTo(vals, 4);   // idle ms
                BitConverter.GetBytes(10000).CopyTo(vals, 8);   // interval ms
                socket.IOControl(IOControlCode.KeepAliveValues, vals, null);
            }
            catch { }
#elif UNITY_EDITOR_LINUX
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)4, 60); } catch { }    // TCP_KEEPIDLE
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)5, 10); } catch { }    // TCP_KEEPINTVL
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)6, 3); } catch { }     // TCP_KEEPCNT
#endif
        }

        private static string RoleToLabel(string role) => role switch
        {
            "mcp"            => "Claude Code session",
            "chat-relay"     => "Chat relay",
            "codex"          => "Codex session",
            "cursor"         => "Cursor session",
            "windsurf"       => "Windsurf session",
            "claude-desktop" => "Claude Desktop session",
            _                => role,
        };

        // internal so tests can verify fast-path / slow-path classification without TCP.
        internal static bool IsSlowPath(string cmd) =>
            cmd != "ping" && cmd != "get_version" && cmd != "status" &&
            cmd != "get_enabled_tools" && cmd != "client_hello";

        // ARC-15 T1: exact 4-byte ASCII prefixes of the HTTP methods an AV/EDR scanner or
        // health-checker probe most commonly opens with. Verified (ARC-15-http-garbage-detect.md
        // §1) as BE-uint32 values that all comfortably exceed MaxMessageSize, so this classifier
        // is only ever consulted from the existing overflow branch — never a legitimate frame.
        private static readonly byte[][] KnownHttpProbePrefixes =
        {
            Encoding.ASCII.GetBytes("GET "),
            Encoding.ASCII.GetBytes("POST"),
            Encoding.ASCII.GetBytes("HEAD"),
            Encoding.ASCII.GetBytes("PUT "),
            Encoding.ASCII.GetBytes("DELE"), // (TE)
            Encoding.ASCII.GetBytes("OPTI"), // (ONS)
            Encoding.ASCII.GetBytes("HTTP"), // (/1.x response)
        };

        private const byte TlsHandshakeContentType = 0x16;

        // internal so tests can classify a raw 4-byte header without opening a socket.
        // Pure/static, no Unity API: called only after the existing length-prefix overflow
        // check reinterprets the header bytes as ASCII/TLS — the header itself is never mutated.
        internal static bool IsKnownForeignProtocolProbe(byte[] header)
        {
            if (header == null || header.Length < 4) return false;
            if (header[0] == TlsHandshakeContentType) return true;
            foreach (var prefix in KnownHttpProbePrefixes)
            {
                if (header[0] == prefix[0] && header[1] == prefix[1] &&
                    header[2] == prefix[2] && header[3] == prefix[3])
                    return true;
            }
            return false;
        }

        // ARC-15 T2: rate limiter for the *unrecognized* desync-warning path only — a probe
        // classified by IsKnownForeignProtocolProbe never reaches this (routes to Debug.Log
        // instead). At most one LogWarning per window; calls inside the window are silently
        // counted and folded into the next window-opening call's suppressed count. Pure
        // function of an injected nowTicks — tests never sleep the real window.
        // internal so tests can drive it without waiting on a real clock; nested here since
        // ClientConnectionHandler already co-locates several such single-purpose helpers.
        internal sealed class DesyncWarnLimiter
        {
            private readonly long _windowTicks;
            private readonly object _lock = new object();
            private long _windowStartTicks;
            private bool _hasLogged;
            private int _suppressedCount;

            internal DesyncWarnLimiter(long windowTicks) => _windowTicks = windowTicks;

            // Returns shouldLog=true at most once per window; suppressed is the exact count of
            // calls silently dropped since the last logged call, folded into the call that
            // (re)opens the next window.
            internal (bool shouldLog, int suppressed) Record(long nowTicks)
            {
                lock (_lock)
                {
                    if (!_hasLogged || nowTicks - _windowStartTicks >= _windowTicks)
                    {
                        var suppressed = _hasLogged ? _suppressedCount : 0;
                        _windowStartTicks = nowTicks;
                        _hasLogged = true;
                        _suppressedCount = 0;
                        return (true, suppressed);
                    }

                    _suppressedCount++;
                    return (false, 0);
                }
            }
        }

        // ARC-15 T3: single process-wide limiter instance shared by every connection —
        // intentional (ARC-15-http-garbage-detect.md §6): one cap on total warning volume,
        // not a per-connection or per-port limiter. DesyncWarnLimiter.Record is lock-guarded
        // so concurrent ThreadPool handler threads share it safely. Not readonly: see
        // ResetDesyncLimiterForTests below.
        private static DesyncWarnLimiter _desyncLimiter =
            new DesyncWarnLimiter(TimeSpan.FromSeconds(DesyncWarnWindowSeconds).Ticks);

#if UNITY_INCLUDE_TESTS
        // Test-only seam (mirrors MCPServer.ResetDomainStateForTests). Re-creates the shared
        // limiter so repeated full-suite EditMode runs in the same Editor domain don't
        // inherit real-clock suppression state — a real 30s window would otherwise make
        // "first call after reset logs" nondeterministic across runs less than 30s apart.
        internal static void ResetDesyncLimiterForTests() =>
            _desyncLimiter = new DesyncWarnLimiter(TimeSpan.FromSeconds(DesyncWarnWindowSeconds).Ticks);
#endif

        // Renders a 4-byte header as printable ASCII (non-printable -> '.') for the quiet
        // probe log, so a human (and the loopback test) can see e.g. "GET " without
        // decoding hex. Never throws — same defensive shape as IsKnownForeignProtocolProbe.
        private static string HeaderAsciiPreview(byte[] header)
        {
            var chars = new char[header.Length];
            for (int i = 0; i < header.Length; i++)
            {
                var b = header[i];
                chars[i] = (b >= 0x20 && b < 0x7F) ? (char)b : '.';
            }
            return new string(chars);
        }

        // internal so tests can verify the cross-language response format without TCP.
        // helloVersion:2 is the Python discriminant: present → fast-path (1 RTT), absent → 3-RTT fallback.
        // T19: projectId added (cloudProjectId or sha256[:12]) — stable across path moves.
        internal static string BuildClientHelloResponse(string msgId, string ver, string projPath) =>
            $"{{\"id\":\"{JsonHelper.EscapeJson(msgId)}\",\"ok\":true," +
            $"\"data\":\"pong\",\"helloVersion\":2," +
            $"\"version\":\"{JsonHelper.EscapeJson(ver)}\"," +
            $"\"projectPath\":\"{JsonHelper.EscapeJson(projPath)}\"," +
            $"\"projectId\":\"{JsonHelper.EscapeJson(MCPServer._cachedProjectId)}\"}}";

        // MCP-CAP-025: typed rejection sent to clients that arrive when all slots are full.
        // Python discriminant: error == "CLIENT_CAPACITY_BUSY" → raises CapacityBusyError → retried.
        // internal so unit tests can verify the response format without a live TCP connection.
        internal static string BuildCapacityRejectionResponse(int capacity, int active) =>
            $"{{\"error\":\"CLIENT_CAPACITY_BUSY\",\"capacity\":{capacity},\"active\":{active},\"retry_after_seconds\":5}}";

        // Fire-and-forget: send typed capacity rejection, then close.
        // Existing connected clients are never touched here.
        private static async Task RejectCapacityAsync(TcpClient client,
            int active, string label)
        {
            try
            {
                using (client)
                {
                    var json = BuildCapacityRejectionResponse(ClientSlot.MaxClients, active);
                    await SendAsync(client.GetStream(), json, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"{BiomeLabel.Tag} RejectCapacityAsync error: {ex.Message}"); }
            finally
            {
                var lbl = label;
                MainThreadDispatcher.Enqueue(() =>
                    Debug.LogWarning($"{BiomeLabel.Tag} {lbl} rejected connection: client capacity exceeded ({active}/{ClientSlot.MaxClients})"));
            }
        }



        private static async Task HandleClientAsync(TcpClient client, ClientSlot slot, int index, long generation,
            string label, CancellationToken clientToken)
        {
            var endPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            var receivedFirstMessage = false;
            var refInvalidated = false;
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var header = new byte[4];

                    while (client.Connected && !clientToken.IsCancellationRequested)
                    {
                        if (!await ReadExactAsync(stream, header, clientToken).ConfigureAwait(false))
                            break;

                        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                        if (length > MaxMessageSize)
                        {
                            var len = length;
                            if (IsKnownForeignProtocolProbe(header))
                            {
                                // ARC-15 T1/T3: AV/EDR scanners and health-checkers HTTP/TLS-probe
                                // the raw port constantly — informational only, never Warning, so
                                // it drops out of ASSERT_CONSOLE_CLEAN.
                                var preview = HeaderAsciiPreview(header);
                                MainThreadDispatcher.Enqueue(() => Debug.Log($"{BiomeLabel.Tag} Foreign protocol probe (\"{preview}\" 0x{len:X8}) — closing quietly"));
                            }
                            else
                            {
                                // Honest desync: rate-limited so a stuck/misbehaving peer can't
                                // spam the console (ARC-15 T2/T3).
                                var (shouldLog, suppressed) = _desyncLimiter.Record(DateTime.UtcNow.Ticks);
                                if (shouldLog)
                                {
                                    var suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : "";
                                    MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Protocol desync: length prefix {len} bytes (0x{len:X8}) exceeds {MaxMessageSize} — reconnecting{suffix}"));
                                }
                            }
                            break;
                        }

                        var payload = new byte[length];
                        if (!await ReadExactAsync(stream, payload, clientToken).ConfigureAwait(false))
                            break;

                        var json = Encoding.UTF8.GetString(payload);

                        // Extract cmd/id early — needed by client_hello fast-path before
                        // the generic first-message block runs.
                        var cmdName = JsonHelper.ExtractString(json, "cmd");
                        var msgId = JsonHelper.ExtractString(json, "id");

                        // client_hello: combined handshake — replaces ping + project_path + get_version
                        // with a single roundtrip. Handles first-message bookkeeping itself.
                        if (cmdName == "client_hello")
                        {
                            var sessionId   = JsonHelper.ExtractString(json, "sessionId");
                            var lockToken   = JsonHelper.ExtractString(json, "lockToken");
                            var role        = JsonHelper.ExtractString(json, "role");
                            var displayName = JsonHelper.ExtractString(json, "displayName");
                            var agentId     = JsonHelper.ExtractString(json, "agentId");
                            var bridgePid   = JsonHelper.ExtractInt(json, "bridgePid");
                            var chatMode    = JsonHelper.ExtractString(json, "chatMode") ?? "";

                            if (!string.IsNullOrEmpty(role)) label = RoleToLabel(role);
                            if (!string.IsNullOrEmpty(displayName)) label = displayName;

                            slot.SetEntrySession(index, generation, sessionId, lockToken, agentId,
                                !string.IsNullOrEmpty(displayName) ? displayName : label,
                                bridgePid, chatMode);
                            slot.SetEntryLabel(index, generation, label);

                            var ep0 = endPoint; var lbl0 = label;
                            MainThreadDispatcher.Enqueue(() => Debug.Log(
                                $"{BiomeLabel.Tag} {lbl0} connected from {ep0}"));
                            receivedFirstMessage = true;

                            // Combined response: pong + version + projectPath in one frame
                            var stamp = MCPServer._domainStamp;
                            var ver   = MCPServer.BuildVersionString(stamp, MCPServer.PluginVersion);
                            var projPath = MCPServer._cachedDataPath != null
                                ? (System.IO.Path.GetDirectoryName(MCPServer._cachedDataPath)?.Replace('\\', '/') ?? "")
                                : "";
                            await SendAsync(stream, BuildClientHelloResponse(msgId, ver, projPath), clientToken).ConfigureAwait(false);
                            continue;
                        }

                        // Log "connected" on first real message; probes (no data) stay silent.
                        if (!receivedFirstMessage)
                        {
                            slot.Label = null;  // clear stale label from previous session
                            var role = JsonHelper.ExtractString(json, "role");
                            if (!string.IsNullOrEmpty(role))
                            {
                                label = RoleToLabel(role);
                                slot.SetEntryLabel(index, generation, label);
                            }
                            var ep0 = endPoint; var lbl0 = label;
                            MainThreadDispatcher.Enqueue(() => Debug.Log($"{BiomeLabel.Tag} {lbl0} connected from {ep0}"));
                            receivedFirstMessage = true;
                        }

                        // Fast-path: ping/get_version/status bypass main thread (works even when Editor is busy)
                        if (cmdName == "ping")
                        {
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, "pong", null), clientToken).ConfigureAwait(false);
                            continue;
                        }
                        if (cmdName == "get_version")
                        {
                            // RC-5: include domain stamp so reconnect can detect stale DLL.
                            // Use cached stamp — SyncHelper.CurrentDomainStamp is SessionState (main-thread only).
                            var stamp = MCPServer._domainStamp;
                            var ver = MCPServer.BuildVersionString(stamp, MCPServer.PluginVersion);
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, ver, null), clientToken).ConfigureAwait(false);
                            continue;
                        }
                        if (cmdName == "status")
                        {
                            var isCompiling = MCPServer._isCompiling;
                            var elapsed = isCompiling ? (DateTime.UtcNow - MCPServer._compileStartTime).TotalSeconds : 0.0;
                            await SendAsync(stream, MCPServer.FormatStatusResponse(msgId, isCompiling, elapsed), clientToken).ConfigureAwait(false);
                            continue;
                        }
                        if (cmdName == "get_enabled_tools")
                        {
                            var tools = CommandRouter.ExecGetEnabledToolsCached();
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, tools, null), clientToken).ConfigureAwait(false);
                            continue;
                        }

                        // Dispatch to main thread (supports async commands like run_tests)
                        var tcs = new TaskCompletionSource<string>();
                        var timeoutSec = MCPServer.GetCommandTimeout(cmdName);
                        using var cmdTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            clientToken, cmdTimeout.Token);
                        // Invalidate on first slow-path command (not on connection, not on fast-path probes).
                        var needsInvalidate = !refInvalidated;
                        slot.BeginCommand(index, generation, cmdName);
                        var slotChatMode = slot.GetEntryChatMode(index, generation);
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            // Skip if Python already gave up (per-command timeout fired and
                            // sent retry:2000) — prevents the queued action running a 2nd time
                            // after Python re-sent it → duplicate mutations.
                            if (MCPServer._shuttingDown || tcs.Task.IsCompleted) { tcs.TrySetCanceled(); return; }
                            // Set flag inside lambda so it stays false if the guard above cancels.
                            // Safe: await tcs.Task on pump thread happens-after this write.
                            if (needsInvalidate) { RefManager.Invalidate(); refInvalidated = true; }
                            try
                            {
                                CommandRouter.ProcessAsync(json, tcs, slotChatMode);
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                                tcs.TrySetException(e);
                            }
                        });
                        // QueuePlayerLoopUpdate wakes the main thread to drain the dispatcher queue.
                        // Wrapped in Enqueue so it runs on main thread — closes the invariant
                        // "zero Unity API on ThreadPool" (even if it were thread-safe, the pattern is consistent).
                        MainThreadDispatcher.Enqueue(() => EditorApplication.QueuePlayerLoopUpdate());

                        // Stop() / client replacement / per-command timeout unblocks this await
                        using var reg = linkedCts.Token.Register(() =>
                        {
                            if (MCPServer._shuttingDown || clientToken.IsCancellationRequested)
                                tcs.TrySetCanceled();
                            else
                                tcs.TrySetResult(
                                    $"{{\"id\":\"{JsonHelper.EscapeJson(msgId)}\",\"ok\":false,\"err\":\"Command '{JsonHelper.EscapeJson(cmdName)}' timed out after {timeoutSec}s (Unity main thread blocked). Retry.\",\"retry\":2000}}");
                        });
                        var result = await tcs.Task.ConfigureAwait(false);
                        slot.EndCommand(index, generation);
                        await SendAsync(stream, result, clientToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { /* clean shutdown or client replaced */ }
            catch (Exception e)
            {
                if (!MCPServer._shuttingDown && !clientToken.IsCancellationRequested)
                {
                    // Log at Info level — "connection reset by peer" on test teardown is expected, not an error.
                    var msg = e.Message; MainThreadDispatcher.Enqueue(() => Debug.Log($"{BiomeLabel.Tag} Client disconnect: {msg}"));
                }
            }
            finally
            {
                var entryLabel = slot.GetEntryLabel(index, generation) ?? slot.Label ?? label;
                slot.Clear(index, generation);
                if (receivedFirstMessage)
                {
                    var lbl = entryLabel; var gen = generation;
                    MainThreadDispatcher.Enqueue(() => Debug.Log($"{BiomeLabel.Tag} {lbl} disconnected (gen={gen})"));
                }
            }
        }

        // internal (not private) so UnityMCP.Editor.Tests can call directly for seam tests.
        internal static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, token).ConfigureAwait(false);
                if (read == 0)
                    return false;
                totalRead += read;
            }
            return true;
        }

        // internal (not private) so UnityMCP.Editor.Tests can call directly for seam tests.
        // ConfigureAwait(false) on BOTH awaits below is mandatory — GREEN state.
        internal static async Task SendAsync(Stream stream, string json, CancellationToken token)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            await stream.WriteAsync(frame, 0, frame.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        // ── Tier 4a: going_away frame ─────────────────────────────────────────

        internal static void SendGoingAwaySync(Stream stream)
        {
            if (stream == null) return;
            try
            {
                var payload = Encoding.UTF8.GetBytes("{\"ev\":\"going_away\",\"reason\":\"domain_reload\"}");
                var header = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
                stream.Write(header, 0, 4);
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
            catch { }
        }
    }
}
