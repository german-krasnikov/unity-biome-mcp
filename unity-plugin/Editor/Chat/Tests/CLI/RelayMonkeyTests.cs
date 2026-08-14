// Monkey / chaos tests for RelayBackend, RelaySpawner, RelayChatProcess.
// Goal: find NullReferenceExceptions, resource leaks, and state corruption via edge-case inputs.
// All tests are fully mocked — no real Python relay required.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Category(TestCategories.Stress)]
    public class RelayMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private Func<RelayChatProcess> _origProcFactory;
        private Func<int>              _origEnsureOverride;

        [SetUp]
        public void SetUp()
        {
            _origProcFactory           = RelayBackend.ProcessFactory;
            _origEnsureOverride        = RelaySpawner.EnsureRunningOverride;
            RelaySpawner.EnsureRunningOverride = () => 19700;
        }

        [TearDown]
        public void TearDown()
        {
            RelayBackend.ProcessFactory        = _origProcFactory;
            RelaySpawner.EnsureRunningOverride  = _origEnsureOverride;
            RelaySpawner.StopForTests();
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static RelayChatProcess MakeFakeProc(string eventsData = "")
        {
            return new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\""))
                    return $"{{\"ok\":true,\"data\":\"{eventsData}\"}}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
        }

        private RelayBackend MakeBackend(string id = "claude", string mode = "agent",
                                          string model = "m", int mcp = 0)
        {
            RelayBackend.ProcessFactory = () => MakeFakeProc();
            return Own(new RelayBackend(id, mode, model, mcp));
        }

        private RelayBackend Own(RelayBackend backend)
        {
            RegisterCleanup(backend.Stop);
            return backend;
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

        private static async Task AwaitSignalAsync(Task signal, string timeoutMessage)
        {
            var completed = await Task.WhenAny(signal, Task.Delay(2000));
            Assert.AreSame(signal, completed, timeoutMessage);
            await signal;
        }

        // ══════════════════════════════════════════════════════════════════════
        // B. RelayBackend lifecycle chaos (19 tests)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void Backend_ConstructWithNullId_DoesNotThrow() =>
            Assert.DoesNotThrow(() => new RelayBackend(null, "agent", "m", 0));

        [Test]
        public void Backend_ConstructWithNullMode_DoesNotThrow() =>
            Assert.DoesNotThrow(() => new RelayBackend("id", null, "m", 0));

        [Test]
        public void Backend_ConstructWithAllNulls_DoesNotThrow() =>
            Assert.DoesNotThrow(() => new RelayBackend(null, null, null, 0));

        [Test]
        public void Backend_IsRunning_BeforeStart_ReturnsFalse() =>
            Assert.IsFalse(MakeBackend().IsRunning);

        [Test]
        public void Backend_SessionId_BeforeStart_IsNull() =>
            Assert.IsNull(MakeBackend().SessionId);

        [Test]
        public void Backend_Stop_BeforeStart_DoesNotThrow() =>
            Assert.DoesNotThrow(() => MakeBackend().Stop());

        [Test]
        public void Backend_Stop_MultipleTimes_DoesNotThrow()
        {
            var b = MakeBackend(); b.Start();
            Assert.DoesNotThrow(() => { b.Stop(); b.Stop(); b.Stop(); });
        }

        [Test]
        public void Backend_Dispose_MultipleTimes_DoesNotThrow()
        {
            var b = MakeBackend(); b.Start();
            Assert.DoesNotThrow(() => { b.Dispose(); b.Dispose(); b.Dispose(); });
        }

        [Test]
        public void Backend_SetMode_BeforeStart_DoesNotThrow() =>
            Assert.DoesNotThrow(() => MakeBackend().SetMode("ask"));

        [Test]
        public void Backend_SetMode_Null_DoesNotThrow()
        {
            var b = MakeBackend(); b.Start();
            Assert.DoesNotThrow(() => b.SetMode(null)); b.Stop();
        }

        [Test]
        public void Backend_SetMode_AfterStop_DoesNotThrow()
        {
            var b = MakeBackend(); b.Start(); b.Stop();
            Assert.DoesNotThrow(() => b.SetMode("ask"));
        }

        [Test]
        public void Backend_DrainEvents_BeforeStart_EmptyOutput()
        {
            var output = new List<ChatEvent>();
            Assert.DoesNotThrow(() => MakeBackend().DrainEvents(output));
            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void Backend_DrainEvents_AfterStop_DoesNotThrow()
        {
            var b = MakeBackend(); b.Start(); b.Stop();
            Assert.DoesNotThrow(() => b.DrainEvents(new List<ChatEvent>()));
        }

        [Test]
        public async Task Backend_DrainEvents_UnknownPrefixLines_Filtered()
        {
            var polled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var proc = new RelayChatProcess(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (json.Contains("\"after_seq\":1"))
                    polled.TrySetResult(true);
                return "{\"ok\":true,\"data\":\"0\\nunknown|garbage\\n1\\nxyz|data\\n\"}";
            });
            RelayBackend.ProcessFactory = () => proc;
            var b = Own(new RelayBackend("id", "m", "model", 0));
            b.Start();
            await AwaitSignalAsync(polled.Task, "Unknown-prefix response was not polled");
            var output = new List<ChatEvent>();
            Assert.DoesNotThrow(() => b.DrainEvents(output));
            Assert.AreEqual(0, output.Count);
            b.Stop();
        }

        [Test]
        public void Backend_SendControlResponse_BeforeStart_DoesNotThrow() =>
            Assert.DoesNotThrow(() => MakeBackend().SendControlResponse("{\"type\":\"ctrl\"}"));

        [Test]
        public void Backend_StartStopStart_Succeeds()
        {
            var b = MakeBackend(); b.Start(); b.Stop();
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        [Test]
        public void Backend_LongModelName_DoesNotThrow()
        {
            RelayBackend.ProcessFactory = () => MakeFakeProc();
            var b = Own(new RelayBackend("id", "agent", new string('m', 1000), 0));
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        [Test]
        public void Backend_UnicodeResumeSessionId_DoesNotThrow()
        {
            RelayBackend.ProcessFactory = () => MakeFakeProc();
            var b = Own(new RelayBackend("id", "agent", "m", 0, "sessión-こんにちは"));
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        [Test]
        public async Task Backend_DrainEvents_EmptyEventsPoll_ProducesNoOutput()
        {
            var polled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
            {
                if (json.Contains("events")) polled.TrySetResult(true);
                return "{\"ok\":true,\"data\":\"\"}";
            });
            var b = Own(new RelayBackend("claude", "agent", "m", 0));
            b.Start();
            await AwaitSignalAsync(polled.Task, "Empty response was not polled");
            var output = new List<ChatEvent>();
            b.DrainEvents(output);
            Assert.AreEqual(0, output.Count);
            b.Stop();
        }

        // ══════════════════════════════════════════════════════════════════════
        // C. RelaySpawner.ParseRelayPort + IsProcessAlive stress (16 tests)
        // ══════════════════════════════════════════════════════════════════════

        // C1. Edge values that parse successfully
        [TestCase("relay_port:0",              0)]
        [TestCase("relay_port:-1",            -1)]
        [TestCase("relay_port:65536",      65536)]
        [TestCase("relay_port:2147483647", 2147483647)]
        [TestCase("relay_port:12345\n",    12345)]
        [TestCase("relay_port: 12345",     12345)]
        public void ParseRelayPort_EdgeValues_ParsesSuccessfully(string input, int expected) =>
            Assert.AreEqual(expected, RelaySpawner.ParseRelayPort(input));

        // C2. Inputs that must throw FormatException
        [TestCase("  relay_port:12345")]
        [TestCase("RELAY_PORT:12345")]
        [TestCase("relay_port:")]
        [TestCase("relay_port:2147483648")]
        [TestCase("relay_port:abc")]
        [TestCase("relay_port:1.5")]
        [TestCase("relay_port:12 345")]
        public void ParseRelayPort_InvalidInput_ThrowsFormatException(string input) =>
            Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort(input));

        // C3. 10 KB junk string — wrong prefix → FormatException
        [Test]
        public void ParseRelayPort_VeryLongBadString_ThrowsFormatException() =>
            Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort(new string('x', 10_000)));

        // C4. Stop is idempotent
        [Test]
        public void Spawner_Stop_WhenNotRunning_IsIdempotent() =>
            Assert.DoesNotThrow(() =>
            {
                RelaySpawner.StopForTests();
                RelaySpawner.StopForTests();
                RelaySpawner.StopForTests();
            });

        // C5. int.MaxValue PID almost certainly does not exist
        [Test]
        public void Spawner_IsProcessAlive_MaxIntPid_ReturnsFalse() =>
            Assert.IsFalse(RelaySpawner.IsProcessAlive(int.MaxValue));

        // ══════════════════════════════════════════════════════════════════════
        // D. RelayChatProcess edge cases (10 tests)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void RCP_DrainLines_BeforeStart_ReturnsEmpty()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            var out_ = new List<string>();
            proc.DrainLines(out_);
            Assert.AreEqual(0, out_.Count);
        }

        [Test]
        public void RCP_SendSetMode_WhenNotRunning_SendsNothing()
        {
            var sent = new List<string>();
            var proc = new RelayChatProcess(json => { lock (sent) sent.Add(json); return "{\"ok\":true,\"data\":\"\"}"; });
            proc.SendSetMode("ask");
            Assert.AreEqual(0, sent.Count);
        }

        [Test]
        public void RCP_StartViaRelay_RelayError_ThrowsInvalidOperation()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":false,\"err\":\"relay down\"}");
            Assert.Throws<InvalidOperationException>(() =>
                proc.StartViaRelay(0, "id", "agent", "m", 0, null));
            proc.Dispose();
        }

        [Test]
        public void RCP_StartViaRelay_NullStrings_DoesNotThrow()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            Assert.DoesNotThrow(() => proc.StartViaRelay(0, null, null, null, 0, null));
            proc.Dispose();
        }

        [Test]
        public void RCP_WriteLine_NullText_DoesNotThrow()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            proc.StartViaRelay(0, "id", "agent", "m", 0, null);
            Assert.DoesNotThrow(() => proc.WriteLine(null));
            proc.Dispose();
        }

        [Test]
        public void RCP_WriteLine_100KPayload_DoesNotThrow()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            proc.StartViaRelay(0, "id", "agent", "m", 0, null);
            Assert.DoesNotThrow(() => proc.WriteLine(new string('a', 100_000)));
            proc.Dispose();
        }

        [Test]
        public void RCP_SendSetMode_Null_DoesNotThrow()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            proc.StartViaRelay(0, "id", "agent", "m", 0, null);
            Assert.DoesNotThrow(() => proc.SendSetMode(null));
            proc.Dispose();
        }

        [Test]
        public void RCP_CloseStdin_BeforeStart_DoesNotThrow()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            Assert.DoesNotThrow(() => proc.CloseStdin());
        }

        [Test]
        public void RCP_DrainLines_AppendsToNonEmptyList()
        {
            var proc = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            var out_ = new List<string> { "existing" };
            proc.DrainLines(out_);
            Assert.AreEqual(1, out_.Count, "Pre-existing entries must survive empty drain");
        }

        // ══════════════════════════════════════════════════════════════════════
        // E. Integration chaos (8 tests)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void Integration_100RapidSendTurns_NoException()
        {
            var b = MakeBackend(); b.Start();
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 100; i++) b.SendTurn($"{{\"turn\":{i}}}");
            });
            b.Stop();
        }

        [Test]
        public void Integration_50RapidSetModes_NoException()
        {
            var b = MakeBackend(); b.Start();
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 50; i++) b.SetMode(i % 2 == 0 ? "agent" : "ask");
            });
            b.Stop();
        }

        [Test]
        public void Integration_SendThenDrainThenStop_NoException()
        {
            var b = MakeBackend(); b.Start();
            b.SendTurn("{\"type\":\"user\",\"text\":\"hello\"}");
            var output = new List<ChatEvent>();
            b.DrainEvents(output);
            Assert.DoesNotThrow(() => b.Stop());
        }

        [Test]
        public void Integration_TwoBackendsSameSpawner_Independent()
        {
            RelayBackend.ProcessFactory = () => MakeFakeProc();
            var b1 = Own(new RelayBackend("id1", "agent", "m", 0));
            var b2 = Own(new RelayBackend("id2", "ask",   "m", 0));
            b1.Start(); b2.Start();
            b1.SendTurn("{\"type\":\"user\"}");
            b2.SetMode("agent");
            b1.DrainEvents(new List<ChatEvent>());
            b2.DrainEvents(new List<ChatEvent>());
            b1.Stop(); b2.Stop();
        }

        [Test]
        public void Integration_DisposeAfterRapidSendTurns_NoException()
        {
            var b = MakeBackend(); b.Start();
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++) b.SendTurn($"{{\"t\":{i}}}");
                b.Dispose();
            });
        }

        [Test]
        public async Task Integration_SessionIdCapturedFromTurnDone()
        {
            // ACP format: turn_completed with session_id at top level sets SessionId
            var proc = new RelayChatProcess(json =>
                json.Contains("events")
                    ? "{\"ok\":true,\"data\":\"0\\n{\\\"kind\\\":\\\"turn_completed\\\",\\\"payload\\\":{},\\\"session_id\\\":\\\"test-sess\\\"}\\n\"}"
                    : "{\"ok\":true,\"data\":\"\"}");
            RelayBackend.ProcessFactory = () => proc;
            var b = Own(new RelayBackend("id", "agent", "m", 0));
            b.Start();
            var events = new List<ChatEvent>();
            await WaitUntilAsync(() => b.SessionId == "test-sess", () => b.DrainEvents(events),
                "TurnDone did not update SessionId");
            b.Stop();
            Assert.AreEqual("test-sess", b.SessionId);
        }

        [Test]
        public void Integration_SpecialCharsInModel_DoesNotThrow()
        {
            RelayBackend.ProcessFactory = () => MakeFakeProc();
            var b = Own(new RelayBackend("id", "agent", "m\"with quotes\"\n", 0));
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        [Test]
        public void Integration_StopImmediatelyAfterStart_IsRunningFalse()
        {
            var b = MakeBackend(); b.Start(); b.Stop();
            Assert.IsFalse(b.IsRunning);
        }
    }
}
