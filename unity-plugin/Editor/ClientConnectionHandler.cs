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
                    try { client.Close(); } catch { }
                    MainThreadDispatcher.Enqueue(() =>
                        Debug.LogWarning($"{BiomeLabel.Tag} {label} rejected connection: client capacity exceeded"));
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
            cmd != "ping" && cmd != "get_version" && cmd != "status" && cmd != "get_enabled_tools";

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
                            var len = length; MainThreadDispatcher.Enqueue(() => Debug.LogWarning($"{BiomeLabel.Tag} Protocol desync: length prefix {len} bytes (0x{len:X8}) exceeds {MaxMessageSize} — reconnecting"));
                            break;
                        }

                        var payload = new byte[length];
                        if (!await ReadExactAsync(stream, payload, clientToken).ConfigureAwait(false))
                            break;

                        var json = Encoding.UTF8.GetString(payload);

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
                        var cmdName = JsonHelper.ExtractString(json, "cmd");
                        var msgId = JsonHelper.ExtractString(json, "id");
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
                                CommandRouter.ProcessAsync(json, tcs);
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
                    var msg = e.Message; MainThreadDispatcher.Enqueue(() => Debug.LogError($"{BiomeLabel.Tag} Client error: {msg}"));
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
