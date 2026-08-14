// TDD: RelayBackend — thin IChatBackend that delegates to Python relay.
// All tests use injected fake RelayChatProcess (no real TCP, no relay process).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayBackendTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private List<string>          _sent;
        private Queue<string>         _pendingLines;
        private RelayChatProcess      _fakeProc;
        private RelayBackend          _backend;

        [SetUp]
        public void SetUp()
        {
            _sent         = new List<string>();
            _pendingLines = new Queue<string>();

            // Fake proc: captures sent JSON, serves queued lines on DrainLines.
            _fakeProc = new RelayChatProcess(json =>
            {
                lock (_sent) _sent.Add(json);
                return "{\"ok\":true,\"data\":\"\"}";
            });

            RelayBackend.ProcessFactory      = () => _fakeProc;
            RelaySpawner.EnsureRunningOverride = () => 19600;

            _backend = new RelayBackend("claude", "agent", "claude-opus-4-5", 9500);
            RegisterCleanup(_backend.Stop);
            _backend.Start();
        }

        [TearDown]
        public void TearDown()
        {
            RelayBackend.ProcessFactory        = null;
            RelaySpawner.EnsureRunningOverride  = null;
            RelaySpawner.StopForTests();
        }

        // ── Start ────────────────────────────────────────────────────────────

        [Test]
        public void Start_SendsStartCommand()
        {
            lock (_sent)
            {
                Assert.IsTrue(_sent.Count > 0, "No commands sent");
                Assert.IsTrue(_sent[0].Contains("\"cmd\":\"start\""), _sent[0]);
            }
        }

        [Test]
        public void Start_StartJson_ContainsBackendId()
        {
            lock (_sent)
                Assert.IsTrue(_sent[0].Contains("\"backend\":\"claude\""), _sent[0]);
        }

        [Test]
        public void Start_StartJson_ContainsMode()
        {
            lock (_sent)
                Assert.IsTrue(_sent[0].Contains("\"mode\":\"agent\""), _sent[0]);
        }

        [Test]
        public void Start_StartJson_ContainsModel()
        {
            lock (_sent)
                Assert.IsTrue(_sent[0].Contains("\"model\":\"claude-opus-4-5\""), _sent[0]);
        }

        // ── SendTurn ─────────────────────────────────────────────────────────

        [Test]
        public void SendTurn_WritesLineToProc()
        {
            _backend.SendTurn("{\"type\":\"user\"}");
            lock (_sent)
                Assert.IsTrue(_sent.Exists(j => j.Contains("\"cmd\":\"send\"")),
                    "SendTurn must write a 'send' command to proc");
        }

        // ── DrainEvents ──────────────────────────────────────────────────────

        private void EnqueueLine(string line)
        {
            // Temporarily override sendFunc so next "events" poll returns this line.
            // We use a second fake proc that has the line pre-queued.
            _fakeProc = new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                    return $"{{\"ok\":true,\"data\":\"0\\n{line}\\n\"}}";
                lock (_sent) _sent.Add(json);
                return "{\"ok\":true,\"data\":\"\"}";
            });
        }

        private static async Task WaitUntilAsync(
            Func<bool> condition, Action poll, string timeoutMessage, int timeoutMs = 2000)
        {
            var timeout = Task.Delay(timeoutMs);
            while (true)
            {
                poll?.Invoke();
                if (condition()) return;
                var nextPoll = Task.Delay(10);
                var completed = await Task.WhenAny(nextPoll, timeout);
                if (completed == timeout)
                    Assert.Fail(timeoutMessage);
                await nextPoll;
            }
        }

        [Test]
        public async Task DrainEvents_TextDelta_EmitsEvent()
        {
            // Inject line directly via fake-proc DrainLines
            var fakeProcWithLine = new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                    return "{\"ok\":true,\"data\":\"0\\nt|Hello relay\\n\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelayBackend.ProcessFactory = () => fakeProcWithLine;

            var b = new RelayBackend("claude", "ask", "", 0);
            RegisterCleanup(b.Stop);
            b.Start();

            var output = new List<ChatEvent>();
            await WaitUntilAsync(() => output.Count > 0, () => b.DrainEvents(output),
                "Expected text event was not delivered");

            b.Stop();

            Assert.IsTrue(output.Count > 0, "Expected at least one event");
            Assert.AreEqual(ChatEventKind.TextDelta, output[0].Kind);
            Assert.AreEqual("Hello relay", output[0].Text);
        }

        [Test]
        public async Task DrainEvents_TurnDone_CapturesSessionId()
        {
            var fakeProcWithLine = new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                    return "{\"ok\":true,\"data\":\"0\\nd|my-session|0.01|100|50\\n\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelayBackend.ProcessFactory = () => fakeProcWithLine;

            var b = new RelayBackend("claude", "ask", "", 0);
            RegisterCleanup(b.Stop);
            b.Start();

            var output = new List<ChatEvent>();
            await WaitUntilAsync(() => b.SessionId == "my-session", () => b.DrainEvents(output),
                "TurnDone did not update SessionId");
            b.Stop();

            Assert.AreEqual("my-session", b.SessionId);
        }

        [Test]
        public async Task DrainEvents_ToolCallComplete_ProducesToolRecord()
        {
            // tc| then tr| — should produce two ToolCallRecords (chip + result)
            var fakeProcWithLine = new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                    return "{\"ok\":true,\"data\":\"0\\ntc|bash|tid1|{}\\n1\\ntr|tid1|true|ok\\n\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelayBackend.ProcessFactory = () => fakeProcWithLine;

            var b = new RelayBackend("claude", "ask", "", 0);
            RegisterCleanup(b.Stop);
            b.Start();

            var output  = new List<ChatEvent>();
            var toolOut = new List<ToolCallRecord>();
            await WaitUntilAsync(() => toolOut.Count > 0, () => b.DrainEvents(output, toolOut),
                "Tool call record was not delivered");
            b.Stop();

            Assert.IsTrue(toolOut.Count >= 1, "Expected at least one ToolCallRecord");
        }

        [Test]
        public async Task DrainEvents_AutoReply_WritesBackToProc()
        {
            var writtenBack = new List<string>();
            var fakeProcWithLine = new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                    return "{\"ok\":true,\"data\":\"0\\nar|{\\\"reply\\\":1}\\n\"}";
                if (json.Contains("\"cmd\":\"send\""))
                    lock (writtenBack) writtenBack.Add(json);
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelayBackend.ProcessFactory = () => fakeProcWithLine;

            var b = new RelayBackend("claude", "ask", "", 0);
            RegisterCleanup(b.Stop);
            b.Start();

            var output = new List<ChatEvent>();
            await WaitUntilAsync(
                () => { lock (writtenBack) return writtenBack.Count > 0; },
                () => b.DrainEvents(output),
                "AutoReply was not written back to the process");
            b.Stop();

            // AutoReply must NOT appear in output
            foreach (var ev in output)
                Assert.AreNotEqual(ChatEventKind.AutoReply, ev.Kind);

            // Must have written the json back to proc stdin
            Assert.IsTrue(writtenBack.Count > 0, "AutoReply must be written back to proc");
        }

        // ── C2: mcp_port protocol ────────────────────────────────────────────

        [Test]
        public void Start_StartJson_ContainsMcpPort_NotMcpConfig()
        {
            lock (_sent)
            {
                var startCmd = _sent.Find(j => j.Contains("\"cmd\":\"start\""));
                Assert.IsNotNull(startCmd);
                Assert.IsFalse(startCmd.Contains("\"mcp_config\""),
                    $"Must NOT send mcp_config, got: {startCmd}");
                Assert.IsTrue(startCmd.Contains("\"mcp_port\""),
                    $"Must send mcp_port, got: {startCmd}");
            }
        }

        [Test]
        public void Start_StartJson_McpPortIsInteger_NotString()
        {
            lock (_sent)
            {
                var startCmd = _sent.Find(j => j.Contains("\"cmd\":\"start\""));
                Assert.IsNotNull(startCmd);
                Assert.IsTrue(startCmd.Contains("\"mcp_port\":"),
                    $"mcp_port key missing: {startCmd}");
                // Integer value: no quote immediately after colon
                var idx        = startCmd.IndexOf("\"mcp_port\":");
                var afterColon = startCmd.Substring(idx + "\"mcp_port\":".Length).TrimStart();
                Assert.AreNotEqual('"', afterColon[0],
                    $"mcp_port value must be an int, not a string: {startCmd}");
            }
        }

        // ── M1: Start() proc leak ────────────────────────────────────────────

        [Test]
        public void Start_CalledTwice_FirstProcIsDisposedBeforeSecond()
        {
            var killCount     = 0;
            var procCallCount = 0;

            RelayBackend.ProcessFactory = () =>
            {
                procCallCount++;
                return new RelayChatProcess(json =>
                {
                    if (json.Contains("\"cmd\":\"kill\"")) killCount++;
                    return "{\"ok\":true,\"data\":\"\"}";
                });
            };

            var b = new RelayBackend("claude", "agent", "claude-opus-4-5", 9500);
            RegisterCleanup(b.Stop);
            b.Start();   // proc #1
            b.Start();   // must Kill proc #1 first, then create proc #2
            b.Stop();

            Assert.AreEqual(2, procCallCount, "ProcessFactory must be called twice");
            Assert.GreaterOrEqual(killCount, 1, "First proc must receive Kill before reassignment");
        }

        [Test]
        public void Start_CalledAfterStop_DoesNotLeakProc()
        {
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
                "{\"ok\":true,\"data\":\"\"}");

            var b = new RelayBackend("claude", "agent", "claude-opus-4-5", 9500);
            RegisterCleanup(b.Stop);
            b.Start();
            b.Stop();    // sets _proc = null
            // Second Start with null _proc must not throw
            Assert.DoesNotThrow(() => { b.Start(); b.Stop(); });
        }

        // ── m2: accumulator reset ────────────────────────────────────────────

        [Test]
        public async Task Start_ResetsAccumulator_DirtyStateDoesNotLeak()
        {
            var pollCount = 0;
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                {
                    pollCount++;
                    if (pollCount == 1)
                        return "{\"ok\":true,\"data\":\"0\\ntc|bash|tid1|{\\\"x\\\":1}\\n\"}";
                }
                return "{\"ok\":true,\"data\":\"\"}";
            });

            var b = new RelayBackend("claude", "agent", "claude-opus-4-5", 9500);
            RegisterCleanup(b.Stop);
            b.Start();

            var output  = new List<ChatEvent>();
            var toolOut = new List<ToolCallRecord>();
            await WaitUntilAsync(() => toolOut.Count > 0, () => b.DrainEvents(output, toolOut),
                "Tool call did not reach the accumulator");

            // Start() again — must reset accumulator
            b.Start();
            b.Stop();

            // After re-start + stop: DrainEvents returns early (_proc null), no phantom record
            var output2  = new List<ChatEvent>();
            var toolOut2 = new List<ToolCallRecord>();
            b.DrainEvents(output2, toolOut2);

            Assert.AreEqual(0, toolOut2.Count,
                "No phantom ToolCallRecord after Start() resets accumulator");
        }

        // ── SetMode ──────────────────────────────────────────────────────────

        [Test]
        public void SetMode_SendsSetModeCommand()
        {
            _backend.SetMode("ask");
            lock (_sent)
                Assert.IsTrue(_sent.Exists(j => j.Contains("\"cmd\":\"set_mode\"")), "set_mode not sent");
        }

        [Test]
        public void SetMode_JsonContainsMode()
        {
            _backend.SetMode("ask");
            lock (_sent)
            {
                var setMode = _sent.Find(j => j.Contains("\"cmd\":\"set_mode\""));
                Assert.IsNotNull(setMode);
                Assert.IsTrue(setMode.Contains("\"mode\":\"ask\""), setMode);
            }
        }

        [Test]
        public async Task SetMode_WhenSessionIdSet_PassesSessionIdToProc()
        {
            // Arrange: proc delivers si| event so backend.SessionId gets set
            var sent2 = new List<string>();
            bool siDelivered = false;
            var proc = new RelayChatProcess(json =>
            {
                lock (sent2) sent2.Add(json);
                if (json.Contains("\"cmd\":\"events\"") && !siDelivered)
                {
                    siDelivered = true;
                    return "{\"ok\":true,\"data\":\"0\\nsi|sess-relay\\n\"}";
                }
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelayBackend.ProcessFactory = () => proc;
            var b = new RelayBackend("claude", "ask", null, 9500);
            RegisterCleanup(b.Stop);
            b.Start();

            var events = new List<ChatEvent>();
            await WaitUntilAsync(() => b.SessionId == "sess-relay", () => b.DrainEvents(events),
                "SessionInit did not update SessionId");
            Assert.AreEqual("sess-relay", b.SessionId, "SessionId must be set from si| event");

            // Act
            b.SetMode("agent");

            // Assert: set_mode JSON includes session_id
            bool found;
            string sentDump;
            lock (sent2)
            {
                found = sent2.Exists(j => j.Contains("\"cmd\":\"set_mode\"") &&
                                          j.Contains("\"session_id\":\"sess-relay\""));
                sentDump = string.Join("\n", sent2);
            }
            Assert.IsTrue(found,
                $"set_mode must include session_id=sess-relay\n{sentDump}");

            b.Stop();
        }

        [Test]
        public void SetMode_WhenSessionIdNull_OmitsSessionIdField()
        {
            // Default backend from SetUp has no SessionId (null)
            _backend.SetMode("ask");
            lock (_sent)
            {
                var setMode = _sent.Find(j => j.Contains("\"cmd\":\"set_mode\""));
                Assert.IsNotNull(setMode, "set_mode not found");
                Assert.IsFalse(setMode.Contains("session_id"),
                    $"session_id must be absent when SessionId is null: {setMode}");
            }
        }

        // ── SetModeAsync (T9) ────────────────────────────────────────────────

        [Test]
        public async Task SetModeAsync_WhenProcNull_CallsCallbackWithFalse()
        {
            // Fresh backend with no proc (proc defaults to null — no Start() needed)
            var b = new RelayBackend("claude", "ask", "", 9500);

            bool? result = null;
            b.SetModeAsync("agent", ok => result = ok);

            // EditorApplication.delayCall fires on next editor tick
            await WaitUntilAsync(() => result.HasValue, null,
                "SetModeAsync with null proc did not callback", 2000);
            Assert.IsFalse(result.Value, "null proc must callback with false");
        }

        [Test]
        public async Task SetModeAsync_WhenSendOk_CallsCallbackWithTrue()
        {
            // Arrange: fresh proc per test (don't share SetUp's _fakeProc)
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
            {
                if (json.Contains("set_mode")) return "{\"ok\":true,\"data\":\"spawned\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            var b = new RelayBackend("claude", "ask", "", 9500);
            RegisterCleanup(b.Stop);
            b.Start();

            bool? result = null;
            b.SetModeAsync("agent", ok => result = ok);

            await WaitUntilAsync(() => result.HasValue, null,
                "SetModeAsync ok=true did not callback", 3000);
            Assert.IsTrue(result.Value, "ok response must callback with true");
        }

        [Test]
        public async Task SetModeAsync_WhenSendFails_CallsCallbackWithFalse()
        {
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
            {
                if (json.Contains("set_mode")) return "{\"ok\":false,\"err\":\"spawn failed\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            var b = new RelayBackend("claude", "ask", "", 9500);
            RegisterCleanup(b.Stop);
            b.Start();

            bool? result = null;
            b.SetModeAsync("agent", ok => result = ok);

            await WaitUntilAsync(() => result.HasValue, null,
                "SetModeAsync ok=false did not callback", 3000);
            Assert.IsFalse(result.Value, "error response must callback with false");
        }

        [Test]
        public void SetModeAsync_UpdatesModeField()
        {
            // _mode is set synchronously before ThreadPool dispatch
            var b = new RelayBackend("claude", "ask", "", 9500);

            b.SetModeAsync("agent", _ => { });

            var mode = (string)typeof(RelayBackend)
                .GetField("_mode", System.Reflection.BindingFlags.NonPublic
                                 | System.Reflection.BindingFlags.Instance)
                .GetValue(b);
            Assert.AreEqual("agent", mode, "_mode must be updated synchronously by SetModeAsync");
        }
    }
}
