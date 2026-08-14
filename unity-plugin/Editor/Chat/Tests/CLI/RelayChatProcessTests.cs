// TDD: RelayChatProcess — relay-backed ChatProcess replacement.
// sendCommand function injected via test constructor — no TCP required.
// All log accesses are locked — poll thread writes concurrently.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayChatProcessTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private RelayChatProcess _sut;

        [TearDown]
        public void TearDown() { _sut?.Dispose(); _sut = null; }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> EmptyEnv   = new Dictionary<string, string>();
        private static readonly string[]                   EmptyStrip = new string[0];

        // Build a send function that records calls and returns a canned JSON response.
        private static Func<string, string> MakeSend(List<string> log, Func<string, string> resp = null)
        {
            return json =>
            {
                lock (log) log.Add(json);
                return resp != null ? resp(json) : "{\"ok\":true,\"data\":\"\"}";
            };
        }

        // Create a SUT with injected send. Spawn it and return the command log.
        private RelayChatProcess Spawn(string binary,
            string[] argv, Dictionary<string, string> env, string[] strip,
            Func<string, string> resp, out List<string> log)
        {
            log = new List<string>();
            var send = MakeSend(log, resp);
            var sut  = new RelayChatProcess(send);
            sut.SpawnViaRelay(0, binary,
                argv  ?? new string[0],
                env   ?? EmptyEnv,
                strip ?? EmptyStrip);
            return sut;
        }

        // Drain log while holding lock.
        private static List<string> SnapLog(List<string> log)
        {
            lock (log) return new List<string>(log);
        }

        private static bool LogContains(List<string> log, string fragment)
        {
            lock (log) return log.Exists(j => j.Contains(fragment));
        }

        private static int LogCount(List<string> log)
        {
            lock (log) return log.Count;
        }

        private static async Task WaitUntilAsync(
            Func<bool> condition, string timeoutMessage, int timeoutMs = 2000)
        {
            var timeout = Task.Delay(timeoutMs);
            while (!condition())
            {
                var nextPoll = Task.Delay(10);
                var completed = await Task.WhenAny(nextPoll, timeout);
                if (completed == timeout)
                    Assert.Fail(timeoutMessage);
                await nextPoll;
            }
        }

        // ── IsRunning ─────────────────────────────────────────────────────────

        [Test]
        public void IsRunning_BeforeSpawn_ReturnsFalse()
        {
            _sut = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            Assert.IsFalse(_sut.IsRunning);
        }

        [Test]
        public void IsRunning_AfterSuccessfulSpawn_ReturnsTrue()
        {
            _sut = Spawn(out _);
            Assert.IsTrue(_sut.IsRunning);
        }

        [Test]
        public void IsRunning_AfterKill_ReturnsFalse()
        {
            _sut = Spawn(out _);
            _sut.Kill();
            Assert.IsFalse(_sut.IsRunning);
        }

        [Test]
        public void IsRunning_AfterDispose_ReturnsFalse()
        {
            _sut = Spawn(out _);
            _sut.Dispose();
            Assert.IsFalse(_sut.IsRunning);
        }

        // overload helpers for cleaner test calls
        private RelayChatProcess Spawn(string binary, out List<string> log) =>
            Spawn(binary, null, null, null, null, out log);

        private RelayChatProcess Spawn(Func<string, string> resp, out List<string> log) =>
            Spawn("/bin/cli", null, null, null, resp, out log);

        private RelayChatProcess Spawn(out List<string> log) =>
            Spawn("/bin/cli", null, null, null, null, out log);

        // ── DrainLines ────────────────────────────────────────────────────────

        [Test]
        public void DrainLines_WhenNoEvents_OutputIsEmpty()
        {
            _sut = Spawn(out _);
            var output = new List<string>();
            _sut.DrainLines(output);
            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public async Task DrainLines_AfterEventsArrival_ReturnsLine()
        {
            bool delivered = false;
            _sut = Spawn(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (!delivered) { delivered = true; return "{\"ok\":true,\"data\":\"0\\nfirst line\\n\"}"; }
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count > 0;
            }, "Expected event line was not delivered");
            Assert.Greater(output.Count, 0, "Expected at least one line");
            Assert.AreEqual("first line", output[0]);
        }

        [Test]
        public async Task DrainLines_MultipleLines_AllReturned()
        {
            bool delivered = false;
            _sut = Spawn(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (!delivered) { delivered = true; return "{\"ok\":true,\"data\":\"0\\nA\\n1\\nB\\n2\\nC\\n\"}"; }
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count >= 3;
            }, "Expected three event lines were not delivered");
            Assert.AreEqual(3, output.Count, $"Expected 3 lines, got {output.Count}");
            Assert.AreEqual("A", output[0]);
            Assert.AreEqual("B", output[1]);
            Assert.AreEqual("C", output[2]);
        }

        [Test]
        public async Task DrainLines_CalledTwice_SecondCallIsEmpty()
        {
            bool delivered = false;
            _sut = Spawn(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (!delivered) { delivered = true; return "{\"ok\":true,\"data\":\"0\\nhello\\n\"}"; }
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var first = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(first);
                return first.Count > 0;
            }, "Expected event line was not delivered");
            var second = new List<string>(); _sut.DrainLines(second);
            Assert.AreEqual(0, second.Count, "Second drain must be empty");
        }

        [Test]
        public void DrainLines_AppendsToExistingList()
        {
            _sut = Spawn(out _);
            var output = new List<string> { "pre" };
            _sut.DrainLines(output);
            Assert.AreEqual(1, output.Count); // no new lines; "pre" stays
        }

        // ── WriteLine ─────────────────────────────────────────────────────────

        [Test]
        public async Task WriteLine_SendsSendCommand()
        {
            _sut = Spawn(out var log);
            _sut.WriteLine("hello");
            await WaitUntilAsync(() => LogContains(log, "\"cmd\":\"send\""),
                "send cmd was not observed");
            Assert.IsTrue(LogContains(log, "\"cmd\":\"send\""),
                "send cmd must be present after WriteLine");
        }

        [Test]
        public async Task WriteLine_SendsLineField()
        {
            _sut = Spawn(out var log);
            _sut.WriteLine("test message");
            await WaitUntilAsync(() => LogContains(log, "test message"),
                "send payload was not observed");
            Assert.IsTrue(LogContains(log, "test message"),
                "text must appear in send payload");
        }

        [Test]
        public async Task WriteLine_SpecialChars_EscapedInJson()
        {
            _sut = Spawn(out var log);
            _sut.WriteLine("say \"hi\"");
            await WaitUntilAsync(() => LogContains(log, "\\\""),
                "escaped send payload was not observed");
            Assert.IsTrue(LogContains(log, "\\\""), "quote must be escaped in JSON");
        }

        [Test]
        public async Task WriteLine_WhenNotRunning_DoesNotSend()
        {
            _sut = Spawn(out var log);
            _sut.Kill();
            // Observation window is the assertion: the stopped poll loop must remain quiescent.
            await Task.Delay(150);
            int before = LogCount(log);
            _sut.WriteLine("should be dropped");
            int after = LogCount(log);
            Assert.AreEqual(before, after, "No send after Kill");
        }

        [Test]
        public async Task WriteLine_RelayError_EnqueuesErrorAndStops()
        {
            bool spawnDone = false;
            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                if (json.Contains("\"cmd\":\"send\"")) return "{\"ok\":false,\"err\":\"binary 'codex' not found in PATH\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            _sut.WriteLine("hello");
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count > 0;
            }, "Relay error line was not delivered");
            Assert.Greater(output.Count, 0, "Error line must be enqueued");
            Assert.IsTrue(output.Exists(l => l.StartsWith("e|") && l.Contains("binary")),
                $"Expected e|relay:... error, got: {string.Join(", ", output)}");
            Assert.IsFalse(_sut.IsRunning, "IsRunning must be false after relay error");
        }

        [Test]
        public void WriteLine_WhenSendThrows_DoesNotPropagate()
        {
            bool spawnDone = false;
            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                if (json.Contains("send")) throw new Exception("disconnected");
                return "{\"ok\":true,\"data\":\"\"}";
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            Assert.DoesNotThrow(() => _sut.WriteLine("hello"));
        }

        // ── CloseStdin ────────────────────────────────────────────────────────

        [Test]
        public void CloseStdin_SendsCloseStdinCommand()
        {
            _sut = Spawn(out var log);
            _sut.CloseStdin();
            Assert.IsTrue(LogContains(log, "close_stdin"),
                "close_stdin must be sent");
        }

        [Test]
        public void CloseStdin_Idempotent_NoException()
        {
            _sut = Spawn(out _);
            Assert.DoesNotThrow(() => { _sut.CloseStdin(); _sut.CloseStdin(); });
        }

        [Test]
        public void CloseStdin_AfterDispose_DoesNotThrow()
        {
            _sut = Spawn(out _);
            _sut.Dispose();
            Assert.DoesNotThrow(() => _sut.CloseStdin());
        }

        // ── Kill ──────────────────────────────────────────────────────────────

        [Test]
        public void Kill_SendsKillCommand()
        {
            _sut = Spawn(out var log);
            _sut.Kill();
            Assert.IsTrue(LogContains(log, "\"cmd\":\"kill\""),
                "kill cmd must be sent");
        }

        [Test]
        public void Kill_SetsIsRunningFalse()
        {
            _sut = Spawn(out _);
            _sut.Kill();
            Assert.IsFalse(_sut.IsRunning);
        }

        [Test]
        public void Kill_WhenAlreadyDead_DoesNotThrow()
        {
            _sut = Spawn(out _);
            _sut.Kill();
            Assert.DoesNotThrow(() => _sut.Kill());
        }

        [Test]
        public void Kill_AfterDispose_DoesNotThrow()
        {
            _sut = Spawn(out _);
            _sut.Dispose();
            Assert.DoesNotThrow(() => _sut.Kill());
        }

        // ── Background poll ───────────────────────────────────────────────────

        [Test]
        public async Task PollLoop_CallsEventsCommand_WhenRunning()
        {
            _sut = Spawn(out var log);
            await WaitUntilAsync(() => LogContains(log, "\"events\""),
                "events cmd was not called by poll thread");
            Assert.IsTrue(LogContains(log, "\"events\""), "events cmd must be called by poll thread");
        }

        [Test]
        public async Task PollLoop_EventsCmd_IncludesAfterSeqField()
        {
            _sut = Spawn(out var log);
            await WaitUntilAsync(() => LogContains(log, "\"events\""),
                "events cmd was not called by poll thread");
            var snap = SnapLog(log);
            var evCmd = snap.Find(j => j.Contains("\"events\""));
            Assert.IsNotNull(evCmd, "No events cmd found");
            Assert.IsTrue(evCmd.Contains("after_seq"), $"after_seq not in: {evCmd}");
        }

        [Test]
        public async Task PollLoop_OnRelayException_SetsIsRunningFalse()
        {
            bool spawnDone = false;
            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                throw new Exception("relay died");
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            await WaitUntilAsync(() => !_sut.IsRunning,
                "Relay exception did not stop the poll loop");
            Assert.IsFalse(_sut.IsRunning, "IsRunning must be false after relay exception");
        }

        [Test]
        public async Task PollLoop_OnRelayException_EnqueuesSyntheticErrorLine()
        {
            bool spawnDone = false;
            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                throw new Exception("relay died");
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count > 0;
            }, "Synthetic relay error was not delivered");
            Assert.Greater(output.Count, 0, "Synthetic error line must be enqueued");
            Assert.IsTrue(output[0].StartsWith("e|"), $"expected pipe error 'e|...' but got: {output[0]}");
        }

        [Test]
        public async Task PollLoop_SyntheticError_ContainsExceptionMessage()
        {
            bool spawnDone = false;
            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                throw new Exception("relay disconnected!");
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count > 0;
            }, "Synthetic relay error was not delivered");
            Assert.Greater(output.Count, 0, "Synthetic error must be enqueued");
            Assert.IsTrue(output[0].Contains("relay disconnected"),
                $"exception msg not in synthetic line: {output[0]}");
        }

        [Test]
        public async Task PollLoop_AfterKill_StopsPolling()
        {
            _sut = Spawn(out var log);
            await WaitUntilAsync(() => LogContains(log, "\"events\""),
                "Initial events poll was not observed");
            int countBefore = LogCount(log);
            _sut.Kill();
            // Observation window is the assertion: no recurring polls are allowed after Kill.
            await Task.Delay(300);
            int countAfter = LogCount(log);
            // At most: 1 kill cmd + 1 in-flight poll = 2 extra calls
            Assert.That(countAfter - countBefore, Is.LessThanOrEqualTo(2),
                "Polling must stop after Kill");
        }

        [Test]
        public async Task PollLoop_EmptyEventsResponse_NoLinesQueued()
        {
            _sut = Spawn(json =>
                json.Contains("events") ? "{\"ok\":true,\"data\":\"\"}" : "{\"ok\":true,\"data\":\"\"}",
                out var log);
            await WaitUntilAsync(() => LogContains(log, "\"events\""),
                "Empty events response was not polled");
            var output = new List<string>();
            _sut.DrainLines(output);
            Assert.AreEqual(0, output.Count, "Empty events must not queue lines");
        }

        [Test]
        public async Task PollLoop_NullDataInEventsResponse_NoLinesQueued()
        {
            _sut = Spawn(json =>
                json.Contains("events") ? "{\"ok\":true}" : "{\"ok\":true,\"data\":\"\"}",
                out var log);
            await WaitUntilAsync(() => LogContains(log, "\"events\""),
                "Missing-data events response was not polled");
            var output = new List<string>();
            _sut.DrainLines(output);
            Assert.AreEqual(0, output.Count, "Missing data field must not queue lines");
        }

        [Test]
        public async Task PollLoop_SeqTracking_SecondPollUsesUpdatedAfterSeq()
        {
            var seqArgs   = new List<string>();
            bool spawnDone = false;

            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";

                int idx;
                lock (seqArgs) { seqArgs.Add(json); idx = seqArgs.Count; }
                // First poll returns seq=0 line; subsequent polls return empty
                return idx == 1
                    ? "{\"ok\":true,\"data\":\"0\\nsome line\\n\"}"
                    : "{\"ok\":true,\"data\":\"\"}";
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            await WaitUntilAsync(() =>
            {
                lock (seqArgs) return seqArgs.Count >= 2;
            }, "Expected at least two event polls");

            lock (seqArgs)
            {
                Assert.GreaterOrEqual(seqArgs.Count, 2, "Expected at least 2 event polls");
                Assert.IsTrue(seqArgs[1].Contains("\"after_seq\":0"),
                    $"after_seq not updated in 2nd poll: {seqArgs[1]}");
            }
        }

        // ── SendSetMode (M6) ──────────────────────────────────────────────────

        [Test]
        public void SendSetMode_WhenSendSucceeds_ReturnsTrue()
        {
            _sut = Spawn(out _);
            var result = _sut.SendSetMode("ask");
            Assert.IsTrue(result, "SendSetMode must return true on success");
        }

        [Test]
        public void SendSetMode_WhenSendThrows_ReturnsFalse()
        {
            bool spawnDone = false;
            _sut = new RelayChatProcess(json =>
            {
                if (!spawnDone) { spawnDone = true; return "{\"ok\":true,\"data\":\"\"}"; }
                if (json.Contains("set_mode")) throw new Exception("disconnected");
                return "{\"ok\":true,\"data\":\"\"}";
            });
            _sut.SpawnViaRelay(0, "/bin/cli", new string[0], EmptyEnv, EmptyStrip);
            var result = _sut.SendSetMode("agent");
            Assert.IsFalse(result, "SendSetMode must return false when send throws");
        }

        [Test]
        public void SendSetMode_WhenNotRunning_ReturnsFalse()
        {
            _sut = Spawn(out _);
            _sut.Kill();
            var result = _sut.SendSetMode("ask");
            Assert.IsFalse(result, "SendSetMode must return false when not running");
        }

        [Test]
        public void SendSetMode_WithSessionId_IncludesSessionIdInJson()
        {
            _sut = Spawn(out var log);
            _sut.SendSetMode("agent", "sess-abc");
            Assert.IsTrue(LogContains(log, "\"session_id\":\"sess-abc\""),
                $"session_id missing from set_mode payload: {string.Join(", ", SnapLog(log))}");
        }

        [Test]
        public void SendSetMode_WithNullSessionId_OmitsSessionIdField()
        {
            _sut = Spawn(out var log);
            _sut.SendSetMode("ask", null);
            var snap = SnapLog(log);
            var setModeCmd = snap.Find(j => j.Contains("set_mode"));
            Assert.IsNotNull(setModeCmd, "set_mode cmd not found");
            Assert.IsFalse(setModeCmd.Contains("session_id"),
                $"session_id must be absent when null: {setModeCmd}");
        }

        [Test]
        public void SendSetMode_WithEmptySessionId_OmitsSessionIdField()
        {
            _sut = Spawn(out var log);
            _sut.SendSetMode("ask", "");
            var snap = SnapLog(log);
            var setModeCmd = snap.Find(j => j.Contains("set_mode"));
            Assert.IsNotNull(setModeCmd, "set_mode cmd not found");
            Assert.IsFalse(setModeCmd.Contains("session_id"),
                $"session_id must be absent when empty: {setModeCmd}");
        }

        [Test]
        public void SendSetMode_WhenRelayReturnsOkFalse_ReturnsFalse()
        {
            // T9: response with ok=false must propagate as false (was always true before)
            _sut = Spawn(json =>
            {
                if (json.Contains("set_mode")) return "{\"ok\":false,\"err\":\"spawn failed\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var result = _sut.SendSetMode("agent");
            Assert.IsFalse(result, "SendSetMode must return false when relay returns ok=false");
        }

        [Test]
        public void SendSetMode_WhenRelayReturnsNoOkField_ReturnsFalse()
        {
            // T9: missing ok field must be treated as failure (safe default)
            _sut = Spawn(json =>
            {
                if (json.Contains("set_mode")) return "{}";
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var result = _sut.SendSetMode("agent");
            Assert.IsFalse(result, "SendSetMode must return false when response has no ok field");
        }

        // ── Tool call / result pass-through ───────────────────────────────────

        [Test]
        public async Task DrainLines_ToolCallEvent_DeliversTcLine()
        {
            bool delivered = false;
            _sut = Spawn(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (!delivered) { delivered = true; return "{\"ok\":true,\"data\":\"0\\ntc|bash|tid1|{}\\n\"}"; }
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count > 0;
            }, "Tool-call event was not delivered");
            Assert.AreEqual(1, output.Count, "Expected one tc| line");
            Assert.IsTrue(output[0].StartsWith("tc|"), $"Expected tc| prefix, got: {output[0]}");
            Assert.IsTrue(output[0].Contains("bash"),  $"Tool name missing: {output[0]}");
        }

        [Test]
        public async Task DrainLines_ToolResultEvent_DeliversTrLine()
        {
            bool delivered = false;
            _sut = Spawn(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (!delivered) { delivered = true; return "{\"ok\":true,\"data\":\"0\\ntr|tid1|true|output\\n\"}"; }
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count > 0;
            }, "Tool-result event was not delivered");
            Assert.AreEqual(1, output.Count, "Expected one tr| line");
            Assert.IsTrue(output[0].StartsWith("tr|"), $"Expected tr| prefix, got: {output[0]}");
        }

        [Test]
        public async Task DrainLines_ToolCallSequence_AllThreeEventsDeliveredInOrder()
        {
            bool delivered = false;
            _sut = Spawn(json =>
            {
                if (!json.Contains("events")) return "{\"ok\":true,\"data\":\"\"}";
                if (!delivered)
                {
                    delivered = true;
                    // seq=0 ToolStart, seq=1 TextDelta, seq=2 ToolResult
                    return "{\"ok\":true,\"data\":\"0\\ntc|bash|tid1|{}\\n1\\nt|processing...\\n2\\ntr|tid1|true|done\\n\"}";
                }
                return "{\"ok\":true,\"data\":\"\"}";
            }, out _);
            var output = new List<string>();
            await WaitUntilAsync(() =>
            {
                _sut.DrainLines(output);
                return output.Count >= 3;
            }, "Tool-call sequence was not delivered");
            Assert.AreEqual(3, output.Count, $"Expected 3 events, got {output.Count}: [{string.Join(", ", output)}]");
            Assert.IsTrue(output[0].StartsWith("tc|"), $"Event 0 must be tc|, got: {output[0]}");
            Assert.IsTrue(output[1].StartsWith("t|"),  $"Event 1 must be t|, got: {output[1]}");
            Assert.IsTrue(output[2].StartsWith("tr|"), $"Event 2 must be tr|, got: {output[2]}");
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        [Test]
        public void Dispose_SetsIsRunningFalse()
        {
            _sut = Spawn(out _);
            _sut.Dispose();
            Assert.IsFalse(_sut.IsRunning);
        }

        [Test]
        public void Dispose_Idempotent_NoException()
        {
            _sut = Spawn(out _);
            Assert.DoesNotThrow(() => { _sut.Dispose(); _sut.Dispose(); });
        }

        [Test]
        public void Dispose_BeforeSpawn_NoException()
        {
            _sut = new RelayChatProcess(json => "{\"ok\":true,\"data\":\"\"}");
            Assert.DoesNotThrow(() => _sut.Dispose());
        }

        [Test]
        public void Dispose_AfterKill_NoException()
        {
            _sut = Spawn(out _);
            _sut.Kill();
            Assert.DoesNotThrow(() => _sut.Dispose());
        }
    }
}
