// TCP client for chat_relay.py. 4-byte BE length prefix. Thread-safe via lock.
// NO Debug.Log — called from background threads (Unity 6 not thread-safe for Debug.*).
using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    internal sealed class RelayTcpClient : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly int    _timeoutMs;
        private TcpClient       _socket;
        private NetworkStream   _stream;
        private bool            _disposed;
        private int             _port;

        internal const int MaxMessage = 10_000_000;

        internal RelayTcpClient(int timeoutMs = 30000)
        {
            _timeoutMs = timeoutMs;
        }

        internal bool IsConnected
        {
            get { lock (_syncRoot) return !_disposed && _stream != null; }
        }

        // Blocking connect to loopback. Throws SocketException on failure.
        internal void Connect(int port)
        {
            lock (_syncRoot)
            {
                _port   = port;
                _socket = new TcpClient();
                _socket.Connect(IPAddress.Loopback, port);
                _socket.NoDelay = true;
                _stream = _socket.GetStream();
                _stream.ReadTimeout  = _timeoutMs;
                _stream.WriteTimeout = _timeoutMs;
            }
        }

        // Close current socket and reconnect to the same port. Called by PollLoop on transient errors.
        internal void Reconnect()
        {
            int port;
            lock (_syncRoot)
            {
                port = _port;
                if (port <= 0) throw new InvalidOperationException("No port stored — call Connect first");
                CloseCore();
            }
            Connect(port); // acquires _syncRoot internally
        }

        // Blocking request-response. Lock prevents interleaving from concurrent callers.
        // Throws InvalidOperationException if not connected or message too large.
        // Throws EndOfStreamException / IOException on connection failure.
        internal string SendCommand(string json)
        {
            lock (_syncRoot)
            {
                if (_stream == null)
                    throw new InvalidOperationException("Not connected to relay");

                var payload = Encoding.UTF8.GetBytes(json);
                if (payload.Length > MaxMessage)
                    throw new InvalidOperationException(
                        $"Request too large: {payload.Length} bytes (max {MaxMessage})");

                WriteFrame(_stream, payload);

                var header = new byte[4];
                ReadExact(_stream, header);
                var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                if (length > MaxMessage)
                    throw new InvalidOperationException(
                        $"Response too large: {length} bytes (max {MaxMessage})");

                var response = new byte[length];
                if (length > 0) ReadExact(_stream, response);
                return Encoding.UTF8.GetString(response);
            }
        }

        internal void Close()
        {
            lock (_syncRoot) CloseCore();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                CloseCore();
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void CloseCore()
        {
            try { _stream?.Close(); } catch { }
            try { _socket?.Close(); } catch { }
            _stream = null;
            _socket = null;
        }

        private static void WriteFrame(Stream stream, byte[] payload)
        {
            var header = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
            stream.Write(header, 0, 4);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static void ReadExact(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0) throw new EndOfStreamException("Relay closed connection");
                total += n;
            }
        }
    }
}
