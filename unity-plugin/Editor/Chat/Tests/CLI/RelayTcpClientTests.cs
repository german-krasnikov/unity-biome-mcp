// TDD: RelayTcpClient — 4-byte BE framing, lock safety, error handling.
// Uses in-process loopback TcpListener; no Unity API required.
using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayTcpClientTests
    {
        private TcpListener          _listener;
        private int                  _port;
        private RelayTcpClient       _sut;
        private ManualResetEventSlim _serverDone;

        [SetUp]
        public void SetUp()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _sut  = new RelayTcpClient();
            _serverDone = new ManualResetEventSlim(false);
        }

        [TearDown]
        public void TearDown()
        {
            _sut?.Dispose();
            try { _listener?.Stop(); } catch { }
        }

        // ── Server helpers ─────────────────────────────────────────────────────

        // Start a server thread that accepts one client and invokes handler.
        private void StartServer(Action<NetworkStream> handler)
        {
            new Thread(() =>
            {
                try
                {
                    using var client = _listener.AcceptTcpClient();
                    handler(client.GetStream());
                }
                catch { /* expected in error-path tests */ }
                finally { _serverDone.Set(); }
            }) { IsBackground = true }.Start();
        }

        // Read one frame (header + payload) from stream. Returns payload bytes.
        private static byte[] ReadFrame(NetworkStream s)
        {
            var hdr = new byte[4];
            ReadExact(s, hdr);
            var len = (int)BinaryPrimitives.ReadUInt32BigEndian(hdr);
            var payload = new byte[len];
            if (len > 0) ReadExact(s, payload);
            return payload;
        }

        // Read one frame, returning header+payload separately for framing assertions.
        private static (byte[] header, byte[] payload) ReadFrameRaw(NetworkStream s)
        {
            var hdr = new byte[4];
            ReadExact(s, hdr);
            var len = (int)BinaryPrimitives.ReadUInt32BigEndian(hdr);
            var payload = new byte[len];
            if (len > 0) ReadExact(s, payload);
            return (hdr, payload);
        }

        // Write a framed response.
        private static void WriteFrame(NetworkStream s, byte[] payload)
        {
            var hdr = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(hdr, (uint)payload.Length);
            s.Write(hdr, 0, 4);
            if (payload.Length > 0) s.Write(payload, 0, payload.Length);
            s.Flush();
        }

        private static void WriteFrame(NetworkStream s, string json)
            => WriteFrame(s, Encoding.UTF8.GetBytes(json));

        private static void ReadExact(Stream s, byte[] buf)
        {
            int total = 0;
            while (total < buf.Length)
            {
                int n = s.Read(buf, total, buf.Length - total);
                if (n == 0) throw new EndOfStreamException();
                total += n;
            }
        }

        private void WaitServer(int ms = 2000) => _serverDone.Wait(ms);

        // ── Group 1: IsConnected state ─────────────────────────────────────────

        [Test]
        public void IsConnected_BeforeConnect_IsFalse()
        {
            Assert.IsFalse(_sut.IsConnected);
        }

        [Test]
        public void Connect_ValidPort_IsConnectedTrue()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            Assert.IsTrue(_sut.IsConnected);
            WaitServer();
        }

        [Test]
        public void IsConnected_AfterClose_IsFalse()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            _sut.Close();
            Assert.IsFalse(_sut.IsConnected);
            WaitServer();
        }

        [Test]
        public void IsConnected_AfterDispose_IsFalse()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            _sut.Dispose();
            Assert.IsFalse(_sut.IsConnected);
            WaitServer();
        }

        [Test]
        public void Close_WhenNotConnected_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sut.Close());
        }

        [Test]
        public void Dispose_Idempotent_DoesNotThrow()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            WaitServer();
            Assert.DoesNotThrow(() =>
            {
                _sut.Dispose();
                _sut.Dispose();
                _sut.Dispose();
            });
        }

        // ── Group 2: Send-side framing ─────────────────────────────────────────

        [Test]
        public void SendCommand_Writes4ByteBigEndianHeader()
        {
            byte[] header = null;
            StartServer(s =>
            {
                var (hdr, _) = ReadFrameRaw(s);
                header = hdr;
                WriteFrame(s, "{}");
            });
            _sut.Connect(_port);
            _sut.SendCommand("{\"cmd\":\"ping\"}");
            WaitServer();

            Assert.IsNotNull(header);
            Assert.AreEqual(4, header.Length);
        }

        [Test]
        public void SendCommand_HeaderValueMatchesUtf8ByteCount()
        {
            const string json = "{\"cmd\":\"test\",\"x\":42}";
            var expected = (uint)Encoding.UTF8.GetByteCount(json);
            uint actual = 0;
            StartServer(s =>
            {
                var (hdr, _) = ReadFrameRaw(s);
                actual = BinaryPrimitives.ReadUInt32BigEndian(hdr);
                WriteFrame(s, "{}");
            });
            _sut.Connect(_port);
            _sut.SendCommand(json);
            WaitServer();

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void SendCommand_PayloadMatchesInputUtf8Bytes()
        {
            const string json = "{\"cmd\":\"spawn\"}";
            byte[] expected = Encoding.UTF8.GetBytes(json);
            byte[] actual   = null;
            StartServer(s =>
            {
                actual = ReadFrame(s);
                WriteFrame(s, "{}");
            });
            _sut.Connect(_port);
            _sut.SendCommand(json);
            WaitServer();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void SendCommand_AsciiString_HeaderEqualsStringLength()
        {
            // For pure ASCII, UTF-8 byte count == string length
            const string json = "{\"a\":1}";
            uint headerLen = 0;
            StartServer(s =>
            {
                var (hdr, _) = ReadFrameRaw(s);
                headerLen = BinaryPrimitives.ReadUInt32BigEndian(hdr);
                WriteFrame(s, "{}");
            });
            _sut.Connect(_port);
            _sut.SendCommand(json);
            WaitServer();

            Assert.AreEqual((uint)json.Length, headerLen);
        }

        [Test]
        public void SendCommand_MultibyteUtf8_HeaderEqualsEncodedByteCount()
        {
            // "héllo" — 'é' is 2 bytes in UTF-8
            const string json = "{\"msg\":\"héllo\"}";
            uint headerLen = 0;
            StartServer(s =>
            {
                var (hdr, _) = ReadFrameRaw(s);
                headerLen = BinaryPrimitives.ReadUInt32BigEndian(hdr);
                WriteFrame(s, "{}");
            });
            _sut.Connect(_port);
            _sut.SendCommand(json);
            WaitServer();

            Assert.AreEqual((uint)Encoding.UTF8.GetByteCount(json), headerLen);
        }

        // ── Group 3: Receive-side framing ──────────────────────────────────────

        [Test]
        public void SendCommand_ReturnsResponseString()
        {
            const string resp = "{\"ok\":true,\"data\":\"pong\"}";
            StartServer(s =>
            {
                ReadFrame(s);
                WriteFrame(s, resp);
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand("{\"cmd\":\"ping\"}");

            Assert.AreEqual(resp, result);
            WaitServer();
        }

        [Test]
        public void SendCommand_EchoRoundtrip_ReturnsInput()
        {
            const string json = "{\"cmd\":\"events\",\"args\":{\"after_seq\":0}}";
            StartServer(s =>
            {
                var payload = ReadFrame(s);
                WriteFrame(s, payload); // echo
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand(json);

            Assert.AreEqual(json, result);
            WaitServer();
        }

        [Test]
        public void SendCommand_ServerSendsDifferentResponse_ReturnsServerResponse()
        {
            StartServer(s =>
            {
                ReadFrame(s);
                WriteFrame(s, "{\"ok\":false,\"err\":\"no session\"}");
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand("{\"cmd\":\"send\"}");

            StringAssert.Contains("no session", result);
            WaitServer();
        }

        [Test]
        public void SendCommand_EmptyResponse_ReturnsEmptyString()
        {
            StartServer(s =>
            {
                ReadFrame(s);
                WriteFrame(s, new byte[0]); // 4-byte zero header, no payload
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand("{\"cmd\":\"test\"}");

            Assert.AreEqual("", result);
            WaitServer();
        }

        [Test]
        public void SendCommand_LargeResponse_AllBytesPreserved()
        {
            // 64 KB response
            var big = new string('Z', 65536);
            StartServer(s =>
            {
                ReadFrame(s);
                WriteFrame(s, big);
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand("{\"cmd\":\"events\"}");

            Assert.AreEqual(big, result);
            WaitServer();
        }

        // ── Group 4: Size limits ───────────────────────────────────────────────

        [Test]
        public void SendCommand_100KB_Accepted()
        {
            var payload = new string('A', 102400);
            StartServer(s =>
            {
                var req = ReadFrame(s);
                WriteFrame(s, req); // echo
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand(payload);

            Assert.AreEqual(payload, result);
            WaitServer();
        }

        [Test]
        public void SendCommand_ExceedsMaxMessage_ThrowsInvalidOperation()
        {
            // Client must be connected, but server never receives anything
            StartServer(s => { Thread.Sleep(200); }); // accept and wait
            _sut.Connect(_port);

            var huge = new string('x', RelayTcpClient.MaxMessage + 1);
            Assert.Throws<InvalidOperationException>(() => _sut.SendCommand(huge));
        }

        [Test]
        public void SendCommand_ServerSendsOversizedResponse_ThrowsInvalidOperation()
        {
            StartServer(s =>
            {
                ReadFrame(s);
                // Write oversized length header (MaxMessage+1), no actual payload
                var hdr = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(hdr, (uint)(RelayTcpClient.MaxMessage + 1));
                s.Write(hdr, 0, 4);
                s.Flush();
            });
            _sut.Connect(_port);

            Assert.Throws<InvalidOperationException>(() => _sut.SendCommand("{\"cmd\":\"x\"}"));
            WaitServer();
        }

        // ── Group 5: Error handling ────────────────────────────────────────────

        [Test]
        public void SendCommand_WhenNotConnected_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(() => _sut.SendCommand("{\"cmd\":\"ping\"}"));
        }

        [Test]
        public void SendCommand_AfterClose_ThrowsInvalidOperation()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            WaitServer();
            _sut.Close();

            Assert.Throws<InvalidOperationException>(() => _sut.SendCommand("{\"cmd\":\"ping\"}"));
        }

        [Test]
        public void SendCommand_AfterDispose_ThrowsInvalidOperation()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            WaitServer();
            _sut.Dispose();

            Assert.Throws<InvalidOperationException>(() => _sut.SendCommand("{\"cmd\":\"ping\"}"));
        }

        [Test]
        public void Connect_ToClosedPort_ThrowsSocketException()
        {
            // Get a port that's not listening
            var tmp = new TcpListener(IPAddress.Loopback, 0);
            tmp.Start();
            var closedPort = ((IPEndPoint)tmp.LocalEndpoint).Port;
            tmp.Stop();

            Assert.Throws<SocketException>(() => _sut.Connect(closedPort));
        }

        [Test]
        public void SendCommand_ServerClosesImmediately_ThrowsIOException()
        {
            // Server closes the connection right after accepting — no response written.
            // Client write may succeed (data sent before RST) or fail (ECONNRESET);
            // either way the subsequent read raises IOException (EndOfStream or SocketError).
            new Thread(() =>
            {
                try { using var c = _listener.AcceptTcpClient(); /* dispose = close */ }
                catch { }
                finally { _serverDone.Set(); }
            }) { IsBackground = true }.Start();

            _sut.Connect(_port);
            Assert.That(() => _sut.SendCommand("{\"cmd\":\"ping\"}"), Throws.InstanceOf<IOException>());
            WaitServer();
        }

        [Test]
        public void SendCommand_ServerClosesAfterReadingRequest_ThrowsEndOfStream()
        {
            StartServer(s =>
            {
                ReadFrame(s);
                // Don't write response — just close
            }); // stream closed by using-block exit
            _sut.Connect(_port);

            Assert.Throws<EndOfStreamException>(() => _sut.SendCommand("{\"cmd\":\"ping\"}"));
            WaitServer();
        }

        [Test]
        public void SendCommand_ReadTimeout_ThrowsIOException()
        {
            var fast = new RelayTcpClient(timeoutMs: 100);
            try
            {
                StartServer(s =>
                {
                    try { ReadFrame(s); } catch { }
                    Thread.Sleep(500); // hold without responding → triggers client timeout
                });
                fast.Connect(_port);

                Assert.Throws<IOException>(() => fast.SendCommand("{\"cmd\":\"ping\"}"));
            }
            finally { fast.Dispose(); }
            WaitServer(1000);
        }

        // ── Group 6: Concurrency ───────────────────────────────────────────────

        [Test]
        public void SendCommand_ConcurrentCallers_SerializedByLock()
        {
            // 3 threads call SendCommand concurrently; lock ensures sequential framing
            var results = new string[3];
            StartServer(s =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var req = ReadFrame(s);
                    WriteFrame(s, req); // echo each
                }
            });
            _sut.Connect(_port);

            var threads = new Thread[3];
            for (int i = 0; i < 3; i++)
            {
                var idx = i;
                var msg = $"{{\"n\":{idx}}}";
                threads[idx] = new Thread(() => results[idx] = _sut.SendCommand(msg));
            }
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join(3000);
            WaitServer();

            // All results are non-null — no deadlock, no corruption
            foreach (var r in results)
                Assert.IsNotNull(r);
        }

        [Test]
        public void SendCommand_MultipleSequential_AllSucceed()
        {
            StartServer(s =>
            {
                for (int i = 0; i < 5; i++)
                {
                    var req = ReadFrame(s);
                    WriteFrame(s, req);
                }
            });
            _sut.Connect(_port);

            for (int i = 0; i < 5; i++)
            {
                var msg = $"{{\"i\":{i}}}";
                Assert.AreEqual(msg, _sut.SendCommand(msg));
            }
            WaitServer();
        }

        [Test]
        public void SendCommand_BackToBack3_FramingPreserved()
        {
            var cmds    = new[] { "{\"cmd\":\"a\"}", "{\"cmd\":\"b\"}", "{\"cmd\":\"c\"}" };
            var captured = new string[3];
            StartServer(s =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var req = ReadFrame(s);
                    captured[i] = Encoding.UTF8.GetString(req);
                    WriteFrame(s, $"{{\"ok\":{i}}}");
                }
            });
            _sut.Connect(_port);

            for (int i = 0; i < 3; i++)
                _sut.SendCommand(cmds[i]);
            WaitServer();

            CollectionAssert.AreEqual(cmds, captured);
        }

        [Test]
        public void DoubleClose_DoesNotThrow()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            WaitServer();
            Assert.DoesNotThrow(() =>
            {
                _sut.Close();
                _sut.Close();
            });
        }

        // ── Group 7: Edge cases ────────────────────────────────────────────────

        [Test]
        public void SendCommand_SpecialJsonChars_Preserved()
        {
            const string json = "{\"msg\":\"hello\\\"world\\\"\",\"tab\":\"\\t\"}";
            StartServer(s =>
            {
                ReadFrame(s);
                WriteFrame(s, json); // respond with same
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand(json);

            Assert.AreEqual(json, result);
            WaitServer();
        }

        [Test]
        public void SendCommand_UnicodePayload_Preserved()
        {
            const string json = "{\"msg\":\"日本語テスト\"}";
            StartServer(s =>
            {
                var req = ReadFrame(s);
                WriteFrame(s, req);
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand(json);

            Assert.AreEqual(json, result);
            WaitServer();
        }

        [Test]
        public void SendCommand_RequestAndResponseDifferentSizes_BothCorrect()
        {
            // Small request, large response
            var bigResp = "{\"data\":\"" + new string('Y', 4096) + "\"}";
            StartServer(s =>
            {
                ReadFrame(s);
                WriteFrame(s, bigResp);
            });
            _sut.Connect(_port);
            var result = _sut.SendCommand("{\"cmd\":\"e\"}");

            Assert.AreEqual(bigResp, result);
            WaitServer();
        }

        [Test]
        public void IsConnected_AfterDoubleClose_IsFalse()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            WaitServer();
            _sut.Close();
            _sut.Close();

            Assert.IsFalse(_sut.IsConnected);
        }

        [Test]
        public void Dispose_ClosesUnderlyingSocket()
        {
            StartServer(_ => { });
            _sut.Connect(_port);
            WaitServer();
            _sut.Dispose();

            // After dispose, IsConnected is false — stream and socket are nulled
            Assert.IsFalse(_sut.IsConnected);
            // Calling Close() after Dispose() must not throw
            Assert.DoesNotThrow(() => _sut.Close());
        }
    }
}
